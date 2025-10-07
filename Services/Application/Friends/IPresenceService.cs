namespace Application.Friends;

public interface IPresenceService
{
    Task<BusinessObjects.Common.Results.Result> HeartbeatAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<BusinessObjects.Common.Results.Result<bool>> IsOnlineAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<BusinessObjects.Common.Results.Result<IReadOnlyDictionary<Guid, bool>>> BatchIsOnlineAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default);
}
