using System;
using System.Net.Http;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace PriceWatcher.App.Services;

/// <summary>
/// Scrapes product prices from Amazon Turkey product pages.
/// </summary>
public sealed class AmazonPriceFetcher : HtmlPriceFetcherBase
{
    public AmazonPriceFetcher(IHttpClientFactory httpClientFactory, ILogger<AmazonPriceFetcher> logger)
        : base(httpClientFactory, logger)
    {
    }

    protected override string ClientName => nameof(AmazonPriceFetcher);

    /// <inheritdoc />
    public override bool CanHandle(string productUrl) => Uri.TryCreate(productUrl, UriKind.Absolute, out var uri) &&
                                                         (uri.Host.Contains("amazon.com.tr", StringComparison.OrdinalIgnoreCase)
                                                          || uri.Host.Contains("amazon.com", StringComparison.OrdinalIgnoreCase));

    protected override HttpRequestMessage BuildRequest(string productUrl)
    {
        var request = base.BuildRequest(productUrl);
        request.Headers.TryAddWithoutValidation("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
        request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");
        return request;
    }

    protected override string? GetPreferredPriceText(HtmlDocument doc)
    {
        var priceSelectors = new[]
        {
            "//span[@id='priceblock_ourprice']",
            "//span[@id='priceblock_dealprice']",
            "//span[@id='priceblock_saleprice']",
            "//span[@data-a-color='price']/span[@class='a-offscreen']",
            "//span[contains(@class,'a-price')]/span[@class='a-offscreen']",
            "//span[@id='tp_price_block_total_price_ww']/span"
        };

        foreach (var selector in priceSelectors)
        {
            var node = doc.DocumentNode.SelectSingleNode(selector);
            if (node is not null && !string.IsNullOrWhiteSpace(node.InnerText))
            {
                return node.InnerText;
            }
        }

        return base.GetPreferredPriceText(doc);
    }
}
