using System.Net.Sockets;

namespace ircica;

public class IrcConnection
{
    public IrcConnection(IrcServer server)
    {
        Server = server;
    }
    public IrcServer Server { get; }
    public bool Running { get; private set; }
    public List<IrcDirectMessage> Messages { get; } = new();
    public Dictionary<string, DateTime> Lines { get; } = new();

    public async Task Start(CancellationToken ct)
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(Server.Url, Server.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream);
            Running = true;

            writer.WriteLine($"USER {C.Settings.UserName} 0 * {C.Settings.RealName}");
            writer.WriteLine($"NICK {C.Settings.NickName}");
            writer.Flush();

            while (client.Connected)
            {
                if (ShouldQuit(writer, ct))
                    return;

                var line = await reader.ReadLineAsync().WaitAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var message = IrcMessage.Parse(line, C.Settings.NickName);
                switch (message)
                {
                    case IrcPingMessage ping:
                        await ping.WriteResponseAsync(writer);
                        break;
                    case IrcMotdEndMessage motd:
                        await motd.JoinChannels(Server.Channels, writer);
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