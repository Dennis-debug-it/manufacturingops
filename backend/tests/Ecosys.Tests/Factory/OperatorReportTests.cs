using System.Text;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class OperatorReportTests
{
    [Fact]
    public async Task Operator_ReportsAFault_FromTheLabelWithoutLoggingIn()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (site, key) = await SeedAsync(db);
        var portal = Portal(db);

        var view = Ok(await portal.Get(key, "tag-000001", CancellationToken.None));
        Assert.Equal("FBD-01", view.AssetCode);
        Assert.Equal("Drying", view.Location);
        Assert.Null(view.OpenJobNumber);

        var result = Ok(await portal.Submit(key, "TAG-000001", new OperatorReportForm
        {
            Description = "Loud knocking from the dryer fan",
            ReporterName = "Jane Operator",
            Phone = "0712 000 000",
            Photo = Png("knock.png"),
        }, CancellationToken.None));
        Assert.False(result.AddedToOpenJob);

        var job = await db.WorkOrders.SingleAsync(x => x.WorkOrderNumber == result.WorkOrderNumber);
        Assert.Equal(FactoryRequestSources.Operator, job.RequestSource);
        Assert.Equal("Jane Operator (0712 000 000)", job.RequestedByName);
        Assert.Equal(WorkOrderStatuses.Requested, job.Status);
        Assert.Equal("High", job.Priority); // criticality A asset
        Assert.False(job.IsBreakdown);
        var photo = await db.WorkOrderPhotoEvidence.SingleAsync(x => x.WorkOrderId == job.Id);
        var uploader = await db.Users.SingleAsync(x => x.Id == photo.UploadedByUserId);
        Assert.False(uploader.IsActive); // the hidden operator account can't sign in
        Assert.Contains(await db.WorkOrderEvents.Where(x => x.WorkOrderId == job.Id).ToListAsync(), x => x.EventType == "OperatorReport" && x.Note!.StartsWith("Jane"));
    }

    [Fact]
    public async Task SecondReport_IsAddedToTheOpenJob_AndAStoppedMachineIsABreakdown()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (site, key) = await SeedAsync(db);
        var portal = Portal(db);

        var first = Ok(await portal.Submit(key, "TAG-000001", new OperatorReportForm { Description = "Dryer stopped", ReporterName = "Jane", MachineStopped = true }, CancellationToken.None));
        var second = Ok(await portal.Submit(key, "TAG-000001", new OperatorReportForm { Description = "Still stopped, smoke smell", ReporterName = "Ali" }, CancellationToken.None));

        Assert.True(second.AddedToOpenJob);
        Assert.Equal(first.WorkOrderNumber, second.WorkOrderNumber);
        var job = await db.WorkOrders.SingleAsync();
        Assert.True(job.IsBreakdown);
        Assert.Equal(WorkOrderStatuses.Approved, job.Status); // breakdowns skip approval
        Assert.Equal(AssetOperationalStatuses.Down, (await db.Assets.SingleAsync(x => x.Id == site.Dryer.Id)).OperationalStatus);
        Assert.Equal(first.WorkOrderNumber, Ok(await portal.Get(key, "TAG-000001", CancellationToken.None)).OpenJobNumber);
    }

    [Fact]
    public async Task WrongKeyWrongTagOrReportingOff_AllLookTheSame()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (site, key) = await SeedAsync(db);
        var portal = Portal(db);

        var wrongKey = await Assert.ThrowsAsync<NotFoundException>(() => portal.Get("not-the-key", "TAG-000001", CancellationToken.None));
        var wrongTag = await Assert.ThrowsAsync<NotFoundException>(() => portal.Get(key, "TAG-999999", CancellationToken.None));
        Assert.Equal(wrongKey.Message, wrongTag.Message);

        (await db.FactorySettings.SingleAsync()).OperatorReportingEnabled = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => portal.Get(key, "TAG-000001", CancellationToken.None));
    }

    [Fact]
    public async Task ManyReportsOnOneMachine_AreRefused_AndBadInputIsRejected()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (_, key) = await SeedAsync(db);
        var portal = Portal(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() => portal.Submit(key, "TAG-000001", new OperatorReportForm { Description = " ", ReporterName = "Jane" }, CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => portal.Submit(key, "TAG-000001", new OperatorReportForm { Description = "x", ReporterName = "" }, CancellationToken.None));
        var script = new FormFile(new MemoryStream(Encoding.ASCII.GetBytes("<script>")), 0, 8, "Photo", "a.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        await Assert.ThrowsAsync<BusinessRuleException>(() => portal.Submit(key, "TAG-000001", new OperatorReportForm { Description = "x", ReporterName = "J", Photo = script }, CancellationToken.None));

        for (var i = 0; i < OperatorReportService.MaxReportsPerAssetPerHour; i++)
        {
            Ok(await portal.Submit(key, "TAG-000001", new OperatorReportForm { Description = $"Report {i}", ReporterName = "Jane" }, CancellationToken.None));
        }

        await Assert.ThrowsAsync<BusinessRuleException>(() => portal.Submit(key, "TAG-000001", new OperatorReportForm { Description = "One more", ReporterName = "Jane" }, CancellationToken.None));
    }

    [Fact]
    public async Task OnlyAdminsSeeTheKey_AndRegeneratingItRetiresOldLabels()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var (site, key) = await SeedAsync(db);
        var planner = FactoryTestSupport.NewUser(site.TenantId, "planner@chelal.test");
        db.Users.Add(planner);
        await db.SaveChangesAsync();

        var plannerView = Ok(await OrgHierarchyTests.SettingsController(db, new FactoryTenantContext(site.TenantId, planner.Id, false)).Get(CancellationToken.None));
        Assert.Null(plannerView.OperatorReportKey);

        var admin = OrgHierarchyTests.SettingsController(db, site.Admin);
        Assert.Equal(key, Ok(await admin.Get(CancellationToken.None)).OperatorReportKey);
        var renewed = Ok(await admin.RegenerateReportKey(CancellationToken.None));
        Assert.NotEqual(key, renewed.OperatorReportKey);

        await Assert.ThrowsAsync<NotFoundException>(() => Portal(db).Get(key, "TAG-000001", CancellationToken.None));
        Assert.Equal("FBD-01", Ok(await Portal(db).Get(renewed.OperatorReportKey!, "TAG-000001", CancellationToken.None)).AssetCode);
    }

    private static async Task<(FactoryWorkOrderTests.Seeded Site, string Key)> SeedAsync(AppDbContext db)
    {
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        db.AssetTags.Add(new AssetTag { TenantId = site.TenantId, AssetId = site.Dryer.Id, TagNumber = "TAG-000001" });
        await db.SaveChangesAsync();
        var settings = await FactoryTestSupport.Settings(db).GetOrCreateAsync(site.TenantId);
        return (site, settings.OperatorReportKey!);
    }

    private static IFormFile Png(string name)
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52];
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Photo", name) { Headers = new HeaderDictionary(), ContentType = "image/png" };
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    /// <summary>The portal as an anonymous caller: no tenant context, so every query must filter by the key's tenant itself.</summary>
    private static OperatorFaultReportController Portal(AppDbContext db)
    {
        var anonymous = new FactoryTenantContext(Guid.Empty, Guid.Empty, false);
        var scope = FactoryTestSupport.Scope(db, anonymous);
        var workOrders = new FactoryWorkOrderService(db, new DocumentNumberingService(db), FactoryTestSupport.Settings(db), new AssetLifecycleService(db, scope), scope, new FactorySlaService(db));
        var service = new OperatorReportService(db, workOrders, new AssetTagNumberService(db, FactoryTestSupport.Settings(db)), new MemoryStorage(), new PasswordHasher<User>());
        return FactoryAssetRegisterTests.WithHttp(new OperatorFaultReportController(service));
    }
}
