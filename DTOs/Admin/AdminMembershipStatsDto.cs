namespace DTOs.Admin;

/// <summary>
/// Thống kê membership plans
/// </summary>
public record AdminMembershipStatsDto
{
    public Guid PlanId { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public long PriceCents { get; init; }
    public int DurationMonths { get; init; }
    public int MonthlyEventLimit { get; init; }
    public bool IsActive { get; init; }

    /// <summary>
    /// Số users đang có plan này
    /// </summary>
    public int ActiveSubscribers { get; init; }

    /// <summary>
    /// Tổng doanh thu từ plan này (cents)
    /// </summary>
    public long TotalRevenueCents { get; init; }

    /// <summary>
    /// Số lần mua trong tháng này
    /// </summary>
    public int PurchasesThisMonth { get; init; }
}
