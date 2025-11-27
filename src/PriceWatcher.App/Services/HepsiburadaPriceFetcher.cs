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
    private static readonly Regex PriceRegex = new("(\u20ba|₺)?\s*(?<value>\d{1,3}(?:\.\d{3})*(?:,\d{2})?)", RegexOptions.Compiled);

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
            var response = await client.GetAsync(productUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch price for {Url}. Status code: {StatusCode}", productUrl, response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

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
        var priceNodes = doc.DocumentNode
            .Descendants()
            .Where(n => n.Name is "span" or "div" || n.Name.Equals("ins", StringComparison.OrdinalIgnoreCase))
            .Where(n => HasPriceHint(n.GetAttributeValue("class", string.Empty)) || HasPriceHint(n.GetAttributeValue("id", string.Empty)))
            .ToList();

        foreach (var node in priceNodes)
        {
            var candidate = node.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(candidate) && PriceRegex.IsMatch(candidate))
            {
                return candidate;
            }
        }

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

        var raw = match.Groups["value"].Value;
        var normalized = raw.Replace(".", string.Empty).Replace(",", ".");
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out price))
        {
            return true;
        }

        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out price);
    }
}
