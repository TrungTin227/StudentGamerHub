using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.DTOs.Memberships;
using WebApi.Common;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class MembershipsController : ControllerBase
{
    private readonly IMembershipReadService _membershipReadService;

    public MembershipsController(IMembershipReadService membershipReadService)
    {
        _membershipReadService = membershipReadService ?? throw new ArgumentNullException(nameof(membershipReadService));
    }

    [HttpGet("tree")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(ClubRoomTreeHybridDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetMyMembershipTree(CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            var failure = Result<ClubRoomTreeHybridDto>.Failure(
                new Error(Error.Codes.Unauthorized, "Authentication required."));
            return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
        }

        var result = await _membershipReadService
            .GetMyClubRoomTreeAsync(userId.Value, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    [HttpGet("tree/{userId:guid}")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(ClubRoomTreeHybridDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetMembershipTree(Guid userId, CancellationToken ct = default)
    {
        var callerId = User.GetUserId();
        if (callerId is null)
        {
            var failure = Result<ClubRoomTreeHybridDto>.Failure(
                new Error(Error.Codes.Unauthorized, "Authentication required."));
            return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
        }

        if (callerId.Value != userId && !User.IsInRole("Admin"))
        {
            var failure = Result<ClubRoomTreeHybridDto>.Failure(
                new Error(Error.Codes.Forbidden, "Only the owner or an admin may view this membership tree."));
            return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
        }

        var result = await _membershipReadService
            .GetMyClubRoomTreeAsync(userId, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }
}
