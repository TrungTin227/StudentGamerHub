namespace Services.Implementations;

/// <summary>
/// Read-only queries for rooms.
/// </summary>
public sealed class RoomReadService : IRoomReadService
{
    private readonly IRoomQueryRepository _roomQuery;
    private readonly IClubQueryRepository _clubQuery;

    public RoomReadService(
        IRoomQueryRepository roomQuery,
        IClubQueryRepository clubQuery)
    {
        _roomQuery = roomQuery ?? throw new ArgumentNullException(nameof(roomQuery));
        _clubQuery = clubQuery ?? throw new ArgumentNullException(nameof(clubQuery));
    }

    public async Task<Result<PagedResult<RoomDetailDto>>> ListByClubAsync(
        Guid clubId,
        Guid? currentUserId,
        OffsetPaging paging,
        CancellationToken ct = default)
    {
        if (clubId == Guid.Empty)
        {
            return Result<PagedResult<RoomDetailDto>>.Failure(
                new Error(Error.Codes.Validation, "ClubId is required."));
        }

        var club = await _clubQuery.GetByIdAsync(clubId, ct).ConfigureAwait(false);
        if (club is null)
        {
            return Result<PagedResult<RoomDetailDto>>.Failure(
                new Error(Error.Codes.NotFound, "Club not found."));
        }

        var sanitizedLimit = Math.Clamp(paging.LimitSafe, 1, 50);
        var sort = string.IsNullOrWhiteSpace(paging.Sort) ? nameof(RoomDetailDto.CreatedAtUtc) : paging.Sort!;
        var desc = string.IsNullOrWhiteSpace(paging.Sort) ? true : paging.Desc;
        var sanitizedPaging = new OffsetPaging(paging.OffsetSafe, sanitizedLimit, sort, desc);
        var pageRequest = sanitizedPaging.ToPageRequest();

        var page = await _roomQuery
            .ListByClubAsync(clubId, currentUserId, pageRequest, ct)
            .ConfigureAwait(false);

        var items = page.Items
            .Select(model => model.ToRoomDetailDto())
            .ToList();

        var dtoPage = new PagedResult<RoomDetailDto>(
            items,
            page.Page,
            page.Size,
            page.TotalCount,
            page.TotalPages,
            page.HasPrevious,
            page.HasNext,
            page.Sort,
            page.Desc);

        return Result<PagedResult<RoomDetailDto>>.Success(dtoPage);
    }
}
