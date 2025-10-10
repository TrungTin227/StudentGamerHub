using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Application.Quests;

namespace WebAPI.Controllers;

/// <summary>
/// Daily Quests MVP (Asia/Ho_Chi_Minh timezone).
/// User có th?:
/// - Xem danh sách quest hôm nay (GET /quests/today)
/// - Check-in manual (POST /quests/check-in)
/// - Mark join room (POST /quests/join-room/{roomId})
/// - Mark attend event (POST /quests/attend-event/{eventId})
/// Các quest khác ???c trigger t? ??ng t? các service t??ng ?ng.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class QuestsController : ControllerBase
{
    private readonly IQuestService _quests;

    public QuestsController(IQuestService quests)
    {
        _quests = quests;
    }

    /// <summary>
    /// GET /quests/today — L?y danh sách quest hôm nay + tr?ng thái Done + Points hi?n t?i.
    /// </summary>
    [HttpGet("today")]
    [ProducesResponseType(typeof(QuestTodayDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetToday(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result<QuestTodayDto>.Failure(
                new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _quests.GetTodayAsync(userId.Value, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// POST /quests/check-in — Manual check-in daily quest (+5 points).
    /// Idempotent: ch? c?ng 1 l?n/ngày (Asia/Ho_Chi_Minh timezone).
    /// </summary>
    [HttpPost("check-in")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> CheckIn(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result.Failure(
                new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _quests.CompleteCheckInAsync(userId.Value, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// POST /quests/join-room/{roomId} — Mark join room quest (+5 points).
    /// Idempotent: ch? c?ng 1 l?n/ngày.
    /// </summary>
    [HttpPost("join-room/{roomId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> MarkJoinRoom(Guid roomId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result.Failure(
                new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _quests.MarkJoinRoomAsync(userId.Value, roomId, ct);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// POST /quests/attend-event/{eventId} — Mark attend event quest (+20 points).
    /// Idempotent: ch? c?ng 1 l?n/ngày.
    /// </summary>
    [HttpPost("attend-event/{eventId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> MarkAttendEvent(Guid eventId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result.Failure(
                new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _quests.MarkAttendEventAsync(userId.Value, eventId, ct);
        return this.ToActionResult(result);
    }
}
