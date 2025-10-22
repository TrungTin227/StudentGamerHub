namespace Services.Interfaces;

/// <summary>
/// Read-only operations for rooms.
/// </summary>
public interface IRoomReadService
{
    /// <summary>
    /// List rooms within a club with offset-based pagination and membership flags.
    /// </summary>
    Task<Result<PagedResult<RoomDetailDto>>> ListByClubAsync(
        Guid clubId,
        Guid? currentUserId,
        OffsetPaging paging,
        CancellationToken ct = default);

    /// <summary>
    /// List members of a room with filtering and offset pagination.
    /// </summary>
    Task<Result<OffsetPage<RoomMemberDto>>> ListMembersAsync(
        Guid roomId,
        RoomMemberListFilter filter,
        OffsetPaging paging,
        Guid? currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch the most recently joined members for a room.
    /// </summary>
    Task<Result<IReadOnlyList<RoomMemberDto>>> ListRecentMembersAsync(
        Guid roomId,
        int limit,
        Guid? currentUserId,
        CancellationToken ct = default);
}
