namespace Services.Realtime;

/// <summary>
/// Validates realtime channel access rules for chat scenarios.
/// </summary>
public interface IChannelValidator
{
    /// <summary>
    /// Ensures the given user can access the specified channel. Throws a <see cref="HubException"/> when unauthorized.
    /// </summary>
    Task EnsureChannelAccessAsync(
        string channel,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the user is an approved member of the provided room.
    /// </summary>
    Task EnsureRoomAccessAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
