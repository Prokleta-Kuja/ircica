using System.Text.RegularExpressions;
using ircica.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ircica;

public static class IrcService
{
    static CancellationTokenSource? _cts;
    public static List<IrcIndexer> Indexers { get; private set; } = new();
    public static void LoadIndexers()
    {
        foreach (var server in C.Settings.Servers)
        {
            var opt = new IrcOptions(C.Settings.UserName, C.Settings.RealName, C.Settings.NickName, server);
            var indexer = new IrcIndexer(opt);
            Indexers.Add(indexer);
        }
    }
    public static void StartAll()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts = null;
        }

        _cts = new();

        foreach (var indexer in Indexers)
            _ = indexer.Start(_cts.Token);
    }
    public static void StopAll() => _cts?.Cancel();
    public static async Task CleanAllAsync()
    {
        var wasRunning = Indexers.Any(i => i.Running);
        if (wasRunning)
        {
            StopAll();
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        var staleBefore = DateTime.UtcNow - TimeSpan.FromHours(24);
        foreach (var indexer in Indexers)
        {
            var stale = indexer.Lines.Where(l => l.Value < staleBefore).Select(l => l.Key);
            foreach (var line in stale)
                indexer.Lines.Remove(line);
        }

        if (wasRunning)
            StartAll();
    }
    public static async Task SaveAllAsync()
    {
        var wasRunning = Indexers.Any(i => i.Running);
        if (wasRunning)
        {
            StopAll();
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        File.Delete(C.Paths.TempDbFile);
        File.Delete(C.Paths.InactiveDbFile);

        using var memoryDbConnection = new SqliteConnection(C.Paths.InMemoryDbConnectionString);
        memoryDbConnection.Open();
        var memoryOpt = new DbContextOptionsBuilder<AppDbContext>();
        memoryOpt.UseSqlite(memoryDbConnection);

        using var memoryDb = new AppDbContext(memoryOpt.Options);
        memoryDb.Database.EnsureCreated();
        FillDb(memoryDb);

        using var tempDbConnection = new SqliteConnection(C.Paths.TempDbConnectionString);
        memoryDbConnection.BackupDatabase(tempDbConnection);
        tempDbConnection.Dispose();

        if (File.Exists(C.Paths.ActiveDbFile))
            File.Move(C.Paths.ActiveDbFile, C.Paths.InactiveDbFile);
        File.Move(C.Paths.TempDbFile, C.Paths.ActiveDbFile);

        try
        {
            File.Delete(C.Paths.InactiveDbFile);
        }
        catch (Exception)
        {
            // In use, we'll get it on next save
        }

        if (wasRunning)
            StartAll();
    }
    static void FillDb(AppDbContext db)
    {
        foreach (var indexer in Indexers)
        {
            var server = new Server(indexer.Opt.Server.Name, indexer.Opt.Server.Url)
            {
                Port = indexer.Opt.Server.Port,
                SSL = indexer.Opt.Server.SSL,
            };

            db.Servers.Add(server);
            db.SaveChanges();

            foreach (var line in indexer.Lines.Keys)
            {
                var data = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var idx = data[0].IndexOf('!');
                var sender = data[0][1..idx];

                var onChannel = data[2].TrimStart('#');
                var remainder = string.Join(string.Empty, data.Skip(3));

                var packStr = Regex.Match(remainder, @"\u0002(.+?)\u0002").Groups[1].Value;
                var sizeStr = Regex.Match(remainder, @"\[(.+?)\]").Groups[1].Value;
                var startIdx = remainder.IndexOf(']');
                var release = remainder[(startIdx + 1)..];

                var pack = -1;
                var size = -1d;
                if (int.TryParse(packStr.TrimStart('#'), out var parsedPack))
                    pack = parsedPack;
                if (double.TryParse(sizeStr[..^1], out var parsedSize))
                {
                    size = sizeStr.Last() switch
                    {
                        'M' or 'm' => parsedSize * 1024 * 1024,
                        'G' or 'g' => parsedSize * 1024 * 1024 * 1024,
                        _ => parsedSize * 1024,
                    };
                }

                var channel = db.Channels.SingleOrDefault(c => c.Name == onChannel);
                if (channel == null)
                    channel = new(onChannel);

                var bot = db.Bots.SingleOrDefault(b => b.Name == sender);
                if (bot == null)
                    bot = new(sender);

                db.Releases.Add(new(release)
                {
                    Bot = bot,
                    Channel = channel,
                    Pack = pack,
                    Size = size,
                    Server = server,
                });
                db.SaveChanges();
            }
        }
    }
}