using IB.WatchCluster.YasTelegramBot.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

namespace IB.WatchCluster.YasTelegramBot.Service;

public class YasBotService : BackgroundService
{
    private readonly YasBotServiceHandler _yasBotHandler;
    private readonly TelegramBotClient _botClient;
    private readonly YasUpdateHandler _yasUpdateHandler;
    private readonly ILogger<YasBotService> _logger;

    public YasBotService(
        OtelMetrics otelMetrics,
        YasBotServiceHandler yasBotHandler,
        IHostApplicationLifetime appLifetime,
        TelegramBotClient botClient,
        YasUpdateHandler yasUpdateHandler,
        ILogger<YasBotService> logger)
    {
        _yasBotHandler = yasBotHandler;
        _botClient = botClient;
        _yasUpdateHandler = yasUpdateHandler;
        _logger = logger;
        appLifetime.ApplicationStarted.Register(otelMetrics.SetInstanceUp);
        appLifetime.ApplicationStopped.Register(otelMetrics.SetInstanceDown);
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _yasBotHandler.IsRunning = true;

            // Running in infinity loop to consume telegram updates
            //
            await _botClient.ReceiveAsync(
                updateHandler: _yasUpdateHandler,
                receiverOptions: new ReceiverOptions
                {
                    AllowedUpdates = new[] { UpdateType.Message }
                },
                cancellationToken: stoppingToken);
        }
        catch(Exception exception)
        {
            _logger.LogCritical(exception, "Fatal Error on telegram updates consumer: {@msg}", exception.Message);
            throw;
        }
        finally
        {
            _yasBotHandler.IsRunning = false;
        }
    }
}