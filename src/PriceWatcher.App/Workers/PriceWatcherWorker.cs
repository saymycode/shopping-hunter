using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatcher.App.Configuration;
using PriceWatcher.App.Models;
using PriceWatcher.App.Services;

namespace PriceWatcher.App.Workers;

/// <summary>
/// Periodically checks product prices and notifies subscribers when needed.
/// </summary>
public sealed class PriceWatcherWorker : BackgroundService
{
    private readonly IPriceFetcher _priceFetcher;
    private readonly IPriceStorage _priceStorage;
    private readonly ITelegramNotificationService _notificationService;
    private readonly IOptions<HepsiburadaOptions> _options;
    private readonly ILogger<PriceWatcherWorker> _logger;

    public PriceWatcherWorker(
        IPriceFetcher priceFetcher,
        IPriceStorage priceStorage,
        ITelegramNotificationService notificationService,
        IOptions<HepsiburadaOptions> options,
        ILogger<PriceWatcherWorker> logger)
    {
        _priceFetcher = priceFetcher;
        _priceStorage = priceStorage;
        _notificationService = notificationService;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Price watcher worker started");
        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.Value.RequestIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckPricesAsync(stoppingToken).ConfigureAwait(false);
            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // expected when stopping
            }
        }
    }

    private async Task CheckPricesAsync(CancellationToken cancellationToken)
    {
        var urls = _options.Value.ProductUrls.Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
        if (!urls.Any())
        {
            _logger.LogInformation("No product URLs configured.");
            return;
        }

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var price = await _priceFetcher.FetchPriceAsync(url, cancellationToken).ConfigureAwait(false);
            if (!price.HasValue)
            {
                continue;
            }

            var existing = await _priceStorage.GetLastPriceAsync(url, cancellationToken).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            var result = new PriceCheckResult
            {
                ProductUrl = url,
                OldPrice = existing?.LastPrice,
                NewPrice = price.Value,
                Changed = existing is null || existing.LastPrice != price.Value,
                ChangeRate = CalculateChangeRate(existing?.LastPrice, price.Value),
                CheckedAt = now
            };

            var shouldNotify = ShouldNotify(result, existing);
            await PersistAsync(existing, result, cancellationToken).ConfigureAwait(false);

            if (shouldNotify)
            {
                await _notificationService.NotifyPriceChangeAsync(result, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Notification sent for {Url} with change {ChangeRate}%", url, result.ChangeRate);
            }
            else
            {
                _logger.LogInformation("Price checked for {Url} with change {ChangeRate}%", url, result.ChangeRate);
            }
        }
    }

    private async Task PersistAsync(ProductPriceHistory? existing, PriceCheckResult result, CancellationToken cancellationToken)
    {
        var history = existing ?? new ProductPriceHistory { ProductUrl = result.ProductUrl };
        history.LastPrice = result.NewPrice;
        history.LastCheckTime = result.CheckedAt;
        await _priceStorage.UpsertPriceAsync(history, cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldNotify(PriceCheckResult result, ProductPriceHistory? existing)
    {
        var options = _options.Value;
        if (options.NotifyOnEveryPull)
        {
            return true;
        }

        if (existing is null)
        {
            return false;
        }

        return Math.Abs(result.ChangeRate) >= options.MinChangePercentageToNotify;
    }

    private static double CalculateChangeRate(decimal? oldPrice, decimal newPrice)
    {
        if (!oldPrice.HasValue || oldPrice.Value == 0)
        {
            return 0;
        }

        var diff = newPrice - oldPrice.Value;
        return (double)(diff / oldPrice.Value * 100);
    }
}
