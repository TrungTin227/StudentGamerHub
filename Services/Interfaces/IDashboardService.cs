namespace Services.Interfaces;

/// <summary>
/// Dashboard service interface for aggregating today's data
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Get dashboard data for today (VN timezone)
    /// Includes: Points, Quests, Events, Activity
    /// </summary>
    Task<Result<DashboardTodayDto>> GetTodayAsync(Guid userId, CancellationToken ct = default);
}
