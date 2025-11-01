using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.DTOs.Memberships;
using DTOs.Memberships;
using WebApi.Common;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class MembershipsController : ControllerBase
{
    private readonly IMembershipReadService _membershipReadService;
    private readonly IMembershipPlanService _membershipPlanService;

    public MembershipsController(IMembershipReadService membershipReadService, IMembershipPlanService membershipPlanService)
    {
        _membershipReadService = membershipReadService ?? throw new ArgumentNullException(nameof(membershipReadService));
        _membershipPlanService = membershipPlanService ?? throw new ArgumentNullException(nameof(membershipPlanService));
    }

    /// <summary>
    /// Gets the current user's active MembershipPlan membership with quota and expiry details.
    /// Returns null if the user has no membership or if it has expired.
    /// </summary>
    [HttpGet("current")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(UserMembershipInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetCurrentMembership(CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            var failure = Result<UserMembershipInfoDto?>.Failure(
                new Error(Error.Codes.Unauthorized, "Authentication required."));
            return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
        }

        var result = await _membershipReadService
            .GetCurrentMembershipAsync(userId.Value, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    /// <summary>
    /// Gets the specified user's active MembershipPlan membership with quota and expiry details.
    /// Only accessible by the membership owner or admins.
    /// </summary>
    [HttpGet("status/{userId:guid}")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(UserMembershipInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetMembershipStatus(Guid userId, CancellationToken ct = default)
    {
        var callerId = User.GetUserId();
        if (callerId is null)
        {
            var failure = Result<UserMembershipInfoDto?>.Failure(
                new Error(Error.Codes.Unauthorized, "Authentication required."));
            return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
        }

        if (callerId.Value != userId && !User.IsInRole("Admin"))
        {
            var failure = Result<UserMembershipInfoDto?>.Failure(
                new Error(Error.Codes.Forbidden, "Only the owner or an admin may view this membership status."));
            return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
        }

        var result = await _membershipReadService
            .GetCurrentMembershipAsync(userId, ct)
            .ConfigureAwait(false);

        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    /// <summary>
    /// Gets the club/room membership tree for the current user.
    /// This endpoint returns club and room memberships, not MembershipPlan memberships.
    /// For MembershipPlan status, use GET /api/Memberships/current instead.
    /// </summary>
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

    /// <summary>
    /// Gets the club/room membership tree for the specified user.
    /// This endpoint returns club and room memberships, not MembershipPlan memberships.
    /// For MembershipPlan status, use GET /api/Memberships/status/{userId} instead.
    /// </summary>
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

    [AllowAnonymous]
    [HttpGet("public")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipPlanSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPublicPlans(CancellationToken ct = default)
    {
        var result = await _membershipPlanService.GetPublicAsync(ct).ConfigureAwait(false);
        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipPlanSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPlans([FromQuery] bool includeInactive = true, CancellationToken ct = default)
    {
        var result = await _membershipPlanService.GetAllAsync(includeInactive, ct).ConfigureAwait(false);
        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    [HttpGet("{planId:guid}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("ReadsLight")]
    [ProducesResponseType(typeof(MembershipPlanDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetPlan(Guid planId, CancellationToken ct = default)
    {
        var result = await _membershipPlanService.GetByIdAsync(planId, ct).ConfigureAwait(false);
        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("PaymentsWrite")]
    [ProducesResponseType(typeof(MembershipPlanDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CreatePlan([FromBody] MembershipPlanCreateRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            return this.ToActionResult(Result<MembershipPlanDetailDto>.Failure(new Error(Error.Codes.Validation, "Request body is required.")));
        }

        var actorId = User.GetUserId();
        if (!actorId.HasValue)
        {
            return this.ToActionResult(Result<MembershipPlanDetailDto>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _membershipPlanService.CreateAsync(request, actorId.Value, ct).ConfigureAwait(false);
        object? routeValues = result.IsSuccess ? new { planId = result.Value!.Id } : null;
        return this.ToCreatedAtAction(result, nameof(GetPlan), routeValues);
    }

    [HttpPut("{planId:guid}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("PaymentsWrite")]
    [ProducesResponseType(typeof(MembershipPlanDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> UpdatePlan(Guid planId, [FromBody] MembershipPlanUpdateRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            return this.ToActionResult(Result<MembershipPlanDetailDto>.Failure(new Error(Error.Codes.Validation, "Request body is required.")));
        }

        var actorId = User.GetUserId();
        if (!actorId.HasValue)
        {
            return this.ToActionResult(Result<MembershipPlanDetailDto>.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _membershipPlanService.UpdateAsync(planId, request, actorId.Value, ct).ConfigureAwait(false);
        return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
    }

    [HttpDelete("{planId:guid}")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("PaymentsWrite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeletePlan(Guid planId, CancellationToken ct = default)
    {
        var actorId = User.GetUserId();
        if (!actorId.HasValue)
        {
            return this.ToActionResult(Result.Failure(new Error(Error.Codes.Unauthorized, "User identity is required.")));
        }

        var result = await _membershipPlanService.DeleteAsync(planId, actorId.Value, ct).ConfigureAwait(false);
        return this.ToActionResult(result);
    }
}
