using System.Threading;
using System.Threading.Tasks;
using PriceWatcher.App.Models;

namespace PriceWatcher.App.Services;

/// <summary>
/// Sends Telegram notifications to subscribers.
/// </summary>
public interface ITelegramNotificationService
{
    /// <summary>
    /// Broadcasts a price change notification to all active subscribers.
    /// </summary>
    Task NotifyPriceChangeAsync(PriceCheckResult result, CancellationToken cancellationToken);

    /// <summary>
    /// Sends an administrative message.
    /// </summary>
    Task NotifyAdminAsync(string message, CancellationToken cancellationToken);
}
