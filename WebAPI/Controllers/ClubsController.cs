using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

/// <summary>
/// Club management within communities.
/// Provides search, creation, and retrieval of clubs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Clubs")]
[Authorize]
public sealed class ClubsController : ControllerBase
{
    private readonly IClubService _clubService;

    public ClubsController(IClubService clubService)
    {
        _clubService = clubService ?? throw new ArgumentNullException(nameof(clubService));
    }

    /// <summary>
    /// Search clubs within a community with filtering and cursor-based pagination.
    /// Filters: name (case-insensitive partial match), visibility, member count range.
    /// Sorted by: MembersCount DESC, Id DESC (stable).
    /// Rate limit: 120 requests per minute per user.
    /// </summary>
    /// <param name="communityId">Community ID</param>
    /// <param name="name">Filter by club name (partial match, case-insensitive)</param>
    /// <param name="isPublic">Filter by public/private status (null = all)</param>
    /// <param name="membersFrom">Minimum members count (inclusive)</param>
    /// <param name="membersTo">Maximum members count (inclusive)</param>
    /// <param name="cursor">Cursor token for pagination (null = first page)</param>
    /// <param name="size">Page size (default: 20, clamped between 1-200)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of club briefs</returns>
    /// <response code="200">Clubs retrieved successfully</response>
    /// <response code="400">Invalid request (validation error)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="429">Rate limit exceeded (120 per minute)</response>
    [HttpGet("~/api/communities/{communityId:guid}/clubs")]
    [EnableRateLimiting("ClubsRead")]
    [ProducesResponseType(typeof(CursorPageResult<ClubBriefDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> SearchClubs(
        Guid communityId,
        [FromQuery] string? name = null,
        [FromQuery] bool? isPublic = null,
        [FromQuery] int? membersFrom = null,
        [FromQuery] int? membersTo = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int size = 20,
        CancellationToken ct = default)
    {
        // Clamp size to valid range
        size = Math.Clamp(size, 1, 200);

        var cursorRequest = new CursorRequest(
            Cursor: cursor,
            Direction: CursorDirection.Next,
            Size: size,
            Sort: "Id", // Stable sort key (for cursor pagination)
            Desc: true  // DESC order
        );

        var result = await _clubService.SearchAsync(
            communityId,
            name,
            isPublic,
            membersFrom,
            membersTo,
            cursorRequest,
            ct);

        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Create a new club within a community.
    /// The authenticated user must already belong to the parent community and becomes the club owner member.
    /// Rate limit: 10 requests per day per user.
    /// </summary>
    /// <param name="request">Club creation request (includes CommunityId)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>New club ID</returns>
    /// <response code="201">Club created successfully</response>
    /// <response code="400">Invalid request (validation error)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="429">Rate limit exceeded (10 per day)</response>
    [HttpPost]
    [EnableRateLimiting("ClubsWrite")]
    [ProducesResponseType(typeof(ClubDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> CreateClub(
        [FromBody] ClubCreateRequestDto request,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _clubService.CreateClubAsync(request, userId.Value, ct);
        return this.ToCreatedAtAction(result, nameof(GetClubById), result.IsSuccess ? new { id = result.Value!.Id } : null);
    }

    [HttpPut("{id:guid}")]
    [EnableRateLimiting("ClubsWrite")]
    [ProducesResponseType(typeof(ClubDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateClub(Guid id, [FromBody] ClubUpdateRequestDto request, CancellationToken ct = default)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
            return Unauthorized();

        var result = await _clubService.UpdateClubAsync(id, request, actorId.Value, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Join a club. Requires community membership and is idempotent if already joined.
    /// </summary>
    [HttpPost("{clubId:guid}/join")]
    [EnableRateLimiting("ClubsWrite")]
    [ProducesResponseType(typeof(ClubDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> JoinClub(Guid clubId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _clubService.JoinClubAsync(clubId, userId.Value, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    [HttpDelete("{id:guid}")]
    [EnableRateLimiting("ClubsWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteClub(Guid id, CancellationToken ct = default)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
            return Unauthorized();

        var result = await _clubService.DeleteClubAsync(id, actorId.Value, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Remove a club member (owner only).
    /// </summary>
    [HttpDelete("{clubId:guid}/members/{userId:guid}")]
    [EnableRateLimiting("ClubsWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveMember(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
            return Unauthorized();

        var result = await _clubService.KickClubMemberAsync(clubId, userId, actorId.Value, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Get club by ID.
    /// </summary>
    /// <param name="id">Club ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Club detail information</returns>
    /// <response code="200">Club retrieved successfully</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="404">Club not found</response>
    [HttpGet("{id:guid}")]
    [EnableRateLimiting("ClubsRead")]
    [ProducesResponseType(typeof(ClubDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetClubById(
        Guid id,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        var result = await _clubService.GetByIdAsync(id, currentUserId, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }
}
