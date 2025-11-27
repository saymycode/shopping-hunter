namespace PriceWatcher.App.Configuration;

/// <summary>
/// Represents storage configuration settings for the application.
/// </summary>
public sealed class StorageOptions
{
    /// <summary>
    /// Gets or sets the LiteDB database file path.
    /// </summary>
    public string DbPath { get; set; } = "data/pricewatcher.db";
}
