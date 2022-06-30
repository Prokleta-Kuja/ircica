namespace test;

public static class Program
{
    const string USER = "kuja2";
    const string REAL = "kita";
    const string NICK = "kujana";
    public static async Task Main()
    {
        {
            // var exampleTransfer = new Transfer(USER, REAL, NICK);
            // var server = "irc.rizon.net";
            // var channel = "#Batcave";
            // var bot = "[FutureBot]-[C21]";
            // var pack = "#101";
            // await exampleTransfer.Start(server, channel, bot, pack);
        }
        {
            var indexer = new Indexer(USER, REAL, NICK);
            var server = "irc.rizon.net";
            var channels = new string[] { "#Batcave" };
            await indexer.Start(server, channels);
        }
    }
}