using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services.Factory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class AssetClassTests
{
    [Fact]
    public async Task NewTenant_StartsWithTheTeaAssetClasses()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);

        Assert.IsType<OkObjectResult>(await Controller(db, new FactoryTenantContext(tenant.Id, admin.Id)).List(false, CancellationToken.None));

        var classes = await db.AssetCategories.Include(x => x.Fields).Where(x => x.TenantId == tenant.Id).ToListAsync();
        Assert.Equal(IndustryTemplates.Tea.AssetClasses.Count, classes.Count);

        var ctc = classes.Single(x => x.Name == "CTC machine");
        Assert.Equal("TEA-CTC", ctc.Code);
        Assert.Equal("Processing", ctc.ParentCategoryName);
        Assert.Equal("A", ctc.DefaultCriticality);
        Assert.Equal("RunHours,Production", ctc.DefaultMeterTypes);
        Assert.Contains(ctc.Fields, x => x.FieldLabel == "Teeth per inch" && x.FieldType == "Number" && x.Unit == "TPI");

        var boiler = classes.Single(x => x.Name == "Boiler");
        Assert.Contains(boiler.Fields, x => x.FieldLabel == "Fuel" && x.FieldType == "Dropdown" && x.DropdownOptions!.Contains("Firewood"));
        Assert.Contains(classes, x => x.Name == "Vehicle" && x.DefaultMeterTypes == MeterTypes.Odometer);
    }

    [Fact]
    public async Task ApplyingAPack_AddsOnlyMissingClasses()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var controller = Controller(db, new FactoryTenantContext(tenant.Id, admin.Id));
        db.AssetCategories.Add(new AssetCategory { TenantId = tenant.Id, Name = "boiler", Icon = "tool" });
        await db.SaveChangesAsync();

        var first = Ok(await controller.ApplyIndustryPack(new ApplyIndustryPackRequest(null), CancellationToken.None));
        Assert.Equal(1, first.Skipped);
        Assert.Equal(IndustryTemplates.Tea.AssetClasses.Count - 1, first.Created);

        var again = Ok(await controller.ApplyIndustryPack(new ApplyIndustryPackRequest("tea"), CancellationToken.None));
        Assert.Equal(0, again.Created);

        // Another industry's classes are added alongside (a group with mixed factories); where a name is
        // taken by the tea class, the new one says whose it is.
        var generic = Ok(await controller.ApplyIndustryPack(new ApplyIndustryPackRequest("generic"), CancellationToken.None));
        Assert.Equal(IndustryTemplates.Generic.AssetClasses.Count - 1, generic.Created); // not its Boiler: the tenant made its own
        Assert.True(await db.AssetCategories.AnyAsync(x => x.Code == "GEN-MTR" && x.Name == "Electric motor (General manufacturing)"));
        Assert.True(await db.AssetCategories.AnyAsync(x => x.Code == "GEN-LIN" && x.Name == "Production machine"));
        Assert.Equal(0, Ok(await controller.ApplyIndustryPack(new ApplyIndustryPackRequest("generic"), CancellationToken.None)).Created);
    }

    [Fact]
    public async Task NewAsset_TakesItsClassDefaults_UnlessCriticalityIsChosen()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var context = new FactoryTenantContext(tenant.Id, admin.Id);
        await new AssetClassService(db).InstallAsync(tenant.Id, IndustryTemplates.TeaKey);
        var section = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        db.OrgNodes.Add(section);
        await db.SaveChangesAsync();
        var ctc = await db.AssetCategories.SingleAsync(x => x.Code == "TEA-CTC");
        var assets = FactoryAssetRegisterTests.AssetsController(db, context);

        var line1 = await CreateAsync(assets, section.Id, ctc.Id, "CTC line 1", "CTC-01", null);
        var line2 = await CreateAsync(assets, section.Id, ctc.Id, "CTC line 2", "CTC-02", "B");

        Assert.Equal("A", line1.Criticality);
        Assert.Equal("B", line2.Criticality);

        var meters = await db.Meters.Where(x => x.AssetId == line1.Id).OrderBy(x => x.Name).ToListAsync();
        Assert.Equal(["Production", "Run hours"], meters.Select(x => x.Name).ToArray());
        Assert.Equal("kg made tea", meters.Single(x => x.MeterType == MeterTypes.Production).Unit);
        Assert.True(meters.Single(x => x.MeterType == MeterTypes.RunHours).IsPrimary);
        Assert.Single(meters, x => x.IsPrimary);
    }

    private static async Task<AssetResponse> CreateAsync(AssetsController controller, Guid orgNodeId, Guid categoryId, string name, string code, string? criticality)
    {
        var request = new UpsertAssetRequest(null, null, null, null, categoryId, name, code, null, null, null, null, null, null,
            null, null, null, null, null, false, null, null, null, "Active", null, OrgNodeId: orgNodeId, Criticality: criticality);
        var result = await controller.Create(request, CancellationToken.None);
        return Assert.IsType<AssetResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static AssetCategoriesController Controller(AppDbContext db, FactoryTenantContext context) =>
        new(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Settings(db), new AssetClassService(db));
}
