namespace Services.Configuration;

/// <summary>
/// Configuration options for chat realtime features.
/// </summary>
public sealed class ChatOptions
{
    /// <summary>
    /// Maximum number of messages to keep in history per channel.
    /// </summary>
    public int HistoryMax { get; set; } = 200;

    /// <summary>
    /// Time-to-live (TTL) for chat history in hours.
    /// </summary>
    public int HistoryTtlHours { get; set; } = 48;

    /// <summary>
    /// Rate limit window duration in seconds.
    /// </summary>
    public int RateLimitWindowSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of messages allowed within the rate limit window.
    /// </summary>
    public int RateLimitMaxMessages { get; set; } = 30;
}
