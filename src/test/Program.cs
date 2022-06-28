using System.Net.Sockets;

namespace test;

public static class Program
{
    const string SERVER = "irc.freenode.net";
    const string USER = "kuja";
    const string REAL = "kuja_36314_malena";
    const string NICK = "YourBotNick";
    const string CHANNEL = "#CHANNEL_NAME";
    public static async Task Main()
    {
        using var client = new TcpClient();
        client.Connect(SERVER, 6667);

        using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream);

        writer.WriteLine($"USER {USER} 0 * {REAL}");
        writer.WriteLine($"NICK {NICK}");
        writer.Flush();

        while (client.Connected)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;


        }





        await Task.CompletedTask;
    }
}