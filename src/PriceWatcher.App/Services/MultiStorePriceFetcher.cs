using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PriceWatcher.App.Services;

/// <summary>
/// Routes price fetching requests to the appropriate site-specific fetcher.
/// </summary>
public sealed class MultiStorePriceFetcher : IPriceFetcher
{
    private readonly IReadOnlyCollection<ISitePriceFetcher> _fetchers;
    private readonly ILogger<MultiStorePriceFetcher> _logger;

    public MultiStorePriceFetcher(IEnumerable<ISitePriceFetcher> fetchers, ILogger<MultiStorePriceFetcher> logger)
    {
        _fetchers = fetchers.ToList();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<decimal?> FetchPriceAsync(string productUrl, CancellationToken cancellationToken)
    {
        var fetcher = _fetchers.FirstOrDefault(f => f.CanHandle(productUrl));
        if (fetcher is null)
        {
            _logger.LogWarning("No price fetcher available for {Url}", productUrl);
            return null;
        }

        _logger.LogInformation("Using {Fetcher} for {Url}", fetcher.GetType().Name, productUrl);
        return await fetcher.FetchPriceAsync(productUrl, cancellationToken).ConfigureAwait(false);
    }
}
