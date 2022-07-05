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
}

public class Settings
{
    public string UserName { get; set; } = "ircica";
    public string RealName { get; set; } = "ircica";
    public string NickName { get; set; } = "ircica";
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

        message = null;
        return true;
    }
}