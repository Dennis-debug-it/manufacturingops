using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class FactoryAssetRegisterTests
{
    [Fact]
    public async Task FactoryAsset_NeedsNoClient_AndRecordsItsRegistration()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedTwoFactoriesAsync(db);
        var assets = AssetsController(db, site.AdminContext);

        var dryer = await CreateAsync(assets, site.ChelalDrying, "Fluid bed dryer 1", "CHL-FBD-01", criticality: "a", lostPerHour: 450m);

        Assert.Null(dryer.ClientId);
        Assert.Equal(site.ChelalDrying, dryer.OrgNodeId);
        Assert.Equal("Drying", dryer.OrgNodeName);
        Assert.Equal("A", dryer.Criticality);
        Assert.Equal(AssetOperationalStatuses.Operational, dryer.OperationalStatus);
        Assert.Equal(450m, dryer.LostProductionPerHour);

        var history = await Lifecycle(db, site.AdminContext).History(dryer.Id, CancellationToken.None);
        var registered = Assert.Single(Ok(history));
        Assert.Equal(AssetLifecycleEventTypes.Registered, registered.EventType);
        Assert.Equal("Drying", registered.ToValue);
    }

    [Fact]
    public async Task FactoryAsset_MustHaveAValidLocation()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedTwoFactoriesAsync(db);
        var assets = AssetsController(db, site.AdminContext);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(assets, null, "Nowhere", "X-01"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(assets, site.Group, "At group level", "X-02"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(assets, Guid.NewGuid(), "Unknown node", "X-03"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(assets, site.ChelalDrying, "Bad criticality", "X-04", criticality: "D"));
    }

    [Fact]
    public async Task Component_TakesItsParentsLocation_AndCannotFormACycle()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedTwoFactoriesAsync(db);
        var assets = AssetsController(db, site.AdminContext);

        var fan = await CreateAsync(assets, site.ChelalWithering, "Withering fan 3", "CHL-WF-03", criticality: "B");
        var motor = await CreateAsync(assets, null, "Fan motor 15 kW", "CHL-WF-03-M", parentAssetId: fan.Id);

        Assert.Equal(site.ChelalWithering, motor.OrgNodeId);
        Assert.Equal(fan.Id, motor.ParentAssetId);
        Assert.Equal("CHL-WF-03", motor.ParentAssetCode);

        // The fan cannot become a component of its own motor.
        await Assert.ThrowsAsync<BusinessRuleException>(() => assets.Update(fan.Id, Request(site.ChelalWithering, "Withering fan 3", "CHL-WF-03", parentAssetId: motor.Id), CancellationToken.None));

        var components = Ok(await Lifecycle(db, site.AdminContext).Components(fan.Id, CancellationToken.None));
        Assert.Equal(motor.Id, Assert.Single(components).Id);

        var topLevel = await ListAsync(assets, topLevelOnly: true);
        Assert.DoesNotContain(topLevel, x => x.Id == motor.Id);
        Assert.Contains(topLevel, x => x.Id == fan.Id);
    }

    [Fact]
    public async Task Transfer_MovesTheAssetWithItsComponents_AndRecordsEachMove()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedTwoFactoriesAsync(db);
        var assets = AssetsController(db, site.AdminContext);
        var lifecycle = Lifecycle(db, site.AdminContext);

        var generator = await CreateAsync(assets, site.ChelalDrying, "Standby generator", "GEN-01", criticality: "A");
        var alternator = await CreateAsync(assets, null, "Alternator", "GEN-01-ALT", parentAssetId: generator.Id);

        // A component is moved with its parent, not on its own.
        await Assert.ThrowsAsync<BusinessRuleException>(() => lifecycle.Transfer(alternator.Id, new TransferAssetRequest(site.MomulDrying, null, null), CancellationToken.None));

        var result = Ok(await lifecycle.Transfer(generator.Id, new TransferAssetRequest(site.MomulDrying, DateTime.UtcNow.Date, "Loan for the peak season"), CancellationToken.None));
        Assert.Equal(2, result.AssetsMoved);
        Assert.Equal(site.MomulDrying, (await db.Assets.SingleAsync(x => x.Id == alternator.Id)).OrgNodeId);

        var transfer = Ok(await lifecycle.History(generator.Id, CancellationToken.None)).First();
        Assert.Equal(AssetLifecycleEventTypes.Transferred, transfer.EventType);
        Assert.Equal(site.ChelalDrying, transfer.FromOrgNodeId);
        Assert.Equal(site.MomulDrying, transfer.ToOrgNodeId);
        Assert.Equal("Loan for the peak season", transfer.Notes);

        await Assert.ThrowsAsync<BusinessRuleException>(() => lifecycle.Transfer(generator.Id, new TransferAssetRequest(site.MomulDrying, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task EditingTheLocation_IsRecordedAsATransfer()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedTwoFactoriesAsync(db);
        var assets = AssetsController(db, site.AdminContext);

        var pump = await CreateAsync(assets, site.ChelalDrying, "Feed pump", "PMP-01");
        var updated = Ok(await assets.Update(pump.Id, Request(site.ChelalWithering, "Feed pump", "PMP-01", operationalStatus: "down"), CancellationToken.None));

        Assert.Equal(site.ChelalWithering, updated.OrgNodeId);
        Assert.Equal(AssetOperationalStatuses.Down, updated.OperationalStatus);
        var types = await db.AssetLifecycleEvents.Where(x => x.AssetId == pump.Id).Select(x => x.EventType).ToListAsync();
        Assert.Contains(AssetLifecycleEventTypes.Transferred, types);
        Assert.Contains(AssetLifecycleEventTypes.StatusChanged, types);
    }

    [Fact]
    public async Task List_FiltersBySubtreeAndCriticality()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedTwoFactoriesAsync(db);
        var assets = AssetsController(db, site.AdminContext);

        await CreateAsync(assets, site.ChelalDrying, "Dryer", "A-01", criticality: "A");
        await CreateAsync(assets, site.ChelalWithering, "Trough", "A-02", criticality: "C");
        await CreateAsync(assets, site.MomulDrying, "Dryer", "B-01", criticality: "A");

        var chelal = await ListAsync(assets, orgNodeId: site.Chelal);
        Assert.Equal(["A-01", "A-02"], chelal.Select(x => x.AssetCode).Order().ToArray());

        var critical = await ListAsync(assets, criticality: "a");
        Assert.Equal(["A-01", "B-01"], critical.Select(x => x.AssetCode).Order().ToArray());
    }

    [Fact]
    public async Task UserScopedToOneFactory_SeesAndChangesOnlyItsAssets()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedTwoFactoriesAsync(db);
        var adminAssets = AssetsController(db, site.AdminContext);
        var ours = await CreateAsync(adminAssets, site.ChelalDrying, "Our dryer", "CHL-01");
        var theirs = await CreateAsync(adminAssets, site.MomulDrying, "Their dryer", "MML-01");

        var engineer = FactoryTestSupport.NewUser(site.TenantId, "fitter@chelal.test");
        db.Users.Add(engineer);
        db.UserOrgNodeAssignments.Add(new UserOrgNodeAssignment { TenantId = site.TenantId, UserId = engineer.Id, OrgNodeId = site.Chelal });
        await db.SaveChangesAsync();
        var context = new FactoryTenantContext(site.TenantId, engineer.Id, false, Ecosys.Shared.Auth.PermissionNames.CanManageAssets);
        var assets = AssetsController(db, context);

        var visible = await ListAsync(assets);
        Assert.Equal(ours.Id, Assert.Single(visible).Id);

        await Assert.ThrowsAsync<NotFoundException>(() => assets.Get(theirs.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => Lifecycle(db, context).History(theirs.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => assets.Update(theirs.Id, Request(site.MomulDrying, "Their dryer", "MML-01"), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(assets, site.MomulDrying, "Sneaky", "MML-02"));
        await Assert.ThrowsAsync<ForbiddenException>(() => Lifecycle(db, context).Transfer(ours.Id, new TransferAssetRequest(site.MomulDrying, null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Disposal_NeedsARetiredAsset_AndIsFinal()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedTwoFactoriesAsync(db);
        var assets = AssetsController(db, site.AdminContext);
        var lifecycle = Lifecycle(db, site.AdminContext);
        var compressor = await CreateAsync(assets, site.ChelalDrying, "Old compressor", "CMP-OLD");

        await Assert.ThrowsAsync<BusinessRuleException>(() => lifecycle.Dispose(compressor.Id, new DisposeAssetRequest(null, "Scrapped", null), CancellationToken.None));

        Ok(await assets.Retire(compressor.Id, new RetireAssetRequest(null, "Beyond economic repair"), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => lifecycle.Dispose(compressor.Id, new DisposeAssetRequest(null, " ", null), CancellationToken.None));
        Ok(await lifecycle.Dispose(compressor.Id, new DisposeAssetRequest(null, "Sold as scrap", "Licensed scrap dealer"), CancellationToken.None));

        await Assert.ThrowsAsync<BusinessRuleException>(() => assets.Reactivate(compressor.Id, CancellationToken.None));
        var types = Ok(await lifecycle.History(compressor.Id, CancellationToken.None)).Select(x => x.EventType).ToList();
        Assert.Equal([AssetLifecycleEventTypes.Disposed, AssetLifecycleEventTypes.Decommissioned, AssetLifecycleEventTypes.Registered], types);
    }

    private sealed record Site(
        Guid TenantId,
        FactoryTenantContext AdminContext,
        Guid Group,
        Guid Chelal,
        Guid ChelalWithering,
        Guid ChelalDrying,
        Guid MomulDrying);

    private static async Task<Site> SeedTwoFactoriesAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var group = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Group, Name = "Group", Code = "G" };
        var chelal = new OrgNode { TenantId = tenant.Id, ParentId = group.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var momul = new OrgNode { TenantId = tenant.Id, ParentId = group.Id, Level = OrgNodeLevels.Factory, Name = "Momul", Code = "MML" };
        var chelalWithering = new OrgNode { TenantId = tenant.Id, ParentId = chelal.Id, Level = OrgNodeLevels.Section, Name = "Withering", Code = "CHL-WITH" };
        var chelalDrying = new OrgNode { TenantId = tenant.Id, ParentId = chelal.Id, Level = OrgNodeLevels.Section, Name = "Drying", Code = "CHL-DRY" };
        var momulDrying = new OrgNode { TenantId = tenant.Id, ParentId = momul.Id, Level = OrgNodeLevels.Section, Name = "Drying", Code = "MML-DRY" };
        db.OrgNodes.AddRange(group, chelal, momul, chelalWithering, chelalDrying, momulDrying);
        await db.SaveChangesAsync();

        return new Site(tenant.Id, new FactoryTenantContext(tenant.Id, admin.Id), group.Id, chelal.Id, chelalWithering.Id, chelalDrying.Id, momulDrying.Id);
    }

    private static UpsertAssetRequest Request(
        Guid? orgNodeId,
        string name,
        string code,
        string? criticality = null,
        Guid? parentAssetId = null,
        decimal? lostPerHour = null,
        string? operationalStatus = null) =>
        new(null, null, null, null, null, name, code, null, null, null, null, null, null,
            null, null, null, null, null, false, null, null, null, "Active", null,
            OrgNodeId: orgNodeId,
            ParentAssetId: parentAssetId,
            Criticality: criticality,
            OperationalStatus: operationalStatus,
            LostProductionPerHour: lostPerHour);

    private static async Task<AssetResponse> CreateAsync(
        AssetsController controller,
        Guid? orgNodeId,
        string name,
        string code,
        string? criticality = null,
        Guid? parentAssetId = null,
        decimal? lostPerHour = null)
    {
        var result = await controller.Create(Request(orgNodeId, name, code, criticality, parentAssetId, lostPerHour), CancellationToken.None);
        return Assert.IsType<AssetResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }

    private static async Task<IReadOnlyList<AssetResponse>> ListAsync(
        AssetsController controller,
        Guid? orgNodeId = null,
        string? criticality = null,
        bool topLevelOnly = false)
    {
        var result = await controller.GetAll(null, null, null, null, null, null, null, null, null, CancellationToken.None,
            orgNodeId: orgNodeId, criticality: criticality, topLevelOnly: topLevelOnly);
        return Assert.IsAssignableFrom<IEnumerable<AssetResponse>>(Assert.IsType<OkObjectResult>(result).Value).ToList();
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    internal static AssetsController AssetsController(AppDbContext db, FactoryTenantContext context)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        return WithHttp(new AssetsController(
            db,
            context,
            new PreventiveMaintenancePlanService(db),
            FactoryTestSupport.Audit(db),
            FactoryTestSupport.Access(context),
            new BranchAccessService(db, context),
            new DocumentNumberingService(db),
            new AllowAllLicenseGuard(),
            scope,
            new AssetLifecycleService(db, scope)));
    }

    internal static AssetLifecycleController Lifecycle(AppDbContext db, FactoryTenantContext context)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        return WithHttp(new AssetLifecycleController(db, context, FactoryTestSupport.Access(context), scope, new AssetLifecycleService(db, scope), FactoryTestSupport.Audit(db)));
    }

    internal static T WithHttp<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        return controller;
    }
}
