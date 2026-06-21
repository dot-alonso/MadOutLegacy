using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace MadOutLegacy.AlwaysOnlineServer;

public static class Server
{
    public static int Main(string[] args)
    {
        var config = AlwaysOnlineConfig.LoadFromArgs(args);

        Console.WriteLine("------------------------------------------------");
        Console.WriteLine("MadOutLegacy AlwaysOnline Standalone Server v1.0");
        Console.WriteLine("https://github.com/dot-alonso/MadOutLegacy");
        Console.WriteLine("Press Ctrl+C to stop");
        Console.WriteLine("------------------------------------------------");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        RawUdpAlwaysOnlineListener listener;
        try
        {
            listener = new RawUdpAlwaysOnlineListener(config);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to bind IPv4 {config.BindIPv4}:{config.ListenPort}: {ex.Message}");
            return 2;
        }

        listener.Start(cts.Token);

        while (!cts.IsCancellationRequested)
        {
            Thread.Sleep(100);
        }

        listener.Stop();

        Console.WriteLine("Stopped.");
        return 0;
    }
}

public sealed class RawUdpAlwaysOnlineListener
{
    private static readonly byte[] OkResponseBytes = Encoding.ASCII.GetBytes("ok");

    private readonly AlwaysOnlineConfig _config;
    private readonly Socket _socket;
    private Thread? _thread;
    private volatile bool _stopped;

    public RawUdpAlwaysOnlineListener(AlwaysOnlineConfig config)
    {
        _config = config;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        IPAddress ip = IPAddress.Parse(config.BindIPv4);
        _socket.Bind(new IPEndPoint(ip, config.ListenPort));
    }

    public void Start(CancellationToken token)
    {
        _thread = new Thread(() => ReceiveLoop(token));
        _thread.IsBackground = true;
        _thread.Name = "AlwaysOnline UDP IPv4";
        _thread.Start();

        Console.WriteLine($"Listening IPv4 on {_config.BindIPv4}:{_config.ListenPort}");
    }

    public void Stop()
    {
        _stopped = true;

        try
        {
            _socket.Close();
        }
        catch
        {
        }
    }

    private void ReceiveLoop(CancellationToken token)
    {
        byte[] buffer = new byte[2048];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (!_stopped && !token.IsCancellationRequested)
        {
            try
            {
                EndPoint from = remote;
                int count = _socket.ReceiveFrom(buffer, ref from);
                if (count <= 0)
                {
                    continue;
                }

                string requestText = Encoding.ASCII.GetString(buffer, 0, count).Trim('\0', '\r', '\n', ' ');
                bool knownPlatform = _config.AllowedRequests.Count == 0 || _config.AllowedRequests.Any(x => string.Equals(x, requestText, StringComparison.OrdinalIgnoreCase));
                bool shouldReply = knownPlatform || _config.ReplyToUnknownRequests;

                if (_config.LogRequests)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] <- {from} bytes={count} text='{requestText}' known={knownPlatform}");
                }

                if (!shouldReply)
                {
                    continue;
                }

                if (_config.ResponseDelayMs > 0)
                {
                    Thread.Sleep(_config.ResponseDelayMs);
                }

                _socket.SendTo(OkResponseBytes, from);

                if (_config.LogRequests)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] -> {from} bytes={OkResponseBytes.Length} text='ok'");
                }
            }
            catch (SocketException ex) when (_stopped || token.IsCancellationRequested || ex.SocketErrorCode == SocketError.Interrupted || ex.SocketErrorCode == SocketError.OperationAborted)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Receive loop error: {ex}");
                Thread.Sleep(250);
            }
        }
    }
}

public sealed class AlwaysOnlineConfig
{
    public int ListenPort { get; set; } = 40022;
    public string BindIPv4 { get; set; } = "0.0.0.0";
    public bool ReplyToUnknownRequests { get; set; } = false;
    public bool LogRequests { get; set; } = true;
    public int ResponseDelayMs { get; set; } = 0;

    public List<string> AllowedRequests { get; set; } = new()
    {
        "Steam",
        "Android",
        "iOS",
        "ServerManager"
    };

    public static AlwaysOnlineConfig LoadFromArgs(string[] args)
    {
        string configPath = GetArg(args, "--config") ?? "config.json";
        AlwaysOnlineConfig config;

        if (File.Exists(configPath))
        {
            string json = File.ReadAllText(configPath);
            config = JsonSerializer.Deserialize<AlwaysOnlineConfig>(json, JsonOptions()) ?? new AlwaysOnlineConfig();
        }
        else
        {
            config = new AlwaysOnlineConfig();
            TryWriteExample(configPath, config);
        }

        string? port = GetArg(args, "--port");
        if (int.TryParse(port, out int parsedPort) && parsedPort > 0 && parsedPort <= 65535)
        {
            config.ListenPort = parsedPort;
        }

        string? bind4 = GetArg(args, "--bindIP");
        if (!string.IsNullOrWhiteSpace(bind4))
        {
            config.BindIPv4 = bind4;
        }

        if (HasArg(args, "--quiet"))
        {
            config.LogRequests = false;
        }

        return config;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            string prefix = name + "=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return arg.Substring(prefix.Length);
            }
        }

        return null;
    }

    private static bool HasArg(string[] args, string name)
    {
        return args.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    private static void TryWriteExample(string configPath, AlwaysOnlineConfig config)
    {
        try
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(configPath));
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(configPath, JsonSerializer.Serialize(config, JsonOptions()));
            Console.WriteLine($"Created example config: {configPath}");
        }
        catch
        {
        }
    }
}