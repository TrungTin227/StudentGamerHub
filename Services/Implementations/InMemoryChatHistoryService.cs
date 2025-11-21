using DTOs.Chat;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Services.Configuration;
using System.Text.Json;

namespace Services.Implementations;

/// <summary>
/// In-memory fallback implementation of chat history service.
/// Used when Redis is not available.
/// </summary>
public sealed class InMemoryChatHistoryService : IChatHistoryService
{
    private readonly IMemoryCache _cache;
    private readonly ChatOptions _options;

    public InMemoryChatHistoryService(
        IMemoryCache cache,
        IOptions<ChatOptions> options)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<string> AppendDmAsync(
        Guid fromUserId,
        Guid toUserId,
        string text,
        CancellationToken ct = default)
    {
        text = SanitizeText(text);

        var (pairMin, pairMax) = GetSortedPair(fromUserId, toUserId);
        var channel = $"dm:{pairMin}_{pairMax}";
        var key = $"chat:{channel}";

        var now = DateTime.UtcNow;
        var messageId = $"{ToUnixMilliseconds(now)}-{Guid.NewGuid():N}";

        var history = GetOrCreateHistory(key);
        
        var message = new ChatMessageDto(
            Id: messageId,
            Channel: channel,
            FromUserId: fromUserId,
            ToUserId: toUserId,
            RoomId: null,
            Text: text,
            SentAt: now);

        lock (history)
        {
            history.Add(message);
            if (history.Count > _options.HistoryMax)
            {
                history.RemoveAt(0);
            }
        }

        return Task.FromResult(messageId);
    }

    public Task<string> AppendRoomAsync(
        Guid fromUserId,
        Guid roomId,
        string text,
        CancellationToken ct = default)
    {
        text = SanitizeText(text);

        var channel = $"room:{roomId}";
        var key = $"chat:{channel}";

        var now = DateTime.UtcNow;
        var messageId = $"{ToUnixMilliseconds(now)}-{Guid.NewGuid():N}";

        var history = GetOrCreateHistory(key);
        
        var message = new ChatMessageDto(
            Id: messageId,
            Channel: channel,
            FromUserId: fromUserId,
            ToUserId: null,
            RoomId: roomId,
            Text: text,
            SentAt: now);

        lock (history)
        {
            history.Add(message);
            if (history.Count > _options.HistoryMax)
            {
                history.RemoveAt(0);
            }
        }

        return Task.FromResult(messageId);
    }

    public Task<ChatHistoryResponse> LoadHistoryAsync(
        string channel,
        string? afterId,
        int? take,
        CancellationToken ct = default)
    {
        var normalizedTake = Math.Clamp(take ?? 50, 1, _options.HistoryMax);
        var key = $"chat:{channel}";

        var history = GetOrCreateHistory(key);
        
        List<ChatMessageDto> messages;
        lock (history)
        {
            var query = history.AsEnumerable();
            
            if (!string.IsNullOrWhiteSpace(afterId))
            {
                query = query.SkipWhile(m => string.Compare(m.Id, afterId, StringComparison.Ordinal) <= 0);
            }
            
            messages = query.Take(normalizedTake).ToList();
        }

        var nextAfterId = messages.Count == normalizedTake ? messages[^1].Id : null;

        return Task.FromResult(new ChatHistoryResponse(channel, messages, nextAfterId));
    }

    private List<ChatMessageDto> GetOrCreateHistory(string key)
    {
        return _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_options.HistoryTtlHours);
            return new List<ChatMessageDto>();
        })!;
    }

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

    private static long ToUnixMilliseconds(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        }
        return (long)(utc - DateTime.UnixEpoch).TotalMilliseconds;
    }
}
