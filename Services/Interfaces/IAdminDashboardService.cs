using BusinessObjects.Common.Pagination;
using BusinessObjects.Common.Results;
using DTOs.Admin;
using DTOs.Admin.Filters;

namespace Services.Interfaces;

/// <summary>
/// Service để quản lý Admin Dashboard
/// </summary>
public interface IAdminDashboardService : IScopedService
{
    /// <summary>
    /// Lấy tổng quan dashboard
    /// </summary>
    Task<Result<AdminDashboardSummaryDto>> GetDashboardSummaryAsync(CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách users với filter và pagination
    /// </summary>
    Task<Result<PagedResult<AdminUserStatsDto>>> GetUsersAsync(
        AdminUserFilter filter,
        PageRequest pageRequest,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy chi tiết user theo ID
    /// </summary>
    Task<Result<AdminUserStatsDto>> GetUserByIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Thống kê doanh thu theo khoảng thời gian
    /// </summary>
    Task<Result<AdminRevenueStatsDto>> GetRevenueStatsAsync(
        string period, // "week", "month", "year"
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy lịch sử giao dịch
    /// </summary>
    Task<Result<PagedResult<AdminTransactionDto>>> GetTransactionsAsync(
        AdminTransactionFilter filter,
        PageRequest pageRequest,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy lịch sử thanh toán (PaymentIntents)
    /// </summary>
    Task<Result<PagedResult<AdminPaymentIntentDto>>> GetPaymentIntentsAsync(
        AdminPaymentIntentFilter filter,
        PageRequest pageRequest,
        CancellationToken ct = default);

    /// <summary>
    /// Thống kê memberships
    /// </summary>
    Task<Result<List<AdminMembershipStatsDto>>> GetMembershipStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Thống kê communities
    /// </summary>
    Task<Result<PagedResult<AdminCommunityStatsDto>>> GetCommunityStatsAsync(
        PageRequest pageRequest,
        bool includeDeleted = false,
        CancellationToken ct = default);

    /// <summary>
    /// Thống kê clubs
    /// </summary>
    Task<Result<PagedResult<AdminClubStatsDto>>> GetClubStatsAsync(
        PageRequest pageRequest,
        bool includeDeleted = false,
        CancellationToken ct = default);

    /// <summary>
    /// Thống kê games
    /// </summary>
    Task<Result<PagedResult<AdminGameStatsDto>>> GetGameStatsAsync(
        PageRequest pageRequest,
        bool includeDeleted = false,
        CancellationToken ct = default);

    /// <summary>
    /// Thống kê roles
    /// </summary>
    Task<Result<List<AdminRoleStatsDto>>> GetRoleStatsAsync(CancellationToken ct = default);
}
