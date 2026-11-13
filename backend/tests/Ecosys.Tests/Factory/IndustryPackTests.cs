using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

/// <summary>Industry packs: each one complete and self-consistent, tea unchanged, and a group mixing industries.</summary>
public sealed class IndustryPackTests
{
    public static TheoryData<string> Industries => new(IndustryTemplates.All.Select(x => x.Key));

    [Theory]
    [MemberData(nameof(Industries))]
    public void EveryPack_RefersOnlyToItsOwnClassesAndZones(string key)
    {
        var pack = IndustryTemplates.Find(key)!;
        var codes = pack.AssetClasses.Select(x => x.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.Equal(pack.Sections.Count, pack.Sections.Select(x => x.Code).Distinct().Count());
        Assert.NotEmpty(pack.Sections);
        Assert.NotEmpty(pack.AssetClasses);
        Assert.False(string.IsNullOrWhiteSpace(pack.Description));
        Assert.False(string.IsNullOrWhiteSpace(pack.Vocabulary.OverhaulSeason));

        Assert.All(pack.RunningChecks, x => Assert.Contains(x.AssetClassCode, codes));
        Assert.All(pack.WindowRoutines.SelectMany(x => x.AssetClassCodes), x => Assert.Contains(x, codes));
        Assert.All(pack.Compliance.Where(x => x.AssetClassCodes is not null).SelectMany(x => x.AssetClassCodes!), x => Assert.Contains(x, codes));
        Assert.All(pack.FailureCodes.Where(x => x.AssetClassCode is not null), x => Assert.Contains(x.AssetClassCode!, codes));
        Assert.Equal(pack.Compliance.Count, pack.Compliance.Select(x => x.Key).Distinct().Count());
        Assert.Equal(pack.RunningChecks.Count, pack.RunningChecks.Select(x => x.Name).Distinct().Count());

        var zones = pack.Zones.Select(x => x.Name).ToList();
        Assert.Equal(zones.Count, zones.Distinct().Count());
        Assert.Contains(pack.Zones, x => x.Level == AccessZoneLevels.Public);
        Assert.All(pack.Zones, x => Assert.NotNull(AccessZoneLevels.Find(x.Level)));
        Assert.All(pack.Checkpoints.Where(x => x.Zone is not null), x => Assert.Contains(x.Zone!, zones));
        Assert.Equal(pack.Checkpoints.Count, pack.Checkpoints.Select(x => x.Name).Distinct().Count());
        Assert.True(pack.LowSeasonStartMonth is null or >= 1 and <= 12);
    }

    [Fact]
    public void Packs_HaveTheirOwnClassCodes()
    {
        var all = IndustryTemplates.All.SelectMany(p => p.AssetClasses.Select(c => (p.Key, c.Code))).ToList();
        Assert.Equal(all.Count, all.Select(x => x.Code).Distinct().Count());
        Assert.Equal(["tea", "sugar", "dairy", "generic"], IndustryTemplates.All.Select(x => x.Key));
        Assert.Equal(IndustryTemplates.Tea, IndustryTemplates.Default);
        Assert.Equal(IndustryTemplates.Sugar, IndustryTemplates.Find(" SUGAR "));
        Assert.Null(IndustryTemplates.Find("cement"));
    }

    [Fact]
    public void TheTeaPack_IsWhatTeaWorkspacesAlreadyHave()
    {
        var tea = IndustryTemplates.Tea;
        Assert.Equal(("kg made tea", "kg MT"), (tea.ProductionUnit, tea.ProductionUnitShort));
        Assert.Equal(9, tea.Sections.Count);
        Assert.All(tea.AssetClasses, x => Assert.StartsWith("TEA-", x.Code));
        Assert.Equal(["Withering fan shift check", "CTC machine shift check", "Fluid bed dryer shift check", "Boiler shift log", "Compressor daily check", "Generator daily check"],
            tea.RunningChecks.Select(x => x.Name));
        Assert.Equal(7, tea.WindowRoutines.Count);
        Assert.Equal(13, tea.Compliance.Count);
        Assert.Equal((10, 12, 10), (tea.Zones.Count, tea.Checkpoints.Count, tea.FailureCodes.Count));
        Assert.Equal("low-crop shutdown", tea.Vocabulary.OverhaulSeason);
        Assert.DoesNotContain(FailureCodeTemplates.Iso, x => x.AssetClassCode is not null);
    }

    // ------------------------------------------------------------------ a group mixing industries

    [Fact]
    public async Task AFactoryCanHaveItsOwnIndustry_AndSectionsUnderItFollowIt()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var controller = OrgHierarchyTests.Controller(db, new FactoryTenantContext(tenant.Id, admin.Id));

        var tea = Created(await controller.Create(new UpsertOrgNodeRequest(null, OrgNodeLevels.Factory, "Chelal", "CHL"), default));
        var mill = Created(await controller.Create(new UpsertOrgNodeRequest(null, OrgNodeLevels.Factory, "Muhoroni", "MUH", Industry: "Sugar"), default));
        Assert.Equal((null, "sugar"), (tea.TemplateKey, mill.TemplateKey));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Create(new UpsertOrgNodeRequest(null, OrgNodeLevels.Factory, "Bamburi", "BAM", Industry: "cement"), default));
        var section = Created(await controller.Create(new UpsertOrgNodeRequest(mill.Id, OrgNodeLevels.Section, "Mill house", "MUH-MILL", Industry: "dairy"), default));
        Assert.Null(section.TemplateKey); // only factories carry an industry

