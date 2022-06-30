using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace test;

public class Indexer
{
    public Indexer(string user, string real, string nick)
    {
        User = user;
        Real = real;
        Nick = nick;
    }

    public string User { get; }
    public string Real { get; }
    public string Nick { get; }
    public HashSet<string> Announcements { get; set; } = new();
    public async Task Start(string server, params string[] channels)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(server, 6667);

        using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream);

        writer.WriteLine($"USER {User} 0 * {Real}");
        writer.WriteLine($"NICK {Nick}");
        writer.Flush();

        var runFor = TimeSpan.FromMinutes(15);
        var sw = new Stopwatch();
        sw.Start();
        while (client.Connected)
        {
            // TODO: Replace with cancellation tokens
            if (sw.Elapsed > runFor)
            {
                writer.WriteLine("QUIT");
                writer.Flush();
                client.Close();
                File.WriteAllLines("kita", Announcements);
                break;
            }

            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // See whats going on in the channel
            //Console.WriteLine($"Received: {line}");

            var d = line.Split(' ');

            // Must always respond or the server will close the connection
            if (d[0] == "PING")
            {
                writer.WriteLine($"PONG {d[1]}");
                writer.Flush();
            }
            else if (d.Length > 1)
            {
                switch (d[1])
                {
                    // Wait untill server is finished with MOTD
                    case "376": // Indicates the end of the Message of the Day to the client. 
                    case "422": // Indicates that the Message of the Day file does not exist or could not be found. 
                        {
                            // Join all channels
                            foreach (var channel in channels)
                            {
                                writer.WriteLine($"JOIN {channel}");
                                writer.Flush();
                            }

                            break;
                        }
                    case "PRIVMSG":
                        {
                            // Respond with softwer used and it's version
                            if (d[2] == Nick && d[3] == ":\u0001VERSION\u0001")
                            {
                                var idx = d[0].IndexOf('!');
                                var sender = d[0][1..idx];
                                var response = $"NOTICE {sender} :\u0001VERSION test 0.1\u0001";
                                writer.WriteLine(response);
                                writer.Flush();
                            }
                            // Not for us, so probably an announcement
                            else if (d[3].StartsWith(":\u0001#"))
                            {
                                if (Announcements.Add(line))
                                    Console.WriteLine($"Added ({Announcements.Count:#,##0}) {line}");
                                else
                                    Console.WriteLine("Already stored");
                            }

                            break;
                        }
                }
            }
        }
    }
}