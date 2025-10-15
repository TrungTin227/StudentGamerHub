using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

/// <summary>
/// Community search controller.
/// Provides cursor-based pagination for community discovery.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CommunitiesController : ControllerBase
{
    private readonly ICommunitySearchService _communitySearch;
    private readonly ICommunityService _communityService;
    private readonly ICommunityDiscoveryService _communityDiscovery;

    public CommunitiesController(
        ICommunitySearchService communitySearch,
        ICommunityService communityService,
        ICommunityDiscoveryService communityDiscovery)
    {
        _communitySearch = communitySearch ?? throw new ArgumentNullException(nameof(communitySearch));
        _communityService = communityService ?? throw new ArgumentNullException(nameof(communityService));
        _communityDiscovery = communityDiscovery ?? throw new ArgumentNullException(nameof(communityDiscovery));
    }

    /// <summary>
    /// Create a new community.
    /// </summary>
    /// <param name="request">Creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Identifier of the created community.</returns>
    /// <response code="201">Community created successfully.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="429">Write rate limit exceeded.</response>
    [HttpPost]
    [EnableRateLimiting("CommunitiesWrite")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> CreateCommunity([FromBody] CommunityCreateRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _communityService.CreateAsync(userId.Value, request, ct);
        return this.ToActionResult(result, value => new { communityId = value }, StatusCodes.Status201Created);
    }

    /// <summary>
    /// Search communities with filtering and cursor-based pagination.
    /// Filters: school (case-insensitive partial match), game, visibility, member count range.
    /// Sorted by: MembersCount DESC, Id DESC (stable).
    /// The cursor uses Id as the key for pagination, but the primary sort is by MembersCount.
    /// Rate limit: 120 requests per minute per user.
    /// </summary>
    /// <param name="school">Filter by school name (partial match, case-insensitive)</param>
    /// <param name="gameId">Filter communities that include this game</param>
    /// <param name="isPublic">Filter by public/private status (null = all)</param>
    /// <param name="membersFrom">Minimum members count (inclusive)</param>
    /// <param name="membersTo">Maximum members count (inclusive)</param>
    /// <param name="cursor">Cursor token for pagination (null = first page)</param>
    /// <param name="size">Page size (default: 20, clamped between 1-200)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of community briefs</returns>
    /// <response code="200">Communities retrieved successfully</response>
    /// <response code="400">Invalid request (validation error)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="429">Rate limit exceeded (120 per minute)</response>
    [HttpGet]
    [EnableRateLimiting("CommunitiesRead")]
    [ProducesResponseType(typeof(CursorPageResult<CommunityBriefDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> SearchCommunities(
        [FromQuery] string? school = null,
        [FromQuery] Guid? gameId = null,
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

        var result = await _communitySearch.SearchAsync(
            school,
            gameId,
            isPublic,
            membersFrom,
            membersTo,
            cursorRequest,
            ct);

        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Retrieve community details by identifier.
    /// </summary>
    /// <param name="id">Community identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detailed community information.</returns>
    /// <response code="200">Community found.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Community not found.</response>
    [HttpGet("{id:guid}")]
    [EnableRateLimiting("CommunitiesRead")]
    [ProducesResponseType(typeof(CommunityDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _communityService.GetByIdAsync(id, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Update a community's metadata.
    /// </summary>
    /// <param name="id">Community identifier.</param>
    /// <param name="request">Update payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">Update succeeded.</response>
    /// <response code="400">Validation failure.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Community not found.</response>
    /// <response code="429">Write rate limit exceeded.</response>
    [HttpPatch("{id:guid}")]
    [EnableRateLimiting("CommunitiesWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Update(Guid id, [FromBody] CommunityUpdateRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _communityService.UpdateAsync(userId.Value, id, request, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Archive a community (soft delete).
    /// </summary>
    /// <param name="id">Community identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">Archive succeeded.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Archive forbidden when approved rooms exist.</response>
    /// <response code="404">Community not found.</response>
    /// <response code="429">Write rate limit exceeded.</response>
    [HttpDelete("{id:guid}")]
    [EnableRateLimiting("CommunitiesWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Archive(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _communityService.ArchiveAsync(userId.Value, id, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Discover popular communities with filtering and stable cursor-based pagination.
    /// Filters by IsPublic=true (default), optional school (exact match), and game.
    /// Sorted by popularity:
    /// 1. MembersCount DESC
    /// 2. RecentActivity48h DESC (room joins in last 48 hours)
    /// 3. CreatedAtUtc DESC
    /// 4. Id ASC (for stable tie-breaking)
    /// </summary>
    /// <param name="school">Filter by school name (case-insensitive exact match)</param>
    /// <param name="gameId">Filter communities that include this game</param>
    /// <param name="cursor">Cursor token for pagination (null = first page)</param>
    /// <param name="size">Page size (default: 20, clamped 1-100)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Discovery response with popular communities and next cursor</returns>
    /// <response code="200">Communities discovered successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="429">Rate limit exceeded</response>
    [HttpGet("discover")]
    [AllowAnonymous] // Public endpoint for discovery
    [EnableRateLimiting("CommunitiesRead")]
    [ProducesResponseType(typeof(DiscoverResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Discover(
        [FromQuery] string? school = null,
        [FromQuery] Guid? gameId = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int? size = null,
        CancellationToken ct = default)
    {
        var result = await _communityDiscovery.DiscoverAsync(school, gameId, cursor, size, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }
}
