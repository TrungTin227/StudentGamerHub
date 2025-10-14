using DTOs.Chat;
using Microsoft.Extensions.Options;
using Services.Configuration;
using StackExchange.Redis;
using System.Text.Json;

namespace Services.Implementations;

/// <summary>
/// Implementation of chat history service using Redis Streams.
/// </summary>
public sealed class ChatHistoryService : IChatHistoryService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ChatOptions _options;

    public ChatHistoryService(
        IConnectionMultiplexer redis,
        IOptions<ChatOptions> options)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<string> AppendDmAsync(
        Guid fromUserId,
        Guid toUserId,
        string text,
        CancellationToken ct = default)
    {
        text = SanitizeText(text);

        var (pairMin, pairMax) = GetSortedPair(fromUserId, toUserId);
        var channel = $"dm:{pairMin}_{pairMax}";
        var key = $"chat:{channel}";

        var db = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow;

        // Try Redis Stream approach first
        try
        {
            var messageId = await db.StreamAddAsync(
                key,
                new[]
                {
                    new NameValueEntry("fromUserId", fromUserId.ToString()),
                    new NameValueEntry("toUserId", toUserId.ToString()),
                    new NameValueEntry("roomId", string.Empty),
                    new NameValueEntry("text", text),
                    new NameValueEntry("ts", now.ToUnixTimeMilliseconds().ToString()),
                    new NameValueEntry("channel", channel)
                },
                maxLength: _options.HistoryMax,
                useApproximateMaxLength: true).ConfigureAwait(false);

            await db.KeyExpireAsync(key, TimeSpan.FromHours(_options.HistoryTtlHours)).ConfigureAwait(false);

            return messageId.ToString();
        }
        catch
        {
            // Fallback to List approach if Stream is not available
            return await AppendToListAsync(
                db,
                key,
                channel,
                fromUserId,
                toUserId,
                null,
                text,
                now).ConfigureAwait(false);
        }
    }

    public async Task<string> AppendRoomAsync(
        Guid fromUserId,
        Guid roomId,
        string text,
        CancellationToken ct = default)
    {
        text = SanitizeText(text);

        var channel = $"room:{roomId}";
        var key = $"chat:{channel}";

        var db = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow;

        // Try Redis Stream approach first
        try
        {
            var messageId = await db.StreamAddAsync(
                key,
                new[]
                {
                    new NameValueEntry("fromUserId", fromUserId.ToString()),
                    new NameValueEntry("toUserId", string.Empty),
                    new NameValueEntry("roomId", roomId.ToString()),
                    new NameValueEntry("text", text),
                    new NameValueEntry("ts", now.ToUnixTimeMilliseconds().ToString()),
                    new NameValueEntry("channel", channel)
                },
                maxLength: _options.HistoryMax,
                useApproximateMaxLength: true).ConfigureAwait(false);

            await db.KeyExpireAsync(key, TimeSpan.FromHours(_options.HistoryTtlHours)).ConfigureAwait(false);

            return messageId.ToString();
        }
        catch
        {
            // Fallback to List approach if Stream is not available
            return await AppendToListAsync(
                db,
                key,
                channel,
                fromUserId,
                null,
                roomId,
                text,
                now).ConfigureAwait(false);
        }
    }

    public async Task<ChatHistoryResponse> LoadHistoryAsync(
        string channel,
        string? afterId,
        int? take,
        CancellationToken ct = default)
    {
        var normalizedTake = Math.Clamp(take ?? 50, 1, _options.HistoryMax);
        var key = $"chat:{channel}";

        var db = _redis.GetDatabase();

        // Try loading from Stream first
        try
        {
            var startId = string.IsNullOrWhiteSpace(afterId) ? "-" : afterId;
            var entries = await db.StreamRangeAsync(key, startId, "+", count: normalizedTake).ConfigureAwait(false);

            if (entries.Length == 0)
            {
                return new ChatHistoryResponse(channel, Array.Empty<ChatMessageDto>(), null);
            }

            var messages = entries
                .Select(e => ParseStreamEntry(e, channel))
                .Where(m => m is not null)
                .Cast<ChatMessageDto>()
                .ToList();

            var nextAfterId = entries.Length == normalizedTake ? entries[^1].Id.ToString() : null;

            return new ChatHistoryResponse(channel, messages, nextAfterId);
        }
        catch
        {
            // Fallback to List approach
            return await LoadFromListAsync(db, key, channel, afterId, normalizedTake).ConfigureAwait(false);
        }
    }

    // Helpers

    private static string SanitizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Message text cannot be empty.", nameof(text));
        }

        text = text.Trim();

        if (text.Length > 2000)
        {
            throw new ArgumentException("Message text exceeds maximum length of 2000 characters.", nameof(text));
        }

        return text;
    }

    private static (Guid min, Guid max) GetSortedPair(Guid a, Guid b)
    {
        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal) < 0
            ? (a, b)
            : (b, a);
    }

    private async Task<string> AppendToListAsync(
        IDatabase db,
        string key,
        string channel,
        Guid fromUserId,
        Guid? toUserId,
        Guid? roomId,
        string text,
        DateTimeOffset timestamp)
    {
        var messageId = $"{timestamp.ToUnixTimeMilliseconds()}-{Guid.NewGuid():N}";

        var payload = JsonSerializer.Serialize(new
        {
            id = messageId,
            fromUserId = fromUserId.ToString(),
            toUserId = toUserId?.ToString() ?? string.Empty,
            roomId = roomId?.ToString() ?? string.Empty,
            text,
            ts = timestamp.ToUnixTimeMilliseconds(),
            channel
        });

        await db.ListRightPushAsync(key, payload).ConfigureAwait(false);
        await db.ListTrimAsync(key, -_options.HistoryMax, -1).ConfigureAwait(false);
        await db.KeyExpireAsync(key, TimeSpan.FromHours(_options.HistoryTtlHours)).ConfigureAwait(false);

        return messageId;
    }

    private static async Task<ChatHistoryResponse> LoadFromListAsync(
        IDatabase db,
        string key,
        string channel,
        string? afterId,
        int take)
    {
        var values = await db.ListRangeAsync(key, -take, -1).ConfigureAwait(false);

        if (values.Length == 0)
        {
            return new ChatHistoryResponse(channel, Array.Empty<ChatMessageDto>(), null);
        }

        var messages = values
            .Select(v => ParseListEntry(v!, channel, afterId))
            .Where(m => m is not null)
            .Cast<ChatMessageDto>()
            .ToList();

        var nextAfterId = messages.Count == take ? messages[^1].Id : null;

        return new ChatHistoryResponse(channel, messages, nextAfterId);
    }

    private static ChatMessageDto? ParseStreamEntry(StreamEntry entry, string channel)
    {
        try
        {
            var values = entry.Values.ToDictionary(
                kv => kv.Name.ToString(),
                kv => kv.Value.ToString(),
                StringComparer.OrdinalIgnoreCase);

            var fromUserId = Guid.Parse(values["fromUserId"]);
            var toUserIdStr = values.GetValueOrDefault("toUserId", string.Empty);
            var roomIdStr = values.GetValueOrDefault("roomId", string.Empty);
            var text = values["text"];
            var ts = long.Parse(values["ts"]);

            return new ChatMessageDto(
                Id: entry.Id.ToString(),
                Channel: channel,
                FromUserId: fromUserId,
                ToUserId: string.IsNullOrEmpty(toUserIdStr) ? null : Guid.Parse(toUserIdStr),
                RoomId: string.IsNullOrEmpty(roomIdStr) ? null : Guid.Parse(roomIdStr),
                Text: text,
                SentAt: DateTimeOffset.FromUnixTimeMilliseconds(ts));
        }
        catch
        {
            return null;
        }
    }

    private static ChatMessageDto? ParseListEntry(string json, string channel, string? afterId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var id = root.GetProperty("id").GetString()!;

            // Filter by afterId if provided
            if (!string.IsNullOrEmpty(afterId) && string.Compare(id, afterId, StringComparison.Ordinal) <= 0)
            {
                return null;
            }

            var fromUserId = Guid.Parse(root.GetProperty("fromUserId").GetString()!);
            var toUserIdStr = root.GetProperty("toUserId").GetString();
            var roomIdStr = root.GetProperty("roomId").GetString();
            var text = root.GetProperty("text").GetString()!;
            var ts = root.GetProperty("ts").GetInt64();

            return new ChatMessageDto(
                Id: id,
                Channel: channel,
                FromUserId: fromUserId,
                ToUserId: string.IsNullOrEmpty(toUserIdStr) ? null : Guid.Parse(toUserIdStr),
                RoomId: string.IsNullOrEmpty(roomIdStr) ? null : Guid.Parse(roomIdStr),
                Text: text,
                SentAt: DateTimeOffset.FromUnixTimeMilliseconds(ts));
        }
        catch
        {
            return null;
        }
    }
}
