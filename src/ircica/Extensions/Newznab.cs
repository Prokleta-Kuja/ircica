using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ircica.Entities;
using Microsoft.EntityFrameworkCore;

namespace ircica.Extensions;

// To be able to search, you must add all categories, not just top level TV category
public static class WebApplicationExtensions
{
    public static void MapNewznab(this WebApplication app)
    {
        // app.Use(async (ctx, next) =>
        // {
        //     var req = ctx.Request;
        //     Console.WriteLine($"Url: {req.Path} - {req.QueryString}");
        //     await next();
        // });
        app.MapGet(C.Routes.NewznabDownload, async (Guid id, HttpContext ctx) =>
        {
            var release = await Newznab.GetByUniqueIdAsync(id);
            if (release == null)
                return Results.NotFound();

            var downloadBytes = Newznab.GetNzbBytes(release);
            return Results.File(downloadBytes, "application/x-nzb", $"{release.Title}.nzb");
        });
        app.MapGet(C.Routes.NewznabApi, async (HttpContext ctx) =>
        {
            if (!ctx.Request.Query.TryGetValue("T", out var t))
                return Results.BadRequest();

            ctx.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            ctx.Response.Headers.Pragma = "no-cache";

            var term = t.ToString();
            if (term == "caps")
                return Results.Content(Newznab.CapResult(), "text/xml");
            if (Newznab.Searches.Contains(term))
                return Results.Content(await Newznab.SearchResultAsync(ctx.Request.Query), "application/rss+xml");

            return Results.BadRequest();
        });
    }
}

