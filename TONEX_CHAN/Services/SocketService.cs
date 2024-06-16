using System.Net;
using System.Net.Sockets;
using System.Text;
using TONEX_CHAN.Config;

namespace TONEX_CHAN.Services;

public class SocketService(
    ILogger<SocketService> logger, 
    RoomsService roomsService, 
    OneBotService oneBotService,
    DiscordService discordService,
    EnvironmentalTextService environmentalTextService) : ITONEX_CHANService
{
    public TcpListener _TcpListener = null!;
    private ServerConfig _config = null!;
    private TONEX_CHANService _cdjService = null!;
    public IPAddress Address => IPAddress.Parse(_config.Ip);
    public Task? _Task;
    

    public ValueTask StartAsync(ServerConfig config, TONEX_CHANService cdjService, CancellationToken cancellationToken)
    {
        _config = config;
        _cdjService = cdjService;
        
        logger.LogInformation("CreateSocket");
        _TcpListener = new TcpListener(Address, _config.Port);
        _TcpListener.Start();
        logger.LogInformation($"Start :{_config.Ip} {_config.Port} {_config.SendToGroup} {_config.BotHttpUrl} {_config.QQID}");
        cancellationToken.Register(() => _TcpListener.Dispose());
        _Task = Task.Factory.StartNew(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var socket = await _TcpListener.AcceptSocketAsync(cancellationToken);
                    if (socket.RemoteEndPoint is not IPEndPoint endPoint)
                        return;

                    if (cdjService.BanIps.Contains(endPoint.Address))
                    {
                        socket.Dispose();
                        logger.LogInformation($"Socket Ban Ip{endPoint.Address}");
                        continue;
                    }

                    var bytes = new byte[60];
                    var count = await socket.ReceiveAsync(bytes);
                    var str = Encoding.Default.GetString(bytes).TrimEnd('\0');
                    logger.LogInformation($"sokcet {_config.Ip} {_config.Port} {str}");
                    
                    if (str == "test")
                    {
                        await socket.SendAsync(Encoding.Default.GetBytes("Test Form SERVER"));
                        continue;
                    }

                    var com = roomsService.TryPareRoom(str, out var room);
                    if (!com)
                    {
                        logger.LogWarning($"{str} Pare Error");
                        continue;
                    }
                    await oneBotService.SendMessage(environmentalTextService.Replace(_config.SendText_zh));
                    await discordService.SendMessage(environmentalTextService.Replace(_config.SendText_en));
                }
                catch (Exception e)
                {
                    logger.LogError(e.ToString());
                }
            }
        }, TaskCreationOptions.LongRunning);
        
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync()
    {
        _Task?.Dispose();
        return ValueTask.CompletedTask;
    }
}