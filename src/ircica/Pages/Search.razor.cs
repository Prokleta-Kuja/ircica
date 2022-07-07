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
        if (System.Diagnostics.Debugger.IsAttached)
            opt.LogTo(message => Console.WriteLine(message), new[] { Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted });
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
                .AsNoTracking()
                .ToListAsync();
        }
        catch (SqliteException)
        {
            _items.Clear();
            _message = "Syntax error";
        }
    }
    static readonly string[] s_sizes = { "B", "KB", "MB", "GB", "TB" };
    string GetHumanSize(double bytes)
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