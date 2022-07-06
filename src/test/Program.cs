using System.Text.RegularExpressions;

namespace test;

public static class Program
{
    const string USER = "hippo2";
    const string REAL = "Hippo2";
    const string NICK = "hippo2";
    public static async Task Main()
    {
        {///msg [MG]-X265|EU|S|WhiteWidow xdcc send #763 irc://irc.abjects.net/moviegods
            // var exampleTransfer = new Transfer(USER, REAL, NICK);
            // var server = "irc.abjects.net";
            // var channel = "#moviegods";
            // var bot = "[MG]-X265|EU|S|WhiteWidow";
            // var pack = "#763";
            // await exampleTransfer.Start(server, channel, bot, pack);
        }
        {
            // var indexer = new Indexer(USER, REAL, NICK);
            // var server = "irc.rizon.net";
            // var channels = new string[] { "#Batcave" };
            // await indexer.Start(server, channels);
        }
        {
            var lines = File.ReadAllLines("kita");
            foreach (var line in lines)
            {
                var data = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var idx = data[0].IndexOf('!');
                var sender = data[0][1..idx];

                var channel = data[2];
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
                Console.WriteLine($"{packStr}-{pack}\t{sizeStr}-{size}\t{release}");
            }
        }
    }
}