using DTOs.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Services.Common.Abstractions;
using Services.Configuration;
using StackExchange.Redis;

namespace WebAPI.Hubs;

/// <summary>
/// SignalR hub for realtime chat (DM & Room).
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IChatHistoryService _chatHistory;
    private readonly ICurrentUserService _currentUser;
    private readonly IConnectionMultiplexer _redis;
    private readonly ChatOptions _options;

    public ChatHub(
        IChatHistoryService chatHistory,
        ICurrentUserService currentUser,
        IConnectionMultiplexer redis,
        IOptions<ChatOptions> options)
    {
        _chatHistory = chatHistory ?? throw new ArgumentNullException(nameof(chatHistory));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public override async Task OnConnectedAsync()
    {
        var currentUserId = _currentUser.GetUserIdOrThrow();

        // Store connection mapping
        var db = _redis.GetDatabase();
        await db.StringSetAsync(
            $"chat:conn:{Context.ConnectionId}",
            currentUserId.ToString(),
            expiry: TimeSpan.FromHours(1)).ConfigureAwait(false);

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"chat:conn:{Context.ConnectionId}").ConfigureAwait(false);
        await db.KeyDeleteAsync($"chat:rl:{Context.ConnectionId}").ConfigureAwait(false);

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a direct message to another user.
    /// </summary>
    public async Task SendDm(Guid toUserId, string text)
    {
        await CheckRateLimitAsync().ConfigureAwait(false);

        var currentUserId = _currentUser.GetUserIdOrThrow();

        if (currentUserId == toUserId)
        {
            throw new HubException("Cannot send a message to yourself.");
        }

        var (pairMin, pairMax) = GetSortedPair(currentUserId, toUserId);
        var channel = $"dm:{pairMin}_{pairMax}";

        var msgId = await _chatHistory.AppendDmAsync(currentUserId, toUserId, text).ConfigureAwait(false);

        var message = new ChatMessageDto(
            Id: msgId,
            Channel: channel,
            FromUserId: currentUserId,
            ToUserId: toUserId,
            RoomId: null,
            Text: text,
            SentAt: DateTime.UtcNow);

        // Ensure both users are in the group
        await Groups.AddToGroupAsync(Context.ConnectionId, channel).ConfigureAwait(false);

        // Broadcast to the DM group
        await Clients.Group(channel).SendAsync("msg", message).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a message to a room.
    /// </summary>
    public async Task SendToRoom(Guid roomId, string text)
    {
        await CheckRateLimitAsync().ConfigureAwait(false);

        var currentUserId = _currentUser.GetUserIdOrThrow();

        var channel = $"room:{roomId}";

        var msgId = await _chatHistory.AppendRoomAsync(currentUserId, roomId, text).ConfigureAwait(false);

        var message = new ChatMessageDto(
            Id: msgId,
            Channel: channel,
            FromUserId: currentUserId,
            ToUserId: null,
            RoomId: roomId,
            Text: text,
            SentAt: DateTime.UtcNow);

        // Ensure user is in the room group
        await Groups.AddToGroupAsync(Context.ConnectionId, channel).ConfigureAwait(false);

        // Broadcast to the room group
        await Clients.Group(channel).SendAsync("msg", message).ConfigureAwait(false);
    }

    /// <summary>
    /// Load chat history for a channel.
    /// </summary>
    public async Task LoadHistory(string channel, string? afterId, int? take)
    {
        var currentUserId = _currentUser.GetUserIdOrThrow();

        // Validate channel format and authorization
        ValidateChannelAccess(channel, currentUserId);

        var normalizedTake = Math.Clamp(take ?? 50, 1, _options.HistoryMax);

        var response = await _chatHistory.LoadHistoryAsync(channel, afterId, normalizedTake).ConfigureAwait(false);

        await Clients.Caller.SendAsync("history", response).ConfigureAwait(false);
    }

    /// <summary>
    /// Join multiple channels at once (batch operation).
    /// </summary>
    public async Task JoinChannels(string[] channels)
    {
        var currentUserId = _currentUser.GetUserIdOrThrow();

        foreach (var channel in channels)
        {
            // Validate access for each channel
            ValidateChannelAccess(channel, currentUserId);

            await Groups.AddToGroupAsync(Context.ConnectionId, channel).ConfigureAwait(false);
        }
    }

    // Helpers

    private async Task CheckRateLimitAsync()
    {
        var key = $"chat:rl:{Context.ConnectionId}";
        var db = _redis.GetDatabase();

        var count = await db.StringIncrementAsync(key).ConfigureAwait(false);

        if (count == 1)
        {
            await db.KeyExpireAsync(key, TimeSpan.FromSeconds(_options.RateLimitWindowSeconds)).ConfigureAwait(false);
        }

        if (count > _options.RateLimitMaxMessages)
        {
            throw new HubException("rate_limited");
        }
    }

    private static (Guid min, Guid max) GetSortedPair(Guid a, Guid b)
    {
        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal) < 0
            ? (a, b)
            : (b, a);
    }

    private static void ValidateChannelAccess(string channel, Guid currentUserId)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new HubException("Channel is required.");
        }

        if (channel.StartsWith("dm:", StringComparison.OrdinalIgnoreCase))
        {
            // Extract min/max from "dm:{min}_{max}"
            var parts = channel.Substring(3).Split('_');
            if (parts.Length != 2 || !Guid.TryParse(parts[0], out var min) || !Guid.TryParse(parts[1], out var max))
            {
                throw new HubException("Invalid DM channel format.");
            }

            // User must be either min or max
            if (currentUserId != min && currentUserId != max)
            {
                throw new HubException("Unauthorized access to DM channel.");
            }
        }
        else if (channel.StartsWith("room:", StringComparison.OrdinalIgnoreCase))
        {
            // Extract roomId from "room:{roomId}"
            var roomIdStr = channel.Substring(5);
            if (!Guid.TryParse(roomIdStr, out _))
            {
                throw new HubException("Invalid room channel format.");
            }

            // For room access, we could check RoomMember in DB, but that's expensive
            // For now, we allow access (security is handled at join time in RoomService)
            // Optionally: implement IRoomMembershipChecker service for validation
        }
        else
        {
            throw new HubException("Invalid channel format. Must start with 'dm:' or 'room:'.");
        }
    }
}
