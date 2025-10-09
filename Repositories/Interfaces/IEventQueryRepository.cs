namespace Repositories.Interfaces;

/// <summary>
/// Query interface for Event entity - Dashboard feature
/// </summary>
public interface IEventQueryRepository
{
    /// <summary>
    /// Get events starting within UTC range [startUtc, endUtc)
    /// Filters: Status != Draft/Canceled, soft-delete enabled
    /// Uses StartsAt index
    /// </summary>
    Task<IReadOnlyList<Event>> GetEventsStartingInRangeUtcAsync(
        DateTimeOffset startUtc, 
        DateTimeOffset endUtc, 
        CancellationToken ct = default);
}
