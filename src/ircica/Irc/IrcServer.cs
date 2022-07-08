namespace ircica;

public class IrcServer
{
    public string Name { get; set; } = null!;
    public string Url { get; set; } = null!;
    public int Port { get; set; }
    public bool SSL { get; set; }
    public HashSet<string> Channels { get; set; } = new();
}