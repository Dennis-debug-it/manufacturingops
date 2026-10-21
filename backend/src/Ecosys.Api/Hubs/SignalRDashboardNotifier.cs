using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.SignalR;

namespace Ecosys.Api.Hubs;

/// <summary>
/// Implements IDashboardNotifier (declared in Ecosys.Shared) using SignalR. Staff connections join
/// their tenant's group in <see cref="ServiceOpsHub.OnConnectedAsync"/>; the event names only the
/// data areas that changed, never any figures, and each client refetches what it is allowed to see.
/// </summary>
internal sealed class SignalRDashboardNotifier(IHubContext<ServiceOpsHub> hubContext) : IDashboardNotifier
{
    public Task DashboardChangedAsync(Guid tenantId, IReadOnlyCollection<string> areas, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(ServiceOpsHub.TenantGroup(tenantId)).SendAsync("DashboardChanged", areas, cancellationToken);
}
