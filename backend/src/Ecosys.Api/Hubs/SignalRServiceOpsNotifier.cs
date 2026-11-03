using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.SignalR;

namespace Ecosys.Api.Hubs;

/// <summary>
/// Implements IServiceOpsNotifier (declared in Ecosys.Shared) using SignalR.
/// Registered in the Api DI container and injected into the Infrastructure
/// lifecycle service — no circular dependency because Infrastructure only
/// references the Shared interface, not this Api-level class.
/// </summary>
internal sealed class SignalRServiceOpsNotifier(IHubContext<ServiceOpsHub> hubContext) : IServiceOpsNotifier
{
    public async Task NotifyWorkOrderUpdatedAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default)
    {
        await hubContext.Clients
            .Group($"tenant-{tenantId}")
            .SendAsync("WorkOrderUpdated", workOrderId, cancellationToken);
    }
}
