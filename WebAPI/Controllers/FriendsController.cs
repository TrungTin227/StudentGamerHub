using Application.Friends;
using DTOs.Friends;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public sealed class FriendsController : ControllerBase
    {
        private const string PageQueryKey = "page";

        private readonly IFriendService _svc;

        public FriendsController(IFriendService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
        }

        [HttpGet("search")]
        [EnableRateLimiting("ReadsLight")]
        [ProducesResponseType(typeof(PagedResponse<UserSearchItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Search(
            [FromQuery(Name = "q")] string? keyword,
            [FromQuery] PageRequest request,
            CancellationToken ct)
        {
            var currentUserId = User.GetUserId();
            if (!currentUserId.HasValue)
            {
                var failure = Result<PagedResponse<UserSearchItemDto>>.Failure(
                    new Error(Error.Codes.Unauthorized, "User identity is required."));
                return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
            }

            var result = await _svc
                .SearchUsersAsync(currentUserId.Value, keyword, request, ct)
                .ConfigureAwait(false);

            return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
        }

        [HttpGet("requests/incoming")]
        [EnableRateLimiting("ReadsLight")]
        [ProducesResponseType(typeof(PagedResponse<FriendRequestItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetIncomingRequests([FromQuery] PageRequest request, CancellationToken ct)
        {
            var currentUserId = User.GetUserId();
            if (!currentUserId.HasValue)
            {
                var failure = Result<PagedResponse<FriendRequestItemDto>>.Failure(
                    new Error(Error.Codes.Unauthorized, "User identity is required."));
                return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
            }

            var result = await _svc
                .GetIncomingRequestsAsync(currentUserId.Value, request, ct)
                .ConfigureAwait(false);

            return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
        }

        [HttpGet("requests/outgoing")]
        [EnableRateLimiting("ReadsLight")]
        [ProducesResponseType(typeof(PagedResponse<FriendRequestItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> GetOutgoingRequests(
            [FromQuery] string? status,
            [FromQuery] PageRequest request,
            CancellationToken ct)
        {
            var currentUserId = User.GetUserId();
            if (!currentUserId.HasValue)
            {
                var failure = Result<PagedResponse<FriendRequestItemDto>>.Failure(
                    new Error(Error.Codes.Unauthorized, "User identity is required."));
                return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
            }

            var result = await _svc
                .GetOutgoingRequestsAsync(currentUserId.Value, status, request, ct)
                .ConfigureAwait(false);

            return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
        }

        [HttpPost("{userId:guid}/invite")]
        [EnableRateLimiting("FriendInvite")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Invite(Guid userId, CancellationToken ct)
        {
            var currentUserId = User.GetUserId();
            if (!currentUserId.HasValue)
            {
                return this.ToActionResult(Result.Failure(
                    new Error(Error.Codes.Unauthorized, "User identity is required.")));
            }

            var result = await _svc.InviteAsync(currentUserId.Value, userId, ct).ConfigureAwait(false);
            return this.ToActionResult(result);
        }

        [HttpPost("{userId:guid}/accept")]
        [EnableRateLimiting("FriendAction")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Accept(Guid userId, CancellationToken ct)
        {
            var currentUserId = User.GetUserId();
            if (!currentUserId.HasValue)
            {
                return this.ToActionResult(Result.Failure(
                    new Error(Error.Codes.Unauthorized, "User identity is required.")));
            }

            var result = await _svc.AcceptAsync(currentUserId.Value, userId, ct).ConfigureAwait(false);
            return this.ToActionResult(result);
        }

        [HttpPost("{userId:guid}/decline")]
        [EnableRateLimiting("FriendAction")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Decline(Guid userId, CancellationToken ct)
        {
            var currentUserId = User.GetUserId();
            if (!currentUserId.HasValue)
            {
                return this.ToActionResult(Result.Failure(
                    new Error(Error.Codes.Unauthorized, "User identity is required.")));
            }

            var result = await _svc.DeclineAsync(currentUserId.Value, userId, ct).ConfigureAwait(false);
            return this.ToActionResult(result);
        }

        [HttpPost("{userId:guid}/cancel")]
        [EnableRateLimiting("FriendAction")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> Cancel(Guid userId, CancellationToken ct)
        {
            var currentUserId = User.GetUserId();
            if (!currentUserId.HasValue)
            {
                return this.ToActionResult(Result.Failure(
                    new Error(Error.Codes.Unauthorized, "User identity is required.")));
            }

            var result = await _svc.CancelAsync(currentUserId.Value, userId, ct).ConfigureAwait(false);
            return this.ToActionResult(result);
        }

        [HttpGet]
        [EnableRateLimiting("ReadsLight")]
        [ProducesResponseType(typeof(CursorPageResult<FriendDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(FriendRequestsOverviewDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(PagedResponse<UserSearchItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> List([FromQuery(Name = "filter")] string? filter, [FromQuery] CursorRequest request, CancellationToken ct)
        {
            var currentUserId = User.GetUserId();
            if (!currentUserId.HasValue)
            {
                var unauthorized = Result<CursorPageResult<FriendDto>>.Failure(
                    new Error(Error.Codes.Unauthorized, "User identity is required."));
                return this.ToActionResult(unauthorized, v => v, StatusCodes.Status200OK);
            }

            var parsedFilter = FriendsFilter.All;
            if (!string.IsNullOrWhiteSpace(filter) && !Enum.TryParse(filter, true, out parsedFilter))
            {
                var allowed = string.Join(", ", Enum.GetNames<FriendsFilter>().Select(n => n.ToLowerInvariant()));
                var failure = Result<CursorPageResult<FriendDto>>.Failure(
                    new Error(Error.Codes.Validation, $"Invalid filter '{filter}'. Allowed values: {allowed}."));
                return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
            }

            switch (parsedFilter)
            {
                case FriendsFilter.All:
                case FriendsFilter.Online:
                {
                    var result = await _svc
                        .ListAsync(currentUserId.Value, parsedFilter, request, ct)
                        .ConfigureAwait(false);
                    return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
                }

                case FriendsFilter.Requests:
                {
                    var take = Math.Clamp(request.SizeSafe, 1, 50);
                    var result = await _svc
                        .GetRequestsOverviewAsync(currentUserId.Value, take, ct)
                        .ConfigureAwait(false);
                    return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
                }

                case FriendsFilter.Suggested:
                {
                var pageNumber = 1;
                if (Request.Query.TryGetValue(PageQueryKey, out var rawPage) &&
                    int.TryParse(rawPage, out var parsedPage) && parsedPage > 0)
                {
                    pageNumber = parsedPage;
                }

                var pageRequest = new PageRequest(
                    Page: pageNumber,
                    Size: Math.Clamp(request.SizeSafe, 1, 20),
                    Sort: request.Sort,
                    Desc: request.Desc);

                    var result = await _svc
                        .GetSuggestedFriendsAsync(currentUserId.Value, pageRequest, ct)
                        .ConfigureAwait(false);
                    return this.ToActionResult(result, v => v, StatusCodes.Status200OK);
                }

                default:
                {
                    var filterName = parsedFilter.ToString().ToLowerInvariant();
                    var failure = Result<CursorPageResult<FriendDto>>.Failure(
                        new Error(Error.Codes.Validation, $"Filter '{filterName}' is not supported."));
                    return this.ToActionResult(failure, v => v, StatusCodes.Status200OK);
                }
            }
        }
    }
}
