using BusinessObjects.Common.Pagination;
using BusinessObjects.Common.Results;
using DTOs.Friends;

namespace Application.Friends;

public interface IFriendService
{
    Task<Result<PagedResponse<UserSearchItemDto>>> SearchUsersAsync(
        Guid currentUserId,
        string? keyword,
        PageRequest page,
        CancellationToken ct = default);

    Task<Result<PagedResponse<FriendRequestItemDto>>> GetIncomingRequestsAsync(
        Guid currentUserId,
        PageRequest page,
        CancellationToken ct = default);

    Task<Result<PagedResponse<FriendRequestItemDto>>> GetOutgoingRequestsAsync(
        Guid currentUserId,
        string? status,
        PageRequest page,
        CancellationToken ct = default);

    Task<Result<FriendRequestsOverviewDto>> GetRequestsOverviewAsync(
        Guid currentUserId,
        int take,
        CancellationToken ct = default);

    Task<Result<PagedResponse<UserSearchItemDto>>> GetSuggestedFriendsAsync(
        Guid currentUserId,
        PageRequest page,
        CancellationToken ct = default);

    Task<Result> InviteAsync(
        Guid requesterId,
        Guid targetUserId,
        CancellationToken ct = default);

    Task<Result> AcceptAsync(
        Guid requesterId,
        Guid targetUserId,
        CancellationToken ct = default);

    Task<Result> DeclineAsync(
        Guid requesterId,
        Guid targetUserId,
        CancellationToken ct = default);

    Task<Result> CancelAsync(
        Guid requesterId,
        Guid targetUserId,
        CancellationToken ct = default);

    Task<Result<CursorPageResult<FriendDto>>> ListAsync(
        Guid requesterId,
        FriendsFilter filter,
        CursorRequest request,
        CancellationToken ct = default);
}