public static class Newznab
{
    static readonly Regex s_TvMatcher = new(@"\.S(?<Season>\d{2,2})E(?<Episode>\d{2,2}|\d{1,2}x\d{2,2})|\.S(?<Season>\d{2,2})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public static readonly HashSet<string> Searches = new() { "search", "tvsearch", "movie" };
    public static string GetString(XDocument doc) => $"<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n{doc}";
    public static string CapResult()
    {
        var server = new XElement("server", new XAttribute("version", "1.0"), new XAttribute("title", "ircica"));
        var limits = new XElement("limits", new XAttribute("max", 512), new XAttribute("default", 256));
        var registration = new XElement("registration", new XAttribute("available", "no"), new XAttribute("open", "no"));
        var search = new XElement("search", new XAttribute("available", "yes"), new XAttribute("supportedParams", "q"));
        var tvSearch = new XElement("tv-search", new XAttribute("available", "yes"), new XAttribute("supportedParams", "q,season,ep"));
        var movieSearch = new XElement("movie-search", new XAttribute("available", "yes"), new XAttribute("supportedParams", "q"));
        var searching = new XElement("searching", search, tvSearch, movieSearch);

        var group = new XElement("group", new XAttribute("id", 999), new XAttribute("name", "irc"));
        group.SetAttributeValue("lastupdate", DateTime.UtcNow); // An announcement is made almost every second
        var groups = new XElement("groups", group);

        var categories = new XElement("categories",
            new XElement("category", new XAttribute("id", 5000), new XAttribute("name", "TV"),
                new XElement("subcat", new XAttribute("id", 5020), new XAttribute("name", "Foreign")),
                new XElement("subcat", new XAttribute("id", 5030), new XAttribute("name", "SD")),
                new XElement("subcat", new XAttribute("id", 5040), new XAttribute("name", "HD")),
                new XElement("subcat", new XAttribute("id", 5045), new XAttribute("name", "UHD")),
                new XElement("subcat", new XAttribute("id", 5050), new XAttribute("name", "Other")),
                new XElement("subcat", new XAttribute("id", 5060), new XAttribute("name", "Sport")),
                new XElement("subcat", new XAttribute("id", 5070), new XAttribute("name", "Anime")),
                new XElement("subcat", new XAttribute("id", 5080), new XAttribute("name", "Documentary"))),
            new XElement("category", new XAttribute("id", 2000), new XAttribute("name", "Movies"),
                new XElement("subcat", new XAttribute("id", 2010), new XAttribute("name", "Foreign")),
                new XElement("subcat", new XAttribute("id", 2020), new XAttribute("name", "Other")),
                new XElement("subcat", new XAttribute("id", 2030), new XAttribute("name", "SD")),
                new XElement("subcat", new XAttribute("id", 2040), new XAttribute("name", "HD")),
                new XElement("subcat", new XAttribute("id", 2045), new XAttribute("name", "UHD")),
                new XElement("subcat", new XAttribute("id", 2050), new XAttribute("name", "BluRay")),
                new XElement("subcat", new XAttribute("id", 2060), new XAttribute("name", "3D"))),
            new XElement("category", new XAttribute("id", 8000), new XAttribute("name", "Misc"),
                new XElement("subcat", new XAttribute("id", 8010), new XAttribute("name", "Other")))
        );

        var genres = new XElement("genres");

        var caps = new XElement("caps", server, limits, registration, searching, categories, groups, genres);
        var doc = new XDocument(new("1.0", "utf-8", "yes"), caps);
        return GetString(doc);
    }
    public static async Task<string> SearchResultAsync(IQueryCollection query)
    {
        var validRequest = true;

        var term = string.Empty;
        if (query.TryGetValue("q", out var termQ))
            term = termQ.ToString();

        var offset = 0;
        if (query.TryGetValue("offset", out var offsetQ) && int.TryParse(offsetQ.ToString(), out var offsetV))
            offset = offsetV;

        var limit = 256;
        if (query.TryGetValue("limit", out var limitQ) && int.TryParse(limitQ.ToString(), out var limitV) && limitV <= 512)
            limit = limitV;

        var sxx = 0;
        if (query.TryGetValue("season", out var seasonQ))
        {
            var seasonV = seasonQ.ToString();
            if (seasonV.Length == 4) // Year for date based releases
                validRequest = false;
            else if (seasonV.StartsWith("s", StringComparison.InvariantCultureIgnoreCase))
                sxx = int.Parse(seasonV[..^1]);
            else
                sxx = int.Parse(seasonV);
        }

        var exx = 0;
        if (query.TryGetValue("ep", out var episodeQ))
        {
            var episodeV = episodeQ.ToString();
            if (episodeV.Contains('/')) // MM/DD (maybe M/D) date for date based releases
                validRequest = false;
            else if (episodeV.StartsWith("E", StringComparison.InvariantCultureIgnoreCase))
                exx = int.Parse(episodeV[..^1]);
            else
                exx = int.Parse(episodeV);
        }

        DateTime? maxAge = null;
        if (query.TryGetValue("maxage", out var maxAgeQ) && int.TryParse(maxAgeQ.ToString(), out var maxAgeV))
            maxAge = DateTime.UtcNow.Date.AddDays(-maxAgeV);

        var tvSuffix = exx != 0 ? $"S{sxx:00}E{exx:00}" : sxx != 0 ? $"S{sxx:00}*" : string.Empty;
        var releases = validRequest ? await DoSearchAsync(offset, limit, term, tvSuffix, maxAge) : (Count: 0, Results: new List<Release>(0));
        var total = releases.Count;

        XNamespace atomNs = "http://www.w3.org/2005/Atom";
        XNamespace newznabNs = "http://www.newznab.com/DTD/2010/feeds/attributes/";

        var response = new XElement(newznabNs + "response");
        response.SetAttributeValue("offset", offset);
        response.SetAttributeValue("total", total);

        var items = new List<XElement>();
        foreach (var result in releases.Results)
        {
            var download = C.Routes.NeznabDownloadFor(result.UniqueId);
            var itemElements = new List<XElement>();
            itemElements.Add(new XElement("title", result.Title));
            itemElements.Add(new XElement("guid", result.UniqueId, new XAttribute("isPermaLink", "true")));
            itemElements.Add(new XElement("link", download));
            itemElements.Add(new XElement("pubDate", result.FirstSeen.ToString("ddd, dd MMM yyyy HH:mm:ss K")));
            itemElements.Add(new XElement(newznabNs + "attr", result.Size.ToString("00"), new XAttribute("name", "size")));
            itemElements.Add(new XElement("enclosure",
                new XAttribute("url", download),
                new XAttribute("length", result.Size.ToString("00")),
                new XAttribute("type", "application/x-nzb")));

            var tvMatch = s_TvMatcher.Match(result.Title);
            var releaseSeason = tvMatch.Groups["Season"].Value;
            var releaseEpisode = tvMatch.Groups["Episode"].Value;
            if (!string.IsNullOrWhiteSpace(releaseSeason))
                itemElements.Add(new XElement(newznabNs + "attr", releaseSeason, new XAttribute("name", "season")));
            if (!string.IsNullOrWhiteSpace(releaseEpisode))
                itemElements.Add(new XElement(newznabNs + "attr", releaseEpisode, new XAttribute("name", "episode")));

            items.Add(new XElement("item", itemElements));
        }

        var rss = new XElement("rss",
            new XAttribute("version", "2.0"),
            new XAttribute(XNamespace.Xmlns + "atom", atomNs),
            new XAttribute(XNamespace.Xmlns + "newznab", newznabNs),
            new XElement("channel", response, items));

        var doc = new XDocument(rss);
        return GetString(doc);
    }
    public static async Task<Release?> GetByUniqueIdAsync(Guid id)
    {
        using var db = GetDb();
        return await db.Releases
            .Include(r => r.Server)
            .Include(r => r.Channel)
            .Include(r => r.Bot)
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.UniqueId == id);
    }
    public static byte[] GetNzbBytes(Release release)
    {
        var download = new IrcDownloadRequest(release.Server!.Url, release.Channel!.Name, release.Bot!.Name, release.Pack);
        var downloadSerialized = JsonSerializer.Serialize(download);

        var doc = new XDocument(new("1.0", "utf-8", "yes"), new XElement("nzb", new XElement("file", downloadSerialized)));
        var docString = GetString(doc);

        return Encoding.UTF8.GetBytes(docString);
    }
    static async Task<(int Count, List<Release> Results)> DoSearchAsync(int offset, int limit, string term, string tvSuffix, DateTime? maxAge)
    {
        using var db = GetDb();
        IQueryable<Release>? query;

        if (string.IsNullOrWhiteSpace(term))
            query = db.Releases.FromSqlRaw($@"
            SELECT 
                r.*
            FROM FTSReleases ft
                INNER JOIN Releases r ON r.ReleaseId = ft.ReleaseId
            ORDER BY r.FirstSeen DESC");
        else
        {
            if (!string.IsNullOrWhiteSpace(tvSuffix))
                term = $"{term} {tvSuffix}";
            query = db.Releases.FromSqlInterpolated($@"
            SELECT 
                r.*
            FROM FTSReleases ft
                INNER JOIN Releases r ON r.ReleaseId = ft.ReleaseId
            WHERE ft.Title MATCH({term})
            ORDER BY rank, r.Size");
        }

        if (maxAge.HasValue)
            query = query.Where(r => r.FirstSeen > maxAge);

        var count = await query.CountAsync();
        var releases = await query
            .Include(r => r.Channel)
            .Include(r => r.Server)
            .Include(r => r.Bot)
            .Skip(offset)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();

        return (count, releases);
    }
    static AppDbContext GetDb()
    {

        var opt = new DbContextOptionsBuilder<AppDbContext>();
        opt.UseSqlite(C.Paths.ActiveDbConnectionString);
        opt.LogTo(message => System.Diagnostics.Debug.WriteLine(message), new[] { Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuting });
        opt.EnableSensitiveDataLogging(true);
        return new AppDbContext(opt.Options);
    }
}