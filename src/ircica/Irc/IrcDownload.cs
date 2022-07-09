using System.Diagnostics;
using System.Net.Sockets;

namespace ircica;

public class IrcDownload
{
    CancellationTokenSource? _cts;
    public IrcDownload(string bot)
    {
        Bot = bot;
    }
    public string Bot { get; }
    public decimal Downloaded { get; private set; }
    public IrcDownloadStatus Status { get; private set; }
    public IrcDownloadMessage? Message { get; private set; }
    public string Progress => Message == null ? "-" : Math.Round(Downloaded / Message.Size * 100m, 2).ToString("0.00");
    public async Task Start(IrcDownloadMessage message)
    {
        Message = message;

        if (message.IsReverseDcc)
        {
            Status = IrcDownloadStatus.FailedReverseDcc;
            return;
        }

        var file = new FileInfo(C.Paths.DataFor(message.FileName));
        try
        {
            using var client = new TcpClient(message.IP.ToString(), message.Port);
            using var clientStream = client.GetStream();
            Status = IrcDownloadStatus.Downloading;
            using var fileStream = file.OpenWrite();

            _cts = new();
            var buffer = new byte[1024 * 128];

            var sw = new Stopwatch();
            sw.Start();
            while (client.Connected && await clientStream.ReadAsync(buffer, _cts.Token) is var read && read > 0)
            {
                Downloaded += read;
                await fileStream.WriteAsync(buffer.AsMemory(0, read), _cts.Token);

                if (Downloaded == message.Size)
                {
                    client.Close();
                    Status = IrcDownloadStatus.Complete;
                }
            }

            if (Status != IrcDownloadStatus.Complete)
                Status = IrcDownloadStatus.Failed;

            sw.Stop();
        }
        catch (Exception)
        {
            Status = IrcDownloadStatus.Failed;
            file.Refresh();
            if (file.Exists)
                file.Delete();
        }
    }
    public void Stop()
    {
        _cts?.Cancel();
    }
}

public enum IrcDownloadStatus
{
    Requested,
    Downloading,
    Complete,
    Failed,
    FailedReverseDcc,
}