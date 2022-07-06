namespace ircica.Entities;

public class Bot
{
    public Bot(string name)
    {
        Name = name;
    }
    public int BotId { get; set; }
    public string Name { get; set; }

    public virtual ICollection<Release> Releases { get; set; } = new HashSet<Release>();
}