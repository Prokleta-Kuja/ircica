namespace ircica.Entities;

public class Server
{
    public Server(string name, string url)
    {
        Name = name;
        Url = url;
    }
    public int ServerId { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public int Port { get; set; }
    public bool SSL { get; set; }

    public virtual ICollection<Release> Releases { get; set; } = new HashSet<Release>();
}