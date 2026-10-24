using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

/// <summary>
/// The daily "work order overdue" email to admins. It rendered by its event key, which has no built-in
/// template, so every send threw "template not found" inside a catch-all and none ever went out. These
/// run the sweep on the real template, subject-rule and outbox services.
/// </summary>
public sealed class OverdueWorkOrderEmailTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OverdueOpenJob_EmailsEachAdmin_OncePerDay()
    {
        await using var db = CreateDbContext();
        var (tenant, workOrder) = await SeedAsync(db);
        AddAdmin(db, tenant, "Amina Otieno", "amina@acme.test");
        AddAdmin(db, tenant, "Brian Admin", "brian@acme.test");
        await db.SaveChangesAsync();
        var worker = CreateWorker();
        using var services = CreateServices(db);

        Assert.Equal(1, await worker.RunOverdueNotificationsAsync(db, services, Now, CancellationToken.None));

        var emails = await db.EmailOutboxMessages.OrderBy(x => x.RecipientEmail).ToListAsync();
        Assert.Equal(["amina@acme.test", "brian@acme.test"], emails.Select(x => x.RecipientEmail));
        Assert.All(emails, email =>
        {
            Assert.Equal(PmSchedulerWorker.OverdueEventKey, email.EventKey);
            Assert.Contains(workOrder.WorkOrderNumber, email.Subject);
            Assert.Contains("Generator 01", email.HtmlBody);
        });
        Assert.True(await db.WorkOrderEvents.AnyAsync(x => x.WorkOrderId == workOrder.Id && x.EventType == "OverdueNotified"));

        // The next run the same day sends nothing more.
        Assert.Equal(0, await worker.RunOverdueNotificationsAsync(db, services, Now.AddHours(2), CancellationToken.None));
        Assert.Equal(2, await db.EmailOutboxMessages.CountAsync());
    }

    [Fact]
    public async Task AdminsWhoTurnedItOff_OrHaveNoMailbox_AreSkipped()
    {
        await using var db = CreateDbContext();
        var (tenant, _) = await SeedAsync(db);
        var optedOut = AddAdmin(db, tenant, "Opted Out", "optout@acme.test");
        var noMailbox = AddAdmin(db, tenant, "No Mailbox", "nomail@no-email.invalid");
        noMailbox.HasEmail = false;
        AddAdmin(db, tenant, "Wants It", "wants@acme.test");
        db.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            TenantId = tenant.Id, UserId = optedOut.Id, NotificationKey = PmSchedulerWorker.OverdueEventKey, EmailEnabled = false,
        });
        await db.SaveChangesAsync();
        using var services = CreateServices(db);

        await CreateWorker().RunOverdueNotificationsAsync(db, services, Now, CancellationToken.None);

        Assert.Equal("wants@acme.test", (await db.EmailOutboxMessages.SingleAsync()).RecipientEmail);
    }

    [Fact]
    public async Task ClosedOrNotYetDueJobs_AreLeftAlone()
    {
        await using var db = CreateDbContext();
        var (tenant, workOrder) = await SeedAsync(db);
        workOrder.Status = WorkOrderStatuses.Closed;
        db.WorkOrders.Add(new WorkOrder { TenantId = tenant.Id, ClientId = workOrder.ClientId, WorkOrderNumber = "WO-000002", Title = "Due tomorrow", Status = WorkOrderStatuses.Open, Priority = "Medium", WorkType = "CorrectiveMaintenance", DueDate = Now.AddDays(1) });
        AddAdmin(db, tenant, "Amina Otieno", "amina@acme.test");
        await db.SaveChangesAsync();
        using var services = CreateServices(db);

        Assert.Equal(0, await CreateWorker().RunOverdueNotificationsAsync(db, services, Now, CancellationToken.None));
        Assert.Empty(await db.EmailOutboxMessages.ToListAsync());
    }

    private static async Task<(Tenant Tenant, WorkOrder WorkOrder)> SeedAsync(AppDbContext db)
    {
        var tenant = new Tenant { Name = "Acme", Slug = $"acme-{Guid.NewGuid():N}"[..12], CompanyName = "Acme Facilities", Email = "ops@acme.test", Country = "Kenya", Status = "Active", LicenseStatus = "Active", IsActive = true };
        var client = new Client { TenantId = tenant.Id, ClientName = "Nairobi West Hospital", IsActive = true };
        var asset = new Asset { TenantId = tenant.Id, ClientId = client.Id, AssetName = "Generator 01", AssetCode = "GEN-01", Status = "Active" };
        var workOrder = new WorkOrder
        {
            TenantId = tenant.Id, ClientId = client.Id, AssetId = asset.Id, WorkOrderNumber = "WO-000001", Title = "Generator failed load test",
            Status = WorkOrderStatuses.Open, Priority = "High", WorkType = "CorrectiveMaintenance", DueDate = Now.AddDays(-2),
        };
        db.AddRange(tenant, client, asset, workOrder);
        await db.SaveChangesAsync();
        return (tenant, workOrder);
    }

    private static User AddAdmin(AppDbContext db, Tenant tenant, string name, string email)
    {
        var user = new User { TenantId = tenant.Id, FullName = name, Email = email, HasEmail = true, PasswordHash = "hash", Role = AppRoles.Admin, IsActive = true };
        db.Users.Add(user);
        return user;
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"ecosys-overdue-email-{Guid.NewGuid():N}").Options);

    private static PmSchedulerWorker CreateWorker() => new(null!, NullLogger<PmSchedulerWorker>.Instance);

    private static ServiceProvider CreateServices(AppDbContext db) =>
        new ServiceCollection()
            .AddSingleton<IEmailTemplateService>(new EmailTemplateService(db))
            .AddSingleton<IEmailSubjectRuleService>(new EmailSubjectRuleService(db))
            .AddSingleton<IEmailOutboxService>(new EmailOutboxService(db, new EmailDeliveryLogService(db)))
            .BuildServiceProvider();
}
