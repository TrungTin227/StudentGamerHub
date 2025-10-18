using Application.Friends;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Services.Common.Auth;

namespace WebAPI.Hubs;

[Authorize]
public sealed class PresenceHub : Hub
{
    private readonly IPresenceService _presence;

    public PresenceHub(IPresenceService presence)
    {
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
    }

    public async Task Heartbeat()
    {
        var userId = Context.User.GetUserId();
        if (userId is null)
        {
            throw new HubException("Authenticated user required for heartbeat");
        }

        var result = await _presence
            .HeartbeatAsync(userId.Value, Context.ConnectionAborted)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new HubException(result.Error.Message ?? "Failed to record heartbeat");
        }
    }
}
