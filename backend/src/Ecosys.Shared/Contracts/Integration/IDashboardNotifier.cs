namespace Ecosys.Shared.Contracts.Integration;

/// <summary>
/// Live dashboard updates — implemented in Ecosys.Api via SignalR (group <c>tenant-{tenantId}</c>) so
/// Infrastructure can push without referencing the Api layer, like <see cref="IInboxNotifier"/>. The
/// event carries only the data areas that changed (<c>work-orders</c>, <c>finance</c>, ...); clients
/// refetch the widgets that read them. Best effort: a missed event is covered by the client's polling.
/// </summary>
public interface IDashboardNotifier
{
    /// <summary>Raises <c>DashboardChanged</c> with the areas whose data changed.</summary>
    Task DashboardChangedAsync(Guid tenantId, IReadOnlyCollection<string> areas, CancellationToken cancellationToken = default);
}
