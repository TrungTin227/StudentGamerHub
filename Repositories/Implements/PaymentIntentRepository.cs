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
}
