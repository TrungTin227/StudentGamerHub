using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

/// <summary>
/// Community search controller.
/// Provides cursor-based pagination for community discovery.
/// </summary>
[ApiController]
[Route("communities")]
[Produces("application/json")]
[Authorize]
public sealed class CommunitiesController : ControllerBase
{
    private readonly ICommunitySearchService _communitySearch;

    public CommunitiesController(ICommunitySearchService communitySearch)
    {
        _communitySearch = communitySearch ?? throw new ArgumentNullException(nameof(communitySearch));
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
}
