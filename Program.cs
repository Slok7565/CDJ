﻿using Discord;
using Discord.WebSocket;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.RegularExpressions;
using System.IO.Compression;

namespace CDJ;

public static class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().
            WriteTo.Console()
            .WriteTo.File("log.txt")
            .CreateBootstrapLogger();
        
        try
        {
            Create(args).Build().Run();
            Log.Logger.Information("Run successfully");
        }
        catch (Exception e)
        {
            Log.Logger.Error($"Run Error: \n {e}");
        } 
    }

    public static IHostBuilder Create(string[] args)
    {
        try
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddControllers();
                        services.AddEndpointsApiExplorer();
                    });

                    webBuilder.Configure((context, app) =>
                    {
                        if (context.HostingEnvironment.IsDevelopment())
                        {
                            app.UseDeveloperExceptionPage();
                        }

                        app.UseRouting();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                            endpoints.MapGet("/api/user", ISAuthService.HandleGetUserRequest);
                        });
                    });
                })
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("config.json");
                })
                .ConfigureServices((context, collection) =>
                {
                    collection.AddHostedService<CDJService>();
                    collection.AddSingleton<SocketService>();
                    collection.AddSingleton<OneBotService>();
                    collection.AddSingleton<DiscordBotService>();
                    collection.AddSingleton<RoomsService>();
                    collection.AddSingleton<EACService>();
                    collection.AddSingleton<ActiveService>();
                    collection.AddScoped<HttpClient>();
                    collection.AddScoped<DiscordSocketClient>();
                    collection.Configure<ServerConfig>(context.Configuration);
                })
                .UseSerilog();

            return hostBuilder;
        }
        catch (Exception e)
        {
            Log.Logger.Error($"Run Error: \n {e}");
            throw;
        }
    }
}

public class CDJService
(
    ILogger<CDJService> logger,
    SocketService socketService, 
    OneBotService oneBotService,
    DiscordBotService discordBotService,
    EACService eacService,
    ActiveService activeService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        
        
        try
        {
            if (!socketService.CreateSocket())
                logger.LogError("Failed to create socket");
        }
        catch
        {
            // ignored
        }
        
        try
        {
            if (!eacService.CreateSocket())
                logger.LogError("Failed to CreateEAC");
        }
        catch
        {
            // ignored
        }

        try
        {
            if (!await oneBotService.ConnectBot())
                logger.LogError("Failed to Connect Bot");
            await oneBotService.Read();
        }
        catch
        {
            // ignored
        }

        try
        {
            await activeService.StartAsync();
        }
        catch
        {
            // ignored
            logger.LogError("Start Error Active Service");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await socketService.Stop();
        await oneBotService.Stop();
        await discordBotService.Stop();
        await eacService.Stop();
        await activeService.StopAsync();
    }
}
public class ISAuthService
{
    public static async Task HandleGetUserRequest(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        var bearerToken = authHeader.ToString().Replace("Bearer ", "");

        (int authStatus, string friendcode) = await AuthWithIS(bearerToken);

        if (authStatus != StatusCodes.Status200OK)
        {
            context.Response.StatusCode = authStatus;
            await context.Response.WriteAsync(authStatus + " Unauthorized");
        }

        var query = context.Request.Query;
        var username = query["username"].ToString();
        var puid = query["puid"].ToString();

        Log.Logger.Information($"Bearer Token: {bearerToken}, Username: {username}, PUID: {puid}");

        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync("");
    }

