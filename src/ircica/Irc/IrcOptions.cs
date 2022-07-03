namespace ircica;

public class IrcServer
{
    public string Name { get; set; } = null!;
    public string Url { get; set; } = null!;
    public int Port { get; set; }
    public bool SSL { get; set; }
    public HashSet<string> Channels { get; set; } = new();
}
public class IrcOptions
{
    public IrcOptions(string userName, string realName, string nickName, IrcServer server)
    {
        UserName = userName;
        RealName = realName;
        NickName = nickName;
        Server = server;
    }

    public string UserName { get; set; }
    public string RealName { get; set; }
    public string NickName { get; set; }
    public IrcServer Server { get; set; }
}
[Flags]
public enum IrcState
{
    Unknown = 0,
    Connected = 1,
    Indexing = 2,
    Error = 4,
    Disposed = 8,
}