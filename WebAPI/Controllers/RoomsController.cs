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
        return this.ToActionResult(result, successStatus: StatusCodes.Status204NoContent);
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