        Assert.Equal("tea", (await IndustryTemplates.ForFactoryAsync(db, tenant.Id, tea.Id)).Key);
        Assert.Equal("sugar", (await IndustryTemplates.ForFactoryAsync(db, tenant.Id, section.Id)).Key);
        Assert.Equal(["tea", "sugar"], (await IndustryTemplates.InUseAsync(db, tenant.Id)).Select(x => x.Key));

        // The mill's default sections are sugar's, without naming the industry again.
        var applied = Ok(await controller.ApplyTemplate(mill.Id, new ApplyTemplateRequest(null), default));
        Assert.Equal(IndustryTemplates.Sugar.Sections.Count - 0, applied.Created + applied.Skipped);
        Assert.True(await db.OrgNodes.AnyAsync(x => x.ParentId == mill.Id && x.Code == "MUH-CENT"));

        // Applying another industry's sections to a factory makes it that industry.
        Ok(await controller.ApplyTemplate(tea.Id, new ApplyTemplateRequest("dairy"), default));
        Assert.Equal("dairy", (await IndustryTemplates.ForFactoryAsync(db, tenant.Id, tea.Id)).Key);

        var industries = Assert.IsAssignableFrom<IReadOnlyCollection<IndustryTemplateResponse>>(Assert.IsType<OkObjectResult>(controller.Templates().Result).Value);
        var sugar = industries.Single(x => x.Key == "sugar");
        Assert.Equal(("Sugar mill", "off-season shutdown", "Cane haulage fleet"), (sugar.Name, sugar.Vocabulary.OverhaulSeason, sugar.Vocabulary.SupplyFleet));
        Assert.True(sugar.RunningChecks > 0 && sugar.StatutoryItems > 0 && sugar.Checkpoints > 0);
    }

    [Fact]
    public async Task ASugarMillInATeaGroup_GetsSugarClassesChecksCodesZonesCheckpointsAndStatutoryItems()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var context = new FactoryTenantContext(tenant.Id, admin.Id);
        var teaFactory = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var mill = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Muhoroni", Code = "MUH", TemplateKey = IndustryTemplates.SugarKey };
        db.AddRange(teaFactory, mill, new FactorySettings { TenantId = tenant.Id });
        await db.SaveChangesAsync();

        var classes = new AssetClassService(db);
        await classes.InstallAsync(tenant.Id, IndustryTemplates.TeaKey);
        var sugarClasses = await classes.InstallAsync(tenant.Id, IndustryTemplates.SugarKey);
        Assert.Equal(IndustryTemplates.Sugar.AssetClasses.Count, sugarClasses.Created);
        Assert.Equal("Electric motor (Sugar mill)", (await db.AssetCategories.SingleAsync(x => x.Code == "SUG-MTR")).Name);
        Assert.Equal("Electric motor", (await db.AssetCategories.SingleAsync(x => x.Code == "TEA-MTR")).Name);
        Assert.Equal("Mill", (await db.AssetCategories.SingleAsync(x => x.Code == "SUG-MIL")).Name);

        // Shift checks: both industries', the shared names told apart.
        await RunningCheckTemplates.InstallAsync(db, tenant.Id);
        var checks = await db.PmTemplates.Where(x => x.WorkType == RunningCheckTemplates.WorkType).Include(x => x.AssetCategory).ToListAsync();
        Assert.Contains(checks, x => x.Name == "CTC machine shift check" && x.AssetCategory!.Code == "TEA-CTC");
        Assert.Contains(checks, x => x.Name == "Mill shift check" && x.AssetCategory!.Code == "SUG-MIL");
        Assert.Contains(checks, x => x.Name == "Boiler shift log" && x.AssetCategory!.Code == "TEA-BLR");
        Assert.Contains(checks, x => x.Name == "Boiler shift log (Sugar mill)" && x.AssetCategory!.Code == "SUG-BLR");
        Assert.Equal(CheckFrequencies.PerShift, RunningCheckTemplates.SuggestedFrequency("Boiler shift log (Sugar mill)"));
        Assert.Equal(0, await RunningCheckTemplates.InstallAsync(db, tenant.Id));

        // Failure codes: ISO plus both industries; added later when the mill joins.
        var codes = new FailureCodeService(db);
        await codes.EnsureSeededAsync(tenant.Id);
        Assert.True(await db.FailureCodes.AnyAsync(x => x.Code == "CTC-TTH"));
        Assert.True(await db.FailureCodes.AnyAsync(x => x.Code == "MIL-CHK" && x.AssetCategory!.Code == "SUG-MIL"));
        Assert.True(await db.FailureCodes.AnyAsync(x => x.Code == "BRD"));

        // Visitor zones and patrol checkpoints are the mill's.
        var visitors = VisitorTests.Service(db);
        await visitors.SeedZonesAsync(tenant.Id, mill.Id);
        await visitors.SeedZonesAsync(tenant.Id, teaFactory.Id);
        Assert.True(await db.AccessZones.AnyAsync(x => x.FactoryId == mill.Id && x.Name == "Mill house"));
        Assert.False(await db.AccessZones.AnyAsync(x => x.FactoryId == mill.Id && x.Name == "Green leaf receiving"));
        Assert.True(await db.AccessZones.AnyAsync(x => x.FactoryId == teaFactory.Id && x.Name == "Green leaf receiving"));
        var patrol = new PatrolService(db);
        Assert.Equal(IndustryTemplates.Sugar.Checkpoints.Count, await patrol.SeedCheckpointsAsync(tenant.Id, mill.Id));
        Assert.Equal("Mill house", (await db.Checkpoints.Include(x => x.AccessZone).SingleAsync(x => x.FactoryId == mill.Id && x.Name == "Mill house")).AccessZone!.Name);

        // Statutory items: the mill's licence and effluent, not the tea factory's set.
        var compliance = new ComplianceService(db, FactoryTestSupport.WorkOrders(db, context), null);
        await compliance.InstallAsync(tenant.Id, mill.Id);
        var items = await db.ComplianceItems.Where(x => x.OrgNodeId == mill.Id).Select(x => x.TemplateKey).ToListAsync();
        Assert.Contains("sugar-licence", items);
        Assert.Contains("effluent-licence", items);
        Assert.Contains("fire-extinguishers", items);
    }

    [Fact]
    public async Task TheSettings_CarryTheIndustrysWords()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        db.FactorySettings.Add(new FactorySettings { TenantId = tenant.Id, IndustryTemplate = IndustryTemplates.DairyKey, ProductionUnit = "litres of milk processed", ProductionUnitShort = "L milk" });
        await db.SaveChangesAsync();
        Assert.Equal("dairy", (await IndustryTemplates.ForTenantAsync(db, tenant.Id)).Key);
        Assert.Equal(IndustryTemplates.Tea, await IndustryTemplates.ForTenantAsync(db, Guid.NewGuid()));
        Assert.Contains(IndustryTemplates.Dairy.Compliance, x => x.Key == "kdb-licence");
        Assert.Equal("Milk collection fleet", IndustryTemplates.Dairy.Vocabulary.SupplyFleet);
    }

    private static OrgNodeResponse Created(ActionResult<OrgNodeResponse> result) =>
        Assert.IsType<OrgNodeResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
