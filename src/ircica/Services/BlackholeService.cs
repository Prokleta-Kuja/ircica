using System.Text.Json;

namespace ircica;

public static class BlackholeService
{
    public static FileSystemWatcher GetWatcher()
    {
        var watcher = new FileSystemWatcher(C.Paths.Blackhole, "*.nzb");
        watcher.Created += OnCreated;
        watcher.Error += OnError;

        return watcher;
    }
    public static void Enable(this FileSystemWatcher watcher) => watcher.EnableRaisingEvents = true;
    private static async void OnCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            var text = File.ReadAllText(e.FullPath);
            var request = JsonSerializer.Deserialize<IrcDownloadRequest>(text);

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private static void OnError(object sender, ErrorEventArgs e) => Console.WriteLine(e.GetException().Message);

}