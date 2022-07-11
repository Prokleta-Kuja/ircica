using System.Xml.Linq;
using ircica.Entities;
using Microsoft.EntityFrameworkCore;

namespace ircica.Extensions;

// To be able to search, you must add all categories, not just top level TV category
public static class WebApplicationExtensions
{
    public static void MapNewznab(this WebApplication app)
    {
        app.Use(async (ctx, next) =>
        {
            var req = ctx.Request;
            Console.WriteLine($"Url: {req.Path} - {req.QueryString}");
            await next();
        });
        // Relativni linkovi rade, tako da dodaj rutu za skidanje
        app.MapGet(C.Routes.Newznab, async (HttpContext ctx) =>
        {
            await Task.CompletedTask;
            //http://192.168.123.3:5000/newznab/api
            if (!ctx.Request.Query.TryGetValue("T", out var t))
                return Results.BadRequest();

            ctx.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            ctx.Response.Headers.Pragma = "no-cache";

            if (t == "caps")
                return Results.Content(Newznab.CapResult(), "text/xml");
            if (Newznab.Searches.Contains(t))
                return Results.Content(await Newznab.SearchResultAsync(ctx.Request.Query), "application/rss+xml");

            return Results.BadRequest();
        });
    }
}

public static class Newznab
{
    public static readonly HashSet<string> Searches = new() { "search", "tvsearch", "movie" };
    public static string GetString(XDocument doc) => $"<?xml version=\"1.0\" encoding=\"UTF-8\" ?>\n{doc}";
    public static string CapResult()
    {
        var server = new XElement("server");
        server.SetAttributeValue("version", "1.0");
        server.SetAttributeValue("title", "ircica");

        var limits = new XElement("limits");
        limits.SetAttributeValue("max", 512);
        limits.SetAttributeValue("default", 256);

        var registration = new XElement("registration");
        registration.SetAttributeValue("available", "no");
        registration.SetAttributeValue("open", "no");

        var search = new XElement("search");
        search.SetAttributeValue("available", "yes");
        search.SetAttributeValue("supportedParams", "q");
        var tvSearch = new XElement("tv-search");
        tvSearch.SetAttributeValue("available", "yes");
        tvSearch.SetAttributeValue("supportedParams", "q,season,ep"); // supportedParams="q,rid,tvdbid,tvmazeid,season,ep"
        var movieSearch = new XElement("movie-search");
        movieSearch.SetAttributeValue("available", "yes");
        movieSearch.SetAttributeValue("supportedParams", "q"); // supportedParams="q,imdbid,genre"
        var searching = new XElement("searching", search, tvSearch, movieSearch);

        var group = new XElement("group");
        group.SetAttributeValue("id", 999);
        group.SetAttributeValue("name", "irc");
        group.SetAttributeValue("lastupdate", DateTime.UtcNow); // TODO: change this
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
            new XElement("category", new XAttribute("id", 2000), new XAttribute("name", "Movies")),
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
            var itemElements = new List<XElement>();
            // TODO: Link i Enclosure su isti
            itemElements.Add(new XElement("title", result.Title));
            itemElements.Add(new XElement("guid", result.UniqueId, new XAttribute("isPermaLink", "true"))); // TODO: link na download
            itemElements.Add(new XElement("link", "/505-sa-crtom.nzb"));
            itemElements.Add(new XElement("pubDate", result.FirstSeen.ToString("ddd, dd MMM yyyy HH:mm:ss K")));
            itemElements.Add(new XElement(newznabNs + "attr", result.Size, new XAttribute("name", "size")));
            if (!string.IsNullOrWhiteSpace(sxx))
                itemElements.Add(new XElement(newznabNs + "attr", sxx[..^1], new XAttribute("name", "season")));
            if (!string.IsNullOrWhiteSpace(exx))
                itemElements.Add(new XElement(newznabNs + "attr", exx[..^1], new XAttribute("name", "episode")));
            itemElements.Add(new XElement("enclosure",
                new XAttribute("url", "/505-sa-crtom.nzb"),
                new XAttribute("length", result.Size),
                new XAttribute("type", "application/x-nzb")));

            var item = new XElement("item", itemElements);
            items.Add(item);
        }

        var channel = new XElement("channel", response, items);
        var rss = new XElement("rss", new XAttribute(XNamespace.Xmlns + "atom", atomNs), new XAttribute(XNamespace.Xmlns + "newznab", newznabNs), channel);
        rss.SetAttributeValue("version", "2.0");
        var doc = new XDocument(rss);
        return GetString(doc);
    }
    static async Task<(int Count, List<Release> Results)> DoSearchAsync(int offset, int limit, string term, string tvSuffix, DateTime? maxAge)
    {
        var opt = new DbContextOptionsBuilder<AppDbContext>();
        opt.UseSqlite(C.Paths.ActiveDbConnectionString);
        using var db = new AppDbContext(opt.Options);

        IQueryable<Release>? query;

        if (string.IsNullOrWhiteSpace(term))
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
        else
            query = db.Releases.FromSqlInterpolated($@"
            SELECT 
                r.*
            FROM FTSReleases ft
                INNER JOIN Releases r ON r.ReleaseId = ft.ReleaseId
            ORDER BY r.FirstSeen DESC");

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
}