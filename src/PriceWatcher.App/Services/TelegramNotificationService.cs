using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatcher.App.Configuration;
using PriceWatcher.App.Models;
using Telegram.Bot;

namespace PriceWatcher.App.Services;

/// <summary>
/// Telegram implementation for broadcasting notifications.
/// </summary>
public sealed class TelegramNotificationService : ITelegramNotificationService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IPriceStorage _priceStorage;
    private readonly ILogger<TelegramNotificationService> _logger;
    private readonly TelegramOptions _options;

    public TelegramNotificationService(
        ITelegramBotClient botClient,
        IPriceStorage priceStorage,
        IOptions<TelegramOptions> options,
        ILogger<TelegramNotificationService> logger)
    {
        _botClient = botClient;
        _priceStorage = priceStorage;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task NotifyPriceChangeAsync(PriceCheckResult result, CancellationToken cancellationToken)
    {
        var message = BuildPriceMessage(result);
        var subscribers = await _priceStorage.GetActiveSubscribersAsync(cancellationToken).ConfigureAwait(false);
        foreach (var subscriber in subscribers)
        {
            try
            {
                await _botClient.SendTextMessageAsync(subscriber.ChatId, message, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to send notification to chat {ChatId}", subscriber.ChatId);
            }
        }
    }

    /// <inheritdoc />
    public Task NotifyAdminAsync(string message, CancellationToken cancellationToken)
    {
        if (long.TryParse(_options.AdminChatId, out var chatId) && chatId != 0)
        {
            return _botClient.SendTextMessageAsync(chatId, message, cancellationToken: cancellationToken);
        }

        _logger.LogWarning("Admin chat id is not configured; cannot send admin message");
        return Task.CompletedTask;
    }

    private static string BuildPriceMessage(PriceCheckResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Hepsiburada fiyat güncellemesi:");
        builder.AppendLine($"Ürün: {result.ProductUrl}");
        if (result.OldPrice.HasValue)
        {
            builder.AppendLine($"Eski fiyat: {result.OldPrice.Value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"))} TL");
        }
        else
        {
            builder.AppendLine("Eski fiyat: -");
        }

        builder.AppendLine($"Yeni fiyat: {result.NewPrice.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"))} TL");
        var direction = result.ChangeRate >= 0 ? "+" : string.Empty;
        builder.AppendLine($"Değişim: {direction}{result.ChangeRate:F2}%");
        builder.AppendLine($"Tarih: {result.CheckedAt:dd.MM.yyyy HH:mm:ss}");
        return builder.ToString();
    }
}
