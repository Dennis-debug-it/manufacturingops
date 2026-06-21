using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Ecosys.Api.Hubs;

[Authorize]
public sealed class ServiceOpsHub : Hub
{
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
}
