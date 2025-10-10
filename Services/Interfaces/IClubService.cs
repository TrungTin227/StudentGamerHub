namespace Services.Interfaces;

/// <summary>
/// Service interface for Club operations.
/// Provides search, creation, and retrieval of clubs within communities.
/// </summary>
public interface IClubService
{
    /// <summary>
    /// Search clubs within a community with filtering and cursor-based pagination.
    /// Filters: name (case-insensitive partial match), visibility, member count range.
    /// Sorted by: MembersCount DESC, Id DESC (stable).
    /// </summary>
    /// <param name="communityId">Community ID to search within</param>
    /// <param name="name">Filter by club name (partial match, case-insensitive)</param>
    /// <param name="isPublic">Filter by public/private status (null = all)</param>
    /// <param name="membersFrom">Minimum members count (inclusive, must be >= 0)</param>
    /// <param name="membersTo">Maximum members count (inclusive, must be >= 0)</param>
    /// <param name="cursor">Cursor pagination request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result with paginated list of club briefs</returns>
    Task<Result<CursorPageResult<ClubBriefDto>>> SearchAsync(
        Guid communityId,
        string? name,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        CursorRequest cursor,
        CancellationToken ct = default);

    /// <summary>
    /// Create a new club within a community.
    /// The creator is NOT automatically added as a member (Room-level membership only).
    /// Initial MembersCount = 0.
    /// </summary>
    /// <param name="currentUserId">Current user ID (for audit trail)</param>
    /// <param name="communityId">Community ID</param>
    /// <param name="name">Club name (required, will be trimmed)</param>
    /// <param name="description">Optional club description</param>
    /// <param name="isPublic">Public/private flag</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result with new club ID</returns>
    Task<Result<Guid>> CreateClubAsync(
        Guid currentUserId,
        Guid communityId,
        string name,
        string? description,
        bool isPublic,
        CancellationToken ct = default);

    /// <summary>
    /// Get club by ID.
    /// </summary>
    /// <param name="clubId">Club ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result with club brief DTO, or NotFound if club doesn't exist</returns>
    Task<Result<ClubBriefDto>> GetByIdAsync(Guid clubId, CancellationToken ct = default);
}
