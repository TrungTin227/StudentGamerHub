using Microsoft.Extensions.Options;
using Services.Configuration;

namespace Services.Implementations;

/// <summary>
/// Read-only wallet queries for end users and platform administrators.
/// </summary>
public sealed class WalletReadService : IWalletReadService
{
    private readonly IWalletRepository _walletRepository;
    private readonly BillingOptions _billingOptions;

    public WalletReadService(IWalletRepository walletRepository, IOptionsSnapshot<BillingOptions> billingOptions)
    {
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _billingOptions = billingOptions?.Value ?? throw new ArgumentNullException(nameof(billingOptions));
    }

    public async Task<Result<WalletSummaryDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        var summary = new WalletSummaryDto(wallet is not null, wallet?.BalanceCents ?? 0);
        return Result<WalletSummaryDto>.Success(summary);
    }

    public async Task<Result<WalletSummaryDto>> GetPlatformWalletAsync(CancellationToken ct = default)
    {
        if (!_billingOptions.PlatformUserId.HasValue || _billingOptions.PlatformUserId.Value == Guid.Empty)
        {
            return Result<WalletSummaryDto>.Failure(new Error(Error.Codes.Unexpected, "Platform wallet is not configured."));
        }

        var wallet = await _walletRepository.GetByUserIdAsync(_billingOptions.PlatformUserId.Value, ct).ConfigureAwait(false);
        var summary = new WalletSummaryDto(wallet is not null, wallet?.BalanceCents ?? 0);
        return Result<WalletSummaryDto>.Success(summary);
    }
}
