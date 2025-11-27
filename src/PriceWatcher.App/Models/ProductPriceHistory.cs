using System;

namespace PriceWatcher.App.Models;

/// <summary>
/// Represents the last known price state for a Hepsiburada product.
/// </summary>
public sealed class ProductPriceHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ProductUrl { get; set; } = string.Empty;

    public decimal LastPrice { get; set; }

    public DateTime LastCheckTime { get; set; }
}
