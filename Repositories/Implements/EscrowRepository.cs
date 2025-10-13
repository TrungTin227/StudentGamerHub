using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

public sealed class EscrowRepository : IEscrowRepository
{
    private readonly AppDbContext _context;

    public EscrowRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<Escrow?> GetByEventIdAsync(Guid eventId, CancellationToken ct = default)
        => _context.Escrows
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public async Task UpsertAsync(Escrow escrow, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(escrow);

        var existing = await _context.Escrows
            .FirstOrDefaultAsync(e => e.EventId == escrow.EventId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await _context.Escrows.AddAsync(escrow, ct).ConfigureAwait(false);
            return;
        }

        existing.AmountHoldCents = escrow.AmountHoldCents;
        existing.Status = escrow.Status;

        _context.Escrows.Update(existing);
    }
}
