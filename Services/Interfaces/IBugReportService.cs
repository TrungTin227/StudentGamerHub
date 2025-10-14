using DTOs.Bugs;

namespace Services.Interfaces;

/// <summary>
/// Service for managing bug reports.
/// </summary>
public interface IBugReportService
{
    /// <summary>
    /// Creates a new bug report.
    /// </summary>
    Task<Result<BugReportDto>> CreateAsync(
        Guid userId,
        BugReportCreateRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a bug report by ID.
    /// </summary>
    Task<Result<BugReportDto>> GetByIdAsync(
        Guid id,
        CancellationToken ct = default);

    /// <summary>
    /// Gets bug reports for a specific user with pagination.
    /// </summary>
    Task<Result<PagedResult<BugReportDto>>> GetByUserIdAsync(
        Guid userId,
        PageRequest paging,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all bug reports with pagination (admin only).
    /// </summary>
    Task<Result<PagedResult<BugReportDto>>> ListAsync(
        PageRequest paging,
        CancellationToken ct = default);

    /// <summary>
    /// Gets bug reports filtered by status with pagination (admin only).
    /// </summary>
    Task<Result<PagedResult<BugReportDto>>> GetByStatusAsync(
        string status,
        PageRequest paging,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the status of a bug report (admin only).
    /// </summary>
    Task<Result<BugReportDto>> UpdateStatusAsync(
        Guid id,
        BugReportStatusPatchRequest request,
        CancellationToken ct = default);
}
