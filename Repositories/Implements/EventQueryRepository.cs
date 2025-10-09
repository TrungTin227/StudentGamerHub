using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

/// <summary>
/// Event query implementation for Dashboard feature
/// Uses AppDbContext for read-only queries
/// Respects soft-delete global filters
/// </summary>
public sealed class EventQueryRepository : IEventQueryRepository
{
    private readonly AppDbContext _context;

    public EventQueryRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get events starting within UTC range [startUtc, endUtc)
    /// Filters: Status != Draft/Canceled, soft-delete enabled
    /// Uses StartsAt index for performance
    /// </summary>
    public async Task<IReadOnlyList<Event>> GetEventsStartingInRangeUtcAsync(
        DateTimeOffset startUtc, 
        DateTimeOffset endUtc, 
        CancellationToken ct = default)
    {
        // Query with StartsAt index
        // Global soft-delete filter is automatically applied
        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.StartsAt >= startUtc && e.StartsAt < endUtc)
            .Where(e => e.Status != EventStatus.Draft && e.Status != EventStatus.Canceled)
            .Select(e => new Event
            {
                Id = e.Id,
                Title = e.Title,
                StartsAt = e.StartsAt,
                EndsAt = e.EndsAt,
                Location = e.Location,
                Mode = e.Mode
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return events;
    }
}
