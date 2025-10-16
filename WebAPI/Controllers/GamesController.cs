using BusinessObjects.Common.Pagination;
using DTOs.Games;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Common;

namespace WebAPI.Controllers;

/// <summary>
/// Administrative endpoints for managing the game catalog.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class GamesController : ControllerBase
{
    private readonly IGameCatalogService _service;

    public GamesController(IGameCatalogService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Creates a new game entry in the catalog.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("GamesWrite")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Create([FromBody] GameCreateRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _service.CreateAsync(request.Name, ct).ConfigureAwait(false);
        return this.ToActionResult(result, id => new { id }, StatusCodes.Status201Created);
    }

    /// <summary>
    /// Renames an existing game.
    /// </summary>
    [HttpPatch("{id:guid}/rename")]
    [EnableRateLimiting("GamesWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Rename(Guid id, [FromBody] GameRenameRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _service.RenameAsync(id, request.Name, ct).ConfigureAwait(false);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Soft-deletes a game. Existing user associations remain but the game is hidden from public listings.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [EnableRateLimiting("GamesWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> SoftDelete(Guid id, CancellationToken ct)
    {
        var result = await _service.SoftDeleteAsync(id, ct).ConfigureAwait(false);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Searches the game catalog with cursor-based pagination.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("GamesRead")]
    [ProducesResponseType(typeof(CursorPageResult<GameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? cursor,
        [FromQuery] CursorDirection direction = CursorDirection.Next,
        [FromQuery] int size = PaginationOptions.DefaultPageSize,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false,
        CancellationToken ct = default)
    {
        var request = new CursorRequest(cursor, direction, size, sort, desc);
        var result = await _service.SearchAsync(q, request, ct).ConfigureAwait(false);
        return this.ToActionResult(result);
    }
}
