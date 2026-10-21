using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ecosys.Api.Hubs;

[Authorize]
public sealed class ServiceOpsHub : Hub
{
    public static string UserGroup(Guid userId) => $"user-{userId}";

    /// <summary>
    /// Every staff connection joins its own user's group, which carries the bell's
    /// NotificationCreated and UnreadCountChanged events. Done here rather than by a client call so a
    /// reconnect can never miss it. The group comes from the token, never from the client.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await JoinUserGroup();
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called by the client immediately after connecting.
    /// Adds this connection to the tenant-scoped group so it receives
    /// WorkOrderUpdated broadcasts for its own tenant only.
    /// </summary>
    public async Task JoinTenantGroup()
    {
        var tenantId = Context.User?.FindFirst("TenantId")?.Value
                    ?? Context.User?.FindFirst("tenant_id")?.Value;

        if (!string.IsNullOrEmpty(tenantId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
    }

    /// <summary>Idempotent: <see cref="OnConnectedAsync"/> already does this.</summary>
    public async Task JoinUserGroup()
    {
        if (Guid.TryParse(Context.User?.FindFirst(TenantClaimTypes.UserId)?.Value, out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
    }
}
