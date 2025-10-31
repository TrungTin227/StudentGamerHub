using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Repositories.Implements;

/// <summary>
/// Repository implementation for wallet operations.
/// Enforces one-wallet-per-user constraint through CreateIfMissingAsync existence check
/// and database unique index on Wallet.UserId (see AppDbContext line 462).
/// </summary>
public sealed class WalletRepository : IWalletRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<WalletRepository> _logger;

    public WalletRepository(AppDbContext context, ILogger<WalletRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _context.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.UserId == userId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// This method is CRITICAL for maintaining the one-wallet-per-user invariant.
    /// It checks if a wallet exists before attempting to create one.
    /// The database unique index on UserId provides an additional safety layer.
    /// </remarks>
    public async Task CreateIfMissingAsync(Guid userId, CancellationToken ct = default)
    {
        var exists = await _context.Wallets
            .AsNoTracking()
            .AnyAsync(w => w.UserId == userId, ct)
            .ConfigureAwait(false);

        if (exists)
        {
            _logger.LogDebug("Wallet already exists for user {UserId}, skipping creation to maintain one-wallet-per-user invariant.", userId);
            return;
        }

        var wallet = new Wallet
        {
            UserId = userId,
        };

        await _context.Wallets.AddAsync(wallet, ct).ConfigureAwait(false);
        _logger.LogInformation("Created new wallet for user {UserId}. One-wallet-per-user invariant protected by unique index on UserId.", userId);
    }

    /// <inheritdoc />
    public async Task<Wallet> EnsureAsync(Guid userId, CancellationToken ct = default)
    {
        await CreateIfMissingAsync(userId, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        var wallet = await GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (wallet is null)
        {
            _logger.LogError("Wallet could not be ensured for user {UserId} despite CreateIfMissing call.", userId);
            throw new InvalidOperationException($"Wallet for user {userId} could not be ensured.");
        }

        return wallet;
    }

    /// <inheritdoc />
    public void Detach(Wallet wallet)
    {
        if (wallet is null)
        {
            return;
        }

        var entry = _context.Entry(wallet);
        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <inheritdoc />
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
