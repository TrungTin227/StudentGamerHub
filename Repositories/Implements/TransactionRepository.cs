using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _context;

    public TransactionRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task CreateAsync(Transaction tx, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tx);
        await _context.Transactions.AddAsync(tx, ct).ConfigureAwait(false);
    }

    public Task<bool> ExistsByProviderRefAsync(string provider, string providerRef, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerRef);

        var normalizedProvider = provider.Trim();
        var normalizedRef = providerRef.Trim();

        return _context.Transactions
            .AsNoTracking()
            .AnyAsync(t => t.Provider == normalizedProvider && t.ProviderRef == normalizedRef, ct);
    }
}
