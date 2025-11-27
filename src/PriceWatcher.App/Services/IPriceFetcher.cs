using System.Threading;
using System.Threading.Tasks;

namespace PriceWatcher.App.Services;

/// <summary>
/// Fetches product prices from an external source.
/// </summary>
public interface IPriceFetcher
{
    /// <summary>
    /// Retrieves the current price for the given product URL.
    /// </summary>
    /// <param name="productUrl">The product URL to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The price if found; otherwise <c>null</c>.</returns>
    Task<decimal?> FetchPriceAsync(string productUrl, CancellationToken cancellationToken);
}
