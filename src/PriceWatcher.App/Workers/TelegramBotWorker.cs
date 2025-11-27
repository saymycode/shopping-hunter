using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace PriceWatcher.App.Workers;

/// <summary>
/// Hosts the Telegram bot receiver loop.
/// </summary>
public sealed class TelegramBotWorker : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly Services.ITelegramUpdateHandler _updateHandler;
    private readonly ILogger<TelegramBotWorker> _logger;

    public TelegramBotWorker(ITelegramBotClient botClient, Services.ITelegramUpdateHandler updateHandler, ILogger<TelegramBotWorker> logger)
    {
        _botClient = botClient;
        _updateHandler = updateHandler;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            ThrowPendingUpdates = true
        };

        _botClient.StartReceiving(_updateHandler.HandleUpdateAsync, _updateHandler.HandleErrorAsync, receiverOptions, cancellationToken: stoppingToken);
        _logger.LogInformation("Telegram bot worker started");
        return Task.CompletedTask;
    }
}
