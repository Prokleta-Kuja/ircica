using System.Globalization;
using System.Text.Json;

namespace ircica;

public static class C
{
    public static readonly TimeZoneInfo DefaultTZ = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zagreb");
    public static readonly CultureInfo DefaultLocale = CultureInfo.GetCultureInfo("en-US");
    public static string GetHumanFileSize(FileInfo file)
    {
        var sizes = new string[] { "B", "KB", "MB", "GB", "TB" };
        double len = file.Length;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
        // show a single decimal place, and no space.
        return String.Format("{0:0.##} {1}", len, sizes[order]);
    }
    public static class Env
    {
        public static string Locale => Environment.GetEnvironmentVariable("LOCALE") ?? "en-US";
        public static string TimeZone => Environment.GetEnvironmentVariable("TZ") ?? "Europe/Zagreb";
    }
    public static class Routes
    {
        public const string Root = "/";
        public const string Forbidden = "/forbidden";
        public const string Search = "/search";
    }
    public static class Query
    {
        public const string Search = "search";
        public const string Sort = "sort";
        public const string Direction = "dir";
    }
    public static class Paths
    {
        public static string Config => Path.Combine(Environment.CurrentDirectory, "config");
        public static string ConfigFor(string file) => Path.Combine(Config, file);
        public static string Data => Path.Combine(Environment.CurrentDirectory, "data");
        public static string DataFor(string file) => Path.Combine(Data, file);
        public static readonly string AppDbConnectionString = $"Data Source={ConfigFor("app.db")}";
    }
    public static class Config
    {
        static readonly FileInfo file = new(Paths.ConfigFor("configuration.json"));
        static readonly JsonSerializerOptions serializerOptions = new()
        {
            WriteIndented = true,
            IgnoreReadOnlyProperties = true,
        };
        public static Settings Current { get; private set; } = new();
        public static async ValueTask LoadAsync()
        {
            if (file.Exists)
                Current = await LoadFromDiskAsync();
            else
                await SaveToDiskAsync(Current);
        }
        public static async Task<Settings> LoadFromDiskAsync()
        {
            var contents = await File.ReadAllTextAsync(file.FullName);
            var settings = JsonSerializer.Deserialize<Settings>(contents) ?? throw new JsonException("Could not load configuration file");
            return settings;
        }
        public static async ValueTask SaveToDiskAsync(Settings settings)
        {
            var contents = JsonSerializer.Serialize(settings, serializerOptions);
            await File.WriteAllTextAsync(file.FullName, contents);
        }
        public static ValueTask SaveToDiskAsync() => SaveToDiskAsync(Current);
    }
}

public class Settings
{
    public string UserName { get; set; } = null!;
    public string RealName { get; set; } = null!;
    public string NickName { get; set; } = null!;
    public List<IrcServer> Servers { get; set; } = new();
    public bool Validate(out string? message)
    {
        if (string.IsNullOrWhiteSpace(UserName))
        {
            message = $"{nameof(UserName)} cannot be empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RealName))
        {
            message = $"{nameof(RealName)} cannot be empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(NickName))
        {
            message = $"{nameof(NickName)} cannot be empty";
            return false;
        }

        message = null;
        return true;
    }
}