using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecosys.Tests;

/// <summary>
/// Follow-up and RFP/RFQ deadline reminders: who gets them, on which channel, how often, and when
/// they stop. Runs the real template, subject-rule and outbox services, so a reminder that passes
/// here is a real queued email.
/// </summary>
public sealed class SalesReminderTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 7, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DueFollowUp_RemindsTheOwner_ThenDaily_AtMostThreeTimes()
    {
        await using var db = CreateDbContext();
        var fixture = await SeedAsync(db);
        var followUp = AddFollowUp(db, fixture, dueAt: Now.AddMinutes(-5));
        await db.SaveChangesAsync();
        var push = new RecordingPushQueue();

        Assert.Equal(1, (await CreateService(db, push).RunAsync(Now)).FollowUpReminders);
        var email = await db.EmailOutboxMessages.SingleAsync();
        Assert.Equal(SalesReminderKeys.FollowUpDue, email.EventKey);
        Assert.Equal(fixture.Owner.Email, email.RecipientEmail);
        Assert.Contains("Rooftop solar", email.Subject);
        Assert.Contains("Call about the revised quote", email.HtmlBody);
        Assert.Contains($"https://app.test/crm/opportunities/{fixture.Deal.Id}", email.HtmlBody);
        var message = Assert.Single(push.Sent);
        Assert.Equal([fixture.Owner.Id], message.UserIds);
        Assert.Equal($"/crm/opportunities/{fixture.Deal.Id}", message.Message.Url);
        Assert.StartsWith("Follow-up due", message.Message.Title);

        // The hourly re-run the same day sends nothing new…
        Assert.Equal(0, (await CreateService(db, push).RunAsync(Now.AddHours(1))).FollowUpReminders);
        // …a day later it nudges again, as overdue…
        Assert.Equal(1, (await CreateService(db, push).RunAsync(Now.AddDays(1))).FollowUpReminders);
        Assert.StartsWith("Follow-up overdue", push.Sent[^1].Message.Title);
        Assert.Equal(1, (await CreateService(db, push).RunAsync(Now.AddDays(2))).FollowUpReminders);
        // …and stops after the third reminder.
        Assert.Equal(0, (await CreateService(db, push).RunAsync(Now.AddDays(3))).FollowUpReminders);
        Assert.Equal(3, (await db.SalesActivities.SingleAsync(x => x.Id == followUp.Id)).ReminderCount);
        Assert.Equal(3, await db.EmailOutboxMessages.CountAsync());
    }

    [Fact]
    public async Task NoReminder_BeforeItIsDue_OnceDone_OrOnAClosedDeal()
    {
        await using var db = CreateDbContext();
        var fixture = await SeedAsync(db);
        AddFollowUp(db, fixture, dueAt: Now.AddHours(2));
        AddFollowUp(db, fixture, dueAt: Now.AddDays(-1), completedAt: Now.AddHours(-3));
        var lost = new SalesOpportunity { TenantId = fixture.Tenant.Id, Title = "Lost deal", Stage = "Lost", OwnerUserId = fixture.Owner.Id };
        db.SalesOpportunities.Add(lost);
        db.SalesActivities.Add(new SalesActivity { TenantId = fixture.Tenant.Id, OpportunityId = lost.Id, Type = "FollowUp", Summary = "Moot", DueAt = Now.AddDays(-1) });
        await db.SaveChangesAsync();

        var result = await CreateService(db, new RecordingPushQueue()).RunAsync(Now);

        Assert.Equal(0, result.FollowUpReminders);
        Assert.Empty(await db.EmailOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task WorkspacesWithoutTheCrm_AreNotReminded()
    {
        await using var db = CreateDbContext();
        var fixture = await SeedAsync(db);
        AddFollowUp(db, fixture, dueAt: Now.AddMinutes(-5));
        await db.SaveChangesAsync();
        var push = new RecordingPushQueue();

        var result = await CreateService(db, push, modules: [ServiceOpsPlans.Operations]).RunAsync(Now);

        Assert.Equal(0, result.FollowUpReminders);
        Assert.Empty(push.Sent);
    }

    [Fact]
    public async Task NotificationSettings_AreHonouredPerChannel()
    {
        await using var db = CreateDbContext();
        var fixture = await SeedAsync(db);
        AddFollowUp(db, fixture, dueAt: Now.AddMinutes(-5));
        db.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            TenantId = fixture.Tenant.Id, UserId = fixture.Owner.Id, NotificationKey = SalesReminderKeys.FollowUpDue,
            EmailEnabled = false, InAppEnabled = true,
        });
        await db.SaveChangesAsync();
        var push = new RecordingPushQueue();

        await CreateService(db, push).RunAsync(Now);

        Assert.Single(push.Sent);
        Assert.Empty(await db.EmailOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task TenderDeadline_WarnsThreeDaysAndOneDayBefore_UntilSubmitted()
    {
        await using var db = CreateDbContext();
        var fixture = await SeedAsync(db);
        var tender = AddTender(db, fixture, deadline: Now.AddDays(2.5));
        var submitted = AddTender(db, fixture, deadline: Now.AddDays(2), status: "Submitted");
        var lastMinute = AddTender(db, fixture, deadline: Now.AddHours(10));
        await db.SaveChangesAsync();
        var push = new RecordingPushQueue();

        // 2.5 days out: the three-day warning; 10 hours out: straight to the final one; submitted: nothing.
        Assert.Equal(2, (await CreateService(db, push).RunAsync(Now)).TenderReminders);
        Assert.Contains(push.Sent, x => x.Message.Url == $"/crm/tenders/{tender.Id}" && x.Message.Title.Contains("in 3 days"));
        Assert.Contains(push.Sent, x => x.Message.Url == $"/crm/tenders/{lastMinute.Id}" && x.Message.Title.Contains("in 10 hours"));
        Assert.DoesNotContain(push.Sent, x => x.Message.Url == $"/crm/tenders/{submitted.Id}");

        Assert.Equal(0, (await CreateService(db, push).RunAsync(Now.AddHours(1))).TenderReminders);
        // Inside the last day the first one gets its final warning; nothing more after that.
        Assert.Equal(1, (await CreateService(db, push).RunAsync(Now.AddDays(1.6))).TenderReminders);
        Assert.Equal(0, (await CreateService(db, push).RunAsync(Now.AddDays(2))).TenderReminders);

        var email = await db.EmailOutboxMessages.FirstAsync(x => x.EventKey == SalesReminderKeys.TenderDeadline);
        Assert.Contains("RFQ", email.Subject);
        Assert.Contains("Supplier portal", email.HtmlBody);
    }

    [Fact]
    public async Task UnansweredQuote_IsChasedAfterThreeDays_AndAgainBeforeItExpires()
    {
        await using var db = CreateDbContext();
        var fixture = await SeedAsync(db);
        var client = new Client { TenantId = fixture.Tenant.Id, ClientName = "Kilele Hotel" };
        var sender = new User { TenantId = fixture.Tenant.Id, FullName = "Finance Clerk", Email = "clerk@acme.test", HasEmail = true, PasswordHash = "hash", Role = AppRoles.Finance, IsActive = true };
        db.AddRange(client, sender);
        TenantQuotation Quote(string number, double sentDaysAgo, double validForDays, Guid? dealId = null, string status = "Sent")
        {
            var quote = new TenantQuotation
            {
                TenantId = fixture.Tenant.Id, ClientId = client.Id, QuoteNumber = number, Status = status, TotalAmount = 250_000m, Currency = "KES",
                QuoteDate = Now.AddDays(-sentDaysAgo), SentAt = Now.AddDays(-sentDaysAgo), ValidUntil = Now.AddDays(validForDays),
                SalesOpportunityId = dealId, SentByUserId = sender.Id,
            };
            db.TenantQuotations.Add(quote);
            return quote;
        }
        Quote("QT-1", sentDaysAgo: 4, validForDays: 20, dealId: fixture.Deal.Id);
        Quote("QT-2", sentDaysAgo: 1, validForDays: 20);
        Quote("QT-3", sentDaysAgo: 4, validForDays: 20, status: "Accepted");
        await db.SaveChangesAsync();
        var push = new RecordingPushQueue();

        // Only the deal's quote has waited long enough; it goes to the deal owner, not the sender.
        Assert.Equal(1, (await CreateService(db, push).RunAsync(Now)).QuoteReminders);
        var first = Assert.Single(push.Sent);
        Assert.Equal([fixture.Owner.Id], first.UserIds);
        Assert.Contains("QT-1", first.Message.Title);
        Assert.Equal(0, (await CreateService(db, push).RunAsync(Now.AddHours(1))).QuoteReminders);

        // Two days before expiry each gets its final nudge; the quote with no deal goes to its sender.
        Assert.Equal(2, (await CreateService(db, push).RunAsync(Now.AddDays(18.5))).QuoteReminders);
        Assert.Contains(push.Sent, x => x.Message.Title.Contains("QT-2") && x.Message.Title.Contains("expires") && x.UserIds.Contains(sender.Id));
        Assert.Equal(0, (await CreateService(db, push).RunAsync(Now.AddDays(19))).QuoteReminders);

        var email = await db.EmailOutboxMessages.FirstAsync(x => x.EventKey == SalesReminderKeys.QuoteAwaitingAnswer);
        Assert.Contains("Kilele Hotel", email.HtmlBody);
    }

    [Fact]
    public async Task ContractInItsNoticePeriod_GetsARenewalDeal_ThatFollowsTheContract()
    {
        await using var db = CreateDbContext();
        var fixture = await SeedAsync(db);
        var client = new Client { TenantId = fixture.Tenant.Id, ClientName = "Kilele Hotel" };
        db.Clients.Add(client);
        Contract AddContract(string number, int endsInDays, int noticeDays = 60, ContractStatus status = ContractStatus.ExpiringSoon)
        {
            var contract = new Contract
            {
                TenantId = fixture.Tenant.Id, ClientId = client.Id, ContractNumber = number, Name = $"Chiller AMC {number}", Status = status,
                StartDate = Now.AddYears(-1), EndDate = Now.Date.AddDays(endsInDays), RenewalNoticeDays = noticeDays,
                ContractValue = 1_200_000m, Currency = "KES", RenewalOwnerUserId = fixture.Owner.Id,
            };
            db.Contracts.Add(contract);
            return contract;
        }
        var due = AddContract("CT-1", endsInDays: 45);
        AddContract("CT-2", endsInDays: 90);
        AddContract("CT-3", endsInDays: 30, status: ContractStatus.Draft);
        var lapsing = AddContract("CT-4", endsInDays: 10);
        await db.SaveChangesAsync();
        var push = new RecordingPushQueue();

        var first = await CreateService(db, push).RunAsync(Now);
        Assert.Equal(2, first.RenewalDealsOpened);
        var deal = await db.SalesOpportunities.SingleAsync(x => x.RenewalOfContractId == due.Id);
        Assert.Equal(("Renewal: Chiller AMC CT-1", fixture.Owner.Id, 1_200_000m, 60), (deal.Title, deal.OwnerUserId, deal.EstimatedValue, deal.Probability));
        // The owner hears about it in the same run, through the follow-up it opens with.
        Assert.Contains(push.Sent, x => x.Message.Url == $"/crm/opportunities/{deal.Id}");
        Assert.Equal(0, (await CreateService(db, push).RunAsync(Now.AddHours(1))).RenewalDealsOpened);

        due.Status = ContractStatus.Renewed;
        lapsing.Status = ContractStatus.Expired;
        await db.SaveChangesAsync();
        Assert.Equal(2, (await CreateService(db, push).RunAsync(Now.AddDays(1))).RenewalDealsClosed);
        Assert.Equal("Won", (await db.SalesOpportunities.SingleAsync(x => x.RenewalOfContractId == due.Id)).Stage);
        var lost = await db.SalesOpportunities.SingleAsync(x => x.RenewalOfContractId == lapsing.Id);
        Assert.Equal("Lost", lost.Stage);
        Assert.Contains("expired without renewal", lost.LostReason);
    }

    [Fact]
    public async Task RenewalDeals_CanBeTurnedOff()
    {
        await using var db = CreateDbContext();
        var fixture = await SeedAsync(db);
        db.TenantSalesSettings.Add(new TenantSalesSettings { TenantId = fixture.Tenant.Id, CreateRenewalDeals = false });
        db.Contracts.Add(new Contract
        {
            TenantId = fixture.Tenant.Id, ClientId = Guid.NewGuid(), ContractNumber = "CT-9", Name = "Lift AMC", Status = ContractStatus.ExpiringSoon,
            StartDate = Now.AddYears(-1), EndDate = Now.Date.AddDays(20), RenewalNoticeDays = 30,
        });
        await db.SaveChangesAsync();

        Assert.Equal(0, (await CreateService(db, new RecordingPushQueue()).RunAsync(Now)).RenewalDealsOpened);
    }

    [Fact]
    public async Task OwnerWithoutAMailbox_GetsThePushOnly()
    {
        await using var db = CreateDbContext();
        var fixture = await SeedAsync(db);
        fixture.Owner.HasEmail = false;
        AddFollowUp(db, fixture, dueAt: Now.AddMinutes(-5));
        await db.SaveChangesAsync();
        var push = new RecordingPushQueue();

        await CreateService(db, push).RunAsync(Now);

        Assert.Single(push.Sent);
        Assert.Empty(await db.EmailOutboxMessages.ToListAsync());
    }

    [Theory]
    [InlineData(0.5, "within the hour")]
    [InlineData(10, "in 10 hours")]
    [InlineData(26, "tomorrow")]
    [InlineData(70, "in 3 days")]
    public void DueIn_ReadsNaturally(double hours, string expected) =>
        Assert.Equal(expected, SalesReminderService.DescribeDueIn(TimeSpan.FromHours(hours)));

    // ── Fixture ──────────────────────────────────────────────────────────────

    private sealed record Fixture(Tenant Tenant, User Owner, SalesOpportunity Deal);

    private static async Task<Fixture> SeedAsync(AppDbContext db)
    {
        var tenant = new Tenant { Name = "Acme", Slug = $"acme-{Guid.NewGuid():N}"[..12], CompanyName = "Acme Facilities", Email = "ops@acme.test", Country = "Kenya", Status = "Active", LicenseStatus = "Active", IsActive = true, TimeZoneId = "Africa/Nairobi" };
        var owner = new User { TenantId = tenant.Id, FullName = "Grace Manager", Email = "grace@acme.test", HasEmail = true, PasswordHash = "hash", Role = AppRoles.Manager, IsActive = true };
        var deal = new SalesOpportunity { TenantId = tenant.Id, Title = "Rooftop solar for warehouse", Stage = "Proposal", CompanyName = "Baraka Logistics", OwnerUserId = owner.Id, OwnerName = owner.FullName };
        db.AddRange(tenant, owner, deal);
        await db.SaveChangesAsync();
        return new Fixture(tenant, owner, deal);
    }

    private static SalesActivity AddFollowUp(AppDbContext db, Fixture fixture, DateTime dueAt, DateTime? completedAt = null)
    {
        var activity = new SalesActivity
        {
            TenantId = fixture.Tenant.Id, OpportunityId = fixture.Deal.Id, Type = "FollowUp",
            Summary = "Call about the revised quote", DueAt = dueAt, CompletedAt = completedAt, OccurredAt = Now.AddDays(-2),
        };
        db.SalesActivities.Add(activity);
        return activity;
    }

    private static SalesTender AddTender(AppDbContext db, Fixture fixture, DateTime deadline, string status = "Preparing")
    {
        var tender = new SalesTender
        {
            TenantId = fixture.Tenant.Id, Type = "RFQ", Reference = "AWS/RFQ/2026/031", Title = "Pump station generators",
            Status = status, IssuerName = "Athi Water Services", SubmissionDeadline = deadline, SubmissionMethod = "Supplier portal",
            OwnerUserId = fixture.Owner.Id, OwnerName = fixture.Owner.FullName,
        };
        db.SalesTenders.Add(tender);
        return tender;
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"ecosys-sales-reminders-{Guid.NewGuid():N}").Options);

    private static SalesReminderService CreateService(AppDbContext db, RecordingPushQueue push, string[]? modules = null) =>
        new(
            db,
            new FixedLicenseGuard(modules ?? [ServiceOpsPlans.Operations, ServiceOpsPlans.Finance]),
            TestTimeZones.Create(db),
            new EmailTemplateService(db),
            new EmailSubjectRuleService(db),
            new EmailOutboxService(db, new EmailDeliveryLogService(db)),
            push,
            Options.Create(new PublicAppOptions { PublicUrl = "https://app.test/" }),
            new SalesOwnerResolver(db, new RolePermissionService(db)),
            NullLogger<SalesReminderService>.Instance);

    private sealed class RecordingPushQueue : IPushNotificationQueue
    {
        public List<(Guid TenantId, IReadOnlyCollection<Guid> UserIds, PushMessage Message)> Sent { get; } = [];
        public void Enqueue(Guid tenantId, IReadOnlyCollection<Guid> userIds, PushMessage message) => Sent.Add((tenantId, userIds, message));
    }

    private sealed class FixedLicenseGuard(string[] modules) : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantLicenseSnapshot(tenantId, Guid.Empty, "essentials", "Essentials", "Active", Now.AddYears(-1), null, null, null,
                false, false, false, 10, 10, null, false, false, null) { Modules = modules });
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
