namespace DTOs.Wallets;

public sealed record WalletSummaryDto(
    bool Exists,
    long BalanceCents);
