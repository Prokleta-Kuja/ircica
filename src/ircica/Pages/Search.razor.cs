using ircica.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ircica.Pages;

public partial class Search
{
    ElementReference _searchInput;
    string _term = "term AND (x265 OR h265)";// string.Empty;
    string _message = string.Empty;
    List<Release> _items = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await _searchInput.FocusAsync();
    }

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
                .Take(128)
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
        var request = new IrcDownloadRequest(release.Server!.Url,
                                             release.Channel!.Name,
                                             release.Bot!.Name,
                                             release.Pack);
        IrcService.RequestDownload(request);
    }
}