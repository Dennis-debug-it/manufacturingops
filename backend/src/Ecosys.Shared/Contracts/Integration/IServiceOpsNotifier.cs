namespace Ecosys.Shared.Contracts.Integration;

/// <summary>
/// Abstraction for real-time dispatch notifications — implemented in Ecosys.Api
/// via SignalR so that Infrastructure can broadcast without referencing the Api layer.
/// </summary>
public interface IServiceOpsNotifier
{
    Task NotifyWorkOrderUpdatedAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default);
}
