using System;

namespace PriceWatcher.App.Models;

/// <summary>
/// Represents the result of a price check operation.
/// </summary>
public sealed class PriceCheckResult
{
    public string ProductUrl { get; set; } = string.Empty;

    public decimal? OldPrice { get; set; }

    public decimal NewPrice { get; set; }

    public bool Changed { get; set; }

    public double ChangeRate { get; set; }

    public DateTime CheckedAt { get; set; }
}
