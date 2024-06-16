using System.Text.Json;
using TONEX_CHAN.TONEX_CHANData;
using TONEX_CHAN.Config;

namespace TONEX_CHAN.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

public class DiscordService(ILogger<DiscordService> _logger, DiscordSocketClient _client) : ITONEX_CHANService
{
    private ServerConfig _config = null!;
    public Queue<SendInfo_DC> SendInfos = new();


    public ValueTask SendMessage(string message)
    {
        var info = new SendInfo_DC(message);
        if (_config.DiscordChannelId != 0)
        {
            info.SendTargets.Add((_config.DiscordChannelId));
        }
        SendInfos.Enqueue(info);
        return ValueTask.CompletedTask;
    }

    public async ValueTask SendMessageToChannel(string message, ulong id)
    {
        var channel = _client.GetChannel(id) as IMessageChannel;
        if (channel != null)
        {
            await channel.SendMessageAsync(message);
            _logger.LogInformation($"Sent to channel id:{id} message:{message}");
        }
        else
        {
            _logger.LogWarning($"Channel id {id} not found.");
        }
    }

    public async ValueTask StartAsync(ServerConfig config, TONEX_CHANService cdjService, CancellationToken cancellationToken)
    {
        _config = config;
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        await _client.LoginAsync(TokenType.Bot, _config.DiscordToken);
        await _client.StartAsync();

        var span = TimeSpan.FromSeconds(_config.MessageInterval);
        await Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (SendInfos.TryDequeue(out var info))
                {
                    foreach (var discordId in info.SendTargets)
                    {
                        try
                        {
                            await SendMessageToChannel(info.Message, discordId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Error sending message. DiscordID:{discordId} Message:{info.Message}");
                        }
                    }
                }
                await Task.Delay(span, cancellationToken);
            }
        }, cancellationToken);
    }

    public ValueTask StopAsync()
    {
        _client.StopAsync();
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task LogAsync(LogMessage log)
    {
        _logger.LogInformation(log.ToString());
        return Task.CompletedTask;
    }

    private Task ReadyAsync()
    {
        _logger.LogInformation($"Connected as -> [{_client.CurrentUser}]");
        return Task.CompletedTask;
    }
}

public class SendInfo_DC
{
    public string Message { get; }
    public List<ulong> SendTargets { get; } = new();

    public SendInfo_DC(string message)
    {
        Message = message;
    }
}