    public static async Task<(int status, string friendcode)> AuthWithIS(string bearerToken)
    {
        try
        {
            // Create a new HttpClient
            using (var client = new HttpClient())
            {
                // Create a new HttpRequestMessage
                var request = new HttpRequestMessage();

                // Set the method to GET
                request.Method = HttpMethod.Get;

                // Set the URL
                var url = "https://backend.innersloth.com/api/user/username";
                request.RequestUri = new Uri(url);

                // Set the headers
                request.Headers.Add("Accept", "application/vnd.api+json");
                request.Headers.Add("Accept-Encoding", "deflate, gzip");
                request.Headers.Add("User-Agent", "UnityPlayer/2020.3.45f1 (UnityWebRequest/1.0, libcurl/7.84.0-DEV)");
                request.Headers.Add("X-Unity-Version", "2020.3.45f1");
                request.Headers.Add("Authorization", "Bearer " + bearerToken);

                // Send the request
                var response = await client.SendAsync(request);

                // Check the response status code
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return (StatusCodes.Status401Unauthorized, string.Empty);
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (StatusCodes.Status200OK, string.Empty);
                }
                else if (response.IsSuccessStatusCode)
                {
                    var contentStream = await response.Content.ReadAsStreamAsync();
                    Stream decompressedStream;

                    if (response.Content.Headers.ContentEncoding.Contains("gzip"))
                    {
                        decompressedStream = new GZipStream(contentStream, CompressionMode.Decompress);
                    }
                    else if (response.Content.Headers.ContentEncoding.Contains("deflate"))
                    {
                        decompressedStream = new DeflateStream(contentStream, CompressionMode.Decompress);
                    }
                    else
                    {
                        decompressedStream = contentStream;
                    }

                    using (var reader = new StreamReader(decompressedStream))
                    {
                        var content = await reader.ReadToEndAsync();
                        var jsonDocument = JsonDocument.Parse(content);
                        var root = jsonDocument.RootElement;

                        if (root.TryGetProperty("data", out var dataProperty) &&
                            dataProperty.TryGetProperty("attributes", out var attributesProperty))
                        {
                            var username = attributesProperty.GetProperty("username").GetString();
                            var discriminator = attributesProperty.GetProperty("discriminator").GetString();
                            var friendcode = $"{username}#{discriminator}";

                            return (StatusCodes.Status200OK, friendcode);
                        }

                        throw new Exception("Could not extract friendcode from response content.");
                    }
                }
                else
                {
                    return (StatusCodes.Status401Unauthorized, string.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            // Log the error (Assuming _logger is accessible, otherwise, you can handle logging accordingly)
            Log.Logger.Error(ex.ToString() + " at IS auth");

            // Catch any other exceptions
            return (StatusCodes.Status401Unauthorized, string.Empty);
        }
    }
}

public class SocketService(ILogger<SocketService> logger, RoomsService roomsService, OneBotService oneBotService, DiscordBotService discordBotService, IOptions<ServerConfig> config)
{
    public TcpListener _TcpListener = null!;
    
    private readonly ServerConfig _config = config.Value;

    public IPAddress Address => IPAddress.Parse(_config.Ip);
    public Task? _Task;
    private readonly CancellationTokenSource _cancellationToken = new();

    public List<Socket> _Sockets = [];
    
    public bool CreateSocket()
    {
        logger.LogInformation("CreateSocket");
        _TcpListener = new TcpListener(Address, _config.Port);
        _TcpListener.Start();
        logger.LogInformation($"Start :{_config.Ip} {_config.Port} {_config.SendToQQ_Group} {_config.BotHttpUrl} {_config.QQID}");
        _cancellationToken.Token.Register(() => _TcpListener.Dispose());
        _Task = Task.Factory.StartNew(async () =>
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var socket = await _TcpListener.AcceptSocketAsync();
                    var bytes = new byte[60];
                    var count =await socket.ReceiveAsync(bytes);
                    var str = Encoding.Default.GetString(bytes).TrimEnd('\0');
                    logger.LogInformation($"sokcet {_config.Ip} {_config.Port} {str}");
                    
                    if (str == "test")
                    {
                        await socket.SendAsync(Encoding.Default.GetBytes("Test Form SERVER"));
                        continue;
                    }

                    roomsService.TryGetRoom(str, out var room);
                    try
                    {
                        var message_QQ = roomsService.ParseRoom_QQ(room);
                        await oneBotService.SendMessage(message_QQ);
                    }
                    catch { }
                    try
                    {
                        var message_DC = roomsService.ParseRoom_DC(room);
                        await discordBotService.SendMessage(message_DC);
                    }
                    catch { }
                }
                catch (Exception e)
                {
                    logger.LogError(e.ToString());
                }
            }
        }, TaskCreationOptions.LongRunning);
        return true;
    }

    public async Task Stop()
    {
        await _cancellationToken.CancelAsync();
        _Task?.Dispose();
    }
}

public class OneBotService(ILogger<OneBotService> _logger, IOptions<ServerConfig> config, HttpClient _client)
{
    private readonly ServerConfig _config = config.Value;
    public bool ConnectIng;
    public List<(long, bool)> _reads = [];

