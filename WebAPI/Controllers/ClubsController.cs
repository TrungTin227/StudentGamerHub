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
    [HttpGet("communities/{communityId:guid}")]
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
    /// The creator is NOT automatically added as a member (Room-level membership only).
    /// Initial MembersCount = 0.
    /// Rate limit: 10 requests per day per user.
    /// </summary>
    /// <param name="request">Club creation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>New club ID</returns>
    /// <response code="201">Club created successfully</response>
    /// <response code="400">Invalid request (validation error)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="429">Rate limit exceeded (10 per day)</response>
    [HttpPost("communities/{communityId:guid}")]
    [EnableRateLimiting("ClubsCreate")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> CreateClub(
        Guid communityId,
        [FromBody] ClubCreateRequestDto request,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _clubService.CreateClubAsync(
            userId.Value,
            communityId,
            request.Name,
            request.Description,
            request.IsPublic,
            ct);

        return this.ToActionResult(result, successStatus: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Get club by ID.
    /// </summary>
    /// <param name="id">Club ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Club brief information</returns>
    /// <response code="200">Club retrieved successfully</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="404">Club not found</response>
    [HttpGet("communities/{communityId:guid}/{id:guid}")]
    [EnableRateLimiting("ClubsRead")]
    [ProducesResponseType(typeof(ClubBriefDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetClubById(
        Guid communityId,
        Guid id,
        CancellationToken ct = default)
    {
        var result = await _clubService.GetByIdAsync(id, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }
}
