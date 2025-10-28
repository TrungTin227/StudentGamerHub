namespace Services.Realtime;

/// <summary>
/// Provides membership checks for chat rooms with distributed caching support.
/// </summary>
public interface IRoomMembershipService
{
    Task<bool> IsMemberAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
