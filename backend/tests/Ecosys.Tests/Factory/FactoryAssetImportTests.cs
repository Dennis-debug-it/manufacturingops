using Ecosys.Api.Common;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class FactoryAssetImportTests
{
    [Fact]
    public async Task Template_ListsTheFactorysLocationsAndClasses_AndReadsBackAsRows()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);

        var file = Assert.IsType<FileContentResult>(await Controller(db, site.Context).Template(CancellationToken.None));
        Assert.Equal(TabularFiles.XlsxContentType, file.ContentType);

        var rows = TabularFiles.Read(new MemoryStream(file.FileContents), "template.xlsx");
        Assert.Equal(2, rows.Count);
        Assert.Equal("FBD-01", rows[0]["AssetCode"]);
        Assert.Equal("CHL-DRY", rows[0]["Location"]);
        Assert.Equal("FBD-01", rows[1]["ParentAssetCode"]);

        // The template's own example rows import cleanly.
        var preview = Ok(await Controller(db, site.Context).Preview(FormFile(file.FileContents, "template.xlsx"), CancellationToken.None));
        Assert.Equal(2, preview.ValidRows);
    }

    [Fact]
    public async Task Preview_ReportsEveryProblemByRow()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db, site.Context);

        var preview = await service.PreviewAsync(site.TenantId, site.Context.UserId!.Value,
        [
            Row("A-1", "Good", "CHL-DRY", assetClass: "fluid bed dryer"),
            Row("EXISTING", "Clash", "CHL-DRY"),
            Row("A-2", "Bad location", "NOPE"),
            Row("A-3", "At group", "GRP"),
            Row("A-4", "Bad class", "CHL-DRY", assetClass: "Flux capacitor"),
            Row("A-5", "Bad criticality", "CHL-DRY", criticality: "Z"),
            Row("A-6", "Orphan", null, parent: "MISSING"),
            Row("A-7", "Bad date", "CHL-DRY", installed: "31st March"),
            Row("A-1", "Duplicate", "CHL-DRY"),
            Row(null, null, null),
        ]);

        Assert.Equal(10, preview.TotalRows);
        Assert.Equal(0, preview.ValidRows);
        string Errors(int index) => string.Join(" | ", preview.Rows[index].Errors);
        Assert.Contains("appears more than once", Errors(0));
        Assert.Contains("already exists", Errors(1));
        Assert.Contains("'NOPE' is not a factory", Errors(2));
        Assert.Contains("not a group", Errors(3));
        Assert.Contains("'Flux capacitor' was not found", Errors(4));
        Assert.Contains("A, B or C", Errors(5));
        Assert.Contains("MISSING was not found", Errors(6));
        Assert.Contains("must be a date", Errors(7));
        Assert.Contains("AssetCode is required", Errors(9));
        Assert.Equal(11, preview.Rows[9].RowNumber); // spreadsheet row numbers: the header is row 1
    }

    [Fact]
    public async Task Commit_CreatesAssetsWithComponentsTagsAndClassDefaults()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db, site.Context);

        var result = await service.CommitAsync(site.TenantId, site.Context.UserId!.Value, "Importer",
        [
            // The component comes first in the file; its parent is created before it.
            Row("FBD-01-FAN", "Dryer fan", null, parent: "FBD-01", criticality: "b"),
            Row("FBD-01", "Fluid bed dryer 1", "CHL-DRY", assetClass: "TEA-FBD", tag: "chl-0001", installed: "2018-06-01"),
            Row("PMP-02", "Feed pump", "CHL-DRY", parent: "EXISTING"),
        ]);

        Assert.Equal(3, result.Created);
        Assert.Equal(1, result.Tagged);

        var dryer = await db.Assets.SingleAsync(x => x.AssetCode == "FBD-01");
        var fan = await db.Assets.SingleAsync(x => x.AssetCode == "FBD-01-FAN");
        Assert.Null(dryer.ClientId);
        Assert.Equal("A", dryer.Criticality); // from the class
        Assert.Equal(new DateTime(2018, 6, 1), dryer.InstallationDate);
        Assert.Equal(dryer.Id, fan.ParentAssetId);
        Assert.Equal(dryer.OrgNodeId, fan.OrgNodeId);
        Assert.Equal("B", fan.Criticality);
        Assert.Equal("CHL-0001", (await db.AssetTags.SingleAsync()).TagNumber);
        Assert.Equal(2, await db.Meters.CountAsync(x => x.AssetId == dryer.Id));
        Assert.Equal(site.Existing.Id, (await db.Assets.SingleAsync(x => x.AssetCode == "PMP-02")).ParentAssetId);
        Assert.Equal(3, await db.AssetLifecycleEvents.CountAsync(x => x.EventType == AssetLifecycleEventTypes.Registered));
    }

    [Fact]
    public async Task Commit_SavesNothingWhenAnyRowIsInvalid_AndRejectsParentLoops()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var service = Service(db, site.Context);
        var before = await db.Assets.CountAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CommitAsync(site.TenantId, site.Context.UserId!.Value, "Importer",
        [
            Row("OK-1", "Fine", "CHL-DRY"),
            Row("BAD-1", "Broken", "NOPE"),
        ]));
        Assert.Equal(before, await db.Assets.CountAsync());

        var loop = await service.PreviewAsync(site.TenantId, site.Context.UserId!.Value,
        [
            Row("X", "X", null, parent: "Y"),
            Row("Y", "Y", null, parent: "X"),
        ]);
        Assert.All(loop.Rows, row => Assert.NotEmpty(row.Errors));
    }

    [Fact]
    public async Task ScopedUser_CanOnlyImportIntoTheirFactory()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var other = new OrgNode { TenantId = site.TenantId, Level = OrgNodeLevels.Factory, Name = "Momul", Code = "MML" };
        var engineer = FactoryTestSupport.NewUser(site.TenantId, "engineer@chelal.test");
        db.OrgNodes.Add(other);
        db.Users.Add(engineer);
        db.UserOrgNodeAssignments.Add(new UserOrgNodeAssignment { TenantId = site.TenantId, UserId = engineer.Id, OrgNodeId = site.Chelal });
        await db.SaveChangesAsync();
        var context = new FactoryTenantContext(site.TenantId, engineer.Id, false, Ecosys.Shared.Auth.PermissionNames.CanManageAssets);

        var preview = await Service(db, context).PreviewAsync(site.TenantId, engineer.Id, [Row("A", "Ours", "CHL-DRY"), Row("B", "Theirs", "MML")]);
        Assert.Empty(preview.Rows[0].Errors);
        Assert.Contains("don't have access", Assert.Single(preview.Rows[1].Errors));
    }

    private sealed record Seeded(Guid TenantId, FactoryTenantContext Context, Guid Chelal, Asset Existing);

    private static async Task<Seeded> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        await new AssetClassService(db).InstallAsync(tenant.Id, IndustryTemplates.TeaKey);
        var group = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Group, Name = "Group", Code = "GRP" };
        var chelal = new OrgNode { TenantId = tenant.Id, ParentId = group.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var drying = new OrgNode { TenantId = tenant.Id, ParentId = chelal.Id, Level = OrgNodeLevels.Section, Name = "Drying", Code = "CHL-DRY" };
        var existing = new Asset { TenantId = tenant.Id, AssetName = "Boiler feed system", AssetCode = "EXISTING", OrgNodeId = drying.Id };
        db.OrgNodes.AddRange(group, chelal, drying);
        db.Assets.Add(existing);
        await db.SaveChangesAsync();
        return new Seeded(tenant.Id, new FactoryTenantContext(tenant.Id, admin.Id), chelal.Id, existing);
    }

    private static IReadOnlyDictionary<string, string?> Row(
        string? code, string? name, string? location,
        string? assetClass = null, string? parent = null, string? criticality = null, string? tag = null, string? installed = null) =>
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["AssetCode"] = code,
            ["AssetName"] = name,
            ["Location"] = location,
            ["AssetClass"] = assetClass,
            ["ParentAssetCode"] = parent,
            ["Criticality"] = criticality,
            ["TagNumber"] = tag,
            ["InstallationDate"] = installed,
        };

    private static IFormFile FormFile(byte[] content, string name) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", name);

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static FactoryAssetImportService Service(AppDbContext db, FactoryTenantContext context)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        return new FactoryAssetImportService(db, scope, new AssetLifecycleService(db, scope), new AssetClassService(db),
            new AssetTagNumberService(db, FactoryTestSupport.Settings(db)));
    }

    private static FactoryAssetImportController Controller(AppDbContext db, FactoryTenantContext context) =>
        FactoryAssetRegisterTests.WithHttp(new FactoryAssetImportController(db, context, FactoryTestSupport.Access(context),
            FactoryTestSupport.Scope(db, context), Service(db, context), FactoryTestSupport.Audit(db)));
}
