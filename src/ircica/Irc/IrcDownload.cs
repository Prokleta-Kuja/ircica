using System.Diagnostics;
using System.Net.Sockets;

namespace ircica;

public class IrcDownload
{
    CancellationTokenSource? _cts;
    public IrcDownload(Guid id, string bot, IrcConnection connection)
    {
        Id = id;
        Bot = bot;
        Connection = connection;
    }
    public Guid Id { get; }
    public string Bot { get; }
    public IrcConnection Connection { get; set; }
    public decimal Downloaded { get; private set; }
    public DateTime? RequestedAt { get; set; }
    public IrcDownloadStatus Status { get; set; }
    public IrcDownloadMessage? Message { get; private set; }
    public List<string> Log { get; set; } = new();
    public string Progress => Message == null ? "-" : Math.Round(Downloaded / Message.Size * 100m, 2).ToString("0.00");
    public async Task Start(IrcConnection connection, IrcDownloadMessage message)
    {
        Message = message;
        RequestedAt = null;

        if (message.IsReverseDcc)
        {
            Status = IrcDownloadStatus.FailedReverseDcc;
            Log.Add("Reverse DCC required. Currently unsupported.");
            connection.ActiveDownloads.Remove(Id);
            return;
        }

        var file = new FileInfo(C.Paths.IncompleteFor(message.FileName));
        try
        {
            using var client = new TcpClient(message.IP.ToString(), message.Port);
            using var clientStream = client.GetStream();
            Status = IrcDownloadStatus.Downloading;
            using var fileStream = file.OpenWrite();

            _cts = new();
            var buffer = new byte[1024 * 128];
            Log.Add("Connected. Starting download...");

            while (client.Connected && await clientStream.ReadAsync(buffer, _cts.Token) is var read && read > 0)
            {
                Downloaded += read;
                await fileStream.WriteAsync(buffer.AsMemory(0, read), _cts.Token);

                if (Downloaded == message.Size)
                {
                    Log.Add("Downloaded. Starting post-processing...");
                    client.Close();
                    fileStream.Flush();
                    fileStream.Close();
                    await PostProcessAsync(file.FullName);
                }
            }

            if (Status != IrcDownloadStatus.Complete)
            {
                Log.Add("Stopped unexpectedly.");
                Status = IrcDownloadStatus.Failed;
            }
        }
        catch (Exception ex)
        {
            Log.Add($"Exception thrown. {ex.Message}");
            Status = IrcDownloadStatus.Failed;
            file.Refresh();
            if (file.Exists)
            {
                Log.Add("Removing failed file from disk.");
                file.Delete();
            }
        }
        finally
        {
            connection.ActiveDownloads.Remove(Id);
        }
    }
    async Task PostProcessAsync(string downloadedFile)
    {
        Status = IrcDownloadStatus.PostProcessing;
        var psi = new ProcessStartInfo("tar", "xvf folder.tar -C test")
        {
            WorkingDirectory = C.Paths.Incomplete
        };
        psi.RedirectStandardError = psi.RedirectStandardOutput = true;
        psi.UseShellExecute = false;


        switch (Path.GetExtension(downloadedFile).ToLower())
        {
            case ".tar":
                psi.FileName = "tar";
                psi.Arguments = $"xvf {downloadedFile} -C {C.Paths.Complete}";
                break;
            default:
                Log.Add("Moving...");
                File.Move(downloadedFile, C.Paths.CompleteFor(Path.GetFileName(downloadedFile)));
                Status = IrcDownloadStatus.Complete;
                return;
        }

        Log.Add("Extracting...");
        var process = Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
            {
                Status = IrcDownloadStatus.Complete;
                Log.Add("Deleting original files...");
                File.Delete(downloadedFile);
            }
            else
            {
                Status = IrcDownloadStatus.Failed;
                while (!process.StandardError.EndOfStream)
                    Log.Add(process.StandardError.ReadLine() ?? string.Empty);
            }
        }
        else
            Status = IrcDownloadStatus.Failed;

        await UpdatePermissionsAsync();
    }
    async Task UpdatePermissionsAsync()
    {
        var psi = new ProcessStartInfo("chmod", $"-R o+r {C.Paths.Complete}")
        {
            WorkingDirectory = C.Paths.Incomplete
        };
        psi.RedirectStandardError = psi.RedirectStandardOutput = true;
        psi.UseShellExecute = false;
        var process = Process.Start(psi);
        await process!.WaitForExitAsync();
        if (process.ExitCode != 0)
            while (!process.StandardError.EndOfStream)
                Log.Add(process.StandardError.ReadLine() ?? string.Empty);
    }
    public void Stop()
    {
        _cts?.Cancel();
        Connection.ActiveDownloads.Remove(Id);
    }
}

public enum IrcDownloadStatus
{
    Waiting,
    Requested,
    Downloading,
    PostProcessing,
    Complete,
    Expired,
    Failed,
    FailedReverseDcc,
}