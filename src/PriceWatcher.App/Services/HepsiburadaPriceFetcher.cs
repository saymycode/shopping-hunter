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
/// Scrapes product prices from Hepsiburada product pages using HTML parsing.
/// </summary>
public sealed class HepsiburadaPriceFetcher : IPriceFetcher
{
    private static readonly Regex PriceRegex = new(
        @"(\\u20BA|₺)?\s*(?<value>\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{2})?)",
        RegexOptions.Compiled
    );

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HepsiburadaPriceFetcher> _logger;

    public HepsiburadaPriceFetcher(IHttpClientFactory httpClientFactory, ILogger<HepsiburadaPriceFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<decimal?> FetchPriceAsync(string productUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(HepsiburadaPriceFetcher));
            
            // Create request message with headers to avoid 403 Forbidden error
            using var request = new HttpRequestMessage(HttpMethod.Get, productUrl);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "tr-TR,tr;q=0.9");
            request.Headers.Add("Referer", "https://www.hepsiburada.com/");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Connection", "keep-alive");
            request.Headers.Add("Cache-Control", "max-age=0");
            request.Headers.Add("Sec-Fetch-Dest", "document");
            request.Headers.Add("Sec-Fetch-Mode", "navigate");
            request.Headers.Add("Sec-Fetch-Site", "same-origin");
            
            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch price for {Url}. Status code: {StatusCode}", productUrl, response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Debug: Log HTML to file for inspection
            System.IO.File.WriteAllText("page_debug.html", html);
            _logger.LogInformation("HTML saved to page_debug.html");

            var priceText = ExtractPriceText(doc);
            if (priceText is null)
            {
                _logger.LogWarning("Price element could not be found for {Url}", productUrl);
                return null;
            }

            if (TryParsePrice(priceText, out var price))
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

    private static string? ExtractPriceText(HtmlDocument doc)
    {
        // First, try to extract from JSON-LD structured data
        var jsonLdScript = doc.DocumentNode
            .SelectNodes("//script[@type='application/ld+json']")?
            .FirstOrDefault();
        
        if (jsonLdScript != null)
        {
            var jsonContent = jsonLdScript.InnerText;
            var priceMatch = Regex.Match(jsonContent, @"""price""\s*:\s*""?(\d+(?:\.\d{2})?|[^\""]+)""?", RegexOptions.IgnoreCase);
            if (priceMatch.Success)
            {
                var priceValue = priceMatch.Groups[1].Value.Trim('"');
                if (!string.IsNullOrWhiteSpace(priceValue))
                {
                    return priceValue;
                }
            }
        }

        // Fallback: Try DOM elements with price-related classes/ids
        var priceNodes = doc.DocumentNode
            .Descendants()
            .Where(n => n.Name is "span" or "div" || n.Name.Equals("ins", StringComparison.OrdinalIgnoreCase))
            .Where(n => HasPriceHint(n.GetAttributeValue("class", string.Empty)) || HasPriceHint(n.GetAttributeValue("id", string.Empty)))
            .ToList();

        decimal? bestPrice = null;
        string? bestCandidate = null;

        foreach (var node in priceNodes)
        {
            var candidate = node.InnerText?.Trim();
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

        // Fallback: Try meta tag
        var metaPrice = doc.DocumentNode
            .SelectNodes("//meta[@itemprop='price']")?
            .Select(n => n.GetAttributeValue("content", string.Empty))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        return metaPrice;
    }

    private static bool HasPriceHint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("price", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("amount", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("value", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParsePrice(string text, out decimal price)
    {
        var match = PriceRegex.Match(text);
        if (!match.Success)
        {
            price = 0;
            return false;
        }

        var raw = match.Groups["value"].Value.Trim();
        var normalized = NormalizeNumberString(raw);

        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
        {
            return true;
        }

        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out price);
    }

    private static string NormalizeNumberString(string raw)
    {
        var value = raw.Replace("\u00A0", string.Empty).Replace(" ", string.Empty);
        var lastComma = value.LastIndexOf(',');
        var lastDot = value.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            if (lastComma > lastDot)
            {
                value = value.Replace(".", string.Empty).Replace(",", ".");
            }
            else
            {
                value = value.Replace(",", string.Empty);
            }
        }
        else if (lastComma >= 0)
        {
            value = value.Replace(".", string.Empty).Replace(",", ".");
        }
        else if (lastDot >= 0)
        {
            value = value.Replace(",", string.Empty);
        }

        return value;
    }
}
