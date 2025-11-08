using BusinessObjects;

namespace DTOs.Admin.Filters;

/// <summary>
/// Filter cho lịch sử thanh toán PaymentIntent (Admin view)
/// </summary>
public class AdminPaymentIntentFilter
{
    /// <summary>
    /// User ID
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Event ID
    /// </summary>
    public Guid? EventId { get; set; }

    /// <summary>
    /// Membership Plan ID
    /// </summary>
    public Guid? MembershipPlanId { get; set; }

    /// <summary>
    /// Payment purpose
    /// </summary>
    public PaymentPurpose? Purpose { get; set; }

    /// <summary>
    /// Payment status
    /// </summary>
    public PaymentIntentStatus? Status { get; set; }

    /// <summary>
    /// Từ ngày
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Đến ngày
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Khoảng thời gian: Week, Month, Year
    /// </summary>
    public string? Period { get; set; }

    /// <summary>
    /// Số tiền tối thiểu (cents)
    /// </summary>
    public long? MinAmountCents { get; set; }

    /// <summary>
    /// Số tiền tối đa (cents)
    /// </summary>
    public long? MaxAmountCents { get; set; }

    /// <summary>
    /// Order code
    /// </summary>
    public long? OrderCode { get; set; }
}
