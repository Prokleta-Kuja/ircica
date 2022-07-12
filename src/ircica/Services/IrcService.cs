using System.Text.RegularExpressions;
using ircica.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ircica;

public static class IrcService
{
    static CancellationTokenSource? _cts;
    static readonly Regex s_unformatter = new(@"[\u0002\u000f\u0011\u001e\u0016\u001d\u001f]|\u0003(\d{2}(,\d{2})?)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public static List<IrcConnection> Connections { get; private set; } = new();
    public static List<IrcDownload> Downloads { get; private set; } = new();
    public static bool Connected { get; private set; }
    public static bool Collecting { get; private set; }
    public static void LoadConnections()
    {
        foreach (var server in C.Settings.Servers)
        {
            var connection = new IrcConnection(server);
            Connections.Add(connection);
        }

        if (C.Settings.AutoConnect)
            ConnectAll();
    }
    public static void ConnectAll()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts = null;
        }

        _cts = new();

        foreach (var connection in Connections)
            _ = connection.Start(_cts.Token);

        Connected = true;

        if (C.Settings.AutoCollect)
            StartCollecting();
    }
    public static void DisconnectAll()
    {
        _cts?.Cancel();
        Connected = false;
    }
    public static void StopCollecting()
    {
        Collecting = false;
        foreach (var connection in Connections)
            connection.Collecting = Collecting;
    }
    public static void StartCollecting()
    {
        Collecting = true;
        foreach (var connection in Connections)
            connection.Collecting = Collecting;
    }
    public static void RequestDownload(IrcDownloadRequest request)
    {
        var connection = Connections.SingleOrDefault(c => c.Server.Url == request.Server);
        if (connection == null)
        {
            Console.WriteLine("Connection not found, skipping download");
            return;
        }

        connection.DownloadRequests.Enqueue(request);
        Downloads.Add(new IrcDownload(request.Bot));
    }
    public static void DownloadRequested(IrcDownloadRequest request)
    {
        var download = Downloads.FirstOrDefault(d => d.Status == IrcDownloadStatus.Waiting && d.Bot.Equals(request.Bot, StringComparison.InvariantCultureIgnoreCase));
        if (download != null)
            download.Status = IrcDownloadStatus.Requested;
    }
    public static void Download(IrcConnection connection, IrcDownloadMessage message)
    {
        var download = Downloads.FirstOrDefault(d => d.Status == IrcDownloadStatus.Requested && d.Bot.Equals(message.Sender, StringComparison.InvariantCultureIgnoreCase));
        if (download != null)
            _ = download.Start(connection, message);
        else
            Console.WriteLine($"Unsolicited download from {message.Sender} {message.Message}");
    }
    public static void BuildIndex()
    {
        var wasCollecting = Collecting;
        if (wasCollecting)
            StopCollecting();

        var start = DateTime.UtcNow;
        var staleBefore = DateTime.UtcNow - TimeSpan.FromHours(24);
        foreach (var indexer in Connections)
        {
            var stale = indexer.Lines.Where(l => l.Value.Last < staleBefore).Select(l => l.Key);
            foreach (var line in stale)
                indexer.Lines.Remove(line);
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

        Console.WriteLine($"Index ({C.GetHumanFileSize(C.Paths.ActiveDbFile)}) built in {DateTime.UtcNow - start}");

        if (wasCollecting)
            StartCollecting();
    }
    static void FillDb(AppDbContext db)
    {
        var chunkSize = 1000;
        var releasesToAdd = new List<Release>(chunkSize);

        void InsertChunk()
        {
            if (releasesToAdd == null || !releasesToAdd.Any())
                return;

            var conn = db.Database.GetDbConnection();
            using var transaction = conn.BeginTransaction();
            var command = conn.CreateCommand();
            command.CommandText = "INSERT INTO Releases(UniqueId,ServerId,ChannelId,BotId,Pack,Size,Title,FirstSeen) VALUES ($uniqueId,$srvId,$chId,$bId,$pack,$size,$title,$first)";

            var uniqueId = command.CreateParameter(); uniqueId.ParameterName = "$uniqueId"; command.Parameters.Add(uniqueId);
            var srvId = command.CreateParameter(); srvId.ParameterName = "$srvId"; command.Parameters.Add(srvId);
            var chId = command.CreateParameter(); chId.ParameterName = "$chId"; command.Parameters.Add(chId);
            var bId = command.CreateParameter(); bId.ParameterName = "$bId"; command.Parameters.Add(bId);
            var pack = command.CreateParameter(); pack.ParameterName = "$pack"; command.Parameters.Add(pack);
            var size = command.CreateParameter(); size.ParameterName = "$size"; command.Parameters.Add(size);
            var title = command.CreateParameter(); title.ParameterName = "$title"; command.Parameters.Add(title);
            var first = command.CreateParameter(); first.ParameterName = "$first"; command.Parameters.Add(first);

            foreach (var release in releasesToAdd)
            {
                uniqueId.Value = release.UniqueId;
                srvId.Value = release.ServerId;
                chId.Value = release.ChannelId;
                bId.Value = release.BotId;
                pack.Value = release.Pack;
                size.Value = release.Size;
                title.Value = release.Title;
                first.Value = release.FirstSeen.ToBinary();

                command.ExecuteNonQuery();
            }

            transaction.Commit();
            releasesToAdd.Clear();
        };

        foreach (var indexer in Connections)
        {
            var server = new Server(indexer.Server.Name, indexer.Server.Url)
            {
                Port = indexer.Server.Port,
                SSL = indexer.Server.SSL,
            };

            db.Servers.Add(server);
            db.SaveChanges();

            var channels = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            var bots = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var line in indexer.Lines)
            {
                if (releasesToAdd.Count == chunkSize)
                    InsertChunk();

                var data = line.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
                    UniqueId = Guid.NewGuid(),
                    BotId = botId,
                    ChannelId = channelId,
                    Pack = pack,
                    Size = size,
                    ServerId = server.ServerId,
                    FirstSeen = line.Value.First,
                });
            }
        }
        InsertChunk();

        db.Database.ExecuteSqlRaw(@"
         CREATE VIRTUAL TABLE FTSReleases USING FTS5(Title, ReleaseId UNINDEXED, content=Releases, content_rowid=ReleaseId, tokenize = ""unicode61 separators '-_'"");
         INSERT INTO FTSReleases (Title, ReleaseId) SELECT Title, ReleaseId FROM Releases;
        ");
    }
}