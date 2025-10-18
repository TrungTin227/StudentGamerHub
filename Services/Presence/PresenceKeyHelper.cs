using StackExchange.Redis;

namespace Services.Presence;

internal static class PresenceKeyHelper
{
    public static string NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        var trimmed = prefix.Trim();
        return trimmed.EndsWith(":", StringComparison.Ordinal)
            ? trimmed
            : string.Concat(trimmed, ":");
    }

    public static RedisKey GetPresenceKey(string prefix, Guid userId) => $"{prefix}presence:{userId:D}";

    public static RedisKey GetLastSeenKey(string prefix, Guid userId) => $"{prefix}lastseen:{userId:D}";

    public static RedisKey GetIndexKey(string prefix) => $"{prefix}presence:index";

    public static RedisValue GetMember(Guid userId) => userId.ToString("D");
}
