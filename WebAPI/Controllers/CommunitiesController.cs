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
    private readonly ICommunityReadService _communityRead;

    public CommunitiesController(
        ICommunitySearchService communitySearch,
        ICommunityService communityService,
        ICommunityReadService communityRead)
    {
        _communitySearch = communitySearch ?? throw new ArgumentNullException(nameof(communitySearch));
        _communityService = communityService ?? throw new ArgumentNullException(nameof(communityService));
        _communityRead = communityRead ?? throw new ArgumentNullException(nameof(communityRead));
    }

    /// <summary>
    /// Create a new community.
    /// The authenticated user becomes the owner member in the same transaction.
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
    [ProducesResponseType(typeof(CommunityDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> CreateCommunity([FromBody] CommunityCreateRequestDto request, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _communityService.CreateCommunityAsync(request, userId.Value, ct);
        return this.ToCreatedAtAction(result, nameof(GetById), result.IsSuccess ? new { id = result.Value!.Id } : null);
    }

    /// <summary>
    /// Update editable community fields. Owner only.
    /// </summary>
    [HttpPut("{id:guid}")]
    [EnableRateLimiting("CommunitiesWrite")]
    [ProducesResponseType(typeof(CommunityDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> UpdateCommunity(Guid id, [FromBody] CommunityUpdateRequestDto request, CancellationToken ct = default)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
            return Unauthorized();

        var result = await _communityService.UpdateCommunityAsync(id, request, actorId.Value, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Join a community. Idempotent if the caller is already a member.
    /// </summary>
    [HttpPost("{communityId:guid}/join")]
    [EnableRateLimiting("CommunitiesWrite")]
    [ProducesResponseType(typeof(CommunityDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> JoinCommunity(Guid communityId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _communityService.JoinCommunityAsync(communityId, userId.Value, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Remove a member from the community (owner only).
    /// </summary>
    [HttpDelete("{communityId:guid}/members/{userId:guid}")]
    [EnableRateLimiting("CommunitiesWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveCommunityMember(Guid communityId, Guid userId, CancellationToken ct = default)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
            return Unauthorized();

        var result = await _communityService.KickCommunityMemberAsync(communityId, userId, actorId.Value, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Soft delete a community. Owner only.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [EnableRateLimiting("CommunitiesWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteCommunity(Guid id, CancellationToken ct = default)
    {
        var actorId = User.GetUserId();
        if (actorId is null)
            return Unauthorized();

        var result = await _communityService.DeleteCommunityAsync(id, actorId.Value, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Search communities with filtering and offset pagination.
    /// Filters: school (case-insensitive partial match), game, visibility, member count range.
    /// Sorted by: MembersCount DESC, Id DESC (stable).
    /// Rate limit: 120 requests per minute per user.
    /// </summary>
    /// <param name="school">Filter by school name (partial match, case-insensitive)</param>
    /// <param name="gameId">Filter communities that include this game</param>
    /// <param name="isPublic">Filter by public/private status (null = all)</param>
    /// <param name="membersFrom">Minimum members count (inclusive)</param>
    /// <param name="membersTo">Maximum members count (inclusive)</param>
    /// <param name="page">Page number (1-based, default: 1)</param>
    /// <param name="size">Page size (default: 20, clamped between 1-200)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of community briefs</returns>
    /// <response code="200">Communities retrieved successfully</response>
    /// <response code="400">Invalid request (validation error)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="429">Rate limit exceeded (120 per minute)</response>
    [HttpGet]
    [EnableRateLimiting("CommunitiesRead")]
    [ProducesResponseType(typeof(PagedResult<CommunityBriefDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> SearchCommunities(
        [FromQuery] string? school = null,
        [FromQuery] Guid? gameId = null,
        [FromQuery] bool? isPublic = null,
        [FromQuery] int? membersFrom = null,
        [FromQuery] int? membersTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken ct = default)
    {
        size = Math.Clamp(size, 1, 200);

        var pageRequest = new PageRequest
        {
            Page = page,
            Size = size,
            Sort = "MembersCount",
            Desc = true
        };

        var result = await _communitySearch.SearchAsync(
            school,
            gameId,
            isPublic,
            membersFrom,
            membersTo,
            pageRequest,
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
        var currentUserId = User.GetUserId();
        var result = await _communityService.GetByIdAsync(id, currentUserId, ct);
        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Discover popular communities with optional free-text search.
    /// Supports offset pagination and ordering by trending (default) or newest.
    /// </summary>
    /// <param name="query">Optional search term matched against name and description.</param>
    /// <param name="offset">Zero-based offset (default 0).</param>
    /// <param name="limit">Page size (default 20, clamped 1-50).</param>
    /// <param name="orderBy">Either "trending" (default) or "newest".</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated discovery result.</returns>
    /// <response code="200">Communities discovered successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="429">Rate limit exceeded</response>
    [HttpGet("discover")]
    [AllowAnonymous] // Public endpoint for discovery
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(PagedResult<CommunityDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Discover(
        [FromQuery] string? query = null,
        [FromQuery] int? offset = null,
        [FromQuery] int? limit = null,
        [FromQuery] string? orderBy = null,
        CancellationToken ct = default)
    {
        var sanitizedOffset = Math.Max(offset ?? 0, 0);
        var sanitizedLimit = Math.Clamp(limit ?? 20, 1, 50);
        var paging = new OffsetPaging(sanitizedOffset, sanitizedLimit);
        var currentUserId = User.GetUserId();

        var result = await _communityRead.SearchDiscoverAsync(
            currentUserId,
            query,
            orderBy ?? "trending",
            paging,
            ct);

        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// List members of a community with optional filtering and offset pagination.
    /// </summary>
    [HttpGet("{communityId:guid}/members")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(OffsetPage<CommunityMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ListMembers(
        Guid communityId,
        [FromQuery] MemberRole? role = null,
        [FromQuery(Name = "q")] string? query = null,
        [FromQuery] string? sort = null,
        [FromQuery] int? offset = null,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        var filter = new MemberListFilter
        {
            Role = role,
            Query = query,
            Sort = sort ?? MemberListSort.JoinedAtDesc
        };

        var sanitizedOffset = Math.Max(offset ?? 0, 0);
        var sanitizedLimit = Math.Clamp(limit ?? 20, 1, 50);
        var paging = new OffsetPaging(sanitizedOffset, sanitizedLimit, filter.Sort, false);
        var currentUserId = User.GetUserId();

        var result = await _communityRead
            .ListMembersAsync(communityId, filter, paging, currentUserId, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Get the most recent members who joined the community.
    /// </summary>
    [HttpGet("{communityId:guid}/members/recent")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(IReadOnlyList<CommunityMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ListRecentMembers(
        Guid communityId,
        [FromQuery] int? limit = null,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        var sanitizedLimit = limit ?? 20;

        var result = await _communityRead
            .ListRecentMembersAsync(communityId, sanitizedLimit, currentUserId, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, successStatus: StatusCodes.Status200OK);
    }
}
