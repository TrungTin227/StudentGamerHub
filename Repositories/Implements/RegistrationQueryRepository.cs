using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

public sealed class RegistrationQueryRepository : IRegistrationQueryRepository
{
    private readonly AppDbContext _context;

    public RegistrationQueryRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<EventRegistration?> GetByEventAndUserAsync(Guid eventId, Guid userId, CancellationToken ct = default)
        => _context.EventRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.UserId == userId, ct);

    public Task<EventRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.EventRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<(IReadOnlyList<EventRegistration> Items, int Total)> ListByEventAsync(
        Guid eventId,
        IEnumerable<EventRegistrationStatus>? statuses,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.EventRegistrations
            .AsNoTracking()
            .Where(r => r.EventId == eventId);

        if (statuses is not null)
        {
            var allowed = statuses.Distinct().ToArray();
            if (allowed.Length > 0)
            {
                query = query.Where(r => allowed.Contains(r.Status));
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

        var items = await query
            .OrderByDescending(r => r.RegisteredAt)
            .ThenByDescending(r => r.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (items, total);
    }

    public async Task<(IReadOnlyList<(EventRegistration Reg, Event Ev)> Items, int Total)> ListByUserAsync(
        Guid userId,
        IEnumerable<EventRegistrationStatus>? statuses,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.EventRegistrations
            .Where(r => r.UserId == userId)
            .Include(r => r.Event)
            .AsNoTracking();

        if (statuses is not null)
        {
            var allowed = statuses.Distinct().ToArray();
            if (allowed.Length > 0)
            {
                query = query.Where(r => allowed.Contains(r.Status));
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

        var items = await query
            .OrderByDescending(r => r.RegisteredAt)
            .ThenByDescending(r => r.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(r => new { Registration = r, Event = r.Event! })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (items.Select(x => ((EventRegistration Reg, Event Ev))(x.Registration, x.Event)).ToList(), total);
    }
}
