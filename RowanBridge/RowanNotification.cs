using System;

namespace RowanBridge;

/// <summary>
/// Represents an informational message that can flow between the strategy and the UI.
/// </summary>
public sealed class RowanNotification
{
    public RowanNotification(string source, string message, NotificationLevel level = NotificationLevel.Info, DateTime? timestampUtc = null)
    {
        Source = string.IsNullOrWhiteSpace(source) ? "Unknown" : source;
        Message = message ?? string.Empty;
        Level = level;
        TimestampUtc = timestampUtc ?? DateTime.UtcNow;
    }

    public string Source { get; }

    public string Message { get; }

    public NotificationLevel Level { get; }

    public DateTime TimestampUtc { get; }
}

public enum NotificationLevel
{
    Info = 0,
    Warning,
    Error,
    Success,
    Debug
}
