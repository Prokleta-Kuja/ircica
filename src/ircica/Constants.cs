using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ircica;

public static class C
{
    public static JsonSerializerOptions JsonOpt { get; } = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };
    public static Settings Settings { get; set; } = new();
    public static readonly TimeZoneInfo DefaultTZ = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zagreb");
    public static readonly CultureInfo DefaultLocale = CultureInfo.GetCultureInfo("en-US");
    static readonly string[] s_sizes = { "B", "KB", "MB", "GB", "TB" };
    public static string GetHumanBytesSize(decimal bytes)
    {
        int order = 0;
        while (bytes >= 1024 && order < s_sizes.Length - 1)
        {
            order++;
            bytes /= 1024;
        }

        // Adjust the format string to your preferences. For example "{0:0.#}{1}" would
        // show a single decimal place, and no space.
        return $"{bytes:0.##} {s_sizes[order]}";
    }
    public static string GetHumanFileSize(FileInfo file) => GetHumanBytesSize(file.Length);
    public static string GetHumanFileSize(string path) => GetHumanFileSize(new FileInfo(path));
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
        public const string Download = "/download";
        public const string NewznabApi = "/newznab/api";
        public const string NewznabDownload = "/newznab/download/{id}";
        public static string NeznabDownloadFor(Guid id) => $"/newznab/download/{id}";
    }
    public static class Paths
    {
        public static string Config => Path.Combine(Environment.CurrentDirectory, "config");
        public static string ConfigFor(string file) => Path.Combine(Config, file);
        public static string Blackhole => Path.Combine(Environment.CurrentDirectory, "blackhole");
        public static string BlackholeFor(string file) => Path.Combine(Blackhole, file);
        public static string Incomplete => Path.Combine(Environment.CurrentDirectory, "incomplete");
        public static string IncompleteFor(string file) => Path.Combine(Incomplete, file);
        public static string Complete => Path.Combine(Environment.CurrentDirectory, "complete");
        public static string CompleteFor(string file) => Path.Combine(Complete, file);

        public static string ActiveDbFile => ConfigFor("app.db");
        public static string InactiveDbFile => ConfigFor("prev.db");
        public static string GetTempDbFile() => ConfigFor($"{Guid.NewGuid()}.tmp");
        public const string InMemoryDbConnectionString = "Data Source=:memory:";
        public static string ActiveDbConnectionString => $"Data Source={ActiveDbFile}";
        public static string GetDbConnectionString(string path) => $"Data Source={path}";
    }
}

public class Settings
{
    public string UserName { get; set; } = "ircica";
    public string RealName { get; set; } = "ircica";
    public string NickName { get; set; } = "ircica";
    public bool AutoConnect { get; set; } = true;
    public bool AutoCollect { get; set; } = true;
    public int BuildIndexEveryHours { get; set; } = 3;
    public int ExpireDownloadsOlderThanMinutes { get; set; } = 15;
    public int RemoveAnnouncmentsNotSeenHours { get; set; } = 6;
    public int RestartAfterInactivityMinutes { get; set; } = 5;
    public List<IrcServer> Servers { get; set; } = new() { new() { Name = "Something", Url = "irc.something.net", Channels = new() { "A", "B" }, Port = 6667 } };
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

        if (BuildIndexEveryHours <= 0)
        {
            message = $"{nameof(BuildIndexEveryHours)} must be larger than 0";
            return false;
        }

        if (ExpireDownloadsOlderThanMinutes <= 0)
        {
            message = $"{nameof(ExpireDownloadsOlderThanMinutes)} must be larger than 0";
            return false;
        }

        if (RemoveAnnouncmentsNotSeenHours <= 0)
        {
            message = $"{nameof(RemoveAnnouncmentsNotSeenHours)} must be larger than 0";
            return false;
        }

        message = null;
        return true;
    }
}