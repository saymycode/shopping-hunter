using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatcher.App.Configuration;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace PriceWatcher.App.Services;

/// <summary>
/// Processes Telegram updates and commands.
/// </summary>
public sealed class TelegramUpdateHandler : ITelegramUpdateHandler
{
    private readonly IPriceStorage _priceStorage;
    private readonly IOptions<PriceWatcherOptions> _options;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(IPriceStorage priceStorage, IOptions<PriceWatcherOptions> options, ILogger<TelegramUpdateHandler> logger)
    {
        _priceStorage = priceStorage;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is null || string.IsNullOrWhiteSpace(update.Message.Text))
        {
            return;
        }

        var chatId = update.Message.Chat.Id;
        var message = update.Message.Text.Trim();
        _logger.LogInformation("Received message {Message} from chat {ChatId}", message, chatId);

        switch (message.ToLowerInvariant())
        {
            case "/start":
                var subscriber = await _priceStorage.AddOrActivateSubscriberAsync(chatId, cancellationToken).ConfigureAwait(false);
                await botClient.SendTextMessageAsync(chatId, "Price watcher botuna abone oldunuz.", cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Chat {ChatId} subscribed", chatId);
                break;
            case "/stop":
                var deactivated = await _priceStorage.DeactivateSubscriberAsync(chatId, cancellationToken).ConfigureAwait(false);
                var stopText = deactivated ? "Aboneliğiniz durduruldu." : "Zaten abone değilsiniz.";
                await botClient.SendTextMessageAsync(chatId, stopText, cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Chat {ChatId} deactivated: {Result}", chatId, deactivated);
                break;
            case "/status":
                var existing = await _priceStorage.GetSubscriberAsync(chatId, cancellationToken).ConfigureAwait(false);
                var activeCount = await _priceStorage.GetWatchedProductCountAsync(cancellationToken).ConfigureAwait(false);
                var interval = _options.Value.RequestIntervalMinutes;
                var statusText = existing?.IsActive == true ? "Aktif abonelik" : "Abone değilsiniz";
                var info = $"Durum: {statusText}\nİzlenen ürün sayısı: {activeCount}\nÇekim aralığı: {interval} dakika";
                await botClient.SendTextMessageAsync(chatId, info, cancellationToken: cancellationToken).ConfigureAwait(false);
                break;
            default:
                await botClient.SendTextMessageAsync(chatId, "Desteklenen komutlar: /start, /stop, /status", cancellationToken: cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    /// <inheritdoc />
    public Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiEx => $"Telegram API Error: {apiEx.ErrorCode}\n{apiEx.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(exception, "Telegram update processing failed: {Message}", errorMessage);
        return Task.CompletedTask;
    }
}