    public async Task Read()
    {
        if (_config.ReadPath == string.Empty) return;
        await using var stream = File.Open(_config.ReadPath, FileMode.OpenOrCreate);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) continue;
            var str = line.Replace(" ", string.Empty).Split('|');
            _reads.Add((long.Parse(str[0]), bool.Parse(str[1])));
        }
    }

    public async Task<bool> ConnectBot()
    {
        var get = await _client.GetAsync($"{_config.BotHttpUrl}/get_login_info");
        if (!get.IsSuccessStatusCode)
            return false;

        _logger.LogInformation(await get.Content.ReadAsStringAsync());
        ConnectIng = true;

        return true;
    }

    public async Task SendMessage(string message)
    {
        if (!ConnectIng)
            await ConnectBot();
        if (_config.QQID != 0)
        {
            if (_config.SendToQQ_Group)
            {
                await SendMessageToQQ_Group(message, _config.QQID);
            }
            else
            {
                await SendMessageToQQ_ContactPerson(message, _config.QQID);
            }
        }
        else
        {
            foreach (var (id, isQQ_Group) in _reads)
            {
                if (isQQ_Group)
                {
                    await SendMessageToQQ_Group(message, id);
                }
                else
                {
                    await SendMessageToQQ_ContactPerson(message, id);
                }
            }
        }
    }

    public async Task SendMessageToQQ_Group(string message, long id)
    {
        var jsonString = JsonSerializer.Serialize(new GroupMessage
        {
            message = message,
            group_id = id.ToString()
        });
        await _client.PostAsync($"{_config.BotHttpUrl}/send_group_msg", new StringContent(jsonString));
        _logger.LogInformation($"Send To Group id:{id} message:{message}");
    }

    public async Task SendMessageToQQ_ContactPerson(string message, long id)
    {
        var jsonString = JsonSerializer.Serialize(new UserMessage
        {
            message = message,
            user_id = id.ToString()
        });
        await _client.PostAsync($"{_config.BotHttpUrl}/send_private_msg", new StringContent(jsonString));
        _logger.LogInformation($"Send To User id:{id} message:{message}");
    }

    public Task Stop()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

}

public class DiscordBotService(ILogger<DiscordBotService> _logger, DiscordSocketClient _client)
{
    //private readonly ILogger<DiscordBotService> _logger;
    //private readonly DiscordSocketClient _client;
    private readonly List<ulong> _channelIds = new();

    //public DiscordBotService
    //{
    //    _logger = logger;
    //    _client = client;
    //}

    private bool _connecting;

    public async Task<bool> ConnectBot()
    {
        if (_connecting)
        {
            return true; // Already connecting, do nothing
        }

        try
        {
            _connecting = true;
            if (_client == null)
            {
                _logger.LogError("Error: Client is not initialized.");
                _connecting = false;
                return false;
            }

            string token = "S93tx9SWr6wARS9T1CLnlfRIRaoNwZ_O";
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _logger.LogInformation("Connected to Discord bot successfully.");
            _connecting = false;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to connect to Discord bot: {ex.Message}");
            _connecting = false;
            return false;
        }
    }

    public async Task SendMessage(string message)
    {
        if (!await ConnectBot())
        {
            return;
        }

        foreach (var guild in _client.Guilds)
        {
            foreach (var channelId in _channelIds.ToList())  // 使用ToList()复制一份集合进行循环
            {
                var targetChannel = guild.GetChannel(channelId) as ISocketMessageChannel;
                if (targetChannel != null)
                {
                    await targetChannel.SendMessageAsync(message);
                    _logger.LogInformation($"Sent message to channel id: {channelId}");
                }
            }
        }
    }

    public Task Stop()
    {
        _connecting = false;
        _client?.Dispose();
        return Task.CompletedTask;
    }
}

public class Login_Info
{
    public long user_id { get; set; }
    public string nickname { get; set; }
}

public abstract class SendMessage
{
    public abstract string message_type { get; }
    
    public string message { get; set; }
}

public class GroupMessage : SendMessage
{
    public override string message_type { get; } = "group";
    public string group_id { get; set; }
}

public class UserMessage : SendMessage
{
    public override string message_type { get; } = "private";
    public string user_id { get; set; }
}

public class RoomsService
{
    public readonly List<Room> _Rooms = [];
    
    public bool TryGetRoom(string text,[MaybeNullWhen(false)] out Room room)
    {
        room = null;
        var strings = text.Split('|');
        Log.Logger.Information("Get Room Length");
        if (strings.Length < 6) return false;

        Log.Logger.Information("Get Room code");
        var code = strings[0];
        if (code.Length != 6) return false;

        Log.Logger.Information("Get Room Version");
        var BuildVersion = "";
        string version = strings[1];

        bool isValidVersion = IsVersionValid(version);
        if (isValidVersion) return false;

        Log.Logger.Information("Get Room Player Count");
        string input = strings[2]; 
        string pattern = @"^\d+$";
        if (!Regex.IsMatch(input, pattern)) return false;
        var count = int.Parse(strings[2]);

        Log.Logger.Information("Get Room Language");
        var langId = Enum.Parse<LangName>(strings[3]);

        Log.Logger.Information("Get Room Server");
        var serverName = strings[4];

        Log.Logger.Information("Get Room Host");
        var playName = strings[5];
        
        room = new Room(code, version, count, langId, serverName, playName, BuildVersion);
        _Rooms.Add(room);
        Log.Logger.Information("Get Room");
        return true;
    }
    static bool IsVersionValid(string version)
    {
        string pattern = @"^\d+\.\d+_\d{8}_(Debug|Canary|Dev|Preview)(?:_\d+)?$";
        Regex regex = new (pattern);
        return regex.IsMatch(version);
    }

