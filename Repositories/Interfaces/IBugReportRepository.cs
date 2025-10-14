namespace Repositories.Interfaces;

/// <summary>
/// Repository interface for BugReport entity.
/// </summary>
public interface IBugReportRepository : IGenericRepository<BugReport, Guid>
{
    /// <summary>
    /// Gets bug reports filtered by user ID with pagination.
    /// </summary>
    Task<PagedResult<BugReport>> GetByUserIdPagedAsync(
        Guid userId,
        PageRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets bug reports filtered by status with pagination.
    /// </summary>
    Task<PagedResult<BugReport>> GetByStatusPagedAsync(
        string status,
        PageRequest request,
        CancellationToken ct = default);
}
