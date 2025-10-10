using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

/// <summary>
/// Room management controller.
/// Handles room creation, joining, member approval, leaving, and moderation.
/// </summary>
[ApiController]
[Route("rooms")]
[Produces("application/json")]
[Authorize]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService ?? throw new ArgumentNullException(nameof(roomService));
    }

    /// <summary>
    /// Create a new room within a club.
    /// Creator becomes the room owner with automatic approval.
    /// Rate limit: 10 requests per day per user.
    /// </summary>
    /// <param name="request">Room creation details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created room ID</returns>
    /// <response code="201">Room created successfully</response>
    /// <response code="400">Invalid request (validation error)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="404">Club not found</response>
    /// <response code="429">Rate limit exceeded (10 per day)</response>
    [HttpPost]
    [EnableRateLimiting("RoomsCreate")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> CreateRoom(
        [FromBody] RoomCreateRequestDto request,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();

        var result = await _roomService.CreateRoomAsync(
            currentUserId.Value,
            request.ClubId,
            request.Name,
            request.Description,
            request.JoinPolicy,
            request.Password,
            request.Capacity,
            ct);

        if (result.IsSuccess)
        {
            return CreatedAtAction(
                actionName: nameof(CreateRoom),
                routeValues: new { id = result.Value },
                value: new { roomId = result.Value });
        }

        return this.ToActionResult(result);
    }

    /// <summary>
    /// Join a room based on its policy.
    /// Open: approved immediately; RequiresApproval: pending; RequiresPassword: needs valid password.
    /// Rate limit: 60 requests per minute per user.
    /// </summary>
    /// <param name="id">Room ID to join</param>
    /// <param name="request">Join request (password if required)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success or error</returns>
    /// <response code="204">Joined successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Forbidden (banned or invalid password)</response>
    /// <response code="404">Room not found</response>
    /// <response code="409">Already a member or room at capacity</response>
    /// <response code="429">Rate limit exceeded (60 per minute)</response>
    [HttpPost("{id:guid}/join")]
    [EnableRateLimiting("RoomsAction")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> JoinRoom(
        Guid id,
        [FromBody] RoomJoinRequestDto request,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();

        var result = await _roomService.JoinRoomAsync(
            currentUserId.Value,
            id,
            request.Password,
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>
    /// Approve a pending member (Owner/Moderator only).
    /// Changes member status from Pending to Approved and updates counters.
    /// Rate limit: 60 requests per minute per user.
    /// </summary>
    /// <param name="id">Room ID</param>
    /// <param name="userId">User ID to approve</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success or error</returns>
    /// <response code="204">Member approved successfully</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Forbidden (not owner/moderator)</response>
    /// <response code="404">Room or member not found</response>
    /// <response code="409">Member not pending or room at capacity</response>
    /// <response code="429">Rate limit exceeded (60 per minute)</response>
    [HttpPost("{id:guid}/approve/{userId:guid}")]
    [EnableRateLimiting("RoomsAction")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> ApproveMember(
        Guid id,
        Guid userId,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();

        var result = await _roomService.ApproveMemberAsync(
            currentUserId.Value,
            id,
            userId,
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>
    /// Leave a room.
    /// Owner cannot leave (returns Forbidden).
    /// Approved members: updates counters; Pending members: just removed.
    /// Rate limit: 60 requests per minute per user.
    /// </summary>
    /// <param name="id">Room ID to leave</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success or error</returns>
    /// <response code="204">Left room successfully</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Forbidden (owner cannot leave)</response>
    /// <response code="404">Room or membership not found</response>
    /// <response code="429">Rate limit exceeded (60 per minute)</response>
    [HttpPost("{id:guid}/leave")]
    [EnableRateLimiting("RoomsAction")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> LeaveRoom(
        Guid id,
        CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();

        var result = await _roomService.LeaveRoomAsync(
            currentUserId.Value,
            id,
            ct);

        return this.ToActionResult(result);
    }

    /// <summary>
    /// Kick or ban a member (Owner/Moderator only).
    /// Approved members: status changed to Banned/Rejected, counters updated.
    /// Pending members: status changed to Banned/Rejected, no counter updates.
    /// Rate limit: 60 requests per minute per user.
    /// </summary>
    /// <param name="id">Room ID</param>
    /// <param name="userId">User ID to kick/ban</param>
    /// <param name="ban">True to ban (persistent), false to kick (can rejoin)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success or error</returns>
    /// <response code="204">Member kicked/banned successfully</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Forbidden (not owner/moderator or trying to kick owner)</response>
    /// <response code="404">Room or member not found</response>
    /// <response code="429">Rate limit exceeded (60 per minute)</response>
    [HttpPost("{id:guid}/kickban/{userId:guid}")]
    [EnableRateLimiting("RoomsAction")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> KickOrBanMember(
        Guid id,
        Guid userId,
        [FromQuery] bool ban = false,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();

        var result = await _roomService.KickOrBanAsync(
            currentUserId.Value,
            id,
            userId,
            ban,
            ct);

        return this.ToActionResult(result);
    }
}
