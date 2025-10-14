using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DTOs.Bugs;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class BugReportsController : ControllerBase
{
    private readonly IBugReportService _bugReportService;

    public BugReportsController(IBugReportService bugReportService)
    {
        _bugReportService = bugReportService ?? throw new ArgumentNullException(nameof(bugReportService));
    }

    /// <summary>
    /// Create a new bug report.
    /// </summary>
    /// <param name="request">Bug report details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created bug report</returns>
    [HttpPost]
    [EnableRateLimiting("BugsWrite")]
    [ProducesResponseType(typeof(BugReportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] BugReportCreateRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _bugReportService.CreateAsync(userId.Value, request, ct);

        return result.Match(
            dto => CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto),
            error => error.ToHttpResult());
    }

    /// <summary>
    /// Get a bug report by ID.
    /// </summary>
    /// <param name="id">Bug report ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Bug report details</returns>
    [HttpGet("{id:guid}")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(BugReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var result = await _bugReportService.GetByIdAsync(id, ct);

        return result.Match(
            Ok,
            error => error.ToHttpResult());
    }

    /// <summary>
    /// Get bug reports for the current user.
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="size">Page size (default: 20, max: 100)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paged list of bug reports</returns>
    [HttpGet("my")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(PagedResult<BugReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy(
        [FromQuery] int? page,
        [FromQuery] int? size,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var paging = new PageRequest(page, size);
        var result = await _bugReportService.GetByUserIdAsync(userId.Value, paging, ct);

        return result.Match(
            Ok,
            error => error.ToHttpResult());
    }

    /// <summary>
    /// [Admin] List all bug reports with pagination.
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="size">Page size (default: 20, max: 100)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paged list of all bug reports</returns>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("DashboardRead")]
    [ProducesResponseType(typeof(PagedResult<BugReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int? page,
        [FromQuery] int? size,
        CancellationToken ct)
    {
        var paging = new PageRequest(page, size);
        var result = await _bugReportService.ListAsync(paging, ct);

        return result.Match(
            Ok,
            error => error.ToHttpResult());
    }

    /// <summary>
    /// [Admin] Get bug reports filtered by status.
    /// </summary>
    /// <param name="status">Status to filter by (Open, InProgress, Resolved, Rejected)</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="size">Page size (default: 20, max: 100)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paged list of bug reports with the specified status</returns>
    [HttpGet("status/{status}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("DashboardRead")]
    [ProducesResponseType(typeof(PagedResult<BugReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByStatus(
        [FromRoute] string status,
        [FromQuery] int? page,
        [FromQuery] int? size,
        CancellationToken ct)
    {
        var paging = new PageRequest(page, size);
        var result = await _bugReportService.GetByStatusAsync(status, paging, ct);

        return result.Match(
            Ok,
            error => error.ToHttpResult());
    }

    /// <summary>
    /// [Admin] Update the status of a bug report.
    /// </summary>
    /// <param name="id">Bug report ID</param>
    /// <param name="request">New status</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated bug report</returns>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(BugReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] BugReportStatusPatchRequest request,
        CancellationToken ct)
    {
        var result = await _bugReportService.UpdateStatusAsync(id, request, ct);

        return result.Match(
            Ok,
            error => error.ToHttpResult());
    }
}
