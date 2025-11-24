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

    public async Task<Result<OffsetPage<RoomMemberDto>>> ListMembersAsync(
        Guid roomId,
        RoomMemberListFilter filter,
        OffsetPaging paging,
        Guid? currentUserId,
        CancellationToken ct = default)
    {
        if (roomId == Guid.Empty)
        {
            return Result<OffsetPage<RoomMemberDto>>.Failure(
                new Error(Error.Codes.Validation, "RoomId is required."));
        }

        ArgumentNullException.ThrowIfNull(filter);

        var room = await _roomQuery.GetByIdAsync(roomId, ct).ConfigureAwait(false);
        if (room is null)
        {
            return Result<OffsetPage<RoomMemberDto>>.Failure(
                new Error(Error.Codes.NotFound, "Room not found."));
        }

        var sanitizedLimit = Math.Clamp(paging.LimitSafe, 1, 50);
        var sanitizedPaging = new OffsetPaging(paging.OffsetSafe, sanitizedLimit, paging.Sort, paging.Desc);

        var page = await _roomQuery
            .ListMembersAsync(roomId, filter, sanitizedPaging, ct)
            .ConfigureAwait(false);

        var dtoPage = page.Map(model => model.ToRoomMemberDto(currentUserId));

        return Result<OffsetPage<RoomMemberDto>>.Success(dtoPage);
    }

    public async Task<Result<IReadOnlyList<RoomMemberDto>>> ListRecentMembersAsync(
        Guid roomId,
        int limit,
        Guid? currentUserId,
        CancellationToken ct = default)
    {
        if (roomId == Guid.Empty)
        {
            return Result<IReadOnlyList<RoomMemberDto>>.Failure(
                new Error(Error.Codes.Validation, "RoomId is required."));
        }

        var room = await _roomQuery.GetByIdAsync(roomId, ct).ConfigureAwait(false);
        if (room is null)
        {
            return Result<IReadOnlyList<RoomMemberDto>>.Failure(
                new Error(Error.Codes.NotFound, "Room not found."));
        }

        var sanitizedLimit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 50);

        var members = await _roomQuery
            .ListRecentMembersAsync(roomId, sanitizedLimit, ct)
            .ConfigureAwait(false);

        var dtos = members
            .Select(model => model.ToRoomMemberDto(currentUserId))
            .ToList();

        return Result<IReadOnlyList<RoomMemberDto>>.Success(dtos);
    }

    public async Task<Result<PagedResult<RoomDetailDto>>> GetAllRoomsAsync(
        string? name,
        RoomJoinPolicy? joinPolicy,
        int? capacity,
        PageRequest paging,
        Guid? currentUserId,
        CancellationToken ct = default)
    {
        var sanitizedPaging = new PageRequest
        {
            Page = paging.PageSafe,
            Size = Math.Clamp(paging.SizeSafe, 1, 50),
            Sort = string.IsNullOrWhiteSpace(paging.Sort) ? "CreatedAtUtc" : paging.Sort!,
            Desc = paging.Desc
        };

        var pagedRooms = await _roomQuery
            .GetAllRoomsAsync(name, joinPolicy, capacity, sanitizedPaging, currentUserId, ct)
            .ConfigureAwait(false);

        var items = pagedRooms.Items
            .Select(model => model.ToRoomDetailDto())
            .ToList();

        var result = new PagedResult<RoomDetailDto>(
            items,
            pagedRooms.Page,
            pagedRooms.Size,
            pagedRooms.TotalCount,
            pagedRooms.TotalPages,
            pagedRooms.HasPrevious,
            pagedRooms.HasNext,
            pagedRooms.Sort,
            pagedRooms.Desc);

        return Result<PagedResult<RoomDetailDto>>.Success(result);
    }
}
