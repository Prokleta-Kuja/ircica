using ircica.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ircica.Pages;

public partial class Search
{
    string _term = string.Empty;
    string _message = string.Empty;
    List<Release> _items = new();

    async Task SearchAsync()
    {
        if (!File.Exists(C.Paths.ActiveDbFile))
        {
            _message = "No index";
            return;
        }
        else
            _message = string.Empty;

        var opt = new DbContextOptionsBuilder<AppDbContext>();
        opt.UseSqlite(C.Paths.ActiveDbConnectionString);
        using var db = new AppDbContext(opt.Options);

        try
        {
            _items = await db.Releases
                .FromSqlInterpolated($@"
                SELECT 
                    r.*
                FROM FTSReleases ft
                    INNER JOIN Releases r ON r.ReleaseId = ft.ReleaseId
                WHERE ft.Title MATCH({_term})")
                .Include(r => r.Channel)
                .Include(r => r.Server)
                .Include(r => r.Bot)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (SqliteException)
        {
            _items.Clear();
            _message = "Syntax error";
        }
    }
    static void Download(Release release)
    {
        var request = new IrcDownloadRequest(release.Channel!.Name, release.Bot!.Name, release.Pack);
        IrcService.RequestDownload(release.Server!.Url, request);
    }
    static readonly string[] s_sizes = { "B", "KB", "MB", "GB", "TB" };
    static string GetHumanSize(double bytes)
    {
        int order = 0;
        while (bytes >= 1024 && order < s_sizes.Length - 1)
        {
            order++;
            bytes = bytes / 1024;
        }

        // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
        // show a single decimal place, and no space.
        return $"{bytes:0.##} {s_sizes[order]}";
    }
}