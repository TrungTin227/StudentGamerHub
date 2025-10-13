using DTOs.Common;
using DTOs.Registrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/events/{eventId:guid}/registrations")]
public sealed class RegistrationsController : ControllerBase
{
    private const int MaxPageSize = 100;

    private readonly IRegistrationService _registrationService;
    private readonly IRegistrationReadService _registrationReadService;

    public RegistrationsController(
        IRegistrationService registrationService,
        IRegistrationReadService registrationReadService)
    {
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
        _registrationReadService = registrationReadService ?? throw new ArgumentNullException(nameof(registrationReadService));
    }

    [HttpPost]
    [EnableRateLimiting("RegistrationsWrite")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Register(Guid eventId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return this.ToActionResult(Result<Guid>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _registrationService.RegisterAsync(currentUserId.Value, eventId, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return this.ToActionResult(result);
        }

        return this.ToCreatedAtAction(
            result,
            nameof(GetByEvent),
            new { eventId, page = 1, pageSize = PaginationOptions.DefaultPageSize },
            id => new { paymentIntentId = id });
    }

    [HttpGet]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(PagedResponse<RegistrationListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetByEvent(
        Guid eventId,
        [FromQuery(Name = "status")] IEnumerable<EventRegistrationStatus>? statuses,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationOptions.DefaultPageSize,
        CancellationToken ct = default)
    {
        var organizerId = User.GetUserId();
        if (!organizerId.HasValue)
        {
            return this.ToActionResult(Result<PagedResponse<RegistrationListItemDto>>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize <= 0
            ? PaginationOptions.DefaultPageSize
            : Math.Clamp(pageSize, 1, Math.Min(PaginationOptions.MaxPageSize, MaxPageSize));

        var result = await _registrationReadService
            .ListForEventAsync(organizerId.Value, eventId, statuses, normalizedPage, normalizedPageSize, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    [HttpGet("~/api/me/registrations")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(PagedResponse<MyRegistrationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetMine(
        [FromQuery(Name = "status")] IEnumerable<EventRegistrationStatus>? statuses,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationOptions.DefaultPageSize,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return this.ToActionResult(Result<PagedResponse<MyRegistrationDto>>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize <= 0
            ? PaginationOptions.DefaultPageSize
            : Math.Clamp(pageSize, 1, Math.Min(PaginationOptions.MaxPageSize, MaxPageSize));

        var result = await _registrationReadService
            .ListMineAsync(currentUserId.Value, statuses, normalizedPage, normalizedPageSize, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }
}
