namespace DTOs.Admin;

/// <summary>
/// Tổng quan dashboard cho Admin
/// </summary>
public record AdminDashboardSummaryDto
{
    /// <summary>
    /// Tổng số người dùng
    /// </summary>
    public int TotalUsers { get; init; }

    /// <summary>
    /// Số người dùng mới trong 30 ngày
    /// </summary>
    public int NewUsersLast30Days { get; init; }

    /// <summary>
    /// Tổng số communities đang hoạt động
    /// </summary>
    public int ActiveCommunities { get; init; }

    /// <summary>
    /// Tổng số clubs
    /// </summary>
    public int TotalClubs { get; init; }

    /// <summary>
    /// Tổng số games
    /// </summary>
    public int TotalGames { get; init; }

    /// <summary>
    /// Tổng số events
    /// </summary>
    public int TotalEvents { get; init; }

    /// <summary>
    /// Tổng doanh thu (VND)
    /// </summary>
    public decimal TotalRevenueVND { get; init; }

    /// <summary>
    /// Doanh thu tháng này (VND)
    /// </summary>
    public decimal RevenueThisMonthVND { get; init; }

    /// <summary>
    /// Số lượng giao dịch thành công
    /// </summary>
    public long SuccessfulTransactions { get; init; }

    /// <summary>
    /// Số lượng memberships đang hoạt động
    /// </summary>
    public int ActiveMemberships { get; init; }

    /// <summary>
    /// Số lượng bug reports đang mở
    /// </summary>
    public int OpenBugReports { get; init; }

    /// <summary>
    /// Thời điểm tạo báo cáo
    /// </summary>
    public DateTime GeneratedAtUtc { get; init; }
}
