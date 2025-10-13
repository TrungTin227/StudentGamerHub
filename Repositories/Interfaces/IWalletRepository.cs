namespace Repositories.Interfaces;

public interface IWalletRepository
{
    Task<Wallet?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task CreateIfMissingAsync(Guid userId, CancellationToken ct = default);
    Task<bool> AdjustBalanceAsync(Guid userId, long deltaCents, CancellationToken ct = default);
}
