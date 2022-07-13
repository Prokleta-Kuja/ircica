using System.Text.Json.Serialization;

namespace ircica;

public class IrcDownloadRequest
{
    internal IrcDownloadRequest()
    {
        Server = null!;
        Channel = null!;
        Bot = null!;
    }
    public IrcDownloadRequest(string server, string channel, string bot, int pack)
    {
        Server = server;
        Channel = channel;
        Bot = bot;
        Pack = pack;
    }
    [JsonIgnore]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Server { get; set; }
    public string Channel { get; }
    public string Bot { get; }
    public int Pack { get; }

    public async Task RequestAsync(StreamWriter writer)
    {
        await writer.WriteLineAsync($"PRIVMSG {Bot} :xdcc send #{Pack}");
        await writer.FlushAsync();
        Console.WriteLine("Download requested");
    }
}