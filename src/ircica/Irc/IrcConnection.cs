using System.Net.Sockets;

namespace ircica;

public class IrcConnection
{
    const int MAX_DOWNLOADS = 2;
    public IrcConnection(IrcServer server)
    {
        Server = server;
    }
    public IrcServer Server { get; }
    public bool Connected { get; private set; }
    public bool Collecting { get; set; }
    public Dictionary<Guid, DateTime> ActiveDownloads { get; set; } = new(MAX_DOWNLOADS);
    public List<IrcDirectMessage> Messages { get; } = new();
    public Dictionary<string, (DateTime First, DateTime Last)> Lines { get; } = new();
    public Queue<IrcDownloadRequest> DownloadRequests { get; set; } = new();
    public async Task Start(CancellationToken ct)
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(Server.Url, Server.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream);

            writer.WriteLine($"USER {C.Settings.UserName} 0 * {C.Settings.RealName}");
            writer.WriteLine($"NICK {C.Settings.NickName}");
            writer.Flush();

            while (client.Connected)
            {
                if (ShouldQuit(writer, ct))
                    return;

                if (Connected && ActiveDownloads.Count < MAX_DOWNLOADS && DownloadRequests.TryDequeue(out var request))
                {
                    await request.RequestAsync(writer);
                    ActiveDownloads.Add(request.Id, DateTime.UtcNow);
                    IrcService.DownloadRequested(request);
                }

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
                        Connected = true;
                        break;
                    case IrcVersionMessage version:
                        await version.WriteResponseAsync(writer);
                        break;
                    case IrcDownloadMessage downloadMessage:
                        Messages.Add(downloadMessage);
                        IrcService.Download(this, downloadMessage);
                        break;
                    case IrcDirectMessage direct:
                        Messages.Add(direct);
                        break;
                    case IrcAnnouncementMessage announce:
                        if (Collecting)
                            if (Lines.ContainsKey(line))
                                Lines[line] = (Lines[line].First, DateTime.UtcNow);
                            else
                                Lines.Add(line, (DateTime.UtcNow, DateTime.UtcNow));
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
            Connected = false;
            Collecting = false;
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