using System.Net.Sockets;

namespace ircica;

public class IrcIndexer
{
    readonly IrcOptions _opt;
    public IrcIndexer(IrcOptions opt)
    {
        _opt = opt;
    }
    public List<IrcDirectMessage> Messages { get; } = new();

    public IrcState State { get; private set; }
    public async Task Start(CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_opt.Server.Url, _opt.Server.Port);
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream);

        writer.WriteLine($"USER {_opt.UserName} 0 * {_opt.RealName}");
        writer.WriteLine($"NICK {_opt.NickName}");
        writer.Flush();
        State |= IrcState.Connected;

        while (client.Connected)
        {
            if (ShouldQuit(writer, ct))
                return;

            var line = await reader.ReadLineAsync().WaitAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var message = IrcMessage.Parse(line, _opt.NickName);
            switch (message)
            {
                case IrcPingMessage ping:
                    await ping.WriteResponseAsync(writer);
                    break;
                case IrcMotdEndMessage motd:
                    await motd.JoinChannels(_opt.Server.Channels, writer);
                    break;
                case IrcVersionMessage version:
                    await version.WriteResponseAsync(writer);
                    break;
                case IrcDirectMessage direct:
                    Messages.Add(direct);
                    break;
                default:
                    break;
            }
        }
        client.Close();
    }
    static bool ShouldQuit(StreamWriter writer, CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
            return false;

        writer.WriteLine("QUIT");
        writer.Flush();
        return true;
    }

}