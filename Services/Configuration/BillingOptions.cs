namespace Services.Configuration;

/// <summary>
/// Billing configuration for platform fees and wallet limits.
/// </summary>
public sealed class BillingOptions
{
    public const string SectionName = "Billing";

    /// <summary>
    /// Event creation fee charged to organizers in VND cents.
    /// </summary>
    public long EventCreationFeeCents { get; set; } = 50_000;

    /// <summary>
    /// Optional platform user identifier used as the platform wallet owner. When not provided, the platform
    /// account service will create or reuse a user automatically.
    /// </summary>
    public Guid? PlatformUserId { get; set; }

    /// <summary>
    /// Hint e-mail for the platform user. Used when <see cref="PlatformUserId"/> is not supplied so the platform
    /// account service can look up or create a deterministic identity.
    /// </summary>
    public string PlatformUserEmail { get; set; } = "platform@studentgamerhub.local";

    /// <summary>
    /// Display name for auto-created platform account.
    /// </summary>
    public string PlatformUserName { get; set; } = "Platform Wallet";

    /// <summary>
    /// Maximum allowed escrow top-up amount in cents.
    /// </summary>
    public long MaxEventEscrowTopUpAmountCents { get; set; } = 50_000_000;

    /// <summary>
    /// Maximum allowed direct wallet top-up amount in cents.
    /// </summary>
    public long MaxWalletTopUpAmountCents { get; set; } = 20_000_000;
}
