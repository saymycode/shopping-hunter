using System;
using System.Net.Http;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace PriceWatcher.App.Services;

/// <summary>
/// Scrapes product prices from Hepsiburada product pages using HTML parsing.
/// </summary>
public sealed class HepsiburadaPriceFetcher : HtmlPriceFetcherBase
{
    public HepsiburadaPriceFetcher(IHttpClientFactory httpClientFactory, ILogger<HepsiburadaPriceFetcher> logger)
        : base(httpClientFactory, logger)
    {
    }

    protected override string ClientName => nameof(HepsiburadaPriceFetcher);

    /// <inheritdoc />
    public override bool CanHandle(string productUrl) => Uri.TryCreate(productUrl, UriKind.Absolute, out var uri) &&
                                                         uri.Host.Contains("hepsiburada.com", StringComparison.OrdinalIgnoreCase);

    protected override HttpRequestMessage BuildRequest(string productUrl)
    {
        var request = base.BuildRequest(productUrl);
        request.Headers.TryAddWithoutValidation("Referer", "https://www.hepsiburada.com/");
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
        request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        return request;
    }
}
