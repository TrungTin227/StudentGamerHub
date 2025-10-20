namespace Services.Interfaces;

public interface IWalletReadService
{
    Task<Result<WalletSummaryDto>> GetAsync(Guid userId, CancellationToken ct = default);
    Task<Result<WalletSummaryDto>> GetPlatformWalletAsync(CancellationToken ct = default);
}
