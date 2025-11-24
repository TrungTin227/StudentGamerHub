using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;
    private readonly IRoomReadService _roomReadService;

    public RoomsController(IRoomService roomService, IRoomReadService roomReadService)
    {
        _roomService = roomService ?? throw new ArgumentNullException(nameof(roomService));
        _roomReadService = roomReadService ?? throw new ArgumentNullException(nameof(roomReadService));
    }

    [HttpGet("{id:guid}")]
    [EnableRateLimiting("RoomsRead")]
    [ProducesResponseType(typeof(RoomDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetRoom(Guid id, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        var result = await _roomService.GetByIdAsync(id, currentUserId, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    [HttpGet("~/api/clubs/{clubId:guid}/rooms")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(PagedResult<RoomDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ListRooms(Guid clubId, [FromQuery] int? offset = null, [FromQuery] int? limit = null, CancellationToken ct = default)
    {
        var sanitizedOffset = Math.Max(offset ?? 0, 0);
        var sanitizedLimit = Math.Clamp(limit ?? 20, 1, 50);
        var paging = new OffsetPaging(sanitizedOffset, sanitizedLimit);
        var currentUserId = User.GetUserId();

        var result = await _roomReadService.ListByClubAsync(clubId, currentUserId, paging, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// List members of a room with optional filtering and offset pagination.
    /// </summary>
    [HttpGet("{roomId:guid}/members")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(OffsetPage<RoomMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ListMembers(
        Guid roomId,
        [FromQuery] RoomRole? role = null,
        [FromQuery] RoomMemberStatus? status = null,
        [FromQuery(Name = "q")] string? query = null,
        [FromQuery] string? sort = null,
        [FromQuery] int? offset = null,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        var filter = new RoomMemberListFilter
        {
            Role = role,
            Status = status,
            Query = query,
            Sort = sort ?? MemberListSort.JoinedAtDesc
        };

        var sanitizedOffset = Math.Max(offset ?? 0, 0);
        var sanitizedLimit = Math.Clamp(limit ?? 20, 1, 50);
        var paging = new OffsetPaging(sanitizedOffset, sanitizedLimit, filter.Sort, false);
        var currentUserId = User.GetUserId();

        var result = await _roomReadService
            .ListMembersAsync(roomId, filter, paging, currentUserId, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Get the most recent members who joined the room.
    /// </summary>
    [HttpGet("{roomId:guid}/members/recent")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(IReadOnlyList<RoomMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ListRecentMembers(
        Guid roomId,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        var sanitizedLimit = limit ?? 20;

        var result = await _roomReadService
            .ListRecentMembersAsync(roomId, sanitizedLimit, currentUserId, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Get all rooms across all clubs with filtering and pagination.
    /// Public endpoint - accessible to any authenticated user.
    /// </summary>
    /// <param name="name">Filter by room name (partial match, case-insensitive)</param>
    /// <param name="joinPolicy">Filter by join policy (null = all)</param>
    /// <param name="capacity">Filter by exact capacity (null = all)</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="size">Page size (default: 20, max: 50)</param>
    /// <param name="sort">Sort field (default: CreatedAtUtc)</param>
    /// <param name="desc">Sort descending (default: true)</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(PagedResult<RoomDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> GetAllRooms(
        [FromQuery] string? name = null,
        [FromQuery] RoomJoinPolicy? joinPolicy = null,
        [FromQuery] int? capacity = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = true,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        
        var paging = new PageRequest
        {
            Page = page,
            Size = size,
            Sort = sort ?? "CreatedAtUtc",
            Desc = desc
        };

        var result = await _roomReadService
            .GetAllRoomsAsync(name, joinPolicy, capacity, paging, currentUserId, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Create a room inside a club and join the caller as the owner member.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("RoomsCreate")]
    [ProducesResponseType(typeof(RoomDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CreateRoom([FromBody] RoomCreateRequestDto request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _roomService.CreateRoomAsync(request, userId.Value, ct);
        return this.ToCreatedAtAction(result, nameof(GetRoom), result.IsSuccess ? new { id = result.Value!.Id } : null);
    }

    [HttpPut("{id:guid}")]
    [EnableRateLimiting("RoomsWrite")]
    [ProducesResponseType(typeof(RoomDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateRoom(Guid id, [FromBody] RoomUpdateRequestDto request, CancellationToken ct)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
            return Unauthorized();

        var result = await _roomService.UpdateRoomAsync(id, request, actorId.Value, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Join a room. Requires existing club membership and respects room join policy.
    /// Idempotent: returns current membership details if already joined.
    /// </summary>
    [HttpPost("{roomId:guid}/join")]
    [EnableRateLimiting("RoomsWrite")]
    [ProducesResponseType(typeof(RoomDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> JoinRoom(Guid roomId, [FromBody] RoomJoinRequestDto? request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _roomService.JoinRoomAsync(roomId, userId.Value, request, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    [HttpDelete("{id:guid}")]
    [EnableRateLimiting("RoomsWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteRoom(Guid id, CancellationToken ct)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
            return Unauthorized();

        var result = await _roomService.DeleteRoomAsync(id, actorId.Value, ct);
        return this.ToActionResult(result);
    }

    [HttpDelete("{roomId:guid}/members/{userId:guid}")]
    [EnableRateLimiting("RoomsWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveMember(Guid roomId, Guid userId, CancellationToken ct)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
            return Unauthorized();

        var result = await _roomService.KickRoomMemberAsync(roomId, userId, actorId.Value, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("{roomId:guid}/members/{userId:guid}/approve")]
    [EnableRateLimiting("RoomsWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> ApproveMember(Guid roomId, Guid userId, CancellationToken ct)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
            return Unauthorized();

        var result = await _roomService.ApproveRoomMemberAsync(roomId, userId, actorId.Value, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("{roomId:guid}/members/{userId:guid}/reject")]
    [EnableRateLimiting("RoomsWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RejectMember(Guid roomId, Guid userId, CancellationToken ct)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
            return Unauthorized();

        var result = await _roomService.RejectRoomMemberAsync(roomId, userId, actorId.Value, ct);
        return this.ToActionResult(result);
    }
}
