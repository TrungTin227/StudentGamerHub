using Application.Friends;
using DTOs.Presence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Repositories.Interfaces;
using Services.Common.Auth;
using Services.Presence;

namespace WebAPI.Hubs;

[Authorize]
public sealed class PresenceHub : Hub
{
    private const string FriendsSnapshotEvent = "presence:friends";
    private const string FriendUpdateEvent = "presence:update";

    private readonly IPresenceService _presence;
    private readonly IPresenceReader _reader;
    private readonly IFriendLinkQuerRepository _friendLinks;
    private readonly PresenceOptions _options;
    private readonly ILogger<PresenceHub> _logger;

    public PresenceHub(
        IPresenceService presence,
        IPresenceReader reader,
        IFriendLinkQuerRepository friendLinks,
        IOptions<PresenceOptions> options,
        ILogger<PresenceHub> logger)
    {
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _friendLinks = friendLinks ?? throw new ArgumentNullException(nameof(friendLinks));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task OnConnectedAsync()
    {
        var userId = EnsureAuthenticatedUser();

        await Groups
            .AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId))
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Presence connection {ConnectionId} joined for user {UserId}.",
            Context.ConnectionId,
            userId);

        await SendFriendsPresenceAsync(userId, Context.ConnectionAborted).ConfigureAwait(false);

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User.GetUserId();
        if (userId is not null)
        {
            await Groups
                .RemoveFromGroupAsync(Context.ConnectionId, GetUserGroup(userId.Value))
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Presence connection {ConnectionId} left for user {UserId}.",
                Context.ConnectionId,
                userId.Value);

            var statusResult = await _presence
                .IsOnlineAsync(userId.Value, CancellationToken.None)
                .ConfigureAwait(false);

            if (statusResult.IsSuccess && !statusResult.Value)
            {
                await BroadcastPresenceAsync(userId.Value, CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                _ = ScheduleOfflineBroadcastAsync(userId.Value);
            }
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    public async Task Heartbeat()
    {
        var userId = EnsureAuthenticatedUser();
        var cancellation = Context.ConnectionAborted;

        var onlineResult = await _presence
            .IsOnlineAsync(userId, cancellation)
            .ConfigureAwait(false);
        var wasOnline = onlineResult.IsSuccess && onlineResult.Value;

        var result = await _presence
            .HeartbeatAsync(userId, cancellation)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Failed to record presence heartbeat for user {UserId}: {Error}",
                userId,
                result.Error.Message);
            throw new HubException(result.Error.Message ?? "Failed to record heartbeat");
        }

        _logger.LogDebug(
            "Presence heartbeat recorded for user {UserId} on connection {ConnectionId}.",
            userId,
            Context.ConnectionId);

        if (!wasOnline)
        {
            await BroadcastPresenceAsync(userId, cancellation).ConfigureAwait(false);
        }
    }

    public Task GetOnlineFriends()
    {
        var userId = EnsureAuthenticatedUser();
        return SendFriendsPresenceAsync(userId, Context.ConnectionAborted);
    }

    private async Task SendFriendsPresenceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var friendIds = await GetFriendIdsAsync(userId, cancellationToken).ConfigureAwait(false);
        if (friendIds.Count == 0)
        {
            await Clients.Caller
                .SendAsync(FriendsSnapshotEvent, Array.Empty<PresenceSnapshotItem>(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var presenceResult = await _reader
            .GetBatchAsync(friendIds, cancellationToken)
            .ConfigureAwait(false);

        if (!presenceResult.IsSuccess || presenceResult.Value is null)
        {
            _logger.LogWarning(
                "Failed to resolve presence snapshot for friends of user {UserId}: {Error}",
                userId,
                presenceResult.Error?.Message);

            await Clients.Caller
                .SendAsync(FriendsSnapshotEvent, Array.Empty<PresenceSnapshotItem>(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        var onlineFriends = presenceResult.Value
            .Where(snapshot => snapshot.IsOnline)
            .ToArray();

        await Clients.Caller
            .SendAsync(FriendsSnapshotEvent, onlineFriends, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task BroadcastPresenceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshotResult = await _reader
            .GetBatchAsync(new[] { userId }, cancellationToken)
            .ConfigureAwait(false);

        if (!snapshotResult.IsSuccess || snapshotResult.Value is null)
        {
            _logger.LogWarning(
                "Failed to broadcast presence for user {UserId}: {Error}",
                userId,
                snapshotResult.Error?.Message);
            return;
        }

        var snapshot = snapshotResult.Value.FirstOrDefault();
        if (snapshot is null)
        {
            return;
        }

        var friendIds = await GetFriendIdsAsync(userId, cancellationToken).ConfigureAwait(false);
        if (friendIds.Count == 0)
        {
            return;
        }

        var targetGroups = friendIds
            .Select(GetUserGroup)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (targetGroups.Length == 0)
        {
            return;
        }

        await Clients
            .Groups(targetGroups)
            .SendAsync(FriendUpdateEvent, snapshot, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<Guid>> GetFriendIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            return await _friendLinks
                .GetAcceptedFriendIdsAsync(userId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve friends for user {UserId}", userId);
            return Array.Empty<Guid>();
        }
    }

    private async Task ScheduleOfflineBroadcastAsync(Guid userId)
    {
        try
        {
            var delaySeconds = Math.Max(_options.TtlSeconds + _options.GraceSeconds + 1, 1);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds)).ConfigureAwait(false);

            var statusResult = await _presence
                .IsOnlineAsync(userId, CancellationToken.None)
                .ConfigureAwait(false);

            if (!statusResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Unable to determine presence status for user {UserId} after disconnect: {Error}",
                    userId,
                    statusResult.Error?.Message);
                return;
            }

            if (!statusResult.Value)
            {
                await BroadcastPresenceAsync(userId, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule offline broadcast for user {UserId}", userId);
        }
    }

    private Guid EnsureAuthenticatedUser()
    {
        var userId = Context.User.GetUserId();
        if (userId is null)
        {
            throw new HubException("Authenticated user required.");
        }

        return userId.Value;
    }

    private static string GetUserGroup(Guid userId) => $"presence:user:{userId:D}";
}
