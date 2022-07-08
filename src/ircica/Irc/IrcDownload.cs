using System.Diagnostics;
using System.Net.Sockets;

namespace ircica;

public class IrcDownload
{
    CancellationTokenSource? _cts;
    public IrcDownload(string channel, string bot, int pack)
    {
        Channel = channel;
        Bot = bot;
        Pack = pack;
    }

    public string Channel { get; }
    public string Bot { get; }
    public int Pack { get; }
    public decimal Progress { get; private set; }
    public IrcDownloadStatus Status { get; set; }

    public async Task Start(IrcDownloadMessage message)
    {
        try
        {
            var file = new FileInfo(C.Paths.DataFor(message.FileName));
            using var fileStream = file.OpenWrite();
            using var client = new TcpClient(message.IP.ToString(), message.Port);
            using var clientStream = client.GetStream();
            Status = IrcDownloadStatus.Downloading;

            _cts = new();
            decimal totalRead = 0;
            var buffer = new byte[1024 * 128];

            var sw = new Stopwatch();
            sw.Start();
            while (client.Connected && await clientStream.ReadAsync(buffer, _cts.Token) is var read && read > 0)
            {
                totalRead += read;
                await fileStream.WriteAsync(buffer.AsMemory(0, read), _cts.Token);

                var newProgress = Math.Round(totalRead / message.Size * 100m, 2);
                if (newProgress != Progress)
                    Progress = newProgress;

                if (totalRead == message.Size)
                    client.Close();
            }

            sw.Stop();
            Status = IrcDownloadStatus.Complete;
        }
        catch (TaskCanceledException)
        {
            Status = IrcDownloadStatus.Failed;
        }
    }
    public void Stop()
    {
        _cts?.Cancel();
    }
}

public enum IrcDownloadStatus
{
    Added,
    Waiting,
    Downloading,
    Complete,
    Failed,
}