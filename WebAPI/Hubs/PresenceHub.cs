using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Services.Presence;
using StackExchange.Redis;

namespace WebAPI.Hubs;

[Authorize]
public sealed class PresenceHub : Hub
{
    private readonly IConnectionMultiplexer _redis;
    private readonly PresenceOptions _options;

    public PresenceHub(IConnectionMultiplexer redis, IOptions<PresenceOptions> options)
    {
        _redis = redis;
        _options = options.Value;
    }

    public async Task Heartbeat()
    {
        var userId = Context.User.GetUserId();
        if (userId is null)
        {
            throw new HubException("Authenticated user required for heartbeat");
        }

        var ttl = TimeSpan.FromSeconds(_options.TtlSeconds);
        var timestamp = DateTime.UtcNow.ToString("O");
        var db = _redis.GetDatabase();

        await Task.WhenAll(
            db.StringSetAsync($"presence:{userId}", "1", ttl),
            db.StringSetAsync($"lastseen:{userId}", timestamp));
    }
}
