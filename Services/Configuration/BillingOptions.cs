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
    /// Optional platform user identifier used as the platform wallet owner.
    /// </summary>
    public Guid? PlatformUserId { get; set; }

    /// <summary>
    /// Maximum allowed escrow top-up amount in cents.
    /// </summary>
    public long MaxEventEscrowTopUpAmountCents { get; set; } = 50_000_000;

    /// <summary>
    /// Maximum allowed direct wallet top-up amount in cents.
    /// </summary>
    public long MaxWalletTopUpAmountCents { get; set; } = 20_000_000;
}
