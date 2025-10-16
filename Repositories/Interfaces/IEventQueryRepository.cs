namespace Repositories.Interfaces;

public interface IEventQueryRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Event?> GetForUpdateAsync(Guid id, CancellationToken ct = default);
    Task<int> CountConfirmedAsync(Guid eventId, CancellationToken ct = default);
    Task<int> CountPendingOrConfirmedAsync(Guid eventId, CancellationToken ct = default);
    Task<IReadOnlyList<Event>> GetEventsStartingInRangeUtcAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
    Task<(IReadOnlyList<Event> Items, int Total)> SearchAsync(
        IEnumerable<EventStatus>? statuses,
        Guid? communityId,
        Guid? organizerId,
        DateTime? from,
        DateTime? to,
        string? search,
        int page,
        int pageSize,
        bool sortAscByStartsAt,
        CancellationToken ct = default);
}
