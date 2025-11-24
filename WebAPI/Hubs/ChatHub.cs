using DTOs.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Services.Common.Abstractions;
using Services.Configuration;
using Services.Realtime;
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
    private readonly IRateLimiter _rateLimiter;
    private readonly IChannelValidator _channelValidator;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IChatHistoryService chatHistory,
        ICurrentUserService currentUser,
        IConnectionMultiplexer redis,
        IOptions<ChatOptions> options,
        IRateLimiter rateLimiter,
        IChannelValidator channelValidator,
        ILogger<ChatHub> logger)
    {
        _chatHistory = chatHistory ?? throw new ArgumentNullException(nameof(chatHistory));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _channelValidator = channelValidator ?? throw new ArgumentNullException(nameof(channelValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a direct message to another user.
    /// </summary>
    public async Task SendDm(Guid toUserId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new HubException("Message text cannot be empty.");
        }

        var cancellation = Context.ConnectionAborted;
        var currentUserId = _currentUser.GetUserIdOrThrow();

        await EnsureWithinRateLimitAsync(currentUserId, cancellation).ConfigureAwait(false);


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
        if (roomId == Guid.Empty)
        {
            throw new HubException("Room id is required.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new HubException("Message text cannot be empty.");
        }

        var cancellation = Context.ConnectionAborted;
        var currentUserId = _currentUser.GetUserIdOrThrow();

        await EnsureWithinRateLimitAsync(currentUserId, cancellation).ConfigureAwait(false);
        await _channelValidator
            .EnsureRoomAccessAsync(roomId, currentUserId, cancellation)
            .ConfigureAwait(false);

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
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new HubException("Channel is required.");
        }

        var currentUserId = _currentUser.GetUserIdOrThrow();
        var cancellation = Context.ConnectionAborted;

        await _channelValidator
            .EnsureChannelAccessAsync(channel, currentUserId, cancellation)
            .ConfigureAwait(false);

        var normalizedTake = Math.Clamp(take ?? 50, 1, _options.HistoryMax);

        var response = await _chatHistory.LoadHistoryAsync(channel, afterId, normalizedTake).ConfigureAwait(false);

        await Clients.Caller.SendAsync("history", response).ConfigureAwait(false);
    }

    /// <summary>
    /// Join multiple channels at once (batch operation).
    /// </summary>
    public async Task JoinChannels(string[] channels)
    {
        if (channels is null || channels.Length == 0)
        {
            throw new HubException("Channels cannot be null or empty.");
        }

        var currentUserId = _currentUser.GetUserIdOrThrow();
        var cancellation = Context.ConnectionAborted;

        foreach (var channel in channels)
        {
            await _channelValidator
                .EnsureChannelAccessAsync(channel, currentUserId, cancellation)
                .ConfigureAwait(false);

            await Groups.AddToGroupAsync(Context.ConnectionId, channel).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Join a specific room.
    /// </summary>
    public async Task JoinRoom(Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            throw new HubException("Room id is required.");
        }

        var currentUserId = _currentUser.GetUserIdOrThrow();
        var cancellation = Context.ConnectionAborted;

        await _channelValidator
            .EnsureRoomAccessAsync(roomId, currentUserId, cancellation)
            .ConfigureAwait(false);

        var channel = $"room:{roomId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, channel).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} joined room {RoomId} on connection {ConnectionId}.",
            currentUserId,
            roomId,
            Context.ConnectionId);
    }

    /// <summary>
    /// Leave a specific room.
    /// </summary>
    public async Task LeaveRoom(Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            throw new HubException("Room id is required.");
        }

        var currentUserId = _currentUser.GetUserIdOrThrow();
        var channel = $"room:{roomId}";

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} left room {RoomId} on connection {ConnectionId}.",
            currentUserId,
            roomId,
            Context.ConnectionId);
    }

    /// <summary>
    /// Leave a specific channel (room or DM).
    /// </summary>
    public async Task LeaveChannel(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new HubException("Channel is required.");
        }

        var currentUserId = _currentUser.GetUserIdOrThrow();

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, channel).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} left channel {Channel} on connection {ConnectionId}.",
            currentUserId,
            channel,
            Context.ConnectionId);
    }

    // Helpers

    private async Task EnsureWithinRateLimitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromSeconds(_options.RateLimitWindowSeconds);
        var allowed = await _rateLimiter
            .IsAllowedAsync(userId, _options.RateLimitMaxMessages, window, cancellationToken)
            .ConfigureAwait(false);

        if (!allowed)
        {
            _logger.LogWarning(
                "Rate limit exceeded for user {UserId} on connection {ConnectionId}.",
                userId,
                Context.ConnectionId);
            throw new HubException("rate_limited");
        }
    }

    private static (Guid min, Guid max) GetSortedPair(Guid a, Guid b)
    {
        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal) < 0
            ? (a, b)
            : (b, a);
    }
}
