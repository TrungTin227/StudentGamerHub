using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

/// <summary>
/// Repository implementation for BugReport entity.
/// </summary>
public sealed class BugReportRepository : GenericRepository<BugReport, Guid>, IBugReportRepository
{
    public BugReportRepository(AppDbContext context) : base(context)
    {
    }

    /// <inheritdoc/>
    public async Task<PagedResult<BugReport>> GetByUserIdPagedAsync(
        Guid userId,
        PageRequest request,
        CancellationToken ct = default)
    {
        return await GetPagedAsync(
            request,
            predicate: b => b.UserId == userId,
            orderBy: q => q.OrderByDescending(b => b.CreatedAtUtc),
            asNoTracking: true,
            ct: ct);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<BugReport>> GetByStatusPagedAsync(
        string status,
        PageRequest request,
        CancellationToken ct = default)
    {
        // Parse status outside the expression tree
        if (!Enum.TryParse<BugStatus>(status, ignoreCase: true, out var targetStatus))
        {
            // Return empty result for invalid status
            return new PagedResult<BugReport>(
                Items: new List<BugReport>(),
                Page: request.PageSafe,
                Size: request.SizeSafe,
                TotalCount: 0,
                TotalPages: 0,
                HasPrevious: false,
                HasNext: false,
                Sort: request.SortSafe,
                Desc: request.Desc);
        }

        return await GetPagedAsync(
            request,
            predicate: b => b.Status == targetStatus,
            orderBy: q => q.OrderByDescending(b => b.CreatedAtUtc),
            asNoTracking: true,
            ct: ct);
    }
}
