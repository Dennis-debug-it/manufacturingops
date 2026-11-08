using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Ecosys.Domain.Entities;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The data areas dashboard widgets read. Each widget names its areas in the catalogue; a save that
/// touches an entity of an area makes the widgets reading it recompute and tells open dashboards.
/// </summary>
public static class DashboardDataAreas
{
    public const string WorkOrders = "work-orders";
    public const string Maintenance = "maintenance";
    public const string Contracts = "contracts";
    public const string Materials = "materials";
    public const string Finance = "finance";
    public const string Projects = "projects";
    public const string Activities = "activities";

    /// <summary>
    /// Only the entities a widget query reads (or shows the name of). Everything else — audit rows,
    /// notifications, settings, sessions — changes no figure and so stays quiet.
    /// </summary>
    private static readonly Dictionary<Type, string> ByEntity = new()
    {
        [typeof(WorkOrder)] = WorkOrders,
        [typeof(WorkOrderTechnicianAssignment)] = WorkOrders,
        [typeof(Technician)] = WorkOrders,
        [typeof(PreventiveMaintenancePlan)] = Maintenance,
        [typeof(Contract)] = Contracts,
        [typeof(MaterialItem)] = Materials,
        [typeof(BranchMaterialStock)] = Materials,
        [typeof(MaterialIssue)] = Materials,
        [typeof(PurchaseOrder)] = Materials,
        [typeof(Supplier)] = Materials,
        [typeof(ActionItem)] = Activities,
    };

    public static string? ForEntity(Type type) => ByEntity.GetValueOrDefault(type);
}

/// <summary>
/// A counter per tenant and data area, bumped after every save that changes the area. Widget cache
/// keys include the counters of the widget's areas, so a change makes the next read recompute while
/// other tenants' and other areas' entries stay warm. In memory, like the widget cache it keys.
/// </summary>
public interface IDashboardDataVersions
{
    long Get(Guid tenantId, string area);
    void Bump(Guid tenantId, IEnumerable<string> areas);
}

internal sealed class DashboardDataVersions : IDashboardDataVersions
{
    private readonly ConcurrentDictionary<(Guid TenantId, string Area), long> versions = new();

    public long Get(Guid tenantId, string area) => versions.GetValueOrDefault((tenantId, area));

    public void Bump(Guid tenantId, IEnumerable<string> areas)
    {
        foreach (var area in areas)
            versions.AddOrUpdate((tenantId, area), 1, (_, version) => version + 1);
    }
}

/// <summary>Sends <c>DashboardChanged</c> to a tenant's open dashboards, throttled per tenant.</summary>
public interface IDashboardChangeBroadcaster
{
    void Notify(Guid tenantId, IReadOnlyCollection<string> areas);
}

/// <summary>
/// At most one broadcast per tenant per <see cref="Interval"/>. The first change goes out at once;
/// changes inside the window are merged and sent when it ends (trailing), so the last one is never
/// lost. Sending runs off the caller's thread and a failure is only logged: it can't affect a save.
/// </summary>
internal sealed class DashboardChangeBroadcaster(IDashboardNotifier notifier, ILogger<DashboardChangeBroadcaster> logger) : IDashboardChangeBroadcaster
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(10);

    private sealed class TenantState
    {
        public readonly HashSet<string> Areas = new(StringComparer.Ordinal);
        public DateTime LastSentUtc = DateTime.MinValue;
        public bool Scheduled;
    }

    private readonly ConcurrentDictionary<Guid, TenantState> states = new();

    /// <summary>Settable for tests.</summary>
    internal TimeSpan Interval { get; init; } = DefaultInterval;

    public void Notify(Guid tenantId, IReadOnlyCollection<string> areas)
    {
        if (areas.Count == 0)
            return;
        var state = states.GetOrAdd(tenantId, _ => new TenantState());
        TimeSpan delay;
        lock (state)
        {
            state.Areas.UnionWith(areas);
            if (state.Scheduled)
                return;
            state.Scheduled = true;
            var wait = state.LastSentUtc + Interval - DateTime.UtcNow;
            delay = wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }
        _ = Task.Run(() => SendAsync(tenantId, state, delay));
    }

    private async Task SendAsync(Guid tenantId, TenantState state, TimeSpan delay)
    {
        try
        {
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);
            string[] areas;
            lock (state)
            {
                areas = [.. state.Areas.Order(StringComparer.Ordinal)];
                state.Areas.Clear();
                state.Scheduled = false;
                state.LastSentUtc = DateTime.UtcNow;
            }
            await notifier.DashboardChangedAsync(tenantId, areas);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast a dashboard change for tenant {TenantId}.", tenantId);
        }
    }
}

/// <summary>Used where no live channel is registered (tests, tools); the Api replaces it with SignalR.</summary>
internal sealed class NullDashboardNotifier : IDashboardNotifier
{
    public Task DashboardChangedAsync(Guid tenantId, IReadOnlyCollection<string> areas, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Watches every <see cref="AppDbContext"/> save: before it, notes which tenants' dashboard areas the
/// tracked changes touch (by each entity's own <c>TenantId</c>, so background workers count too);
/// after it succeeds, bumps those areas' versions and asks for a broadcast. A failed or cancelled
/// save changes nothing. Bulk <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> bypass this; none touches a
/// dashboard area today, and the widget cache's own expiry still applies.
/// </summary>
internal sealed class DashboardChangeInterceptor(
    IDashboardDataVersions versions,
    IDashboardChangeBroadcaster broadcaster,
    ILogger<DashboardChangeInterceptor> logger) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, Dictionary<Guid, HashSet<string>>> pending = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Publish(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Publish(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Discard(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    public override void SaveChangesCanceled(DbContextEventData eventData) => Discard(eventData.Context);

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
            return;
        try
        {
            var changes = new Dictionary<Guid, HashSet<string>>();
            // Entries() runs change detection first, so property edits on tracked entities count.
            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                    continue;
                var area = DashboardDataAreas.ForEntity(entry.Metadata.ClrType);
                if (area is null || entry.Metadata.FindProperty("TenantId") is null)
                    continue;
                var value = entry.State == EntityState.Deleted ? entry.Property("TenantId").OriginalValue : entry.Property("TenantId").CurrentValue;
                if (value is not Guid tenantId || tenantId == Guid.Empty)
                    continue;
                if (!changes.TryGetValue(tenantId, out var areas))
                    changes[tenantId] = areas = new HashSet<string>(StringComparer.Ordinal);
                areas.Add(area);
            }
            if (changes.Count > 0)
                pending.AddOrUpdate(context, changes);
            else
                pending.Remove(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not work out which dashboard areas a save changes.");
        }
    }

    private void Publish(DbContext? context)
    {
        if (context is null || !pending.TryGetValue(context, out var changes))
            return;
        pending.Remove(context);
        try
        {
            foreach (var (tenantId, areas) in changes)
            {
                versions.Bump(tenantId, areas);
                broadcaster.Notify(tenantId, areas);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not record a dashboard data change.");
        }
    }

    private void Discard(DbContext? context)
    {
        if (context is not null)
            pending.Remove(context);
    }
}
