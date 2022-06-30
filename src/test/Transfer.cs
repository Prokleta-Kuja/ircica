using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace test;

public class Transfer
{
    public Transfer(string user, string real, string nick)
    {
        User = user;
        Real = real;
        Nick = nick;
    }

    public string User { get; }
    public string Real { get; }
    public string Nick { get; }
    public async Task Start(string server, string channel, string bot, string pack)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(server, 6667);

        using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream);

        writer.WriteLine($"USER {User} 0 * {Real}");
        writer.WriteLine($"NICK {Nick}");
        writer.Flush();

        var download = Task.CompletedTask;
        while (client.Connected)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // See whats going on in the channel
            Console.WriteLine($"Received: {line}");

            var d = line.Split(' ');

            // Must always respond or the server will close the connection
            if (d[0] == "PING")
                writer.WriteLine($"PONG {d[1]}");
            else if (d.Length > 1)
            {
                switch (d[1])
                {
                    // Wait untill server is finished with MOTD
                    case "376": // Indicates the end of the Message of the Day to the client. 
                    case "422": // Indicates that the Message of the Day file does not exist or could not be found. 
                        {
                            // Must be on a known channel to request a pack
                            writer.WriteLine($"JOIN {channel}");
                            // Ask the bot to send us specific pack and wait in queue
                            writer.WriteLine($"PRIVMSG {bot} :xdcc send {pack}");
                            break;
                        }
                    case "PRIVMSG":
                        {
                            // Bunch of messagess being received, ignore them if they are not ment for us
                            if (d[2] != Nick)
                                break;

                            // Respond with softwer used and it's version
                            if (d[3] == ":\u0001VERSION\u0001")
                            {
                                var idx = d[0].IndexOf('!');
                                var sender = d[0][1..idx];
                                var response = $"NOTICE {sender} :\u0001VERSION test 0.1\u0001";
                                writer.WriteLine(response);
                            }
                            // Bot is ready to serve our request and is sending detail for download
                            else if (d[3].StartsWith(":\u0001DCC") && d[4] == "SEND")
                            {
                                var filename = d[5].Trim('"');
                                var ip = d[6];
                                var port = d[7];
                                var size = d[8].Replace("\u0001", string.Empty);

                                Console.WriteLine($"Downloading {filename} from {ip}:{port}");
                                download = Download(filename, ip, port, size);

                                // Download is happening in the background so we can disconnect from irc
                                writer.WriteLine("QUIT");
                                writer.Flush();
                                client.Close();
                            }
                            else
                                Console.WriteLine($"Unhandled: {line}");

                            break;
                        }
                }
            }

            writer.Flush();
        }
        await download;
    }

    static async Task Download(string filename, string ipStr, string portStr, string sizeStr)
    {
        var ip = IPAddress.Parse(ipStr);
        var port = int.Parse(portStr);
        var size = long.Parse(sizeStr);
        var file = new FileInfo(filename);

        using var fileStream = file.OpenWrite();
        using var client = new TcpClient(ip.ToString(), port);
        using var clientStream = client.GetStream();

        long totalRead = 0;
        var buffer = new byte[1024 * 128];
        var sw = new Stopwatch();
        sw.Start();

        while (client.Connected && await clientStream.ReadAsync(buffer) is var read && read > 0)
        {
            totalRead += read;
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            Console.WriteLine($"{totalRead}/{size}");
            if (totalRead == size)
                client.Close();
        }

        sw.Stop();
        Console.WriteLine($"File downloaded in {sw.Elapsed.TotalSeconds}");
    }
}