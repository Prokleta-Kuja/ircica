using System.Text.RegularExpressions;
using ircica.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ircica;

public static class IrcService
{
    static CancellationTokenSource? _cts;
    static readonly Regex s_unformatter = new(@"[\u0002\u000f\u0011\u001e\u0016\u001d\u001f]|\u0003(\d{2}(,\d{2})?)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public static List<IrcCollector> Indexers { get; private set; } = new();
    public static void LoadIndexers()
    {
        foreach (var server in C.Settings.Servers)
        {
            var opt = new IrcOptions(C.Settings.UserName, C.Settings.RealName, C.Settings.NickName, server);
            var indexer = new IrcCollector(opt);
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

        File.Delete(C.Paths.InactiveDbFile);
        foreach (var tmp in Directory.EnumerateFiles(C.Paths.Config, "*.tmp"))
            File.Delete(tmp);

        using var memoryDbConnection = new SqliteConnection(C.Paths.InMemoryDbConnectionString);
        memoryDbConnection.Open();
        var memoryOpt = new DbContextOptionsBuilder<AppDbContext>();
        memoryOpt.UseSqlite(memoryDbConnection);

        using var memoryDb = new AppDbContext(memoryOpt.Options);
        memoryDb.Database.EnsureCreated();
        FillDb(memoryDb);

        var tempFile = C.Paths.GetTempDbFile();
        using var tempDbConnection = new SqliteConnection(C.Paths.GetDbConnectionString(tempFile));
        memoryDbConnection.BackupDatabase(tempDbConnection);

        if (File.Exists(C.Paths.ActiveDbFile))
            File.Delete(C.Paths.ActiveDbFile);
        File.Move(tempFile, C.Paths.ActiveDbFile);

        if (wasRunning)
            StartAll();
    }
    static void FillDb(AppDbContext db)
    {
        var start = DateTime.UtcNow;
        var chunkSize = 1000;
        var releasesToAdd = new List<Release>(chunkSize);
        void InsertChunk()
        {
            if (releasesToAdd == null || !releasesToAdd.Any())
                return;

            var conn = db.Database.GetDbConnection();
            using var transaction = conn.BeginTransaction();
            var command = conn.CreateCommand();
            command.CommandText = "INSERT INTO Releases(ServerId,ChannelId,BotId,Pack,Size,Title) VALUES ($srvId,$chId,$bId,$pack,$size,$title)";

            var srvId = command.CreateParameter(); srvId.ParameterName = "$srvId"; command.Parameters.Add(srvId);
            var chId = command.CreateParameter(); chId.ParameterName = "$chId"; command.Parameters.Add(chId);
            var bId = command.CreateParameter(); bId.ParameterName = "$bId"; command.Parameters.Add(bId);
            var pack = command.CreateParameter(); pack.ParameterName = "$pack"; command.Parameters.Add(pack);
            var size = command.CreateParameter(); size.ParameterName = "$size"; command.Parameters.Add(size);
            var title = command.CreateParameter(); title.ParameterName = "$title"; command.Parameters.Add(title);

            foreach (var release in releasesToAdd)
            {
                srvId.Value = release.ServerId;
                chId.Value = release.ChannelId;
                bId.Value = release.BotId;
                pack.Value = release.Pack;
                size.Value = release.Size;
                title.Value = release.Title;

                command.ExecuteNonQuery();
            }

            transaction.Commit();
            releasesToAdd.Clear();
        };

        foreach (var indexer in Indexers)
        {
            var server = new Server(indexer.Opt.Server.Name, indexer.Opt.Server.Url)
            {
                Port = indexer.Opt.Server.Port,
                SSL = indexer.Opt.Server.SSL,
            };

            db.Servers.Add(server);
            db.SaveChanges();

            var channels = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            var bots = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);


            foreach (var line in indexer.Lines.Keys)
            {
                if (releasesToAdd.Count == chunkSize)
                    InsertChunk();

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

                if (!channels.TryGetValue(onChannel, out var channelId))
                {
                    var channel = new Channel(onChannel);
                    db.Channels.Add(channel);
                    db.SaveChanges();

                    channelId = channel.ChannelId;
                    channels.Add(channel.Name, channel.ChannelId);
                }

                if (!bots.TryGetValue(sender, out var botId))
                {
                    var bot = new Bot(sender);
                    db.Bots.Add(bot);
                    db.SaveChanges();

                    botId = bot.BotId;
                    bots.Add(bot.Name, bot.BotId);
                }

                var unformatted = s_unformatter.Replace(release, string.Empty);
                releasesToAdd.Add(new(unformatted)
                {
                    BotId = botId,
                    ChannelId = channelId,
                    Pack = pack,
                    Size = size,
                    ServerId = server.ServerId,
                });
            }
        }
        InsertChunk();

        db.Database.ExecuteSqlRaw(@"
         CREATE VIRTUAL TABLE FTSReleases USING FTS5(Title, ReleaseId UNINDEXED, content=Releases, content_rowid=ReleaseId, tokenize = ""unicode61 separators '-_'"");
         INSERT INTO FTSReleases (Title, ReleaseId) SELECT Title, ReleaseId FROM Releases;
        ");

        Console.WriteLine($"Inserted in {start - DateTime.UtcNow}");
    }
}