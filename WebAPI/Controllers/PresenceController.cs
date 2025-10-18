using BusinessObjects.Common.Results;
using DTOs.Presence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Presence;
using WebApi.Common;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/presence")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public sealed class PresenceController : ControllerBase
{
    private readonly IPresenceReader _reader;

    public PresenceController(IPresenceReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    [HttpGet("online")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("PresenceAdminReads")]
    [ProducesResponseType(typeof(PresenceOnlineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetOnline([FromQuery] PresenceOnlineQuery? query, CancellationToken ct)
    {
        var result = await _reader
            .GetOnlineAsync(query ?? new PresenceOnlineQuery(), ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    [HttpPost("batch")]
    [EnableRateLimiting("PresenceBatchReads")]
    [ProducesResponseType(typeof(PresenceBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetBatch([FromBody] PresenceBatchRequest? request, CancellationToken ct)
    {
        request ??= new PresenceBatchRequest(Array.Empty<Guid>());
        var result = await _reader
            .GetBatchAsync(request.UserIds, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, v => new PresenceBatchResponse(v), StatusCodes.Status200OK);
    }

    [HttpPost("summary")]
    [EnableRateLimiting("PresenceAdminReads")]
    [ProducesResponseType(typeof(PresenceSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetSummary([FromBody] PresenceSummaryRequest? request, CancellationToken ct)
    {
        request ??= new PresenceSummaryRequest(null);
        if (request.UserIds is null && !User.IsInRole("Admin"))
        {
            var failure = Result<PresenceSummaryResponse>.Failure(
                new Error(Error.Codes.Forbidden, "Global presence summary requires admin role."));
            return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
        }

        var result = await _reader
            .GetSummaryAsync(request, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }
}
