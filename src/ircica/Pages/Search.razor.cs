using System.Linq.Expressions;
using ircica.Entities;
using ircica.QueryParams;
using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ircica.Pages;

public partial class Search
{
    [Inject] private NavigationManager _navManager { get; set; } = null!;
    string _message = string.Empty;
    List<Release> _items = new();
    Params _params = null!;
    protected override void OnInitialized()
    {
        var uri = new Uri(_navManager.Uri);
        _params = new(uri.Query, SearchCol.FirstSeen, true);
    }
    async Task SearchAsync(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
            _params.ClearSearchTerm();
        else
            _params.SetSearchTerm(term);

        await RefreshListAsync();
    }

    async Task RefreshListAsync()
    {
        if (string.IsNullOrWhiteSpace(_params.SearchTerm))
            return;
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
            var query = db.Releases
                .FromSqlInterpolated($@"
                SELECT 
                    r.*
                FROM FTSReleases ft
                    INNER JOIN Releases r ON r.ReleaseId = ft.ReleaseId
                WHERE ft.Title MATCH({_params.SearchTerm})")
                .Include(r => r.Channel)
                .Include(r => r.Server)
                .Include(r => r.Bot)
                .AsQueryable();

            switch (_params.OrderBy)
            {
                case SearchCol.Server:
                    Expression<Func<Release, string>> server = t => t.Server!.Name;
                    query = _params.OrderDesc ? query.OrderByDescending(server) : query.OrderBy(server);
                    break;
                case SearchCol.Channel:
                    Expression<Func<Release, string>> channel = t => t.Channel!.Name;
                    query = _params.OrderDesc ? query.OrderByDescending(channel) : query.OrderBy(channel);
                    break;
                case SearchCol.Bot:
                    Expression<Func<Release, string>> bot = t => t.Bot!.Name;
                    query = _params.OrderDesc ? query.OrderByDescending(bot) : query.OrderBy(bot);
                    break;
                case SearchCol.Pack:
                    Expression<Func<Release, int>> pack = t => t.Pack;
                    query = _params.OrderDesc ? query.OrderByDescending(pack) : query.OrderBy(pack);
                    break;
                case SearchCol.Size:
                    Expression<Func<Release, decimal>> size = t => t.Size;
                    query = _params.OrderDesc ? query.OrderByDescending(size) : query.OrderBy(size);
                    break;
                case SearchCol.Release:
                    Expression<Func<Release, string>> release = t => t.Title;
                    query = _params.OrderDesc ? query.OrderByDescending(release) : query.OrderBy(release);
                    break;
                case SearchCol.FirstSeen:
                    Expression<Func<Release, DateTime>> firstSeen = t => t.FirstSeen;
                    query = _params.OrderDesc ? query.OrderByDescending(firstSeen) : query.OrderBy(firstSeen);
                    break;
                default: break;
            }

            _items = await query
                .Take(256)
                .AsNoTracking()
                .ToListAsync();

            StateHasChanged();
        }
        catch (SqliteException se)
        {
            _items.Clear();
            _message = $"Syntax error - {se.Message}";
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