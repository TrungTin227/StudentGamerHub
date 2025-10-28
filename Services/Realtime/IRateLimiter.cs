namespace Services.Realtime;

/// <summary>
/// Defines a distributed rate limiter contract for realtime flows.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Attempts to record an event for the specified user within a sliding window.
    /// Returns true when the caller is still within the allowed threshold.
    /// </summary>
    Task<bool> IsAllowedAsync(
        Guid userId,
        int maxEvents,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}
