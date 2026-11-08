using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>Release 3 step 3c: the widget catalogue, role layouts, admin hiding and personal layouts.</summary>
public sealed class RoleDashboardTests
{
    private static readonly string[] AllModules = [.. ServiceOpsPlans.EditionModules(ServiceOpsPlans.Professional)];

    public static TheoryData<string> AllWidgets()
    {
        var data = new TheoryData<string>();
        foreach (var widget in DashboardWidgetCatalog.All)
            data.Add(widget.Key);
        return data;
    }

    public static TheoryData<string> ModuleGatedWidgets()
    {
        var data = new TheoryData<string>();
        foreach (var widget in DashboardWidgetCatalog.All.Where(x => x.Modules.Count > 0))
            data.Add(widget.Key);
        return data;
    }

    public static TheoryData<string> PermissionGatedWidgets()
    {
        var data = new TheoryData<string>();
        foreach (var widget in DashboardWidgetCatalog.All.Where(x => x.AnyPermissions.Count > 0))
            data.Add(widget.Key);
        return data;
    }

    // ── Catalogue ──────────────────────────────────────────────────────────────

    [Fact]
    public void Catalogue_EveryRoleDefaultIsAKnownWidget_AndKeysAreUnique()
    {
        Assert.Equal(DashboardWidgetCatalog.All.Count, DashboardWidgetCatalog.All.Select(x => x.Key).Distinct().Count());
        foreach (var role in DashboardRoleDefaults.ConfigurableRoles)
            Assert.All(DashboardRoleDefaults.For(role), key => Assert.NotNull(DashboardWidgetCatalog.Find(key)));
        Assert.True(ServiceOpsPlans.IsClassified("RoleDashboard"));
        Assert.Empty(ServiceOpsPlans.RequiredModules("RoleDashboard", "/api/dashboard/widgets/finance.cash-received"));
    }

    // ── Server-side enforcement ────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(ModuleGatedWidgets))]
    public async Task EachWidget_Returns403_WithoutItsModule(string key)
    {
        var widget = DashboardWidgetCatalog.Find(key)!;
        var harness = await Harness.CreateAsync(modules: AllModules.Except([widget.Modules[0]]).ToArray());
        var service = harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true);

