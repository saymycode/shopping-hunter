using System.Collections.Generic;

namespace PriceWatcher.App.Configuration;

/// <summary>
/// Represents configuration settings for scraping e-commerce product pages.
/// </summary>
public sealed class PriceWatcherOptions
{
    /// <summary>
    /// Gets or sets the collection of product URLs to watch.
    /// </summary>
    public List<string> ProductUrls { get; set; } = new();

    /// <summary>
    /// Gets or sets the interval, in minutes, between successive price checks.
    /// </summary>
    public int RequestIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether notifications should be sent on every pull regardless of change amount.
    /// </summary>
    public bool NotifyOnEveryPull { get; set; }

    /// <summary>
    /// Gets or sets the minimum percentage difference required to notify when <see cref="NotifyOnEveryPull"/> is false.
    /// </summary>
    public double MinChangePercentageToNotify { get; set; } = 0.1;
}
