namespace DTOs.Admin;

/// <summary>
/// Thống kê doanh thu theo khoảng thời gian
/// </summary>
public record AdminRevenueStatsDto
{
    /// <summary>
    /// Loại thời gian (Week, Month, Year)
    /// </summary>
    public string PeriodType { get; init; } = string.Empty;

    /// <summary>
    /// Ngày bắt đầu
    /// </summary>
    public DateTime PeriodStart { get; init; }

    /// <summary>
    /// Ngày kết thúc
    /// </summary>
    public DateTime PeriodEnd { get; init; }

    /// <summary>
    /// Tổng doanh thu (cents)
    /// </summary>
    public long TotalRevenueCents { get; init; }

    /// <summary>
    /// Doanh thu từ membership (cents)
    /// </summary>
    public long MembershipRevenueCents { get; init; }

    /// <summary>
    /// Doanh thu từ event tickets (cents)
    /// </summary>
    public long EventRevenueCents { get; init; }

    /// <summary>
    /// Doanh thu từ wallet top-up (cents)
    /// </summary>
    public long TopUpRevenueCents { get; init; }

    /// <summary>
    /// Số lượng giao dịch
    /// </summary>
    public long TransactionCount { get; init; }

    /// <summary>
    /// Số giao dịch thành công
    /// </summary>
    public long SuccessfulCount { get; init; }

    /// <summary>
    /// Số giao dịch thất bại
    /// </summary>
    public long FailedCount { get; init; }

    /// <summary>
    /// Chi tiết theo ngày
    /// </summary>
    public List<DailyRevenueDto> DailyBreakdown { get; init; } = new();
}

/// <summary>
/// Doanh thu theo ngày
/// </summary>
public record DailyRevenueDto
{
    public DateTime Date { get; init; }
    public long RevenueCents { get; init; }
    public int TransactionCount { get; init; }
}