    public string ParseRoom_QQ(Room room)
    {
        var ln = lang_forZh.TryGetValue(room.LangId, out var value) ? value : Enum.GetName(room.LangId);
        var ver = room.Version == null ? room.BuildId : room.Version.ToString();
        var def = $@"房间号: {room.Code}
版本号: {ver}
人数: {room.Count}
语言: {ln}
服务器: {room.ServerName}
房主: {room.PlayerName}
"; ;
        Log.Logger.Information(def);
        return def;
    }

    public static readonly Dictionary<LangName, string> lang_forZh = new()
    {
        { LangName.English, "英语" },
    { LangName.Latam, "拉丁美洲" },
    { LangName.Brazilian, "巴西" },
    { LangName.Portuguese, "葡萄牙" },
    { LangName.Korean, "韩语" },
    { LangName.Russian, "俄语" },
    { LangName.Dutch, "荷兰语" },
    { LangName.Filipino, "菲律宾语" },
    { LangName.French, "法语" },
    { LangName.German, "德语" },
    { LangName.Italian, "意大利语" },
    { LangName.Japanese, "日语" },
    { LangName.Spanish, "西班牙语" },
    { LangName.SChinese, "简体中文" },
    { LangName.TChinese, "繁体中文" },
    { LangName.Irish, "爱尔兰语" }
    };

    public string ParseRoom_DC(Room room)
    {
        var ln = lang_forEn.TryGetValue(room.LangId, out var value) ? value : Enum.GetName(room.LangId);
        var ver = room.Version == null ? room.BuildId : room.Version.ToString();
        var def = $@"Room Code: {room.Code}
Version: {ver}
People: {room.Count}
Language: {ln}
Server: {room.ServerName}
Host: {room.PlayerName}
"; ;
        Log.Logger.Information(def);
        return def;
    }

    public static readonly Dictionary<LangName, string> lang_forEn = new()
    {
      
    { LangName.English, "English" },
    { LangName.Latam, "Latam" },
    { LangName.Brazilian, "Brazilian" },
    { LangName.Portuguese, "Portuguese" },
    { LangName.Korean, "Korean" },
    { LangName.Russian, "Russian" },
    { LangName.Dutch, "Dutch" },
    { LangName.Filipino, "Filipino" },
    { LangName.French, "French" },
    { LangName.German, "German" },
    { LangName.Italian, "Italian" },
    { LangName.Japanese, "Japanese" },
    { LangName.Spanish, "Spanish" },
    { LangName.SChinese, "SChinese" },
    { LangName.TChinese, "TChinese" },
    { LangName.Irish, "Irish" }

};
}

public record Room(string Code, string Version, int Count, LangName LangId, string ServerName, string PlayerName, string BuildId = "");

public enum LangName : byte
{
    English,
    Latam,
    Brazilian,
    Portuguese,
    Korean,
    Russian,
    Dutch,
    Filipino,
    French,
    German,
    Italian,
    Japanese,
    Spanish,
    SChinese,
    TChinese,
    Irish
}


public class EACService
{
    private readonly ServerConfig _Config;
    private readonly Stream _stream;
    private readonly StreamWriter _writer;
    private readonly ILogger<EACService> logger;
    private readonly OneBotService _oneBotService;

    public EACService(IOptions<ServerConfig> options, ILogger<EACService> logger, OneBotService oneBotService)
    {
        _Config = options.Value;
        _stream = File.Open(_Config.EACPath, FileMode.OpenOrCreate, FileAccess.Write);
        _writer = new StreamWriter(_stream)
        {
            AutoFlush = true
        };
        this.logger = logger;
        _oneBotService = oneBotService;
    }
    
    public TcpListener _TcpListener = null!;
    public IPAddress Address => IPAddress.Parse(_Config.Ip);
    public Task? _Task;
    private readonly CancellationTokenSource _cancellationToken = new();
    public List<Socket> _Sockets = [];

