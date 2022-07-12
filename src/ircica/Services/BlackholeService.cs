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

        foreach (var file in Directory.EnumerateFiles(C.Paths.Blackhole, "*.nzb"))
            Process(file);

        return watcher;
    }
    private static void Process(string filePath)
    {
        try
        {
            var doc = XDocument.Load(filePath, LoadOptions.None);
            var file = doc.Root?.Element("file");
            if (file == null)
                throw new Exception("Couldn't parse XML from irc file");

            var request = JsonSerializer.Deserialize<IrcDownloadRequest>(file.Value);
            IrcService.RequestDownload(request!);

            File.Delete(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    public static void Enable(this FileSystemWatcher watcher) => watcher.EnableRaisingEvents = true;
    private static async void OnCreated(object sender, FileSystemEventArgs e)
    {
        await Task.Delay(TimeSpan.FromSeconds(1));
        Process(e.FullPath);
    }

    private static void OnError(object sender, ErrorEventArgs e) => Console.WriteLine(e.GetException().Message);

}