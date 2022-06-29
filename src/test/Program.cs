using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace test;

public static class Program
{
    //irc://irc.rizon.net/Batcave
    const string SERVER = "irc.rizon.net";
    const string USER = "kuja";
    const string REAL = "kuja_36314_malena";
    const string NICK = "kujan";
    const string CHANNEL = "#Batcave";
    public static async Task Main()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(SERVER, 6667);

        using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream);

        writer.WriteLine($"USER {USER} 0 * {REAL}");
        writer.WriteLine($"NICK {NICK}");
        writer.Flush();

        while (client.Connected)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            Console.WriteLine($"Received: {line}");
            var d = line.Split(' ');

            if (d[0] == "PING")
            {
                writer.WriteLine($"PONG {d[1]}");
            }
            else if (d.Length > 1)
            {
                switch (d[1])
                {
                    case "376":
                    case "422":
                        {
                            Console.WriteLine("Joining channel");
                            writer.WriteLine($"JOIN {CHANNEL}");
                            break;
                        }
                    /*
                        :alice!a@localhost PRIVMSG bob :\x01VERSION\x01
                        :bob!b@localhost NOTICE alice :\x01VERSION Snak for Mac 4.13\x01
                    */
                    case "PRIVMSG":
                        {
                            if (d[2] != NICK)
                                break;

                            if (d[3] == ":\u0001VERSION\u0001")
                            {
                                var idx = d[0].IndexOf('!');
                                var sender = d[0][1..idx];
                                var response = $"NOTICE {sender} :\u0001VERSION test0.1\u0001";
                                writer.WriteLine(response);

                                // /msg [FutureBot]-[C21] xdcc send #101
                                writer.WriteLine($"PRIVMSG [FutureBot]-[C21] :xdcc send #101");
                            }
                            /*
                                [0] [string]:":[FutureBot]-[C21]!~cha0s@Rizon-14009606.ip-37-187-117.eu"
                                [1] [string]:"PRIVMSG"
                                [2] [string]:"kujan"
                                [3] [string]:":\u0001DCC"
                                [4] [string]:"SEND"
                                [5] [string]:"[Alternative]_Rolling_Blackouts_Coastal_Fever-The_Way_It_Shatters-SINGLE-WEB-2022-ENRiCH.tar"
                                [6] [string]:"633042345"
                                [7] [string]:"52531"
                                [8] [string]:"10822699\u0001"
                            */

                            else if (d[3].StartsWith(":\u0001DCC") && d[4] == "SEND")
                            {
                                // :[FutureBot]-[C21]!~cha0s@Rizon-14009606.ip-37-187-117.eu PRIVMSG kujan :DCC SEND [Alternative]_Rolling_Blackouts_Coastal_Fever-The_Way_It_Shatters-SINGLE-WEB-2022-ENRiCH.tar 633042345 51665 10822699
                                var filename = d[5].Trim('"');
                                var ip = d[6];
                                var port = d[7];
                                var size = d[8].Replace("\u0001", string.Empty);

                                Console.WriteLine($"Downloading {filename} from {ip}:{port}");
                                await Download(filename, ip, port, size);
                            }
                            break;
                        }
                }
            }

            writer.Flush();
        }
    }

    static async Task Download(string filename, string ipStr, string portStr, string sizeStr)
    {
        var ip = IPAddress.Parse(ipStr);
        var port = int.Parse(portStr);
        var size = long.Parse(sizeStr); // trimati \u0001 sa kraja
        var file = new FileInfo(filename);

        using var fileStream = file.OpenWrite();
        using var client = new TcpClient(ip.ToString(), port);
        using var clientStream = client.GetStream();

        long totalRead = 0;
        var buffer = new byte[1024 * 128];
        var sw = new Stopwatch();
        sw.Start();
        while (await clientStream.ReadAsync(buffer) is int read && read > 0)
        {
            totalRead += read;
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            Console.WriteLine($"{totalRead}/{size}");
            if (totalRead == size)
            {
                client.Close();
                break;
            }
        }

        sw.Stop();
        //await clientStream.CopyToAsync(fileStream,);
        Console.WriteLine($"File downloaded in {sw.Elapsed.TotalSeconds}");
    }
}