        var error = await Assert.ThrowsAsync<LicenseException>(() => service.GetWidgetAsync(key, null, false));
        Assert.Equal(403, error.StatusCode);
        Assert.DoesNotContain((await service.GetLayoutAsync()).Available, x => x.Key == key);
    }

    [Theory]
    [MemberData(nameof(PermissionGatedWidgets))]
    public async Task EachWidget_Returns403_WithoutItsPermission(string key)
    {
        var harness = await Harness.CreateAsync();
        // A viewer with every permission except the ones this widget accepts.
        var widget = DashboardWidgetCatalog.Find(key)!;
        var granted = PermissionNames.All.Except(widget.AnyPermissions).ToArray();
        var service = harness.Service(harness.ViewerId, AppRoles.Viewer, permissions: granted);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => service.GetWidgetAsync(key, null, false));
        Assert.Equal(403, error.StatusCode);

        // With one of them, the same widget answers.
        var allowed = harness.Service(harness.ViewerId, AppRoles.Viewer, permissions: [.. granted, widget.AnyPermissions[0]]);
        Assert.Equal(key, (await allowed.GetWidgetAsync(key, null, true)).Key);
    }

    [Theory]
    [MemberData(nameof(AllWidgets))]
    public async Task EachWidget_RunsForAnAdmin_AndEveryFigureHasALink(string key)
    {
        var harness = await Harness.CreateAsync();
        await harness.SeedSampleDataAsync();
        var data = await harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true).GetWidgetAsync(key, null, false);

        Assert.Equal(key, data.Key);
        Assert.All(data.Stats, stat => Assert.StartsWith("/", stat.Link));
        Assert.All(data.Items, item => Assert.StartsWith("/", item.Link));
        Assert.True(data.Stats.Count > 0 || data.Note is not null);
    }

    [Fact]
    public async Task UnknownWidget_IsNotFound()
    {
        var harness = await Harness.CreateAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true).GetWidgetAsync("nope", null, false));
    }

    // ── Admin hiding ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminHiding_RemovesTheWidgetForThatRoleOnly_AndItsEndpointRefuses()
    {
        var harness = await Harness.CreateAsync();
        var admin = harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true);
        var settings = await admin.SetHiddenAsync("manager", [DashboardWidgetCatalog.ContractsExpiring, DashboardWidgetCatalog.ContractsExpiring]);
        Assert.Equal([DashboardWidgetCatalog.ContractsExpiring], settings.Roles.Single(x => x.Role == AppRoles.Manager).HiddenKeys);

        var manager = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: PermissionNames.All);
        var layout = await manager.GetLayoutAsync();
        Assert.DoesNotContain(layout.Items, x => x.Key == DashboardWidgetCatalog.ContractsExpiring);
        Assert.DoesNotContain(layout.Available, x => x.Key == DashboardWidgetCatalog.ContractsExpiring);
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetWidgetAsync(DashboardWidgetCatalog.ContractsExpiring, null, false));

        // Saving it into a personal layout doesn't bring it back.
        var saved = await manager.SaveLayoutAsync([new(DashboardWidgetCatalog.ContractsExpiring, "wide")]);
        Assert.Empty(saved.Items);

        // Other roles keep it.
        Assert.Contains((await admin.GetLayoutAsync()).Items, x => x.Key == DashboardWidgetCatalog.ContractsExpiring);

        // Unhiding restores it for the manager.
        await admin.SetHiddenAsync(AppRoles.Manager, []);
        Assert.Equal(DashboardWidgetCatalog.ContractsExpiring, (await manager.GetWidgetAsync(DashboardWidgetCatalog.ContractsExpiring, null, false)).Key);
    }

    [Fact]
    public async Task AdminSettings_AreAdminOnly_AndRejectUnknownKeysAndRoles()
    {
        var harness = await Harness.CreateAsync();
        var manager = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: PermissionNames.All);
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetAdminSettingsAsync());
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.SetHiddenAsync(AppRoles.Manager, []));

        var admin = harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true);
        await Assert.ThrowsAsync<BusinessRuleException>(() => admin.SetHiddenAsync(AppRoles.Manager, ["not-a-widget"]));
        await Assert.ThrowsAsync<BusinessRuleException>(() => admin.SetHiddenAsync("Owner", []));
        var settings = await admin.GetAdminSettingsAsync();
        Assert.Equal(DashboardRoleDefaults.ConfigurableRoles, settings.Roles.Select(x => x.Role));
    }

    // ── Personal layouts ───────────────────────────────────────────────────────

    [Fact]
    public async Task RoleDefault_IsFilteredByModuleAndPermission()
    {
        var harness = await Harness.CreateAsync(modules: [ServiceOpsPlans.Operations, ServiceOpsPlans.Maintenance, ServiceOpsPlans.Inventory]);
        var dispatcher = harness.Service(harness.DispatcherId, AppRoles.Dispatcher,
            permissions: [PermissionNames.CanViewWorkOrders, PermissionNames.CanAssignWorkOrders]);
        var layout = await dispatcher.GetLayoutAsync();
        Assert.False(layout.IsCustomized);
        Assert.Equal(DashboardRoleDefaults.For(AppRoles.Dispatcher), layout.Items.Select(x => x.Key));

        // A viewer gets the manager widgets they have permission for, and nothing from a missing module.
        var viewer = harness.Service(harness.ViewerId, AppRoles.User, permissions: [PermissionNames.CanViewWorkOrders]);
        var viewerLayout = await viewer.GetLayoutAsync();
        Assert.Equal(AppRoles.Viewer, viewerLayout.Role);
        Assert.Equal(
            [DashboardWidgetCatalog.WorkOrdersOpenOverdue, DashboardWidgetCatalog.WorkOrdersSlaAtRisk, DashboardWidgetCatalog.MaintenancePmCompliance, DashboardWidgetCatalog.ContractsExpiring],
            viewerLayout.Items.Select(x => x.Key));
    }

    [Fact]
    public async Task SavedLayout_CannotIncludeAWidgetTheUserIsNotAllowed()
    {
        var harness = await Harness.CreateAsync();
        var dispatcher = harness.Service(harness.DispatcherId, AppRoles.Dispatcher,
            permissions: [PermissionNames.CanViewWorkOrders, PermissionNames.CanAssignWorkOrders]);

        var saved = await dispatcher.SaveLayoutAsync(
        [
            new(DashboardWidgetCatalog.TechniciansOnSite, "wide"),
            new(DashboardWidgetCatalog.PurchaseOrdersOpen, "small"),   // no materials.view
            new("made-up", "small"),
            new(DashboardWidgetCatalog.TechniciansOnSite, "small"),     // duplicate
            new(DashboardWidgetCatalog.WorkOrdersUnassigned, "huge"),   // bad size → default
        ]);

        Assert.True(saved.IsCustomized);
        Assert.Equal([new DashboardLayoutItem(DashboardWidgetCatalog.TechniciansOnSite, "wide"), new DashboardLayoutItem(DashboardWidgetCatalog.WorkOrdersUnassigned, "small")], saved.Items);
        var stored = DashboardLayoutRules.Parse((await harness.Db.UserDashboardLayouts.SingleAsync()).LayoutJson)!;
        Assert.DoesNotContain(stored.Items, x => x.Key == DashboardWidgetCatalog.PurchaseOrdersOpen);
        // The layout can't open the widget either.
        await Assert.ThrowsAsync<ForbiddenException>(() => dispatcher.GetWidgetAsync(DashboardWidgetCatalog.PurchaseOrdersOpen, null, false));
    }

    [Fact]
    public async Task WidgetsTheUserLosesAccessTo_DropOutOfTheirLayout()
    {
        var harness = await Harness.CreateAsync();
        var before = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: PermissionNames.All);
        await before.SaveLayoutAsync([new(DashboardWidgetCatalog.PurchaseOrdersOpen, "small"), new(DashboardWidgetCatalog.ContractsExpiring, "small")]);

        var after = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: [PermissionNames.CanViewWorkOrders]);
        var layout = await after.GetLayoutAsync();
        Assert.DoesNotContain(layout.Items, x => x.Key == DashboardWidgetCatalog.PurchaseOrdersOpen);
        Assert.Equal(DashboardWidgetCatalog.ContractsExpiring, layout.Items[0].Key);
    }

    [Fact]
    public async Task Reset_RestoresTheRoleDefault()
    {
        var harness = await Harness.CreateAsync();
        var manager = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: PermissionNames.All);
        var original = await manager.GetLayoutAsync();

        await manager.SaveLayoutAsync([new(DashboardWidgetCatalog.MaterialsLowStock, "wide")]);
        Assert.Equal([DashboardWidgetCatalog.MaterialsLowStock], (await manager.GetLayoutAsync()).Items.Select(x => x.Key));

        var reset = await manager.ResetLayoutAsync();
        Assert.False(reset.IsCustomized);
        Assert.Equal(original.Items, reset.Items);
        Assert.False(await harness.Db.UserDashboardLayouts.AnyAsync());
    }

    [Fact]
    public async Task NewRoleDefaults_AppearAtTheEndOnce()
    {
        var harness = await Harness.CreateAsync();
        var manager = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: PermissionNames.All);
        var defaults = DashboardRoleDefaults.For(AppRoles.Manager);
        // A layout saved before "contracts expiring" joined the manager default.
        harness.Db.UserDashboardLayouts.Add(new UserDashboardLayout
        {
            TenantId = harness.TenantId,
            UserId = harness.ManagerId,
            LayoutJson = DashboardLayoutRules.Serialize(new StoredDashboardLayout
            {
                Items = [new(DashboardWidgetCatalog.MaterialsLowStock, "small")],
                SeenDefaults = [.. defaults.Where(x => x != DashboardWidgetCatalog.ContractsExpiring)],
            }),
        });
        await harness.Db.SaveChangesAsync();

        var layout = await manager.GetLayoutAsync();
        Assert.Equal([DashboardWidgetCatalog.MaterialsLowStock, DashboardWidgetCatalog.ContractsExpiring], layout.Items.Select(x => x.Key));

        // The user removes it; it does not come back.
        await manager.SaveLayoutAsync([new(DashboardWidgetCatalog.MaterialsLowStock, "small")]);
        Assert.Equal([DashboardWidgetCatalog.MaterialsLowStock], (await manager.GetLayoutAsync()).Items.Select(x => x.Key));
    }

    [Fact]
    public void LayoutRules_HiddenDefaultsAreNotMarkedSeen_SoUnhidingOffersThemAgain()
    {
        var defaults = new[] { DashboardWidgetCatalog.ContractsExpiring, DashboardWidgetCatalog.MaterialsLowStock };
        var allowed = new HashSet<string> { DashboardWidgetCatalog.MaterialsLowStock };
        var stored = DashboardLayoutRules.Sanitize([new(DashboardWidgetCatalog.MaterialsLowStock, "small")], null, defaults, allowed);
        Assert.Equal([DashboardWidgetCatalog.MaterialsLowStock], stored.SeenDefaults);

        allowed.Add(DashboardWidgetCatalog.ContractsExpiring);
        Assert.Equal([DashboardWidgetCatalog.MaterialsLowStock, DashboardWidgetCatalog.ContractsExpiring],
            DashboardLayoutRules.Resolve(stored, defaults, allowed).Select(x => x.Key));
        Assert.Null(DashboardLayoutRules.Parse("not json"));
    }

    // ── Scope and cache ────────────────────────────────────────────────────────

    [Fact]
    public async Task BranchScoping_FollowsTheUsersBranchAccess()
    {
        var harness = await Harness.CreateAsync();
        var (north, south) = await harness.AddBranchesAsync();
        harness.Db.UserBranchAssignments.Add(new UserBranchAssignment { TenantId = harness.TenantId, UserId = harness.ManagerId, BranchId = north, IsDefault = true });
        harness.AddWorkOrder(north, "WO-N1");
        harness.AddWorkOrder(north, "WO-N2", overdue: true);
        harness.AddWorkOrder(south, "WO-S1", overdue: true);
        await harness.Db.SaveChangesAsync();

        var manager = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: PermissionNames.All);
        var mine = await manager.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, false);
        Assert.Equal([2m, 1m], mine.Stats.Select(x => x.Value));
        Assert.Equal("/work-orders?state=overdue", mine.Stats[1].Link);
        await Assert.ThrowsAsync<ForbiddenException>(() => manager.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, south, false));

        var admin = harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true);
        Assert.Equal([3m, 2m], (await admin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, false)).Stats.Select(x => x.Value));
        Assert.Equal([1m, 1m], (await admin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, south, false)).Stats.Select(x => x.Value));
    }

    [Fact]
    public async Task Results_AreCachedPerScope_AndRefreshRecomputes()
    {
        var harness = await Harness.CreateAsync();
        harness.AddWorkOrder(null, "WO-1");
        await harness.Db.SaveChangesAsync();
        var admin = harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true);
        Assert.Equal(1m, (await admin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, false)).Stats[0].Value);

        // A write this process never saw (another instance, a bulk update) leaves the cache as it was.
        await using (var elsewhere = harness.UnwatchedDb())
        {
            elsewhere.WorkOrders.Add(new WorkOrder { TenantId = harness.TenantId, ClientId = harness.ClientId, WorkOrderNumber = "WO-2", Title = "Leak", Priority = "Low", Status = "Open" });
            await elsewhere.SaveChangesAsync();
        }
        Assert.Equal(1m, (await admin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, false)).Stats[0].Value);
        Assert.Equal(2m, (await admin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, true)).Stats[0].Value);
    }

    [Fact]
    public void WorkOrderListStateFilter_MatchesWhatTheDashboardCounts()
    {
        var now = DateTime.UtcNow;
        var rows = new[]
        {
            new WorkOrder { WorkOrderNumber = "open", Status = "Open", DueDate = now.AddDays(1) },
            new WorkOrder { WorkOrderNumber = "late", Status = "In Progress", DueDate = now.AddDays(-1) },
            new WorkOrder { WorkOrderNumber = "done-late", Status = "Completed", DueDate = now.AddDays(-1) },
            new WorkOrder { WorkOrderNumber = "cancelled", Status = "Cancelled" },
        }.AsQueryable();

        Assert.Equal(["open", "late"], WorkOrderListStates.Apply(rows, "open", now).Select(x => x.WorkOrderNumber));
        Assert.Equal(["late"], WorkOrderListStates.Apply(rows, " Overdue ", now).Select(x => x.WorkOrderNumber));
        Assert.Throws<BusinessRuleException>(() => WorkOrderListStates.Apply(rows, "stale", now));
    }

    // ── Change-driven invalidation and live push ──────────────────────────────

    [Fact]
    public void Catalogue_EveryWidgetReadsAKnownArea()
    {
        string[] known = [DashboardDataAreas.WorkOrders, DashboardDataAreas.Maintenance, DashboardDataAreas.Contracts,
            DashboardDataAreas.Materials, DashboardDataAreas.Activities];
        Assert.All(DashboardWidgetCatalog.All, widget => Assert.NotEmpty(widget.Areas));
        Assert.All(DashboardWidgetCatalog.All.SelectMany(x => x.Areas), area => Assert.Contains(area, known));
    }

    [Fact]
    public async Task WorkOrderWrite_RecomputesWorkOrderWidgets_ForThatTenantOnly()
    {
        var first = await Harness.CreateAsync();
        var second = await Harness.SecondTenantAsync(first);
        first.AddWorkOrder(null, "WO-1");
        second.AddWorkOrder(null, "WO-1");
        await first.Db.SaveChangesAsync();
        await second.Db.SaveChangesAsync();

        var firstAdmin = first.Service(first.AdminId, AppRoles.Admin, isAdmin: true);
        var secondAdmin = second.Service(second.AdminId, AppRoles.Admin, isAdmin: true);
        var firstBefore = await firstAdmin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, false);
        var secondBefore = await secondAdmin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, false);
        var contractsBefore = await firstAdmin.GetWidgetAsync(DashboardWidgetCatalog.ContractsExpiring, null, false);
        Assert.Equal(1, firstBefore.Stats.Single(x => x.Label == "Open").Value);

        // A new work order for the first tenant, saved through any context (a service, a worker).
        first.AddWorkOrder(null, "WO-2", overdue: true);
        await first.Db.SaveChangesAsync();

        var firstAfter = await firstAdmin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, false);
        Assert.Equal(2, firstAfter.Stats.Single(x => x.Label == "Open").Value);
        Assert.Equal(1, firstAfter.Stats.Single(x => x.Label == "Overdue").Value);
        // The other tenant's entry and the first tenant's unrelated widget are still served from cache.
        Assert.Same(secondBefore, await secondAdmin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, false));
        Assert.Same(contractsBefore, await firstAdmin.GetWidgetAsync(DashboardWidgetCatalog.ContractsExpiring, null, false));

        // The broadcast names the area and goes to the first tenant only.
        var sent = await WaitForBroadcastsAsync(first.Singletons.Notifier, x => x.TenantId == first.TenantId, 1);
        Assert.Contains(sent, x => x.Areas.SequenceEqual([DashboardDataAreas.WorkOrders]));
        Assert.All(first.Singletons.Notifier.Snapshot().Where(x => x.TenantId == second.TenantId), x => Assert.Equal([DashboardDataAreas.WorkOrders], x.Areas));
    }

    [Fact]
    public async Task UnrelatedWrite_KeepsTheCachedWidget()
    {
        var harness = await Harness.CreateAsync();
        harness.AddWorkOrder(null, "WO-1");
        await harness.Db.SaveChangesAsync();
        var admin = harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true);
        var before = await admin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, false);
        var version = harness.Singletons.Versions.Get(harness.TenantId, DashboardDataAreas.WorkOrders);
        await WaitForBroadcastsAsync(harness.Singletons.Notifier, x => x.TenantId == harness.TenantId, 1);

        // A client, an audit row and a layout are no dashboard area: nothing is bumped or announced.
        harness.Db.Clients.Single(x => x.Id == harness.ClientId).ClientName = "Acme Mall Ltd";
        harness.Db.AuditLogs.Add(new AuditLog { TenantId = harness.TenantId, Action = "test", EntityName = "Test", EntityId = "1" });
        harness.Db.UserDashboardLayouts.Add(new UserDashboardLayout { TenantId = harness.TenantId, UserId = harness.AdminId, LayoutJson = "{}" });
        await harness.Db.SaveChangesAsync();
        await Task.Delay(100);
        Assert.Single(harness.Singletons.Notifier.Snapshot());

        // A contract is another area: only its own widgets recompute.
        harness.Db.Contracts.Add(new Contract { TenantId = harness.TenantId, ClientId = harness.ClientId, ContractNumber = "C-1", Name = "Mall HVAC", EndDate = DateTime.UtcNow.AddDays(10) });
        await harness.Db.SaveChangesAsync();

        Assert.Same(before, await admin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, false));
        Assert.Equal(version, harness.Singletons.Versions.Get(harness.TenantId, DashboardDataAreas.WorkOrders));
        Assert.Equal(1, harness.Singletons.Versions.Get(harness.TenantId, DashboardDataAreas.Contracts));
        Assert.Equal(0, harness.Singletons.Versions.Get(harness.TenantId, DashboardDataAreas.Materials));
    }

    [Fact]
    public async Task Broadcasts_AreThrottledPerTenant_WithATrailingSend()
    {
        var notifier = new RecordingDashboardNotifier();
        var broadcaster = new DashboardChangeBroadcaster(notifier, NullLogger<DashboardChangeBroadcaster>.Instance) { Interval = TimeSpan.FromMilliseconds(300) };
        var busy = Guid.NewGuid();
        var quiet = Guid.NewGuid();

        broadcaster.Notify(busy, [DashboardDataAreas.WorkOrders]);
        await WaitForBroadcastsAsync(notifier, x => x.TenantId == busy, 1);
        broadcaster.Notify(busy, [DashboardDataAreas.Activities]);
        broadcaster.Notify(busy, [DashboardDataAreas.Contracts, DashboardDataAreas.Activities]);
        broadcaster.Notify(quiet, [DashboardDataAreas.Materials]);

        // The other tenant isn't held back by the busy one's window.
        var quietSent = await WaitForBroadcastsAsync(notifier, x => x.TenantId == quiet, 1);
        Assert.Equal([DashboardDataAreas.Materials], quietSent.Single().Areas);
        Assert.Single(notifier.Snapshot(), x => x.TenantId == busy);

        // The changes inside the window go out together when it ends.
        var busySent = await WaitForBroadcastsAsync(notifier, x => x.TenantId == busy, 2);
        Assert.Equal([DashboardDataAreas.WorkOrders], busySent[0].Areas);
        Assert.Equal([DashboardDataAreas.Activities, DashboardDataAreas.Contracts], busySent[1].Areas);
    }

    [Fact]
    public async Task FailingBroadcast_NeverAffectsTheSave()
    {
        var harness = await Harness.CreateAsync(singletons: new DashboardSingletons(failingNotifier: true));
        harness.AddWorkOrder(null, "WO-1");
        await harness.Db.SaveChangesAsync();
        harness.AddWorkOrder(null, "WO-2");
        harness.Db.SaveChanges();

        Assert.Equal(2, await harness.Db.WorkOrders.CountAsync(x => x.TenantId == harness.TenantId));
        Assert.Equal(2, harness.Singletons.Versions.Get(harness.TenantId, DashboardDataAreas.WorkOrders));
    }

    private static async Task<(Guid TenantId, string[] Areas)[]> WaitForBroadcastsAsync(
        RecordingDashboardNotifier notifier, Func<(Guid TenantId, string[] Areas), bool> match, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            var sent = notifier.Snapshot().Where(match).ToArray();
            if (sent.Length >= count || DateTime.UtcNow > deadline)
            {
                Assert.True(sent.Length >= count, $"Expected {count} broadcasts, saw {sent.Length}.");
                return sent;
            }
            await Task.Delay(20);
        }
    }

    [Fact]
    public void PurchaseOrderStateFilter_MatchesWhatTheDashboardCounts()
    {
        var today = new DateTime(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc);
        var rows = new[]
        {
            new PurchaseOrder { PoNumber = "draft-late", Status = "Draft", ExpectedAt = today.AddDays(-5) },
            new PurchaseOrder { PoNumber = "sent-late", Status = "Sent", ExpectedAt = today.AddDays(-1) },
            new PurchaseOrder { PoNumber = "partial", Status = "PartiallyReceived", ExpectedAt = today.AddDays(2) },
            new PurchaseOrder { PoNumber = "received", Status = "Received", ExpectedAt = today.AddDays(-9) },
            new PurchaseOrder { PoNumber = "cancelled", Status = "Cancelled" },
        }.AsQueryable();

        Assert.Equal(["draft-late", "sent-late", "partial"], PurchaseOrderListStates.Apply(rows, "open", today).Select(x => x.PoNumber));
        Assert.Equal(["sent-late"], PurchaseOrderListStates.Apply(rows, "Late", today).Select(x => x.PoNumber));
        Assert.Throws<BusinessRuleException>(() => PurchaseOrderListStates.Apply(rows, "overdue", today));
    }

    [Fact]
    public async Task WidgetLinks_OpenFilteredLists()
    {
        var harness = await Harness.CreateAsync();
        await harness.SeedSampleDataAsync();
        var admin = harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true);
        async Task<string> Link(string key, string label) =>
            (await admin.GetWidgetAsync(key, null, true)).Stats.First(x => x.Label == label).Link;

        Assert.Equal("/inventory/purchase-orders?state=late", await Link(DashboardWidgetCatalog.PurchaseOrdersOpen, "Past expected date"));
        Assert.Equal("/inventory/requests?stage=Requested", await Link(DashboardWidgetCatalog.MaterialsPendingIssues, "Awaiting approval"));
        Assert.Equal("/contracts?expiring=30", await Link(DashboardWidgetCatalog.ContractsExpiring, "Within 30 days"));
    }

    [Fact]
    public async Task ActionItemsWidget_CountsOnlyTheCallersOpenItems_ByDueDate()
    {
        var harness = await Harness.CreateAsync(modules: []);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        void Add(string title, Guid? assignee, DateOnly? due, string status = ActionItemStatuses.Open) =>
            harness.Db.ActionItems.Add(new ActionItem { TenantId = harness.TenantId, Title = title, AssigneeUserId = assignee, DueDate = due, Status = status });
        Add("Late", harness.ManagerId, today.AddDays(-2));
        Add("Soon", harness.ManagerId, today.AddDays(3));
        Add("Later", harness.ManagerId, today.AddDays(30));
        Add("No date", harness.ManagerId, null);
        Add("Done late", harness.ManagerId, today.AddDays(-5), ActionItemStatuses.Done);
        Add("Someone else's", harness.ViewerId, today.AddDays(-1));
        await harness.Db.SaveChangesAsync();

        // Shared: no module needed, only activities.view.
        var manager = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: [PermissionNames.ActivitiesView]);
        var data = await manager.GetWidgetAsync(DashboardWidgetCatalog.ActionItemsDue, null, true);
        decimal Stat(string label) => data.Stats.Single(x => x.Label == label).Value;
        Assert.Equal(1, Stat("Overdue"));
        Assert.Equal(1, Stat("Due this week"));
        Assert.Equal(4, Stat("Open"));
        Assert.Equal("/action-items?due=overdue", data.Stats.Single(x => x.Label == "Overdue").Link);
        Assert.Equal(["Late", "Soon"], data.Items.Select(x => x.Label));

        // It's in the finance user's default layout.
        Assert.Contains(DashboardWidgetCatalog.ActionItemsDue, DashboardRoleDefaults.For(AppRoles.FinanceUser));
    }

    // ── PostgreSQL ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task Postgres_EveryWidgetQuery_Translates()
    {
        await WithPostgresAsync(async connection =>
        {
            var harness = await Harness.CreateAsync((context, interceptor) => PostgresContext(connection, context, interceptor), seedTenant: true);
            var (north, _) = await harness.AddBranchesAsync();
            harness.Db.UserBranchAssignments.Add(new UserBranchAssignment { TenantId = harness.TenantId, UserId = harness.ManagerId, BranchId = north, IsDefault = true });
            await harness.Db.SaveChangesAsync();
            await harness.SeedSampleDataAsync();

            var admin = harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true);
            var manager = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: PermissionNames.All);
            foreach (var widget in DashboardWidgetCatalog.All)
            {
                Assert.Equal(widget.Key, (await admin.GetWidgetAsync(widget.Key, null, true)).Key);
                Assert.Equal(widget.Key, (await admin.GetWidgetAsync(widget.Key, north, true)).Key);
                Assert.Equal(widget.Key, (await manager.GetWidgetAsync(widget.Key, null, true)).Key);
            }

            var saved = await manager.SaveLayoutAsync([new(DashboardWidgetCatalog.ContractsExpiring, "small")]);
            Assert.Equal(DashboardWidgetCatalog.ContractsExpiring, saved.Items[0].Key);
            Assert.True((await manager.GetLayoutAsync()).IsCustomized);
            Assert.False((await manager.ResetLayoutAsync()).IsCustomized);
            await admin.SetHiddenAsync(AppRoles.Manager, [DashboardWidgetCatalog.ContractsExpiring]);
            Assert.Equal([DashboardWidgetCatalog.ContractsExpiring], (await admin.GetAdminSettingsAsync()).Roles.Single(x => x.Role == AppRoles.Manager).HiddenKeys);
        });
    }

    // ── Harness ────────────────────────────────────────────────────────────────

    /// <summary>Records broadcasts; optionally fails every one.</summary>
    private sealed class RecordingDashboardNotifier(bool fail = false) : IDashboardNotifier
    {
        public readonly List<(Guid TenantId, string[] Areas)> Sent = [];

        public Task DashboardChangedAsync(Guid tenantId, IReadOnlyCollection<string> areas, CancellationToken cancellationToken = default)
        {
            if (fail)
                throw new InvalidOperationException("Hub down.");
            lock (Sent)
                Sent.Add((tenantId, [.. areas]));
            return Task.CompletedTask;
        }

        public (Guid TenantId, string[] Areas)[] Snapshot()
        {
            lock (Sent)
                return [.. Sent];
        }
    }

    private sealed class NoDashboardInterceptor : SaveChangesInterceptor;

    /// <summary>The process-wide pieces: one widget cache, one set of data versions, one interceptor.</summary>
    private sealed class DashboardSingletons
    {
        public readonly IMemoryCache Cache = new MemoryCache(new MemoryCacheOptions());
        public readonly DashboardDataVersions Versions = new();
        public readonly RecordingDashboardNotifier Notifier;
        public readonly DashboardChangeInterceptor Interceptor;

        public DashboardSingletons(bool failingNotifier = false)
        {
            Notifier = new RecordingDashboardNotifier(failingNotifier);
            var broadcaster = new DashboardChangeBroadcaster(Notifier, NullLogger<DashboardChangeBroadcaster>.Instance);
            Interceptor = new DashboardChangeInterceptor(Versions, broadcaster, NullLogger<DashboardChangeInterceptor>.Instance);
        }
    }

    private sealed class Harness
    {
        private Func<ITenantContext?, AppDbContext> createContext = null!;
        private Func<ITenantContext?, IInterceptor, AppDbContext> createWithInterceptor = null!;
        public DashboardSingletons Singletons { get; private set; } = null!;
        private IMemoryCache cache => Singletons.Cache;
        public AppDbContext Db { get; private set; } = null!;
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid AdminId { get; private set; }
        public Guid ManagerId { get; private set; }
        public Guid DispatcherId { get; private set; }
        public Guid ViewerId { get; private set; }
        public Guid ClientId { get; private set; }
        public IReadOnlyCollection<string> Modules { get; private set; } = AllModules;

        public static Task<Harness> CreateAsync(string[]? modules = null, DashboardSingletons? singletons = null)
        {
            var databaseName = $"ecosys-dashboards-{Guid.NewGuid():N}";
            return CreateAsync((context, interceptor) => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).AddInterceptors(interceptor).Options, context),
                seedTenant: false, modules, singletons);
        }

        /// <summary>A second tenant in the same database and process as <paramref name="other"/>.</summary>
        public static Task<Harness> SecondTenantAsync(Harness other) =>
            CreateAsync(other.createWithInterceptor, seedTenant: false, null, other.Singletons);

        public static async Task<Harness> CreateAsync(Func<ITenantContext?, IInterceptor, AppDbContext> createContext, bool seedTenant, string[]? modules = null, DashboardSingletons? singletons = null)
        {
            var shared = singletons ?? new DashboardSingletons();
            var harness = new Harness
            {
                createWithInterceptor = createContext, createContext = context => createContext(context, shared.Interceptor),
                Singletons = shared, Modules = modules ?? AllModules,
            };
            harness.Db = harness.createContext(null);
            if (seedTenant)
            {
                harness.Db.Tenants.Add(TestTenant(harness.TenantId));
                await harness.Db.SaveChangesAsync();
            }
            harness.AdminId = await harness.AddUserAsync("Ada Admin", AppRoles.Admin);
            harness.ManagerId = await harness.AddUserAsync("Mo Manager", AppRoles.Manager);
            harness.DispatcherId = await harness.AddUserAsync("Dee Dispatcher", AppRoles.Dispatcher);
            harness.ViewerId = await harness.AddUserAsync("Vic Viewer", AppRoles.Viewer);
            var client = new Client { TenantId = harness.TenantId, ClientName = "Acme Mall", IsActive = true };
            harness.Db.Clients.Add(client);
            await harness.Db.SaveChangesAsync();
            harness.ClientId = client.Id;
            return harness;
        }

        private async Task<Guid> AddUserAsync(string name, string role)
        {
            var user = new User { TenantId = TenantId, FullName = name, Email = $"{name.Split(' ')[0].ToLowerInvariant()}@acme.test", Role = role, IsActive = true };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            return user.Id;
        }

        /// <summary>A context without the dashboard interceptor.</summary>
        public AppDbContext UnwatchedDb() => createWithInterceptor(null, new NoDashboardInterceptor());

        public IRoleDashboardService Service(Guid userId, string role, bool isAdmin = false, IReadOnlyCollection<string>? permissions = null)
        {
            var context = new FakeTenantContext(TenantId, userId, role, isAdmin, permissions ?? []);
            var db = createContext(context);
            return new RoleDashboardService(db, context, new UserAccessService(context), new BranchAccessService(db, context),
                new FixedModules(Modules), new DashboardSettingsService(db),
                new RoleDashboardQueries(db, TestTimeZones.Create(db)), cache, Singletons.Versions);
        }

        public async Task<(Guid North, Guid South)> AddBranchesAsync()
        {
            var north = new Branch { TenantId = TenantId, Name = "North", Code = "N", IsActive = true };
            var south = new Branch { TenantId = TenantId, Name = "South", Code = "S", IsActive = true };
            Db.Branches.AddRange(north, south);
            await Db.SaveChangesAsync();
            return (north.Id, south.Id);
        }

        public WorkOrder AddWorkOrder(Guid? branchId, string number, bool overdue = false)
        {
            var workOrder = new WorkOrder
            {
                TenantId = TenantId, BranchId = branchId, ClientId = ClientId, WorkOrderNumber = number, Title = "Chiller trip",
                Priority = "High", Status = "Open", DueDate = overdue ? DateTime.UtcNow.AddDays(-2) : DateTime.UtcNow.AddDays(2),
            };
            Db.WorkOrders.Add(workOrder);
            return workOrder;
        }

        /// <summary>A little of everything the widgets read, so their in-memory steps run too.</summary>
        public async Task SeedSampleDataAsync()
        {
            var overdue = AddWorkOrder(null, "WO-100", overdue: true);
            overdue.SlaResolutionDeadline = DateTime.UtcNow.AddMinutes(30);
            overdue.ScheduledStartAt = DateTime.UtcNow;
            var technician = new Technician { TenantId = TenantId, FullName = "Tess Tech", IsActive = true };
            Db.Technicians.Add(technician);
            Db.WorkOrderTechnicianAssignments.Add(new WorkOrderTechnicianAssignment
            {
                TenantId = TenantId, WorkOrderId = overdue.Id, TechnicianId = technician.Id, Status = "Accepted", ArrivalAt = DateTime.UtcNow.AddMinutes(-20),
            });
            AddWorkOrder(null, "WO-101");
            Db.MaterialItems.Add(new MaterialItem { TenantId = TenantId, ItemCode = "FLT-1", ItemName = "Filter", UnitOfMeasure = "pc", QuantityOnHand = 1, ReorderLevel = 5, IsActive = true });
            await Db.SaveChangesAsync();
        }
    }

    private sealed class FixedModules(IReadOnlyCollection<string> modules) : ITenantModuleResolver
    {
        public Task<IReadOnlyCollection<string>> GetModulesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(modules);
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId, string role, bool isAdmin, IReadOnlyCollection<string> permissions) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public string? Email => null;
        public string? Role => role;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => isAdmin;
        public bool HasRole(string value) => string.Equals(value, role, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => permissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase);
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => throw new NotSupportedException();
    }

    private static Tenant TestTenant(Guid id) => new()
    {
        Id = id, Name = "Tenant", Slug = $"t-{id:N}"[..14], CompanyName = "Tenant Co", Email = $"{id:N}@tenant.test", Country = "Kenya",
        Status = "Active", LicenseStatus = "Active", PrimaryColor = "#0F4C81", SecondaryColor = "#F4B942", IsActive = true,
    };

    private static AppDbContext PostgresContext(string connection, ITenantContext? tenantContext, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection);
        if (interceptor is not null)
            options.AddInterceptors(interceptor);
        return new(options.Options, tenantContext);
    }

    /// <summary>
    /// A throwaway database built from the model (not the migrations), so this suite does not depend on
    /// the migration that adds <c>user_dashboard_layouts</c>.
    /// </summary>
    private static async Task WithPostgresAsync(Func<string, Task> body)
    {
        var server = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_dashboards_{Guid.NewGuid():N}";
        var database = new NpgsqlConnectionStringBuilder(server) { Database = databaseName, Pooling = false }.ConnectionString;
        var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres", Pooling = false }.ConnectionString;

        await ExecuteAdminAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using (var setup = PostgresContext(database, null))
                await setup.Database.EnsureCreatedAsync();
            await body(database);
        }
        finally
        {
            await ExecuteAdminAsync(admin, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
