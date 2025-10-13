using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Repositories.Implements;

public sealed class EventQueryRepository : IEventQueryRepository
{
    private readonly AppDbContext _context;

    public EventQueryRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, ct);

    public async Task<Event?> GetForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        var query = _context.Events
            .Where(e => e.Id == id && !e.IsDeleted);

        if (_context.Database.IsNpgsql())
        {
            query = query.ForUpdate();
        }

        return await query.FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public Task<int> CountConfirmedAsync(Guid eventId, CancellationToken ct = default)
    {
        return _context.EventRegistrations
            .AsNoTracking()
            .Where(r => r.EventId == eventId)
            .Where(r => r.Status == EventRegistrationStatus.Confirmed || r.Status == EventRegistrationStatus.CheckedIn)
            .CountAsync(ct);
    }

    public Task<int> CountPendingOrConfirmedAsync(Guid eventId, CancellationToken ct = default)
    {
        return _context.EventRegistrations
            .AsNoTracking()
            .Where(r => r.EventId == eventId)
            .Where(r => r.Status == EventRegistrationStatus.Pending ||
                        r.Status == EventRegistrationStatus.Confirmed ||
                        r.Status == EventRegistrationStatus.CheckedIn)
            .CountAsync(ct);
    }

    public async Task<(IReadOnlyList<Event> Items, int Total)> SearchAsync(
        IEnumerable<EventStatus>? statuses,
        Guid? communityId,
        Guid? organizerId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? search,
        int page,
        int pageSize,
        bool sortAscByStartsAt,
        CancellationToken ct = default)
    {
        var query = _context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .AsQueryable();

        if (statuses is not null)
        {
            var statusList = statuses.Distinct().ToArray();
            if (statusList.Length > 0)
            {
                query = query.Where(e => statusList.Contains(e.Status));
            }
        }

        if (communityId.HasValue)
        {
            query = query.Where(e => e.CommunityId == communityId);
        }

        if (organizerId.HasValue)
        {
            query = query.Where(e => e.OrganizerId == organizerId);
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.StartsAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.StartsAt <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var pattern = $"%{term}%";
            if (_context.Database.IsNpgsql())
            {
                query = query.Where(e => EF.Functions.ILike(e.Title, pattern) ||
                                          (e.Description != null && EF.Functions.ILike(e.Description, pattern)));
            }
            else
            {
                query = query.Where(e => EF.Functions.Like(e.Title, pattern) ||
                                          (e.Description != null && EF.Functions.Like(e.Description, pattern)));
            }
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        if (pageSize <= 0)
        {
            pageSize = 20;
        }

        if (page <= 0)
        {
            page = 1;
        }

        var skip = (page - 1) * pageSize;

        query = sortAscByStartsAt
            ? query.OrderBy(e => e.StartsAt).ThenBy(e => e.Id)
            : query.OrderByDescending(e => e.StartsAt).ThenByDescending(e => e.Id);

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (items, total);
    }
}
