using System.Threading;
using System.Threading.Tasks;

namespace PriceWatcher.App.Services;

/// <summary>
/// Represents a price fetcher that can handle a specific e-commerce site.
/// </summary>
public interface ISitePriceFetcher : IPriceFetcher
{
    /// <summary>
    /// Determines whether the fetcher can handle the provided product URL.
    /// </summary>
    /// <param name="productUrl">The product URL.</param>
    /// <returns><c>true</c> if the URL is supported; otherwise, <c>false</c>.</returns>
    bool CanHandle(string productUrl);
}
