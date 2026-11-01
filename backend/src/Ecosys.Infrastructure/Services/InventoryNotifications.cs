using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Inventory's notifications: stores hears about new requests and parts falling to their reorder
/// level, and the person who asked hears when their request is decided or on its way. Inventory sent
/// none before, so a technician's request sat unseen until someone opened the queue.
/// Every send goes through <see cref="INotificationDispatcher"/>, which applies each user's
/// preferences and never lets a failed notification undo the change that caused it.
/// </summary>
public static class InventoryNotifications
{
    public const string RequestRaised = "inventory.request-raised";
    public const string RequestDecided = "inventory.request-decided";
    public const string RequestDispatched = "inventory.request-dispatched";
    public const string LowStock = "inventory.low-stock";

    /// <summary>
    /// Stores: active admins and materials controllers, plus anyone whose role has been given the
    /// approve or issue materials permission under Users &amp; Access.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> StoresUsersAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var grantedRoles = await dbContext.RolePermissions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && (x.CanIssueMaterials || x.CanApproveMaterials))
            .Select(x => x.Role)
            .ToListAsync(cancellationToken);
        var roles = grantedRoles.Concat([AppRoles.Admin, AppRoles.MaterialsController]).Distinct().ToList();
        return await dbContext.Users.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && roles.Contains(x.Role))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>A request is waiting on stores. The person who raised it is not told about their own request.</summary>
    public static async Task NotifyRequestRaisedAsync(
        INotificationDispatcher? dispatcher, AppDbContext dbContext, Guid tenantId, Guid raisedByUserId,
        string reference, string? workOrderNumber, string? requestedByName, int partCount, bool needsApproval,
        CancellationToken cancellationToken)
    {
        if (dispatcher is null) return;
        var recipients = (await StoresUsersAsync(dbContext, tenantId, cancellationToken)).Where(x => x != raisedByUserId).ToList();
        if (recipients.Count == 0) return;
        var what = $"{partCount} part{(partCount == 1 ? "" : "s")}{(workOrderNumber is null ? string.Empty : $" for {workOrderNumber}")}";
        await dispatcher.NotifyAsync(new NotificationRequest(
            tenantId, RequestRaised, recipients,
            needsApproval ? $"Parts request to approve: {reference}" : $"Parts requested: {reference}",
            $"{requestedByName ?? "Someone"} asked stores for {what}.",
            $"/inventory/requests?stage=Requested&q={Uri.EscapeDataString(reference)}",
            "InventoryRequest", null,
            new Dictionary<string, string?>
            {
                ["Reference"] = reference,
                ["WorkOrderNumber"] = workOrderNumber ?? "—",
                ["RequestedBy"] = requestedByName ?? "—",
                ["Parts"] = partCount.ToString(),
            }), cancellationToken);
    }

    /// <summary>The requester's request was approved or rejected.</summary>
    public static async Task NotifyRequestDecidedAsync(
        INotificationDispatcher? dispatcher, Guid tenantId, Guid requesterUserId, Guid actorUserId,
        string reference, string? workOrderNumber, Guid? workOrderId, bool approved, string? reason,
        CancellationToken cancellationToken)
    {
        if (dispatcher is null || requesterUserId == actorUserId) return;
        var status = approved ? "approved" : "rejected";
        await dispatcher.NotifyAsync(new NotificationRequest(
            tenantId, RequestDecided, [requesterUserId],
            $"Parts request {status}: {reference}",
            approved
                ? $"Stores approved your request{(workOrderNumber is null ? string.Empty : $" for {workOrderNumber}")}. You'll be told when it's dispatched."
                : $"Stores rejected your request{(workOrderNumber is null ? string.Empty : $" for {workOrderNumber}")}: {reason}",
            workOrderId is Guid id ? $"/work-orders/{id}?tab=parts" : "/inventory/requests",
            "InventoryRequest", null,
            new Dictionary<string, string?>
            {
                ["Reference"] = reference,
                ["WorkOrderNumber"] = workOrderNumber ?? "—",
                ["Status"] = status,
                ["Reason"] = string.IsNullOrWhiteSpace(reason) ? "—" : reason,
            },
            approved ? NotificationSeverities.Info : NotificationSeverities.Warning), cancellationToken);
    }

    /// <summary>The requester's parts were dispatched or issued.</summary>
    public static async Task NotifyRequestDispatchedAsync(
        INotificationDispatcher? dispatcher, Guid tenantId, Guid requesterUserId, Guid actorUserId,
        string reference, string? workOrderNumber, Guid? workOrderId, string? fromLocation,
        CancellationToken cancellationToken)
    {
        if (dispatcher is null || requesterUserId == actorUserId) return;
        await dispatcher.NotifyAsync(new NotificationRequest(
            tenantId, RequestDispatched, [requesterUserId],
            $"Parts on the way: {reference}",
            $"Stores sent your parts{(workOrderNumber is null ? string.Empty : $" for {workOrderNumber}")}{(fromLocation is null ? string.Empty : $" from {fromLocation}")}.",
            workOrderId is Guid id ? $"/work-orders/{id}?tab=parts" : "/inventory/requests",
            "InventoryRequest", null,
            new Dictionary<string, string?>
            {
                ["Reference"] = reference,
                ["WorkOrderNumber"] = workOrderNumber ?? "—",
                ["From"] = fromLocation ?? "stores",
            }), cancellationToken);
    }

    /// <summary>Parts that have just fallen to or below their reorder level at a location.</summary>
    public static async Task NotifyLowStockAsync(
        INotificationDispatcher? dispatcher, AppDbContext dbContext, Guid tenantId,
        IReadOnlyCollection<LowStockCrossing> crossings, CancellationToken cancellationToken)
    {
        if (dispatcher is null || crossings.Count == 0) return;
        var recipients = await StoresUsersAsync(dbContext, tenantId, cancellationToken);
        if (recipients.Count == 0) return;
        var first = crossings.First();
        var title = crossings.Count == 1 ? $"Low stock: {first.PartName}" : $"{crossings.Count} parts low on stock";
        var body = string.Join("; ", crossings.Take(5).Select(x => $"{x.PartName} at {x.LocationName}: {x.QuantityOnHand:0.##} left (reorder at {x.ReorderLevel:0.##})"))
            + (crossings.Count > 5 ? "…" : string.Empty);
        await dispatcher.NotifyAsync(new NotificationRequest(
            tenantId, LowStock, recipients, title, body, "/inventory/alerts", "MaterialItem", crossings.Count == 1 ? first.MaterialId : null,
            new Dictionary<string, string?>
            {
                ["Parts"] = body,
                ["Count"] = crossings.Count.ToString(),
            },
            NotificationSeverities.Warning), cancellationToken);
    }
}

/// <summary>A part at one location that went from above its reorder level to at or below it.</summary>
public sealed record LowStockCrossing(Guid MaterialId, string PartName, string LocationName, decimal QuantityOnHand, decimal ReorderLevel);
