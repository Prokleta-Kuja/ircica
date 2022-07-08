using System.Net.Sockets;

namespace ircica;

public class IrcDownloader
{
    public List<string> kurčina = new();
    CancellationTokenSource? _cts;
    public IrcDownloader(IrcServer server, IrcDownload? request = null)
    {
        Server = server;
        if (request != null)
        {
            Downloads.Add(request);
            Server.Channels.Add(request.Channel);
        }

        _ = Connect();
    }
    public IrcServer Server { get; }
    public bool Connected { get; private set; }
    public DateTime LastActivity { get; private set; }
    public List<IrcDirectMessage> Messages { get; } = new();
    public List<IrcDownload> Downloads { get; set; } = new();

    public async Task Connect()
    {
        using var cts = new CancellationTokenSource();
        _cts = cts;
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(Server.Url, Server.Port);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream);

            writer.WriteLine($"USER {C.Settings.DwUserName} 0 * {C.Settings.DwRealName}");
            writer.WriteLine($"NICK {C.Settings.DwNickName}");
            writer.Flush();

            while (client.Connected)
            {
                if (ShouldQuit(writer, _cts.Token))
                    return;

                if (Connected)
                    foreach (var download in Downloads.Where(d => d.Status == IrcDownloadStatus.Added))
                    {
                        if (!Server.Channels.Contains(download.Channel))
                        {
                            await writer.WriteLineAsync($"JOIN #{download.Channel.TrimStart('#')}");
                            if (download.Channel == "moviegods")
                                await writer.WriteLineAsync($"JOIN #mg-chat");

                            await writer.FlushAsync();
                            Server.Channels.Add(download.Channel);
                        }

                        writer.WriteLine($"PRIVMSG {download.Bot} :xdcc send #{download.Pack}");
                        download.Status = IrcDownloadStatus.Waiting;
                    }

                var line = await reader.ReadLineAsync().WaitAsync(_cts.Token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                kurčina.Add(line);
                //File.WriteAllLines("KURAC123", kurčina);
                var message = IrcMessage.Parse(line, C.Settings.DwNickName);
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
                    case IrcDownloadMessage download:
                        var dw = Downloads.SingleOrDefault(d => d.Bot.Equals(download.Sender, StringComparison.InvariantCultureIgnoreCase));
                        if (dw == null)
                            Console.WriteLine("Download request not found");
                        else
                            _ = dw.Start(download);
                        break;
                    case IrcDirectMessage direct:
                        Messages.Add(direct);
                        break;
                    case IrcAnnouncementMessage announce:
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
    public void Enqueue(IrcDownload request)
    {
        Downloads.Add(request);
    }
}