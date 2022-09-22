namespace ircica.Entities;

public class Release
{
    public Release(string title)
    {
        Title = title;
    }
    public int ReleaseId { get; set; }
    public Guid UniqueId { get; set; }
    public int ServerId { get; set; }
    public int ChannelId { get; set; }
    public int BotId { get; set; }
    public int Pack { get; set; }
    public decimal Size { get; set; }
    public string Title { get; set; }
    public DateTime FirstSeen { get; set; }

    public Server? Server { get; set; }
    public Channel? Channel { get; set; }
    public Bot? Bot { get; set; }
}