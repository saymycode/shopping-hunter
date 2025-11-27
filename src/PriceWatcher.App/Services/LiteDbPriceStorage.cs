using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PriceWatcher.App.Configuration;
using PriceWatcher.App.Models;

namespace PriceWatcher.App.Services;

/// <summary>
/// LiteDB-backed implementation of <see cref="IPriceStorage"/>.
/// </summary>
public sealed class LiteDbPriceStorage : IPriceStorage, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly ILogger<LiteDbPriceStorage> _logger;
    private readonly object _sync = new();

    public LiteDbPriceStorage(IOptions<StorageOptions> options, ILogger<LiteDbPriceStorage> logger)
    {
        _logger = logger;
        var dbPath = options.Value.DbPath;
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _database = new LiteDatabase(dbPath);
        EnsureIndexes();
    }

    private ILiteCollection<ProductPriceHistory> PriceHistoryCollection => _database.GetCollection<ProductPriceHistory>("product_price_history");

    private ILiteCollection<TelegramSubscriber> SubscribersCollection => _database.GetCollection<TelegramSubscriber>("telegram_subscribers");

    public Task<ProductPriceHistory?> GetLastPriceAsync(string productUrl, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_sync)
            {
                return PriceHistoryCollection.FindOne(x => x.ProductUrl == productUrl);
            }
        }, cancellationToken);
    }

    public Task UpsertPriceAsync(ProductPriceHistory history, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_sync)
            {
                PriceHistoryCollection.Upsert(history);
            }
        }, cancellationToken);
    }

    public Task<IReadOnlyList<TelegramSubscriber>> GetActiveSubscribersAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<TelegramSubscriber>>(() =>
        {
            lock (_sync)
            {
                return SubscribersCollection.Find(x => x.IsActive).ToList();
            }
        }, cancellationToken);
    }

    public Task<TelegramSubscriber?> GetSubscriberAsync(long chatId, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_sync)
            {
                return SubscribersCollection.FindOne(x => x.ChatId == chatId);
            }
        }, cancellationToken);
    }

    public Task<TelegramSubscriber> AddOrActivateSubscriberAsync(long chatId, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_sync)
            {
                var existing = SubscribersCollection.FindOne(x => x.ChatId == chatId);
                if (existing is null)
                {
                    existing = new TelegramSubscriber
                    {
                        ChatId = chatId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    SubscribersCollection.Insert(existing);
                }
                else if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    SubscribersCollection.Update(existing);
                }

                return existing;
            }
        }, cancellationToken);
    }

    public Task<bool> DeactivateSubscriberAsync(long chatId, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_sync)
            {
                var existing = SubscribersCollection.FindOne(x => x.ChatId == chatId);
                if (existing is null)
                {
                    return false;
                }

                existing.IsActive = false;
                return SubscribersCollection.Update(existing);
            }
        }, cancellationToken);
    }

    public Task<int> GetWatchedProductCountAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            lock (_sync)
            {
                return PriceHistoryCollection.Count();
            }
        }, cancellationToken);
    }

    private void EnsureIndexes()
    {
        try
        {
            PriceHistoryCollection.EnsureIndex(x => x.ProductUrl, unique: true);
            SubscribersCollection.EnsureIndex(x => x.ChatId, unique: true);
        }
        catch (LiteException ex)
        {
            _logger.LogWarning(ex, "Failed to ensure LiteDB indexes");
        }
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}
