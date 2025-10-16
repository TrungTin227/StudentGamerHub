using DTOs.Games;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Common;

namespace WebAPI.Controllers;

/// <summary>
/// Endpoints for users to manage their personal game catalog.
/// </summary>
[ApiController]
[Route("api/me/games")]
[Authorize]
public sealed class UserGamesController : ControllerBase
{
    private readonly IUserGameService _service;

    public UserGamesController(IUserGameService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Lists the games currently tracked by the authenticated user.
    /// </summary>
    [HttpGet]
    [EnableRateLimiting("GamesRead")]
    [ProducesResponseType(typeof(IReadOnlyList<UserGameDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> ListMine(CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result.Failure(new Error(Error.Codes.Forbidden, "User identity is required.")));
        }

        var result = await _service.ListMineAsync(userId.Value, ct).ConfigureAwait(false);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Adds a new game association for the authenticated user.
    /// </summary>
    [HttpPost("{gameId:guid}")]
    [EnableRateLimiting("GamesWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Add(Guid gameId, [FromBody] UserGameUpsertRequestDto request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result.Failure(new Error(Error.Codes.Forbidden, "User identity is required.")));
        }

        ArgumentNullException.ThrowIfNull(request);

        var result = await _service.AddAsync(userId.Value, gameId, request.InGameName, request.Skill, ct).ConfigureAwait(false);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Updates an existing user-game association.
    /// </summary>
    [HttpPut("{gameId:guid}")]
    [EnableRateLimiting("GamesWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Update(Guid gameId, [FromBody] UserGameUpsertRequestDto request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result.Failure(new Error(Error.Codes.Forbidden, "User identity is required.")));
        }

        ArgumentNullException.ThrowIfNull(request);

        var result = await _service.UpdateAsync(userId.Value, gameId, request.InGameName, request.Skill, ct).ConfigureAwait(false);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Removes a game from the authenticated user's catalog.
    /// </summary>
    [HttpDelete("{gameId:guid}")]
    [EnableRateLimiting("GamesWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> Remove(Guid gameId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!userId.HasValue)
        {
            return this.ToActionResult(Result.Failure(new Error(Error.Codes.Forbidden, "User identity is required.")));
        }

        var result = await _service.RemoveAsync(userId.Value, gameId, ct).ConfigureAwait(false);
        return this.ToActionResult(result);
    }
}
