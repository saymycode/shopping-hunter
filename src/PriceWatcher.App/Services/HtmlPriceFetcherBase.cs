using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace PriceWatcher.App.Services;

/// <summary>
/// Base implementation for HTML-based price fetchers.
/// </summary>
public abstract class HtmlPriceFetcherBase : ISitePriceFetcher
{
    private static readonly Regex PriceRegex = new(
        @"(\\u20BA|₺|TRY)?\s*(?<value>\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{2})?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    protected HtmlPriceFetcherBase(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// The HTTP client name to use when resolving from the factory.
    /// </summary>
    protected abstract string ClientName { get; }

    /// <inheritdoc />
    public abstract bool CanHandle(string productUrl);

    /// <inheritdoc />
    public virtual async Task<decimal?> FetchPriceAsync(string productUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient(ClientName);
            using var request = BuildRequest(productUrl);

            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch price for {Url}. Status code: {StatusCode}", productUrl, response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var priceText = GetPreferredPriceText(doc) ?? ExtractPriceText(doc);
            if (string.IsNullOrWhiteSpace(priceText))
            {
                _logger.LogWarning("Price element could not be found for {Url}", productUrl);
                return null;
            }
            if (TryParsePrice(priceText, out decimal price))
            {
                return price;
            }

            _logger.LogWarning("Price text could not be parsed for {Url}: {PriceText}", productUrl, priceText);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "An error occurred while fetching price for {Url}", productUrl);
            return null;
        }
    }

    /// <summary>
    /// Builds an HTTP request with default headers suitable for the target site.
    /// </summary>
    protected virtual HttpRequestMessage BuildRequest(string productUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, productUrl);
        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        );
        request.Headers.TryAddWithoutValidation(
            "Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8"
        );
        request.Headers.TryAddWithoutValidation("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
        request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
        return request;
    }

    /// <summary>
    /// Allows derived classes to prioritize site-specific price extraction before fallbacks.
    /// </summary>
    protected virtual string? GetPreferredPriceText(HtmlDocument doc) => null;

    /// <summary>
    /// Attempts to extract the price text using common patterns such as JSON-LD and price-related elements.
    /// </summary>
    protected virtual string? ExtractPriceText(HtmlDocument doc)
    {
        foreach (var jsonLdScript in doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']") ?? Enumerable.Empty<HtmlNode>())
        {
            var priceMatch = Regex.Match(
                jsonLdScript.InnerText,
                @"\""price\""\s*:\s*\""?(?<value>[\d\.,\s]+)\""?",
                RegexOptions.IgnoreCase | RegexOptions.Multiline
            );


            if (priceMatch.Success)
            {
                var priceValue = priceMatch.Groups[1].Value.Trim('"', '\'', ' ');
                if (!string.IsNullOrWhiteSpace(priceValue))
                {
                    return priceValue;
                }
            }
        }

        var priceNodes = doc.DocumentNode
            .Descendants()
            .Where(n => n.Name is "span" or "div" or "ins" || n.Name.Equals("meta", StringComparison.OrdinalIgnoreCase))
            .Where(n => HasPriceHint(n.GetAttributeValue("class", string.Empty)) ||
                        HasPriceHint(n.GetAttributeValue("id", string.Empty)) ||
                        HasPriceHint(n.GetAttributeValue("data-testid", string.Empty)))
            .ToList();

        decimal? bestPrice = null;
        string? bestCandidate = null;

        foreach (var node in priceNodes)
        {
            var candidate = node.Name.Equals("meta", StringComparison.OrdinalIgnoreCase)
                ? node.GetAttributeValue("content", string.Empty)
                : node.InnerText?.Trim();

            if (string.IsNullOrWhiteSpace(candidate) || !PriceRegex.IsMatch(candidate))
            {
                continue;
            }

            if (TryParsePrice(candidate, out var parsedPrice))
            {
                if (bestPrice is null || parsedPrice > bestPrice)
                {
                    bestPrice = parsedPrice;
                    bestCandidate = candidate;
                }
            }
            else if (bestCandidate is null)
            {
                bestCandidate = candidate;
            }
        }

        if (bestCandidate is not null)
        {
            return bestCandidate;
        }

        var metaPrice = doc.DocumentNode
            .SelectNodes("//meta[@itemprop='price' or @property='product:price:amount' or @property='og:price:amount']")?
            .Select(n => n.GetAttributeValue("content", string.Empty))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        return metaPrice;
    }

    protected static bool HasPriceHint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("price", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("amount", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("value", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("fiyat", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("prc", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("amt", StringComparison.OrdinalIgnoreCase);
    }

    protected static bool TryParsePrice(string text, out decimal price)
    {
        var match = PriceRegex.Match(text);
        if (!match.Success)
        {
            price = 0;
            return false;
        }

        var raw = match.Groups["value"].Value.Trim();
        var normalized = NormalizeNumberString(text);

        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
        {
            return true;
        }

        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out price);
    }

    private static string NormalizeNumberString(string raw)
    {
        var value = raw.Replace("\u00A0", "").Replace(" ", "");

        // 1) Nokta + Virgül birlikte ise → Türkçe format demektir: 36.499,00
        if (value.Contains(".") && value.Contains(","))
        {
            // binlikleri sil
            value = value.Replace(".", "");
            // ondalığı . yap
            return value.Replace(",", ".");
        }

        // 2) Sadece virgül varsa → ondalık virgül demektir: 36499,00
        if (value.Contains(","))
        {
            return value.Replace(".", "").Replace(",", ".");
        }

        // 3) Sadece nokta varsa → İngilizce ondalık: 36499.00
        // Hiç dokunma
        return value;
    }

}
