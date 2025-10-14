using DTOs.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Repositories.WorkSeeds.Extensions;
using Repositories.WorkSeeds.Interfaces;
using Services.Common.Abstractions;
using Services.Interfaces;
using System.Text.Json;
using WebApi.Common;

namespace WebAPI.Controllers;

/// <summary>
/// Controller for reporting abuse in chat.
/// </summary>
[ApiController]
[Authorize]
[Route("api/abuse")]
public sealed class AbuseController : ControllerBase
{
    private readonly IBugReportService _bugReportService;
    private readonly ICurrentUserService _currentUser;
    private readonly IGenericUnitOfWork _uow;

    public AbuseController(
        IBugReportService bugReportService,
        ICurrentUserService currentUser,
        IGenericUnitOfWork uow)
    {
        _bugReportService = bugReportService ?? throw new ArgumentNullException(nameof(bugReportService));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    /// <summary>
    /// Report abusive content in chat.
    /// </summary>
    [HttpPost("report")]
    [EnableRateLimiting("BugsWrite")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> ReportAbuse(
        [FromBody] AbuseReportRequest request,
        CancellationToken ct = default)
    {
        var reporterUserId = _currentUser.GetUserIdOrThrow();

        // Validate request
        if (string.IsNullOrWhiteSpace(request.Channel))
        {
            return this.ToActionResult(Result<Guid>.Failure(
                new Error(Error.Codes.Validation, "Channel is required.")));
        }

        if (string.IsNullOrWhiteSpace(request.MessageId))
        {
            return this.ToActionResult(Result<Guid>.Failure(
                new Error(Error.Codes.Validation, "Message ID is required.")));
        }

        // Build comprehensive description with metadata
        var metadata = new
        {
            channel = request.Channel,
            messageId = request.MessageId,
            offenderUserId = request.OffenderUserId?.ToString(),
            reporterNotes = request.Text,
            snapshot = request.Snapshot is not null
                ? new
                {
                    fromUserId = request.Snapshot.FromUserId.ToString(),
                    toUserId = request.Snapshot.ToUserId?.ToString(),
                    roomId = request.Snapshot.RoomId?.ToString(),
                    message = request.Snapshot.Message
                }
                : null
        };

        var description = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var result = await _uow.ExecuteTransactionAsync<Guid>(async innerCt =>
        {
            var createRequest = new DTOs.Bugs.BugReportCreateRequest
            {
                Category = "Abuse",
                Description = description,
                ImageUrl = null
            };

            var bugReportResult = await _bugReportService.CreateAsync(
                reporterUserId,
                createRequest,
                innerCt).ConfigureAwait(false);

            return bugReportResult.IsSuccess
                ? Result<Guid>.Success(bugReportResult.Value!.Id)
                : Result<Guid>.Failure(bugReportResult.Error);

        }, ct: ct).ConfigureAwait(false);

        return this.ToActionResult(result, id => new { id });
    }
}
