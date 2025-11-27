using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PriceWatcher.App.Models;

namespace PriceWatcher.App.Services;

/// <summary>
/// Provides persistence for price history and Telegram subscribers.
/// </summary>
public interface IPriceStorage
{
    Task<ProductPriceHistory?> GetLastPriceAsync(string productUrl, CancellationToken cancellationToken);

    Task UpsertPriceAsync(ProductPriceHistory history, CancellationToken cancellationToken);

    Task<IReadOnlyList<TelegramSubscriber>> GetActiveSubscribersAsync(CancellationToken cancellationToken);

    Task<TelegramSubscriber?> GetSubscriberAsync(long chatId, CancellationToken cancellationToken);

    Task<TelegramSubscriber> AddOrActivateSubscriberAsync(long chatId, CancellationToken cancellationToken);

    Task<bool> DeactivateSubscriberAsync(long chatId, CancellationToken cancellationToken);

    Task<int> GetWatchedProductCountAsync(CancellationToken cancellationToken);
}
