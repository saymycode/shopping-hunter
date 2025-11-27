using System;

namespace PriceWatcher.App.Models;

/// <summary>
/// Represents a Telegram user subscription for notifications.
/// </summary>
public sealed class TelegramSubscriber
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public long ChatId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
