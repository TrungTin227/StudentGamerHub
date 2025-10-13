using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

public sealed class WalletRepository : IWalletRepository
{
    private readonly AppDbContext _context;

    public WalletRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);

    public async Task CreateIfMissingAsync(Guid userId, CancellationToken ct = default)
    {
        var exists = await _context.Wallets
            .AsNoTracking()
            .AnyAsync(w => w.UserId == userId, ct)
            .ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        var wallet = new Wallet
        {
            UserId = userId,
        };

        await _context.Wallets.AddAsync(wallet, ct).ConfigureAwait(false);
    }

    public async Task<bool> AdjustBalanceAsync(Guid userId, long deltaCents, CancellationToken ct = default)
    {
        if (deltaCents == 0)
        {
            return true;
        }

        var query = _context.Wallets.Where(w => w.UserId == userId);

        if (deltaCents < 0)
        {
            query = query.Where(w => w.BalanceCents + deltaCents >= 0);
        }

        var affected = await query.ExecuteUpdateAsync(setters =>
            setters.SetProperty(w => w.BalanceCents, w => w.BalanceCents + deltaCents), ct).ConfigureAwait(false);

        return affected > 0;
    }
}
