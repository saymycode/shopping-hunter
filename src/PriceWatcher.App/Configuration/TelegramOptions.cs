namespace PriceWatcher.App.Configuration;

/// <summary>
/// Represents Telegram bot configuration settings.
/// </summary>
public sealed class TelegramOptions
{
    /// <summary>
    /// Gets or sets the bot token obtained from BotFather.
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the administrator chat identifier.
    /// </summary>
    public string AdminChatId { get; set; } = string.Empty;
}
