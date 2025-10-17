using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class EventsController : ControllerBase
{
    private const int MaxPageSize = 100;

    private readonly IEventService _eventService;
    private readonly IEventReadService _eventReadService;

    public EventsController(IEventService eventService, IEventReadService eventReadService)
    {
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
        _eventReadService = eventReadService ?? throw new ArgumentNullException(nameof(eventReadService));
    }

    [HttpPost]
    [EnableRateLimiting("EventsWrite")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Create([FromBody] EventCreateRequestDto request, CancellationToken ct)
    {
        if (request is null)
        {
            return this.ToActionResult(Result<Guid>.Failure(
                new Error(Error.Codes.Validation, "Request body is required.")));
        }

        var organizerId = User.GetUserId();
        if (!organizerId.HasValue)
        {
            return this.ToActionResult(Result<Guid>.Failure(
                new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _eventService.CreateAsync(organizerId.Value, request, ct)
                                        .ConfigureAwait(false);

        if (!result.IsSuccess)
            return this.ToActionResult(result);

        var id = result.Value;

        // 201 Created + Location: /api/events/{eventId}
        // body tuỳ bạn, ở đây trả về eventId tối giản.
        return CreatedAtRoute(
            routeName: "GetEventById",
            routeValues: new { eventId = id },
            value: new { eventId = id });
    }


    [HttpPost("{eventId:guid}/open")]
    [EnableRateLimiting("EventsWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Open(Guid eventId, CancellationToken ct)
    {
        var organizerId = User.GetUserId();
        if (!organizerId.HasValue)
        {
            return this.ToActionResult(Result.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _eventService.OpenAsync(organizerId.Value, eventId, ct).ConfigureAwait(false);
        if (!result.IsSuccess && result.Error.Code == Error.Codes.Forbidden &&
            TryParseTopUpNeeded(result.Error.Message, out var needed))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { topUpNeededCents = needed });
        }

        return this.ToActionResult(result);
    }

    [HttpPost("{eventId:guid}/cancel")]
    [EnableRateLimiting("EventsWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Cancel(Guid eventId, CancellationToken ct)
    {
        var organizerId = User.GetUserId();
        if (!organizerId.HasValue)
        {
            return this.ToActionResult(Result.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _eventService.CancelAsync(organizerId.Value, eventId, ct).ConfigureAwait(false);
        return this.ToActionResult(result);
    }

    [HttpGet("{eventId:guid}", Name = "GetEventById")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(EventDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetById(Guid eventId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return this.ToActionResult(Result<EventDetailDto>.Failure(
                new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _eventReadService
            .GetByIdAsync(currentUserId.Value, eventId, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }


    [HttpGet]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(PagedResponse<EventDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Search(
        [FromQuery(Name = "status")] IEnumerable<EventStatus>? statuses,
        [FromQuery] Guid? communityId,
        [FromQuery] Guid? organizerId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationOptions.DefaultPageSize,
        CancellationToken ct = default)
    {
        var currentUserId = User.GetUserId();
        if (!currentUserId.HasValue)
        {
            return this.ToActionResult(Result<PagedResponse<EventDetailDto>>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        if (!TryParseSort(sort, out var sortAsc, out var sortError))
        {
            var failure = Result<PagedResponse<EventDetailDto>>.Failure(new Error(Error.Codes.Validation, sortError!));
            return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
        }

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize <= 0
            ? PaginationOptions.DefaultPageSize
            : Math.Clamp(pageSize, 1, Math.Min(PaginationOptions.MaxPageSize, MaxPageSize));

        var result = await _eventReadService.SearchAsync(
            currentUserId.Value,
            statuses,
            communityId,
            organizerId,
            from,
            to,
            search,
            sortAsc,
            normalizedPage,
            normalizedPageSize,
            ct).ConfigureAwait(false);

        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    [HttpGet("~/api/organizer/events")]
    [EnableRateLimiting("ReadsHeavy")]
    [ProducesResponseType(typeof(PagedResponse<EventDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> ListMyEvents(
        [FromQuery(Name = "status")] IEnumerable<EventStatus>? statuses,
        [FromQuery] Guid? communityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PaginationOptions.DefaultPageSize,
        CancellationToken ct = default)
    {
        var organizerId = User.GetUserId();
        if (!organizerId.HasValue)
        {
            return this.ToActionResult(Result<PagedResponse<EventDetailDto>>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        if (!TryParseSort(sort, out var sortAsc, out var sortError))
        {
            var failure = Result<PagedResponse<EventDetailDto>>.Failure(new Error(Error.Codes.Validation, sortError!));
            return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
        }

        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize <= 0
            ? PaginationOptions.DefaultPageSize
            : Math.Clamp(pageSize, 1, Math.Min(PaginationOptions.MaxPageSize, MaxPageSize));

        var result = await _eventReadService.SearchMyOrganizedAsync(
            organizerId.Value,
            statuses,
            communityId,
            from,
            to,
            search,
            sortAsc,
            normalizedPage,
            normalizedPageSize,
            ct).ConfigureAwait(false);

        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    private static bool TryParseTopUpNeeded(string? message, out long needed)
    {
        needed = 0;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        const string token = "topUpNeededCents=";
        var index = message.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var start = index + token.Length;
        var span = message.AsSpan(start);
        int endIndex = -1;
        for (int i = 0; i < span.Length; i++)
        {
            var c = span[i];
            if (c == ' ' || c == '.' || c == ',' || c == ';')
            {
                endIndex = i;
                break;
            }
        }
        var numberSpan = endIndex >= 0 ? span[..endIndex] : span;
        return long.TryParse(numberSpan, out needed);
    }

    private static bool TryParseSort(string? sort, out bool sortAsc, out string? error)
    {
        sortAsc = true;
        error = null;

        if (string.IsNullOrWhiteSpace(sort))
        {
            return true;
        }

        var normalized = sort.Trim();
        var lower = normalized.ToLowerInvariant();

        if (lower is "startsat" or "startsat:asc" or "startsat_asc" or "startsat asc" or "asc" or "+startsat")
        {
            sortAsc = true;
            return true;
        }

        if (lower is "-startsat" or "startsat:desc" or "startsat_desc" or "startsat desc" or "desc")
        {
            sortAsc = false;
            return true;
        }

        error = $"Invalid sort '{sort}'. Allowed values: StartsAt, -StartsAt.";
        return false;
    }
}
