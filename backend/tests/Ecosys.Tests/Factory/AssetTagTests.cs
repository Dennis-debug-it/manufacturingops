using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class AssetTagTests
{
    [Fact]
    public async Task Tags_AreNumberedInSequence_AndAnAssetHasOnlyOneActiveTag()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var tags = Controller(db, site.Context);

        var first = Ok(await tags.Issue(site.Dryer.Id, new IssueAssetTagRequest(), CancellationToken.None));
        var second = Ok(await tags.Issue(site.Boiler.Id, new IssueAssetTagRequest(), CancellationToken.None));

        Assert.Equal("TAG-000001", first.TagNumber);
        Assert.Equal("TAG-000002", second.TagNumber);
        Assert.Equal("https://factory.test/scan/TAG-000001", first.ScanUrl);
        await Assert.ThrowsAsync<BusinessRuleException>(() => tags.Issue(site.Dryer.Id, new IssueAssetTagRequest(), CancellationToken.None));
        Assert.Contains(await db.AssetLifecycleEvents.Where(x => x.AssetId == site.Dryer.Id).ToListAsync(),
            x => x.EventType == AssetLifecycleEventTypes.Tagged && x.ToValue == "TAG-000001");
    }

    [Fact]
    public async Task PrePrintedTag_CanBeRegistered_ButNumbersAreNeverReused()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var tags = Controller(db, site.Context);

        var preprinted = Ok(await tags.Issue(site.Dryer.Id, new IssueAssetTagRequest(" chl-0042 ", "nfc"), CancellationToken.None));
        Assert.Equal("CHL-0042", preprinted.TagNumber);
        Assert.Equal(AssetTagSymbologies.Nfc, preprinted.Symbology);

        Ok(await tags.Retire(preprinted.Id, new RetireAssetTagRequest("Asset re-labelled"), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => tags.Issue(site.Boiler.Id, new IssueAssetTagRequest("CHL-0042"), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => tags.Issue(site.Boiler.Id, new IssueAssetTagRequest("bad tag!"), CancellationToken.None));
    }

    [Fact]
    public async Task ReplacingADamagedTag_IssuesANewNumber_AndTheOldOneStillFindsTheAsset()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var tags = Controller(db, site.Context);
        var old = Ok(await tags.Issue(site.Dryer.Id, new IssueAssetTagRequest(), CancellationToken.None));

        await Assert.ThrowsAsync<BusinessRuleException>(() => tags.Replace(old.Id, new ReplaceAssetTagRequest(" "), CancellationToken.None));
        var replacement = Ok(await tags.Replace(old.Id, new ReplaceAssetTagRequest("Burnt by dryer heat"), CancellationToken.None));

        Assert.Equal("TAG-000002", replacement.TagNumber);
        var history = Ok(await tags.History(site.Dryer.Id, CancellationToken.None));
        Assert.Equal(1, history.Count(x => x.Status == AssetTagStatuses.Active));
        Assert.Equal(replacement.Id, history.Single(x => x.Id == old.Id).ReplacedByTagId);

        var scan = Ok(await tags.Scan(old.TagNumber, CancellationToken.None));
        Assert.Equal(site.Dryer.Id, scan.AssetId);
        Assert.Equal("TAG-000002", scan.ActiveTagNumber);
        Assert.Contains("replaced", scan.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scan_AcceptsTagNumbersLinksAndAssetCodes_AndShowsWhatATechnicianNeeds()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var tags = Controller(db, site.Context);
        var tag = Ok(await tags.Issue(site.Motor.Id, new IssueAssetTagRequest(), CancellationToken.None));
        db.Meters.Add(new Meter { TenantId = site.TenantId, AssetId = site.Motor.Id, Name = "Run hours", Unit = "h", CurrentValue = 812m });
        await db.SaveChangesAsync();

        var byNumber = Ok(await tags.Scan(tag.TagNumber.ToLowerInvariant(), CancellationToken.None));
        Assert.Equal("Tag", byNumber.MatchedBy);
        Assert.Null(byNumber.Warning);
        Assert.Equal("Chelal › Drying", byNumber.Location);
        Assert.Equal(site.Dryer.Id, byNumber.Parent?.Id);
        Assert.Equal(812m, Assert.Single(byNumber.Meters).CurrentValue);

        var byLink = Ok(await tags.Scan(tag.ScanUrl, CancellationToken.None));
        Assert.Equal(site.Motor.Id, byLink.AssetId);

        var byCode = Ok(await tags.Scan("fbd-01", CancellationToken.None));
        Assert.Equal(site.Dryer.Id, byCode.AssetId);
        Assert.Equal("AssetCode", byCode.MatchedBy);
        Assert.Equal("This asset has no tag yet.", byCode.Warning);
        Assert.Equal(site.Motor.Id, Assert.Single(byCode.Components).Id);

        await Assert.ThrowsAsync<NotFoundException>(() => tags.Scan("TAG-999999", CancellationToken.None));
    }

    [Fact]
    public async Task ScopedUser_CannotScanAssetsInAnotherFactory()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var tag = Ok(await Controller(db, site.Context).Issue(site.Boiler.Id, new IssueAssetTagRequest(), CancellationToken.None));

        var outsider = FactoryTestSupport.NewUser(site.TenantId, "fitter@other.test");
        var otherFactory = new OrgNode { TenantId = site.TenantId, Level = OrgNodeLevels.Factory, Name = "Momul", Code = "MML" };
        db.Users.Add(outsider);
        db.OrgNodes.Add(otherFactory);
        db.UserOrgNodeAssignments.Add(new UserOrgNodeAssignment { TenantId = site.TenantId, UserId = outsider.Id, OrgNodeId = otherFactory.Id });
        await db.SaveChangesAsync();

        var tags = Controller(db, new FactoryTenantContext(site.TenantId, outsider.Id, isAdmin: false));
        await Assert.ThrowsAsync<NotFoundException>(() => tags.Scan(tag.TagNumber, CancellationToken.None));
    }

    [Fact]
    public async Task BulkIssue_TagsOnlyUntaggedActiveAssets_AndLabelsPrintAsPdf()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var tags = Controller(db, site.Context);
        Ok(await tags.Issue(site.Dryer.Id, new IssueAssetTagRequest(), CancellationToken.None));
        site.Retired.Status = "Inactive";
        await db.SaveChangesAsync();

        var bulk = Ok(await tags.BulkIssue(new BulkIssueTagsRequest(null, site.Factory), CancellationToken.None));
        Assert.Equal(2, bulk.Issued); // boiler and motor; dryer already tagged, retired asset skipped
        Assert.Equal(2, bulk.Skipped);

        var file = Assert.IsType<FileContentResult>(await tags.Labels(new PrintLabelsRequest(null, null, site.Factory, "a4-10"), CancellationToken.None));
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(file.FileContents, 0, 4));
        Assert.All(await db.AssetTags.ToListAsync(), x => Assert.Equal(1, x.PrintCount));

        await Assert.ThrowsAsync<BusinessRuleException>(() => tags.Labels(new PrintLabelsRequest(null, null, site.Factory, "letter"), CancellationToken.None));
    }

    [Fact]
    public async Task TagNumbers_StayUniqueWhenReservedTogether()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (tenant, _) = await FactoryTestSupport.SeedTenantAsync(db);
        var service = new AssetTagNumberService(db, FactoryTestSupport.Settings(db));

        var batch = await service.ReserveAsync(tenant.Id, 3);
        var next = await service.ReserveAsync(tenant.Id, 1);

        Assert.Equal(["TAG-000001", "TAG-000002", "TAG-000003"], batch);
        Assert.Equal("TAG-000004", Assert.Single(next));
        Assert.Equal("TAG-000007", service.NormalizeCode("https://factory.test/scan/tag-000007"));
    }

    private sealed record Seeded(Guid TenantId, FactoryTenantContext Context, Guid Factory, Asset Dryer, Asset Motor, Asset Boiler, Asset Retired);

    private static async Task<Seeded> SeedAsync(AppDbContext db)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var group = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Group, Name = "Group", Code = "G" };
        var factory = new OrgNode { TenantId = tenant.Id, ParentId = group.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        var drying = new OrgNode { TenantId = tenant.Id, ParentId = factory.Id, Level = OrgNodeLevels.Section, Name = "Drying", Code = "CHL-DRY" };
        var dryer = new Asset { TenantId = tenant.Id, AssetName = "Fluid bed dryer", AssetCode = "FBD-01", OrgNodeId = drying.Id, Criticality = "A" };
        var motor = new Asset { TenantId = tenant.Id, AssetName = "Dryer fan motor", AssetCode = "FBD-01-M", OrgNodeId = drying.Id, ParentAssetId = dryer.Id };
        var boiler = new Asset { TenantId = tenant.Id, AssetName = "Boiler", AssetCode = "BLR-01", OrgNodeId = factory.Id };
        var retired = new Asset { TenantId = tenant.Id, AssetName = "Old pump", AssetCode = "PMP-OLD", OrgNodeId = factory.Id };
        db.OrgNodes.AddRange(group, factory, drying);
        db.Assets.AddRange(dryer, motor, boiler, retired);
        await db.SaveChangesAsync();
        return new Seeded(tenant.Id, new FactoryTenantContext(tenant.Id, admin.Id), factory.Id, dryer, motor, boiler, retired);
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static AssetTagsController Controller(AppDbContext db, FactoryTenantContext context)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        return FactoryAssetRegisterTests.WithHttp(new AssetTagsController(
            db,
            context,
            FactoryTestSupport.Access(context),
            scope,
            new AssetTagNumberService(db, FactoryTestSupport.Settings(db)),
            new AssetLabelPdfRenderer(),
            new AssetLifecycleService(db, scope),
            FactoryTestSupport.Audit(db),
            Microsoft.Extensions.Options.Options.Create(new PublicAppOptions { PublicUrl = "https://factory.test/" })));
    }
}
