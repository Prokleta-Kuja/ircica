using System.Security.Cryptography;
using System.Text;

namespace ircica;

public class IrcMessage
{
    protected readonly static MD5 s_hasher = MD5.Create();
    public static IrcMessage Parse(string line, string nickName)
    {
        var data = line.Split(' ');
        if (data.Length < 2)
            return new IrcUnknownMessage(data);

        if (data[0].Equals("PING", StringComparison.InvariantCultureIgnoreCase))
            return new IrcPingMessage(data[1]);

        if (data[1] == "376" || data[1] == "422")
            return new IrcMotdEndMessage();

        if (data.Length >= 4 && data[1].Equals("PRIVMSG", StringComparison.InvariantCultureIgnoreCase))
        {
            if (data[2] == nickName)
            {
                if (data[3] == ":\u0001VERSION\u0001")
                    return new IrcVersionMessage(data);
                else
                    return new IrcDirectMessage(data);
            }
            if (data[3].StartsWith(":\u0001#"))
                return new IrcAnnouncementMessage(line, data);

        }
        return new IrcUnknownMessage(data);
    }
}



public class IrcUnknownMessage : IrcMessage
{
    public string[] Data { get; }
    public IrcUnknownMessage(string[] data)
    {
        Data = data;
    }
}

public class IrcPingMessage : IrcMessage
{
    public string Id { get; }
    public IrcPingMessage(string id)
    {
        Id = id;
    }
    public async Task WriteResponseAsync(StreamWriter writer)
    {
        await writer.WriteLineAsync($"PONG {Id}");
        await writer.FlushAsync();
    }
}

public class IrcMotdEndMessage : IrcMessage
{
    public async Task JoinChannels(HashSet<string> channels, StreamWriter writer)
    {
        foreach (var channel in channels)
        {
            await writer.WriteLineAsync($"JOIN #{channel.TrimStart('#')}");
            await writer.FlushAsync();
        }
    }
}

public class IrcVersionMessage : IrcMessage
{
    public string Sender { get; }
    public IrcVersionMessage(string[] data)
    {
        var idx = data[0].IndexOf('!');
        Sender = data[0][1..idx];
    }
    public async Task WriteResponseAsync(StreamWriter writer)
    {
        var response = $"NOTICE {Sender} :\u0001VERSION test 0.1\u0001";
        await writer.WriteLineAsync(response);
        await writer.FlushAsync();
    }
}
public class IrcDirectMessage : IrcMessage
{
    public string Sender { get; }
    public IEnumerable<string> Message { get; }
    public DateTime Sent { get; } = DateTime.UtcNow;
    public IrcDirectMessage(string[] data)
    {
        var idx = data[0].IndexOf('!');
        Sender = data[0][1..idx];
        Message = data.Skip(3);
    }
}

public class IrcAnnouncementMessage : IrcMessage
{
    public byte[] Hash { get; }
    public string Sender { get; }
    public string Channel { get; set; }
    public IrcAnnouncementMessage(string line, string[] data)
    {
        var lineBytes = Encoding.UTF8.GetBytes(line);
        Hash = s_hasher.ComputeHash(lineBytes);
    }
}