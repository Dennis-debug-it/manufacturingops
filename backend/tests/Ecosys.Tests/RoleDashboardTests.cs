using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>Release 3 step 3c: the widget catalogue, role layouts, admin hiding and personal layouts.</summary>
public sealed class RoleDashboardTests
{
    private static readonly string[] AllModules = [ServiceOpsPlans.Operations, ServiceOpsPlans.Finance, ServiceOpsPlans.Projects];

    public static TheoryData<string> AllWidgets()
    {
        var data = new TheoryData<string>();
        foreach (var widget in DashboardWidgetCatalog.All)
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
            Assert.All(DashboardRoleDefaults.For(role, isProjectManager: true), key => Assert.NotNull(DashboardWidgetCatalog.Find(key)));
        Assert.True(ServiceOpsPlans.IsClassified("RoleDashboard"));
        Assert.Empty(ServiceOpsPlans.RequiredModules("RoleDashboard", "/api/dashboard/widgets/finance.cash-received"));
    }

    // ── Server-side enforcement ────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AllWidgets))]
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

    [Fact]
    public async Task AdminHidingForProjectManagers_AppliesToPeopleRunningAProject()
    {
        var harness = await Harness.CreateAsync();
        await harness.AddProjectAsync(harness.ManagerId);
        var admin = harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true);
        var manager = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: PermissionNames.All);

        var keys = (await manager.GetLayoutAsync()).Items.Select(x => x.Key).ToList();
        Assert.Contains(DashboardWidgetCatalog.ProjectsMine, keys);
        Assert.Contains(DashboardWidgetCatalog.ProjectsMusterToday, keys);

        await admin.SetHiddenAsync(DashboardRoleDefaults.ProjectManager, [DashboardWidgetCatalog.ProjectsMusterToday]);
        Assert.DoesNotContain((await manager.GetLayoutAsync()).Items, x => x.Key == DashboardWidgetCatalog.ProjectsMusterToday);
        // An admin who runs no project keeps it.
        Assert.Equal(DashboardWidgetCatalog.ProjectsMusterToday, (await admin.GetWidgetAsync(DashboardWidgetCatalog.ProjectsMusterToday, null, false)).Key);
    }

    // ── Personal layouts ───────────────────────────────────────────────────────

    [Fact]
    public async Task RoleDefault_IsFilteredByModuleAndPermission()
    {
        var harness = await Harness.CreateAsync(modules: [ServiceOpsPlans.Operations]);
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
            new(DashboardWidgetCatalog.FinanceCashReceived, "small"),   // no finance.view
            new("made-up", "small"),
            new(DashboardWidgetCatalog.TechniciansOnSite, "small"),     // duplicate
            new(DashboardWidgetCatalog.WorkOrdersUnassigned, "huge"),   // bad size → default
        ]);

        Assert.True(saved.IsCustomized);
        Assert.Equal([new DashboardLayoutItem(DashboardWidgetCatalog.TechniciansOnSite, "wide"), new DashboardLayoutItem(DashboardWidgetCatalog.WorkOrdersUnassigned, "small")], saved.Items);
        var stored = DashboardLayoutRules.Parse((await harness.Db.UserDashboardLayouts.SingleAsync()).LayoutJson)!;
        Assert.DoesNotContain(stored.Items, x => x.Key == DashboardWidgetCatalog.FinanceCashReceived);
        // The layout can't open the widget either.
        await Assert.ThrowsAsync<ForbiddenException>(() => dispatcher.GetWidgetAsync(DashboardWidgetCatalog.FinanceCashReceived, null, false));
    }

    [Fact]
    public async Task WidgetsTheUserLosesAccessTo_DropOutOfTheirLayout()
    {
        var harness = await Harness.CreateAsync();
        var before = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: PermissionNames.All);
        await before.SaveLayoutAsync([new(DashboardWidgetCatalog.FinanceCashReceived, "small"), new(DashboardWidgetCatalog.ContractsExpiring, "small")]);

        var after = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: [PermissionNames.CanViewWorkOrders]);
        var layout = await after.GetLayoutAsync();
        Assert.DoesNotContain(layout.Items, x => x.Key == DashboardWidgetCatalog.FinanceCashReceived);
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
        // A layout saved before "receivables overdue" joined the manager default.
        harness.Db.UserDashboardLayouts.Add(new UserDashboardLayout
        {
            TenantId = harness.TenantId,
            UserId = harness.ManagerId,
            LayoutJson = DashboardLayoutRules.Serialize(new StoredDashboardLayout
            {
                Items = [new(DashboardWidgetCatalog.MaterialsLowStock, "small")],
                SeenDefaults = [.. defaults.Where(x => x != DashboardWidgetCatalog.FinanceReceivablesOverdue)],
            }),
        });
        await harness.Db.SaveChangesAsync();

        var layout = await manager.GetLayoutAsync();
        Assert.Equal([DashboardWidgetCatalog.MaterialsLowStock, DashboardWidgetCatalog.FinanceReceivablesOverdue], layout.Items.Select(x => x.Key));

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

        harness.AddWorkOrder(null, "WO-2");
        await harness.Db.SaveChangesAsync();
        Assert.Equal(1m, (await admin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, false)).Stats[0].Value);
        Assert.Equal(2m, (await admin.GetWidgetAsync(DashboardWidgetCatalog.WorkOrdersOpenOverdue, null, true)).Stats[0].Value);
    }

    [Fact]
    public async Task ProjectWidgets_HonourMembersOnlyVisibility()
    {
        var harness = await Harness.CreateAsync();
        await harness.AddProjectAsync(harness.ManagerId, behind: true);
        harness.Db.TenantModuleSettings.Add(new TenantModuleSetting { TenantId = harness.TenantId, ModuleKey = "projects", ValueJson = "{\"visibility\":\"MembersOnly\"}" });
        await harness.Db.SaveChangesAsync();

        var outsider = harness.Service(harness.ViewerId, AppRoles.Viewer, permissions: [PermissionNames.ProjectsView]);
        Assert.Equal(0m, (await outsider.GetWidgetAsync(DashboardWidgetCatalog.ProjectsBehindSchedule, null, false)).Stats[0].Value);
        var pm = harness.Service(harness.ManagerId, AppRoles.Viewer, permissions: [PermissionNames.ProjectsView]);
        var behind = await pm.GetWidgetAsync(DashboardWidgetCatalog.ProjectsBehindSchedule, null, false);
        Assert.Equal(1m, behind.Stats[0].Value);
        Assert.Contains("?tab=milestones", Assert.Single(behind.Items).Link);
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

    // ── PostgreSQL ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task Postgres_EveryWidgetQuery_Translates()
    {
        await WithPostgresAsync(async connection =>
        {
            var harness = await Harness.CreateAsync(context => PostgresContext(connection, context), seedTenant: true);
            var (north, _) = await harness.AddBranchesAsync();
            harness.Db.UserBranchAssignments.Add(new UserBranchAssignment { TenantId = harness.TenantId, UserId = harness.ManagerId, BranchId = north, IsDefault = true });
            await harness.Db.SaveChangesAsync();
            await harness.SeedSampleDataAsync();
            await harness.AddProjectAsync(harness.ManagerId, behind: true);

            var admin = harness.Service(harness.AdminId, AppRoles.Admin, isAdmin: true);
            var manager = harness.Service(harness.ManagerId, AppRoles.Manager, permissions: PermissionNames.All);
            foreach (var widget in DashboardWidgetCatalog.All)
            {
                Assert.Equal(widget.Key, (await admin.GetWidgetAsync(widget.Key, null, true)).Key);
                Assert.Equal(widget.Key, (await admin.GetWidgetAsync(widget.Key, north, true)).Key);
                Assert.Equal(widget.Key, (await manager.GetWidgetAsync(widget.Key, null, true)).Key);
            }

            var saved = await manager.SaveLayoutAsync([new(DashboardWidgetCatalog.ProjectsMine, "small")]);
            Assert.Equal(DashboardWidgetCatalog.ProjectsMine, saved.Items[0].Key);
            Assert.True((await manager.GetLayoutAsync()).IsCustomized);
            Assert.False((await manager.ResetLayoutAsync()).IsCustomized);
            await admin.SetHiddenAsync(AppRoles.Manager, [DashboardWidgetCatalog.ContractsExpiring]);
            Assert.Equal([DashboardWidgetCatalog.ContractsExpiring], (await admin.GetAdminSettingsAsync()).Roles.Single(x => x.Role == AppRoles.Manager).HiddenKeys);
        });
    }

    // ── Harness ────────────────────────────────────────────────────────────────

    private sealed class Harness
    {
        private Func<ITenantContext?, AppDbContext> createContext = null!;
        private readonly IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        public AppDbContext Db { get; private set; } = null!;
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid AdminId { get; private set; }
        public Guid ManagerId { get; private set; }
        public Guid DispatcherId { get; private set; }
        public Guid ViewerId { get; private set; }
        public Guid ClientId { get; private set; }
        public IReadOnlyCollection<string> Modules { get; private set; } = AllModules;

        public static Task<Harness> CreateAsync(string[]? modules = null)
        {
            var databaseName = $"ecosys-dashboards-{Guid.NewGuid():N}";
            return CreateAsync(context => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options, context), seedTenant: false, modules);
        }

        public static async Task<Harness> CreateAsync(Func<ITenantContext?, AppDbContext> createContext, bool seedTenant, string[]? modules = null)
        {
            var harness = new Harness { createContext = createContext, Modules = modules ?? AllModules };
            harness.Db = createContext(null);
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

        public IRoleDashboardService Service(Guid userId, string role, bool isAdmin = false, IReadOnlyCollection<string>? permissions = null)
        {
            var context = new FakeTenantContext(TenantId, userId, role, isAdmin, permissions ?? []);
            var db = createContext(context);
            var projectSettings = new ProjectSettingsService(db);
            return new RoleDashboardService(db, context, new UserAccessService(context), new BranchAccessService(db, context),
                new FixedModules(Modules), new DashboardSettingsService(db),
                new RoleDashboardQueries(db, projectSettings, TestTimeZones.Create(db)), cache);
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

        /// <summary>A project managed by <paramref name="managerUserId"/>, optionally behind its planned milestones.</summary>
        public async Task<Project> AddProjectAsync(Guid managerUserId, bool behind = false)
        {
            var type = new ProjectType { TenantId = TenantId, Name = "Installation" };
            var stage = new ProjectStage { TenantId = TenantId, Name = "In Progress", Category = ProjectStageCategories.Active, SortOrder = 10 };
            Db.AddRange(type, stage);
            var project = new Project
            {
                TenantId = TenantId, ProjectNumber = $"PRJ-{Random.Shared.Next(1000, 9999)}", Name = "Westlands Mall", ClientId = ClientId,
                ProjectTypeId = type.Id, StageId = stage.Id, ProjectManagerUserId = managerUserId, OriginalValue = 1_000_000m, Currency = "KES",
            };
            Db.Projects.Add(project);
            Db.ProjectMilestones.Add(new ProjectMilestone
            {
                TenantId = TenantId, ProjectId = project.Id, Name = "Rough-in", Weight = 1,
                PlannedDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(behind ? -10 : 10),
            });
            var worker = new ProjectWorker { TenantId = TenantId, ProjectId = project.Id, FullName = "Juma", IsActive = true };
            Db.ProjectWorkers.Add(worker);
            Db.ProjectMusterEntries.Add(new ProjectMusterEntry
            {
                TenantId = TenantId, ProjectId = project.Id, ProjectWorkerId = worker.Id, WorkDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1),
                Status = ProjectMusterStatuses.Present, DayFraction = 1, MarkedByUserId = managerUserId,
            });
            Db.ProjectVariations.Add(new ProjectVariation
            {
                TenantId = TenantId, ProjectId = project.Id, VariationNumber = "VO-001", Title = "Extra ducts", Amount = 50_000m,
                Status = ProjectVariationStatuses.Submitted, SubmittedAt = DateTime.UtcNow,
            });
            Db.ProjectValuations.Add(new ProjectValuation
            {
                TenantId = TenantId, ProjectId = project.Id, ValuationNumber = "VAL-001", PeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = ProjectValuationStatuses.Certified, CertifiedAmount = 200_000m, AmountDue = 200_000m,
            });
            await Db.SaveChangesAsync();
            return project;
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
            var invoice = new TenantInvoice
            {
                TenantId = TenantId, ClientId = ClientId, InvoiceNumber = "INV-001", Status = "Sent", InvoiceDate = DateTime.UtcNow.AddDays(-50),
                DueDate = DateTime.UtcNow.AddDays(-40), Subtotal = 1000, TotalAmount = 1160, TaxAmount = 160, Balance = 1160, Currency = "KES",
            };
            Db.TenantInvoices.Add(invoice);
            Db.TenantInvoices.Add(new TenantInvoice
            {
                TenantId = TenantId, ClientId = ClientId, InvoiceNumber = "INV-002", Status = "Draft", InvoiceDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30), TotalAmount = 500, Balance = 500, Currency = "KES",
            });
            Db.TenantPayments.Add(new TenantPayment
            {
                TenantId = TenantId, ClientId = ClientId, InvoiceId = invoice.Id, PaymentNumber = "RCT-1", Amount = 100, PaymentDate = DateTime.UtcNow, Status = "Confirmed",
            });
            Db.WithholdingCertificates.Add(new WithholdingCertificate
            {
                TenantId = TenantId, ClientId = ClientId, InvoiceId = invoice.Id, Amount = 20, CertificateNumber = "WHT-1",
                CertificateDate = DateOnly.FromDateTime(DateTime.UtcNow), RecordedByUserId = AdminId,
            });
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

    private static AppDbContext PostgresContext(string connection, ITenantContext? tenantContext) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options, tenantContext);

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
