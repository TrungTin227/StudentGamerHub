using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

/// <summary>
/// Dashboard controller for aggregating today's data
/// - Points, Quests, Events, Activity
/// - VN timezone (Asia/Ho_Chi_Minh UTC+7)
/// - Optimized with batch Redis operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
    }

    /// <summary>
    /// GET /dashboard/today - Get today's dashboard data (VN timezone)
    /// Includes: User points, daily quests status, events today, and activity metrics
    /// </summary>
    /// <remarks>
    /// Sample response:
    /// ```json
    /// {
    ///   "points": 125,
    ///   "quests": {
    ///     "points": 15,
    ///     "quests": [
    ///       {
    ///         "code": "CHECK_IN_DAILY",
    ///         "title": "Check-in Daily",
    ///         "reward": 5,
    ///         "done": true
    ///       },
    ///       {
    ///         "code": "JOIN_ANY_ROOM",
    ///         "title": "Join Any Room",
    ///         "reward": 5,
    ///         "done": false
    ///       }
    ///     ]
    ///   },
    ///   "eventsToday": [
    ///     {
    ///       "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///       "title": "Mini Tournament",
    ///       "startsAt": "2025-01-15T14:00:00+07:00",
    ///       "endsAt": "2025-01-15T18:00:00+07:00",
    ///       "location": "Online",
    ///       "mode": "Online"
    ///     }
    ///   ],
    ///   "activity": {
    ///     "onlineFriends": 8,
    ///     "questsDoneLast60m": 34
    ///   }
    /// }
    /// ```
    /// </remarks>
    /// <response code="200">Dashboard data retrieved successfully</response>
    /// <response code="401">Unauthorized - Valid JWT token required</response>
    /// <response code="429">Too many requests - Rate limit exceeded (120 requests per minute)</response>
    /// <response code="500">Internal server error - Check logs for details</response>
    [HttpGet("today")]
    [EnableRateLimiting("DashboardRead")]
    [ProducesResponseType(typeof(DashboardTodayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetToday(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result<DashboardTodayDto>.Failure(
                new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _dashboard.GetTodayAsync(userId.Value, ct);
        return this.ToActionResult(result, v => v);
    }
}
