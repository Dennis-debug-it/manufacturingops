using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class OrgHierarchyTests
{
    [Fact]
    public async Task Tenant_WithTwoFactories_GetsTeaSectionsFromTemplate_AndReapplyingAddsNothing()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var controller = Controller(db, new FactoryTenantContext(tenant.Id, admin.Id));

        var group = await CreateAsync(controller, null, OrgNodeLevels.Group, "Kericho Group", "KG");
        var factoryA = await CreateAsync(controller, group.Id, OrgNodeLevels.Factory, "Chelal", "CHL");
        var factoryB = await CreateAsync(controller, group.Id, OrgNodeLevels.Factory, "Momul", "MML");

        var first = Ok(await controller.ApplyTemplate(factoryA.Id, new ApplyTemplateRequest(null), CancellationToken.None));
        Assert.Equal(IndustryTemplates.Tea.Sections.Count, first.Created);
        Ok(await controller.ApplyTemplate(factoryB.Id, new ApplyTemplateRequest(IndustryTemplates.TeaKey), CancellationToken.None));

        var again = Ok(await controller.ApplyTemplate(factoryA.Id, new ApplyTemplateRequest(null), CancellationToken.None));
        Assert.Equal(0, again.Created);
        Assert.Equal(IndustryTemplates.Tea.Sections.Count, again.Skipped);

        var sections = await db.OrgNodes.Where(x => x.Level == OrgNodeLevels.Section).ToListAsync();
        Assert.Equal(IndustryTemplates.Tea.Sections.Count * 2, sections.Count);
        Assert.Contains(sections, x => x.Code == "CHL-WITH" && x.ParentId == factoryA.Id && x.TemplateKey == "tea");
        Assert.Contains(sections, x => x.Code == "MML-WITH" && x.ParentId == factoryB.Id);
    }

    [Fact]
    public async Task Hierarchy_RejectsLevelsInTheWrongPlace()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var controller = Controller(db, new FactoryTenantContext(tenant.Id, admin.Id));

        var group = await CreateAsync(controller, null, OrgNodeLevels.Group, "Group", "G1");
        var factory = await CreateAsync(controller, group.Id, OrgNodeLevels.Factory, "Factory", "F1");

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(controller, null, OrgNodeLevels.Section, "Orphan", "S0"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(controller, group.Id, OrgNodeLevels.Section, "Under group", "S1"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(controller, factory.Id, OrgNodeLevels.Factory, "Nested factory", "F2"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateAsync(controller, null, "Plant", "Bad level", "X1"));
        await Assert.ThrowsAsync<ConflictException>(() => CreateAsync(controller, group.Id, OrgNodeLevels.Factory, "Other", "f1"));
        await Assert.ThrowsAsync<ConflictException>(() => CreateAsync(controller, group.Id, OrgNodeLevels.Factory, "factory", "F3"));

        var section = await CreateAsync(controller, factory.Id, OrgNodeLevels.Section, "Withering", "F1-WITH");
        var sub = await CreateAsync(controller, section.Id, "sub-section", "Trough 1-4", "F1-WITH-A");
        Assert.Equal(OrgNodeLevels.SubSection, sub.Level);
    }

    [Fact]
    public async Task Node_CannotBeMovedUnderItsOwnDescendant_OrDeactivatedWithActiveChildren()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var controller = Controller(db, new FactoryTenantContext(tenant.Id, admin.Id));

        var groupA = await CreateAsync(controller, null, OrgNodeLevels.Group, "Group A", "GA");
        var groupB = await CreateAsync(controller, groupA.Id, OrgNodeLevels.Group, "Group B", "GB");

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Update(groupA.Id,
            new UpsertOrgNodeRequest(groupB.Id, OrgNodeLevels.Group, "Group A", "GA"), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Deactivate(groupA.Id, CancellationToken.None));

        var factory = await CreateAsync(controller, groupB.Id, OrgNodeLevels.Factory, "Factory", "F1");
        db.Assets.Add(new Asset { TenantId = tenant.Id, AssetName = "Boiler 1", AssetCode = "BLR-01", OrgNodeId = factory.Id });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Deactivate(factory.Id, CancellationToken.None));

        // Moving the factory to the other group is fine.
        var moved = Ok(await controller.Update(factory.Id, new UpsertOrgNodeRequest(groupA.Id, OrgNodeLevels.Factory, "Factory", "F1"), CancellationToken.None));
        Assert.Equal(groupA.Id, moved.ParentId);
        Assert.Equal(1, moved.AssetCount);
    }

    [Fact]
    public async Task UserScopedToOneFactory_CannotSeeTheOther()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var adminContext = new FactoryTenantContext(tenant.Id, admin.Id);
        var adminController = Controller(db, adminContext);

        var group = await CreateAsync(adminController, null, OrgNodeLevels.Group, "Group", "G1");
        var factoryA = await CreateAsync(adminController, group.Id, OrgNodeLevels.Factory, "Chelal", "CHL");
        var factoryB = await CreateAsync(adminController, group.Id, OrgNodeLevels.Factory, "Momul", "MML");
        Ok(await adminController.ApplyTemplate(factoryA.Id, new ApplyTemplateRequest(null), CancellationToken.None));
        Ok(await adminController.ApplyTemplate(factoryB.Id, new ApplyTemplateRequest(null), CancellationToken.None));

        var engineer = FactoryTestSupport.NewUser(tenant.Id, "engineer@chelal.test");
        db.Users.Add(engineer);
        await db.SaveChangesAsync();

        var settingsController = SettingsController(db, adminContext);
        await settingsController.SetUserScope(engineer.Id, new SetUserOrgScopeRequest([factoryA.Id]), CancellationToken.None);

        var engineerController = Controller(db, new FactoryTenantContext(tenant.Id, engineer.Id, isAdmin: false));
        var visible = Ok(await engineerController.List(false, CancellationToken.None));

        Assert.DoesNotContain(visible, x => x.Id == factoryB.Id);
        Assert.DoesNotContain(visible, x => x.Code.StartsWith("MML-", StringComparison.Ordinal));
        Assert.Contains(visible, x => x.Id == factoryA.Id && x.CanAccess);
        Assert.Equal(IndustryTemplates.Tea.Sections.Count, visible.Count(x => x.Code.StartsWith("CHL-", StringComparison.Ordinal) && x.CanAccess));
        // The group is shown for context but is not accessible.
        Assert.Contains(visible, x => x.Id == group.Id && !x.CanAccess);

        await Assert.ThrowsAsync<ForbiddenException>(() => engineerController.Get(factoryB.Id, CancellationToken.None));

        var scope = await FactoryTestSupport.Scope(db, new FactoryTenantContext(tenant.Id, engineer.Id, isAdmin: false))
            .GetScopeAsync(tenant.Id, engineer.Id);
        Assert.False(scope.IsUnrestricted);
        Assert.True(scope.Allows(factoryA.Id));
        Assert.False(scope.Allows(factoryB.Id));
        Assert.False(scope.Allows(null));
    }

    [Fact]
    public async Task ScopedManager_CannotAddTopLevelNodes_OrWriteOutsideTheirFactory()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var adminContext = new FactoryTenantContext(tenant.Id, admin.Id);
        var adminController = Controller(db, adminContext);
        var factoryA = await CreateAsync(adminController, null, OrgNodeLevels.Factory, "Chelal", "CHL");
        var factoryB = await CreateAsync(adminController, null, OrgNodeLevels.Factory, "Momul", "MML");

        var manager = FactoryTestSupport.NewUser(tenant.Id, "manager@chelal.test");
        db.Users.Add(manager);
        await db.SaveChangesAsync();
        await SettingsController(db, adminContext).SetUserScope(manager.Id, new SetUserOrgScopeRequest([factoryA.Id]), CancellationToken.None);

        var controller = Controller(db, new FactoryTenantContext(tenant.Id, manager.Id, false, Ecosys.Shared.Auth.PermissionNames.CanManageSettings));
        await CreateAsync(controller, factoryA.Id, OrgNodeLevels.Section, "Workshop", "CHL-WKSP");
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(controller, factoryB.Id, OrgNodeLevels.Section, "Workshop", "MML-WKSP"));
        await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(controller, null, OrgNodeLevels.Factory, "New", "NEW"));
    }

    [Fact]
    public async Task FactorySettings_DefaultToTea_AndGuardLevelSwitches()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var context = new FactoryTenantContext(tenant.Id, admin.Id);
        var settingsController = SettingsController(db, context);

        var defaults = Ok(await settingsController.Get(CancellationToken.None));
        Assert.Equal("kg made tea", defaults.ProductionUnit);
        Assert.Equal("Monday", defaults.WeeklyWindowDay);
        Assert.True(defaults.UseGroupLevel);

        await CreateAsync(Controller(db, context), null, OrgNodeLevels.Group, "Group", "G1");
        var request = new UpdateFactorySettingsRequest("tea", "kg made tea", "kg MT", false, true, true, "Monday", "06:00", "18:00", 6, 8);
        await Assert.ThrowsAsync<BusinessRuleException>(() => settingsController.Update(request, CancellationToken.None));

        var updated = Ok(await settingsController.Update(request with { UseGroupLevel = true, WeeklyWindowDay = "tuesday", ProductionUnit = "kg made tea (black)" }, CancellationToken.None));
        Assert.Equal("Tuesday", updated.WeeklyWindowDay);
        Assert.Equal("kg made tea (black)", updated.ProductionUnit);
        Assert.Equal(6, updated.LowCropStartMonth);

        await Assert.ThrowsAsync<BusinessRuleException>(() => settingsController.Update(request with { UseGroupLevel = true, WeeklyWindowEnd = "05:00" }, CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => settingsController.Update(request with { UseGroupLevel = true, IndustryTemplate = "unknown" }, CancellationToken.None));
    }

    [Fact]
    public void Descendants_WalksTheWholeSubtree()
    {
        var root = Guid.NewGuid();
        var child = Guid.NewGuid();
        var grandchild = Guid.NewGuid();
        var other = Guid.NewGuid();
        var result = OrgScopeService.Descendants([(root, null), (child, root), (grandchild, child), (other, null)], [root]);
        Assert.Equal(new HashSet<Guid> { root, child, grandchild }, result);
    }

    private static async Task<OrgNodeResponse> CreateAsync(OrgNodesController controller, Guid? parentId, string level, string name, string code)
    {
        var result = await controller.Create(new UpsertOrgNodeRequest(parentId, level, name, code), CancellationToken.None);
        return Assert.IsType<OrgNodeResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    internal static OrgNodesController Controller(AppDbContext db, FactoryTenantContext context) => new(
        db,
        context,
        FactoryTestSupport.Access(context),
        FactoryTestSupport.Scope(db, context),
        FactoryTestSupport.Settings(db),
        FactoryTestSupport.Audit(db));

    internal static FactorySettingsController SettingsController(AppDbContext db, FactoryTenantContext context) => new(
        context,
        db,
        FactoryTestSupport.Access(context),
        FactoryTestSupport.Settings(db),
        FactoryTestSupport.Audit(db));
}
