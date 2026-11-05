using System.Text;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class ComplianceTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    [Fact]
    public async Task The_statutory_pack_adds_items_per_boiler_and_compressor_and_once_per_factory_for_the_rest()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        await AddAssetAsync(db, site, "BLR-01", "Firewood boiler", "TEA-BLR");
        await AddAssetAsync(db, site, "CMP-01", "Air compressor", "TEA-CMP");
        var compliance = Controller(db, site.Admin, new RecordingDispatcher());

        var added = Ok(await compliance.Install(new InstallComplianceRequest(site.Chelal), CancellationToken.None)).ItemsAdded;
        Assert.Equal(0, Ok(await compliance.Install(new InstallComplianceRequest(site.Chelal), CancellationToken.None)).ItemsAdded);
        var items = Ok(await compliance.Items(null, null, null, null, false, CancellationToken.None));
        Assert.Equal(added, items.Count);
        Assert.Contains(items, x => x.Name == "Boiler examination by an approved person – BLR-01" && x.IntervalMonths == 14 && x.OrgNodeId == site.Drying);
        Assert.Contains(items, x => x.Name == "Air receiver (pressure vessel) examination – CMP-01");
        Assert.Contains(items, x => x.Name == "Fire extinguishers service" && x.AssetId == null && x.OrgNodeId == site.Chelal);
        Assert.DoesNotContain(items, x => x.Name.StartsWith("Weighbridge"));
        Assert.All(items, x => Assert.Equal(ComplianceStatuses.NoDate, x.Status));
    }

    [Fact]
    public async Task Recording_a_certificate_moves_the_due_date_on_and_a_failed_examination_makes_it_due_now()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var boiler = await AddAssetAsync(db, site, "BLR-01", "Firewood boiler", "TEA-BLR");
        var compliance = Controller(db, site.Admin, new RecordingDispatcher(), new MemoryStorage());
        var item = Ok(await compliance.Create(new SaveComplianceItemRequest("Boiler examination – BLR-01", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.67", 12, null, boiler,
            Today.AddMonths(-11), null, null, "DOSH approved person: J. Kamau", 30, null, null), CancellationToken.None)).Item;
        Assert.Equal(Today.AddMonths(1), item.DueDate);
        Assert.Equal(ComplianceStatuses.DueSoon, item.Status);

        var passed = Ok(await compliance.Record(item.Id, new ComplianceRecordForm
        {
            DoneAt = Today.AddDays(-2), Result = "Pass", CertificateNumber = "DOSH/BLR/2026/118", Examiner = "J. Kamau", Certificate = Pdf("certificate.pdf"),
        }, CancellationToken.None));
        Assert.Equal(Today.AddDays(-2).AddMonths(12), passed.Item.DueDate);
        Assert.Equal(ComplianceStatuses.Ok, passed.Item.Status);
        Assert.Equal("DOSH/BLR/2026/118", passed.Item.LastCertificateNumber);
        var record = Assert.Single(passed.Records);
        Assert.Equal("certificate.pdf", record.CertificateFileName);
        Assert.IsType<FileStreamResult>(await compliance.Certificate(record.CertificateId!.Value, CancellationToken.None));

        // An older record doesn't move the due date back.
        var older = Ok(await compliance.Record(item.Id, new ComplianceRecordForm { DoneAt = Today.AddMonths(-12), Result = "Pass" }, CancellationToken.None));
        Assert.Equal(passed.Item.DueDate, older.Item.DueDate);

        var failed = Ok(await compliance.Record(item.Id, new ComplianceRecordForm { DoneAt = Today.AddDays(-1), Result = "Fail", Notes = "Safety valve seized" }, CancellationToken.None));
        Assert.Equal(Today.AddDays(-1), failed.Item.DueDate);
        Assert.Equal(ComplianceStatuses.Overdue, failed.Item.Status);

        await Assert.ThrowsAsync<BusinessRuleException>(() => compliance.Record(item.Id, new ComplianceRecordForm { DoneAt = Today.AddDays(-1), Result = "Maybe" }, CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => compliance.Record(item.Id, new ComplianceRecordForm { DoneAt = Today.AddDays(5), Result = "Pass" }, CancellationToken.None));
    }

    [Fact]
    public async Task Items_falling_due_raise_one_statutory_job_and_alert_at_30_14_and_7_days_then_weekly_once_overdue()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var dispatcher = new RecordingDispatcher();
        var compliance = Controller(db, site.Admin, dispatcher);
        var item = Ok(await compliance.Create(new SaveComplianceItemRequest("Lifting tackle examination", ComplianceKinds.StatutoryInspection, null, 6, site.Drying, null,
            null, Today.AddDays(20), site.Admin.UserId, null, 30, null, null), CancellationToken.None)).Item;
        var service = Service(db, site.Admin, dispatcher);

        var (jobs, alerts) = await service.RunAsync(site.TenantId, Today);
        Assert.Equal((1, 1), (jobs, alerts));
        var job = await db.WorkOrders.SingleAsync();
        Assert.Equal(FactoryWorkTypes.Statutory, job.WorkType);
        Assert.Equal(WorkOrderStatuses.Approved, job.Status);
        Assert.Equal(Today.AddDays(20), job.DueDate);
        Assert.Equal(ComplianceService.DueKey, dispatcher.Sent.Single().Key);
        Assert.Contains(site.Admin.UserId!.Value, dispatcher.Sent.Single().UserIds);
        Assert.Equal(job.WorkOrderNumber, Ok(await compliance.Get(item.Id, CancellationToken.None)).Item.OpenWorkOrderNumber);

        Assert.Equal((0, 0), await service.RunAsync(site.TenantId, Today.AddDays(1)));
        Assert.Equal((0, 1), await service.RunAsync(site.TenantId, Today.AddDays(8)));   // 12 days left: the 14-day alert
        Assert.Equal((0, 1), await service.RunAsync(site.TenantId, Today.AddDays(15)));  // 5 days left: the 7-day alert
        Assert.Equal((0, 0), await service.RunAsync(site.TenantId, Today.AddDays(16)));
        Assert.Equal((0, 1), await service.RunAsync(site.TenantId, Today.AddDays(21)));  // overdue
        Assert.Equal(ComplianceService.OverdueKey, dispatcher.Sent.Last().Key);
        Assert.Equal((0, 0), await service.RunAsync(site.TenantId, Today.AddDays(24)));
        Assert.Equal((0, 1), await service.RunAsync(site.TenantId, Today.AddDays(29)));  // a week later, again

        var summary = Ok(await compliance.Summary(null, CancellationToken.None));
        Assert.Equal(1, summary.Total);
        Assert.Equal(0, summary.Overdue); // overdue by the real clock only from day 21
        Assert.Equal(1, summary.DueSoon);
    }

    [Fact]
    public async Task Compliance_items_stay_inside_the_users_area()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var other = new OrgNode { TenantId = site.TenantId, Level = OrgNodeLevels.Factory, Name = "Momul", Code = "MML" };
        db.OrgNodes.Add(other);
        await db.SaveChangesAsync();
        var theirs = Ok(await Controller(db, site.Admin, new RecordingDispatcher()).Create(new SaveComplianceItemRequest("Fire audit", ComplianceKinds.FireSafety, null, 12, other.Id, null,
            null, Today.AddDays(5), null, null, null, null, null), CancellationToken.None)).Item;

        var supervisor = FactoryTestSupport.NewUser(site.TenantId, "sup@chelal.test", AppRoles.Manager);
        db.Users.Add(supervisor);
        db.UserOrgNodeAssignments.Add(new UserOrgNodeAssignment { TenantId = site.TenantId, UserId = supervisor.Id, OrgNodeId = site.Chelal });
        await db.SaveChangesAsync();
        var mine = Controller(db, new FactoryTenantContext(site.TenantId, supervisor.Id, false, PermissionNames.CanViewWorkOrders, PermissionNames.CanAssignWorkOrders), new RecordingDispatcher());
        Assert.Empty(Ok(await mine.Items(null, null, null, null, false, CancellationToken.None)));
        await Assert.ThrowsAsync<NotFoundException>(() => mine.Get(theirs.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => mine.Create(new SaveComplianceItemRequest("Sneaky", ComplianceKinds.Other, null, 12, other.Id, null, null, null, null, null, null, null, null), CancellationToken.None));
    }

    private static async Task<Guid> AddAssetAsync(AppDbContext db, FactoryWorkOrderTests.Seeded site, string code, string name, string classCode)
    {
        var category = await db.AssetCategories.FirstOrDefaultAsync(x => x.Code == classCode)
            ?? db.AssetCategories.Add(new AssetCategory { TenantId = site.TenantId, Name = classCode, Code = classCode }).Entity;
        var asset = new Asset { TenantId = site.TenantId, AssetCode = code, AssetName = name, OrgNodeId = site.Drying, AssetCategoryId = category.Id };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return asset.Id;
    }

    private static IFormFile Pdf(string name)
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\ncertificate\n%%EOF");
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "certificate", name) { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    }

    private static ComplianceService Service(AppDbContext db, FactoryTenantContext context, INotificationDispatcher dispatcher)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        var workOrders = new FactoryWorkOrderService(db, new DocumentNumberingService(db), FactoryTestSupport.Settings(db), new AssetLifecycleService(db, scope), scope, new FactorySlaService(db), new FactoryPmProgress(db), new FactoryJobGuards(db));
        return new ComplianceService(db, workOrders, dispatcher);
    }

    private static ComplianceController Controller(AppDbContext db, FactoryTenantContext context, INotificationDispatcher dispatcher, IFileStorageService? storage = null) =>
        FactoryAssetRegisterTests.WithHttp(new ComplianceController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context),
            Service(db, context, dispatcher), storage ?? new MemoryStorage()));

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private sealed class RecordingDispatcher : INotificationDispatcher
    {
        public List<NotificationRequest> Sent { get; } = [];
        public Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }
    }
}
