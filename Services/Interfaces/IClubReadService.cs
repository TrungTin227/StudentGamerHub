namespace Services.Interfaces;

/// <summary>
/// Read-only operations for club membership directories.
/// </summary>
public interface IClubReadService
{
    Task<Result<OffsetPage<ClubMemberDto>>> ListMembersAsync(
        Guid clubId,
        MemberListFilter filter,
        OffsetPaging paging,
        Guid? currentUserId,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<ClubMemberDto>>> ListRecentMembersAsync(
        Guid clubId,
        int limit,
        Guid? currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Get all clubs across all communities with filtering and pagination.
    /// Public endpoint accessible to any role.
    /// </summary>
    Task<Result<PagedResult<ClubBriefDto>>> GetAllClubsAsync(
        string? name,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        PageRequest paging,
        CancellationToken ct = default);
}
