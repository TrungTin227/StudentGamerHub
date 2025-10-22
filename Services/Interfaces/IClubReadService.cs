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
}
