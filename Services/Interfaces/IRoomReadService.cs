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
}
