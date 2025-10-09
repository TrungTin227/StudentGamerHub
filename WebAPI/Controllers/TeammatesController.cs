using BusinessObjects.Common;
using DTOs.Teammates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

/// <summary>
/// Teammates search controller
/// Provides endpoints for finding potential teammates based on games, university, skill level, and online status.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public sealed class TeammatesController : ControllerBase
{
    private readonly ITeammateFinderService _teammateFinder;

    public TeammatesController(ITeammateFinderService teammateFinder)
    {
        _teammateFinder = teammateFinder ?? throw new ArgumentNullException(nameof(teammateFinder));
    }

    /// <summary>
    /// Search for potential teammates
    /// </summary>
    /// <param name="gameId">Optional: filter by specific game</param>
    /// <param name="university">Optional: filter by university</param>
    /// <param name="skill">Optional: filter by skill level (Casual=0, Intermediate=1, Competitive=2)</param>
    /// <param name="onlineOnly">If true, only return currently online users</param>
    /// <param name="cursor">Pagination cursor from previous response</param>
    /// <param name="size">Number of results per page (default: 20, max: 100)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Cursor-paginated list of potential teammates sorted by online status, points, shared games, and user ID (all DESC)</returns>
    /// <response code="200">Success - returns paginated teammates</response>
    /// <response code="400">Bad Request - invalid parameters</response>
    /// <response code="401">Unauthorized - authentication required</response>
    /// <response code="429">Too Many Requests - rate limit exceeded</response>
    /// <response code="500">Internal Server Error</response>
    [HttpGet]
    [EnableRateLimiting("TeammatesRead")]
    [ProducesResponseType(typeof(CursorPageResult<TeammateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Search(
        [FromQuery] Guid? gameId,
        [FromQuery] string? university,
        [FromQuery] GameSkillLevel? skill,
        [FromQuery] bool onlineOnly = false,
        [FromQuery] string? cursor = null,
        [FromQuery] int size = 20,
        CancellationToken ct = default)
    {
        // Get current user ID from claims
        var currentUserId = User.GetUserId();
        if (currentUserId is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "User ID claim not found. Please login again.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        // Build cursor request
        var cursorRequest = new CursorRequest(
            Cursor: cursor,
            Direction: CursorDirection.Next,
            Size: size
        );

        // Call service
        var result = await _teammateFinder.SearchAsync(
            currentUserId.Value,
            gameId,
            university,
            skill,
            onlineOnly,
            cursorRequest,
            ct);

        // Return action result
        return this.ToActionResult(result);
    }
}
