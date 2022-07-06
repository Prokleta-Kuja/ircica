namespace ircica.Entities;

public class Channel
{
    public Channel(string name)
    {
        Name = name;
    }

    public int ChannelId { get; set; }
    public string Name { get; set; }

    public virtual ICollection<Release> Releases { get; set; } = new HashSet<Release>();
}