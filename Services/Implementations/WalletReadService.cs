using Services.Interfaces;

namespace Services.Implementations;

/// <summary>
/// Read-only wallet queries for end users and platform administrators.
/// </summary>
public sealed class WalletReadService : IWalletReadService
{
    private readonly IWalletRepository _walletRepository;
    private readonly IPlatformAccountService _platformAccountService;

    public WalletReadService(
        IWalletRepository walletRepository,
        IPlatformAccountService platformAccountService)
    {
        _walletRepository = walletRepository ?? throw new ArgumentNullException(nameof(walletRepository));
        _platformAccountService = platformAccountService ?? throw new ArgumentNullException(nameof(platformAccountService));
    }

    public async Task<Result<WalletSummaryDto>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        // Ensure wallet exists for the user - critical for maintaining one-wallet-per-user invariant
        await _walletRepository.CreateIfMissingAsync(userId, ct).ConfigureAwait(false);

        var wallet = await _walletRepository.GetByUserIdAsync(userId, ct).ConfigureAwait(false);
        var summary = new WalletSummaryDto(wallet is not null, wallet?.BalanceCents ?? 0);
        return Result<WalletSummaryDto>.Success(summary);
    }

    public async Task<Result<WalletSummaryDto>> GetPlatformWalletAsync(CancellationToken ct = default)
    {
        var platformUserResult = await _platformAccountService.GetOrCreatePlatformUserIdAsync(ct).ConfigureAwait(false);
        if (platformUserResult.IsFailure)
        {
            return Result<WalletSummaryDto>.Failure(platformUserResult.Error);
        }

        var platformUserId = platformUserResult.Value;

        await _walletRepository.CreateIfMissingAsync(platformUserId, ct).ConfigureAwait(false);

        var wallet = await _walletRepository.GetByUserIdAsync(platformUserId, ct).ConfigureAwait(false);
        if (wallet is null)
        {
            return Result<WalletSummaryDto>.Failure(new Error(Error.Codes.Unexpected, "Platform wallet could not be loaded."));
        }

        var summary = new WalletSummaryDto(true, wallet.BalanceCents);
        return Result<WalletSummaryDto>.Success(summary);
    }
}
