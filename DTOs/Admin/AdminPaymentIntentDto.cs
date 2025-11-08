using BusinessObjects;

namespace DTOs.Admin;

/// <summary>
/// Chi tiết payment intent cho Admin
/// </summary>
public record AdminPaymentIntentDto
{
    public Guid Id { get; init; }

    /// <summary>
    /// Thông tin user
    /// </summary>
    public Guid UserId { get; init; }
    public string? UserName { get; init; }
    public string? UserEmail { get; init; }

    /// <summary>
    /// Số tiền và mục đích
    /// </summary>
    public long AmountCents { get; init; }
    public PaymentPurpose Purpose { get; init; }
    public string PurposeDisplay { get; init; } = string.Empty;

    /// <summary>
    /// Event liên quan (nếu là EventTicket)
    /// </summary>
    public Guid? EventId { get; init; }
    public string? EventTitle { get; init; }
    public Guid? EventRegistrationId { get; init; }

    /// <summary>
    /// Membership plan liên quan (nếu là Membership)
    /// </summary>
    public Guid? MembershipPlanId { get; init; }
    public string? MembershipPlanName { get; init; }

    /// <summary>
    /// Payment status và thông tin
    /// </summary>
    public PaymentIntentStatus Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;
    public long? OrderCode { get; init; }
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Timestamps
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}
