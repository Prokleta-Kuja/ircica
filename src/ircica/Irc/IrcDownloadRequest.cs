namespace ircica;

public class IrcDownloadRequest
{
    public IrcDownloadRequest(string channel, string bot, int pack)
    {
        Channel = channel;
        Bot = bot;
        Pack = pack;
    }

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