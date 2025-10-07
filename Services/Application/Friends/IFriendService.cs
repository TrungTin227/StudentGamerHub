namespace Application.Friends;

public interface IFriendService
{
    Task<BusinessObjects.Common.Results.Result> InviteAsync(
        Guid requesterId,
        Guid targetUserId,
        CancellationToken ct = default);

    Task<BusinessObjects.Common.Results.Result> AcceptAsync(
        Guid requesterId,
        Guid targetUserId,
        CancellationToken ct = default);

    Task<BusinessObjects.Common.Results.Result> DeclineAsync(
        Guid requesterId,
        Guid targetUserId,
        CancellationToken ct = default);

    Task<BusinessObjects.Common.Results.Result> CancelAsync(
        Guid requesterId,
        Guid targetUserId,
        CancellationToken ct = default);

    Task<BusinessObjects.Common.Results.Result<BusinessObjects.Common.Pagination.CursorPageResult<DTOs.Friends.FriendDto>>> ListAsync(
        Guid requesterId,
        BusinessObjects.Common.Pagination.CursorRequest request,
        CancellationToken ct = default);
}
