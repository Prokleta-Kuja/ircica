using System.Text.Json;
using System.Xml.Linq;

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
            await Task.Delay(TimeSpan.FromSeconds(1));
            var doc = XDocument.Load(e.FullPath, LoadOptions.None);
            var file = doc.Root?.Element("file");
            if (file == null)
                throw new Exception("Couldn't parse XML from irc file");

            var request = JsonSerializer.Deserialize<IrcDownloadRequest>(file.Value);
            IrcService.RequestDownload(request!);

            File.Delete(e.FullPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private static void OnError(object sender, ErrorEventArgs e) => Console.WriteLine(e.GetException().Message);

}