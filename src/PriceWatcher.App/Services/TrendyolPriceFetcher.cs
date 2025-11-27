using System;
using System.Net.Http;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace PriceWatcher.App.Services;

/// <summary>
/// Scrapes product prices from Trendyol product pages.
/// </summary>
public sealed class TrendyolPriceFetcher : HtmlPriceFetcherBase
{
    public TrendyolPriceFetcher(IHttpClientFactory httpClientFactory, ILogger<TrendyolPriceFetcher> logger)
        : base(httpClientFactory, logger)
    {
    }

    protected override string ClientName => nameof(TrendyolPriceFetcher);

    /// <inheritdoc />
    public override bool CanHandle(string productUrl) => Uri.TryCreate(productUrl, UriKind.Absolute, out var uri) &&
                                                         uri.Host.Contains("trendyol.com", StringComparison.OrdinalIgnoreCase);

    protected override string? GetPreferredPriceText(HtmlDocument doc)
    {
        var discountedPrice = doc.DocumentNode.SelectSingleNode("//span[contains(@class,'prc-dsc')]")?.InnerText;
        if (!string.IsNullOrWhiteSpace(discountedPrice))
        {
            return discountedPrice;
        }

        var currentPrice = doc.DocumentNode.SelectSingleNode("//*[@data-testid='price-current-price']")?.InnerText;
        if (!string.IsNullOrWhiteSpace(currentPrice))
        {
            return currentPrice;
        }

        return base.GetPreferredPriceText(doc);
    }
}
