using BusinessObjects.Common.Pagination;
using BusinessObjects.Common.Results;
using DTOs.Friends;

namespace Application.Friends;

public interface IFriendService
{
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
