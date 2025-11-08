using BusinessObjects;

namespace DTOs.Admin.Filters;

/// <summary>
/// Filter cho lịch sử giao dịch (Admin view)
/// </summary>
public class AdminTransactionFilter
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
    /// Transaction status
    /// </summary>
    public TransactionStatus? Status { get; set; }

    /// <summary>
    /// Transaction direction
    /// </summary>
    public TransactionDirection? Direction { get; set; }

    /// <summary>
    /// Transaction method
    /// </summary>
    public TransactionMethod? Method { get; set; }

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
    /// Payment provider
    /// </summary>
    public string? Provider { get; set; }
}
