using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

public sealed class PaymentIntentRepository : IPaymentIntentRepository
{
    private readonly AppDbContext _context;

    public PaymentIntentRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<PaymentIntent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(pi => pi.Id == id, ct);

    public Task<PaymentIntent?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => _context.PaymentIntents
            .Where(pi => pi.UserId == userId && pi.Id == id)
            .Include(pi => pi.EventRegistration)
                .ThenInclude(r => r.PaidTransaction)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    public Task<PaymentIntent?> GetByProviderRefAsync(string providerRef, CancellationToken ct = default)
        => _context.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(pi => pi.ClientSecret == providerRef, ct);

    public async Task CreateAsync(PaymentIntent pi, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pi);
        await _context.PaymentIntents.AddAsync(pi, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(PaymentIntent pi, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pi);
        _context.PaymentIntents.Update(pi);
        return Task.CompletedTask;
    }

    public Task<int> CountActivePendingByEventAsync(Guid eventId, DateTime nowUtc, CancellationToken ct = default)
    {
        return _context.EventRegistrations
            .AsNoTracking()
            .Where(r => r.EventId == eventId && r.Status == EventRegistrationStatus.Pending)
            .Join(
                _context.PaymentIntents.AsNoTracking(),
                r => r.Id,
                pi => pi.EventRegistrationId,
                (r, pi) => new { Registration = r, Intent = pi })
            .Where(x => x.Intent.Purpose == PaymentPurpose.EventTicket &&
                        x.Intent.Status == PaymentIntentStatus.RequiresPayment &&
                        x.Intent.ExpiresAt > nowUtc)
            .CountAsync(ct);
    }
    public async Task<PaymentIntent?> GetByOrderCodeAsync(long orderCode, CancellationToken ct = default)
    {
        return await _context.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderCode == orderCode, ct);
    }

    public async Task<bool> TrySetOrderCodeAsync(Guid id, long orderCode, CancellationToken ct = default)
    {
        var pi = await _context.PaymentIntents.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (pi is null) return false;
        if (pi.OrderCode.HasValue) return true; // đã có -> coi như ok (idempotent)

        pi.OrderCode = orderCode;
        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // race hoặc trùng số -> revert state, báo false để caller thử số khác
            _context.Entry(pi).State = EntityState.Unchanged;
            return false;
        }
    }
}
