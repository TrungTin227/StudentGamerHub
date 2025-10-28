using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Services.Realtime;

/// <summary>
/// Centralizes validation logic for chat channels.
/// </summary>
public sealed class ChannelValidatorService : IChannelValidator
{
    private readonly IRoomMembershipService _membershipService;
    private readonly ILogger<ChannelValidatorService> _logger;

    public ChannelValidatorService(
        IRoomMembershipService membershipService,
        ILogger<ChannelValidatorService> logger)
    {
        _membershipService = membershipService ?? throw new ArgumentNullException(nameof(membershipService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureChannelAccessAsync(
        string channel,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new HubException("Channel is required.");
        }

        if (channel.StartsWith("dm:", StringComparison.OrdinalIgnoreCase))
        {
            EnsureDirectMessageAccess(channel, userId);
            return;
        }

        if (channel.StartsWith("room:", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(channel.AsSpan(5), out var roomId))
            {
                throw new HubException("Invalid room channel format.");
            }

            await EnsureRoomAccessAsync(roomId, userId, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new HubException("Invalid channel format. Must start with 'dm:' or 'room:'.");
    }

    public async Task EnsureRoomAccessAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty)
        {
            throw new HubException("Room id is required.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var isMember = await _membershipService
            .IsMemberAsync(roomId, userId, cancellationToken)
            .ConfigureAwait(false);

        if (!isMember)
        {
            _logger.LogWarning(
                "Access denied for user {UserId} attempting to access room {RoomId}.",
                userId,
                roomId);
            throw new HubException("Access denied: not a room member.");
        }
    }

    private static void EnsureDirectMessageAccess(string channel, Guid userId)
    {
        var payload = channel.AsSpan(3);
        var separatorIndex = payload.IndexOf('_');
        if (separatorIndex <= 0)
        {
            throw new HubException("Invalid DM channel format.");
        }

        var firstSpan = payload[..separatorIndex];
        var secondSpan = payload[(separatorIndex + 1)..];

        if (!Guid.TryParse(firstSpan, out var first) || !Guid.TryParse(secondSpan, out var second))
        {
            throw new HubException("Invalid DM channel format.");
        }

        if (userId != first && userId != second)
        {
            throw new HubException("Access denied: not a DM participant.");
        }
    }
}
