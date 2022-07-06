using System.Diagnostics;
using System.Net.Sockets;

namespace ircica;

public class IrcIndexer
{
    public IrcIndexer(IrcOptions opt)
    {
        Opt = opt;
    }
    public IrcOptions Opt { get; }
    public bool Running { get; private set; }
    public List<IrcDirectMessage> Messages { get; } = new();
    public Dictionary<string, DateTime> Lines { get; } = new();

    public async Task Start(CancellationToken ct)
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(Opt.Server.Url, Opt.Server.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream);
            Running = true;

            writer.WriteLine($"USER {Opt.UserName} 0 * {Opt.RealName}");
            writer.WriteLine($"NICK {Opt.NickName}");
            writer.Flush();

            while (client.Connected)
            {
                if (ShouldQuit(writer, ct))
                    return;

                var line = await reader.ReadLineAsync().WaitAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var message = IrcMessage.Parse(line, Opt.NickName);
                switch (message)
                {
                    case IrcPingMessage ping:
                        await ping.WriteResponseAsync(writer);
                        break;
                    case IrcMotdEndMessage motd:
                        await motd.JoinChannels(Opt.Server.Channels, writer);
                        break;
                    case IrcVersionMessage version:
                        await version.WriteResponseAsync(writer);
                        break;
                    case IrcDirectMessage direct:
                        Messages.Add(direct);
                        break;
                    case IrcAnnouncementMessage announce:
                        if (Lines.ContainsKey(line))
                            Lines[line] = DateTime.UtcNow;
                        else
                            Lines.Add(line, DateTime.UtcNow);
                        break;
                    default:
                        break;
                }
            }
            client.Close();
        }
        catch (TaskCanceledException) { }
        finally
        {
            Running = false;
        }
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