using BusinessObjects.Common.Pagination;
using DTOs.Admin;
using DTOs.Admin.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;
using WebApi.Common;

namespace WebAPI.Controllers;

/// <summary>
/// Controller cho Admin Dashboard - tập trung tất cả API quản lý cho Admin
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _dashboardService;
    private readonly ILogger<AdminDashboardController> _logger;

    public AdminDashboardController(
        IAdminDashboardService dashboardService,
        ILogger<AdminDashboardController> logger)
    {
        _dashboardService = dashboardService ?? throw new ArgumentNullException(nameof(dashboardService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Dashboard Summary

    /// <summary>
    /// Lấy tổng quan dashboard với các thống kê chính
    /// </summary>
    [HttpGet("summary")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(AdminDashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> GetSummary(CancellationToken ct)
    {
        var result = await _dashboardService.GetDashboardSummaryAsync(ct);
        return this.ToActionResult(result);
    }

    #endregion

    #region User Management

    /// <summary>
    /// Lấy danh sách users với filter và pagination
    /// </summary>
    [HttpGet("users")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(PagedResult<AdminUserStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> GetUsers(
        [FromQuery] AdminUserFilter filter,
        [FromQuery] PageRequest page,
        CancellationToken ct)
    {
        var result = await _dashboardService.GetUsersAsync(filter, page, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Lấy chi tiết user theo ID
    /// </summary>
    [HttpGet("users/{userId:guid}")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(AdminUserStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetUserById(Guid userId, CancellationToken ct)
    {
        var result = await _dashboardService.GetUserByIdAsync(userId, ct);
        return this.ToActionResult(result);
    }

    #endregion

    #region Revenue Statistics

    /// <summary>
    /// Thống kê doanh thu theo khoảng thời gian
    /// </summary>
    /// <param name="period">Khoảng thời gian: week, month, year, custom</param>
    /// <param name="startDate">Ngày bắt đầu (cho custom period)</param>
    /// <param name="endDate">Ngày kết thúc (cho custom period)</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("revenue")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(AdminRevenueStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetRevenueStats(
        [FromQuery] string period = "month",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var result = await _dashboardService.GetRevenueStatsAsync(period, startDate, endDate, ct);
        return this.ToActionResult(result);
    }

    #endregion

    #region Transaction History

    /// <summary>
    /// Lấy lịch sử giao dịch với filter
    /// </summary>
    [HttpGet("transactions")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(PagedResult<AdminTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetTransactions(
        [FromQuery] AdminTransactionFilter filter,
        [FromQuery] PageRequest page,
        CancellationToken ct)
    {
        var result = await _dashboardService.GetTransactionsAsync(filter, page, ct);
        return this.ToActionResult(result);
    }

    #endregion

    #region Payment Intent History

    /// <summary>
    /// Lấy lịch sử thanh toán (PaymentIntents) với filter
    /// </summary>
    [HttpGet("payments")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(PagedResult<AdminPaymentIntentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetPaymentIntents(
        [FromQuery] AdminPaymentIntentFilter filter,
        [FromQuery] PageRequest page,
        CancellationToken ct)
    {
        var result = await _dashboardService.GetPaymentIntentsAsync(filter, page, ct);
        return this.ToActionResult(result);
    }

    #endregion

    #region Membership Management

    /// <summary>
    /// Thống kê các membership plans
    /// </summary>
    [HttpGet("memberships")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(List<AdminMembershipStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetMembershipStats(CancellationToken ct)
    {
        var result = await _dashboardService.GetMembershipStatsAsync(ct);
        return this.ToActionResult(result);
    }

    #endregion

    #region Community Management

    /// <summary>
    /// Thống kê communities
    /// </summary>
    [HttpGet("communities")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(PagedResult<AdminCommunityStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetCommunityStats(
        [FromQuery] PageRequest page,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var result = await _dashboardService.GetCommunityStatsAsync(page, includeDeleted, ct);
        return this.ToActionResult(result);
    }

    #endregion

    #region Club Management

    /// <summary>
    /// Thống kê clubs
    /// </summary>
    [HttpGet("clubs")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(PagedResult<AdminClubStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetClubStats(
        [FromQuery] PageRequest page,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var result = await _dashboardService.GetClubStatsAsync(page, includeDeleted, ct);
        return this.ToActionResult(result);
    }

    #endregion

    #region Game Management

    /// <summary>
    /// Thống kê games
    /// </summary>
    [HttpGet("games")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(PagedResult<AdminGameStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetGameStats(
        [FromQuery] PageRequest page,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var result = await _dashboardService.GetGameStatsAsync(page, includeDeleted, ct);
        return this.ToActionResult(result);
    }

    #endregion

    #region Role Management

    /// <summary>
    /// Thống kê roles trong hệ thống
    /// </summary>
    [HttpGet("roles")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(List<AdminRoleStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetRoleStats(CancellationToken ct)
    {
        var result = await _dashboardService.GetRoleStatsAsync(ct);
        return this.ToActionResult(result);
    }

    #endregion
}