    public List<EACData> _EacDatas = [];
    
    public bool CreateSocket()
    {
        logger.LogInformation("CreateEACSocket");
        _TcpListener = new TcpListener(Address, _Config.EACPort);
        _TcpListener.Start();
        logger.LogInformation($"Start EAC:{_Config.Ip} {_Config.EACPort}");
        _cancellationToken.Token.Register(() => _TcpListener.Dispose());
        _Task = Task.Factory.StartNew(async () =>
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var socket = await _TcpListener.AcceptSocketAsync();
                    _Sockets.Add(socket);
                    var bytes = new byte[60];
                    await socket.ReceiveAsync(bytes);
                    var str = Encoding.Default.GetString(bytes).TrimEnd('\0');
                    logger.LogInformation($"sokcet {_Config.Ip} {_Config.EACPort} {str}");
                    if (str == "test")
                    {
                        await socket.SendAsync(Encoding.Default.GetBytes("Test Form SERVER"));
                        continue;
                    }

                    var data = GET(str, out var clientId, out var name, out var reason);
                    if (data != null)
                    {
                        data.Count++;
                        data.ClientId = clientId;
                        data.Name = name;
                        data.Reason = reason;
                    }
                    else
                    {
                        data = EACData.Get(str);
                        _EacDatas.Add(data);
                        if (data.Count > _Config.EACCount)
                            await Ban(data);
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e.ToString());
                }
            }
        }, TaskCreationOptions.LongRunning);
        return true;
    }

    public async Task Stop()
    {
        await _cancellationToken.CancelAsync();
        _writer.Close();
        _stream.Close();
        _Task?.Dispose();
        foreach (var so in _Sockets)
            so.Dispose();
    }
    public EACData? GET(string s, out int clientId, out string name, out string reason)
    {
        var strings = s.Split('|');
        clientId = int.Parse(strings[0]);
        name = strings[2];
        reason = strings[3];
        return _EacDatas.FirstOrDefault(n => n.FriendCode == strings[1]);
    }

    public async Task Ban(EACData data)
    {
        data.Ban = true;
        await _writer.WriteLineAsync($"Id:{data.ClientId}FriendCode:{data.FriendCode}Name:{data.Name}Reason:{data.Reason} : Count{data.Count}");
        await _oneBotService.SendMessage(
            $"AddBan\nName:{data.Name}\nFriendCode:{data.FriendCode}Reason:{data.Reason}Count:{data.Count}");
    }
}

public class EACData
{
    public int ClientId { get; set; }
    public string FriendCode { get; init; }
    public string Name { get; set; }
    public string Reason { get; set; }

    public int Count;
    public bool Ban;
    
    public static EACData Get(string s)
    {
        var strings = s.Split('|');
        return new EACData
        {
            ClientId = int.Parse(strings[0]),
            FriendCode = strings[1],
            Name = strings[2],
            Reason = strings[3]
        };
    }
}

public class ServerConfig
{
    public string Ip { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 25000;

    public string BotHttpUrl { get; set; } = "http://localhost:3000";
    public bool SendToQQ_Group { get; set; } = false;
    public long QQID { get; set; }
    public int EACPort { get; set; } = 25250;
    public int EACCount { get; set; } = 5;
    public string EACPath { get; set; } = "./EAC.txt";

    public string ReadPath = string.Empty;
    public string ApiUrl = string.Empty;
    public int Time = 30;
}

public class ActiveService(ILogger<ActiveService> _logger, IOptions<ServerConfig> _options, HttpClient _client)
{
    public Task? _Task;
    private readonly CancellationTokenSource _source = new();
    public ValueTask StartAsync()
    {
        if(_options.Value.ApiUrl == string.Empty) return ValueTask.CompletedTask;
        _source.Token.Register(() => _Task?.Dispose());
        var time = TimeSpan.FromSeconds(30);
        _Task = Task.Factory.StartNew(async () =>
        {
            while (!_source.IsCancellationRequested)
            {
                try
                {
                    var responseMessage = await _client.GetAsync(_options.Value.ApiUrl);
                    _logger.LogInformation($"Get{_options.Value.ApiUrl} {responseMessage.StatusCode} {await responseMessage.Content.ReadAsStringAsync()}");
                    Thread.Sleep(time);
                }
                catch
                {
                    // ignored
                }
            }
        }, TaskCreationOptions.LongRunning);
        return ValueTask.CompletedTask;
    }
    
    public async ValueTask StopAsync()
    {
        await _source.CancelAsync();
    }
}

