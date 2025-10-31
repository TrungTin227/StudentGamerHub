namespace Repositories.Interfaces;

/// <summary>
/// Repository for wallet operations. Enforces the one-wallet-per-user invariant through:
/// 1. Database unique index on UserId (see AppDbContext line 462)
/// 2. CreateIfMissingAsync checks existence before creating
/// 3. All wallet operations use userId as the lookup key
/// </summary>
public interface IWalletRepository
{
    /// <summary>
    /// Gets the wallet for a specific user. Returns null if no wallet exists.
    /// </summary>
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Creates a wallet for the user if one doesn't already exist.
    /// CRITICAL: This method maintains the one-wallet-per-user invariant by checking existence first.
    /// The database enforces uniqueness via unique index on UserId.
    /// </summary>
    Task CreateIfMissingAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Ensures a wallet exists for the user and returns it. Creates the wallet if missing.
    /// Throws InvalidOperationException if wallet cannot be created or retrieved.
    /// </summary>
    Task<Wallet> EnsureAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Detaches a wallet entity from the EF Core change tracker.
    /// </summary>
    void Detach(Wallet wallet);

    /// <summary>
    /// Adjusts the wallet balance by the specified delta amount.
    /// Returns true if the adjustment succeeded, false if insufficient funds (for negative deltas).
    /// </summary>
    Task<bool> AdjustBalanceAsync(Guid userId, long deltaCents, CancellationToken ct = default);
}
