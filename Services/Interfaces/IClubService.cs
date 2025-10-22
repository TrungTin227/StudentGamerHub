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

    Task<Result<ClubDetailDto>> CreateClubAsync(ClubCreateRequestDto req, Guid currentUserId, CancellationToken ct = default);
    Task<Result<ClubDetailDto>> JoinClubAsync(Guid clubId, Guid currentUserId, CancellationToken ct = default);
    Task<Result> KickClubMemberAsync(Guid clubId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default);
    Task<Result<ClubDetailDto>> GetByIdAsync(Guid clubId, Guid? currentUserId = null, CancellationToken ct = default);
    Task<Result<ClubDetailDto>> UpdateClubAsync(Guid id, ClubUpdateRequestDto req, Guid actorId, CancellationToken ct = default);
    Task<Result> DeleteClubAsync(Guid id, Guid actorId, CancellationToken ct = default);
}
