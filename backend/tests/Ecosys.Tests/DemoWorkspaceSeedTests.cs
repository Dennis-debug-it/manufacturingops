using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>
/// The demo workspace is created in production next to real tenants. These pin what makes that safe and what
/// makes the demo usable: it can never be billed, expired or emailed; it only ever resets the tenant carrying the
/// demo id and slug; and the seeded data satisfies the same invariants the app maintains (assignments, branches,
/// numbering), so every login and the portal have something to show. The purge itself runs raw SQL and needs
/// PostgreSQL; it is exercised end to end against a real database rather than the in-memory provider here.
/// </summary>
public sealed class DemoWorkspaceSeedTests
{
    private static readonly Guid DemoId = DemoWorkspace.TenantId;

    [Fact]
    public async Task FirstReset_CreatesAPerpetualWorkspace_ThatIsNeverBilledOrEmailed()
    {
        await using var db = CreateDbContext();
        var result = await CreateService(db).ResetAsync(null, "platform.owner@test", CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == DemoId);
        Assert.Equal(DemoWorkspace.Slug, tenant.Slug);
        Assert.True(tenant.IsActive);

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == DemoId);
        Assert.Equal(LicenseStatuses.Active, license.Status);
        Assert.True(license.IsPerpetual);
        Assert.Null(license.ExpiresAt);
        Assert.Null(license.NextBillingDate);
        Assert.Null(license.TrialEndsAt);

        Assert.False((await db.EmailSettings.SingleAsync(x => x.TenantId == DemoId)).IsEnabled);
        Assert.True(result.Status.Exists);
        Assert.Equal(4, result.Status.Clients);
        Assert.Equal(20, result.Status.WorkOrders);
    }

    [Fact]
    public async Task EveryDemoLogin_AcceptsTheReturnedPassword_AndHasNoMailbox()
    {
        await using var db = CreateDbContext();
        var result = await CreateService(db).ResetAsync(null, null, CancellationToken.None);

        var users = await db.Users.Where(x => x.TenantId == DemoId).ToListAsync();
        Assert.Equal(["admin", "dispatcher", "manager", "technician"], users.Select(x => x.Username!).Order());
        var hasher = new PasswordHasher<User>();
        Assert.All(users, user =>
        {
            Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(user, user.PasswordHash, result.Password));
            Assert.False(user.HasEmail);
            Assert.False(user.MustChangePassword);
            Assert.EndsWith("@no-email.invalid", user.Email);
        });
    }

    [Fact]
    public async Task SeededJobs_SatisfyTheAssignmentAndBranchInvariants()
    {
        await using var db = CreateDbContext();
        await CreateService(db).ResetAsync(null, null, CancellationToken.None);

        var jobs = await db.WorkOrders.Where(x => x.TenantId == DemoId).ToListAsync();
        var assignments = await db.WorkOrderTechnicianAssignments.Where(x => x.TenantId == DemoId).ToListAsync();

        // Assignment rows are the source of truth; the job column only mirrors them.
        Assert.All(jobs.Where(x => x.AssignedTechnicianId.HasValue), job =>
            Assert.Contains(assignments, a => a.WorkOrderId == job.Id && a.TechnicianId == job.AssignedTechnicianId));
        Assert.All(jobs, job => Assert.Equal(job.AssignedTechnicianId is null ? "Unassigned" : "IndividualTechnician", job.AssignmentType));
        // Branch-scoped staff never see jobs without a branch.
        Assert.All(jobs, job => Assert.NotNull(job.BranchId));

        var technicianUser = await db.Users.SingleAsync(x => x.TenantId == DemoId && x.Username == "technician");
        var technician = await db.Technicians.SingleAsync(x => x.UserId == technicianUser.Id);
        Assert.True(await db.UserBranchAssignments.AnyAsync(x => x.UserId == technicianUser.Id));
        Assert.Equal(5, assignments.Count(x => x.TechnicianId == technician.Id));

        // History is back-dated rather than all "created just now".
        Assert.True(jobs.Count(x => x.CreatedAt < DateTime.UtcNow.AddDays(-7)) >= 8);
    }

    [Fact]
    public async Task Numbering_ContinuesAfterTheSeededDocuments()
    {
        await using var db = CreateDbContext();
        await CreateService(db).ResetAsync(null, null, CancellationToken.None);

        async Task AssertNext(string documentType, int seeded) =>
            Assert.Equal(seeded + 1, (await db.NumberingSettings.SingleAsync(x => x.TenantId == DemoId && x.DocumentType == documentType)).NextNumber);

        await AssertNext(DocumentTypes.WorkOrder, await db.WorkOrders.CountAsync(x => x.TenantId == DemoId));
        await AssertNext(DocumentTypes.Quotation, await db.TenantQuotations.CountAsync(x => x.TenantId == DemoId));
        await AssertNext(DocumentTypes.Invoice, await db.TenantInvoices.CountAsync(x => x.TenantId == DemoId));
        await AssertNext(DocumentTypes.Receipt, await db.TenantPayments.CountAsync(x => x.TenantId == DemoId));
    }

    [Fact]
    public async Task PortalContact_SeesNoDraftQuotes_AndBalancesAreConsistent()
    {
        await using var db = CreateDbContext();
        await CreateService(db).ResetAsync(null, null, CancellationToken.None);

        var contact = await db.ClientPortalUsers.SingleAsync(x => x.TenantId == DemoId);
        Assert.True(contact.IsActive);
        Assert.DoesNotContain(await db.TenantQuotations.Where(x => x.ClientId == contact.ClientId).Select(x => x.Status).ToListAsync(), s => s == "Draft");
        Assert.All(await db.TenantInvoices.Where(x => x.TenantId == DemoId).ToListAsync(), invoice =>
            Assert.Equal(invoice.TotalAmount - invoice.AmountPaid, invoice.Balance));
    }

    [Fact]
    public async Task OpeningARole_OnlyAcceptsTheDemosOwnLogins()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.FindLoginUserIdAsync("technician"));
        await service.ResetAsync(null, null, CancellationToken.None);
        db.Users.Add(new User { TenantId = Guid.NewGuid(), FullName = "Real admin", Username = "owner", Email = "owner@realco.test", PasswordHash = "x", Role = AppRoles.Admin, IsActive = true });
        await db.SaveChangesAsync();

        var technicianId = await service.FindLoginUserIdAsync(" TECHNICIAN ");
        Assert.Equal(DemoId, (await db.Users.IgnoreQueryFilters().SingleAsync(x => x.Id == technicianId)).TenantId);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.FindLoginUserIdAsync("owner"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.FindLoginUserIdAsync(null));
    }

    [Fact]
    public async Task Reset_RefusesATenantThatIsNotTheDemo()
    {
        await using var db = CreateDbContext();
        db.Tenants.Add(new Tenant { Id = DemoId, Name = "Real Co", CompanyName = "Real Co", Slug = "real-co", Email = "ops@real.test" });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(db).ResetAsync(null, null, CancellationToken.None));
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task Create_RefusesWhenAnotherTenantHoldsTheDemoSlug()
    {
        await using var db = CreateDbContext();
        db.Tenants.Add(new Tenant { Name = "Squatter", CompanyName = "Squatter", Slug = DemoWorkspace.Slug, Email = "x@squatter.test" });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(db).ResetAsync(null, null, CancellationToken.None));
        Assert.False(await db.Tenants.AnyAsync(x => x.Id == DemoId));
    }

    [Fact]
    public async Task Projects_AreSeededAtThreeStages_WithConsistentMoney()
    {
        await using var db = CreateDbContext();
        await CreateService(db).ResetAsync(null, null, CancellationToken.None);

        var projects = await db.Projects.Include(x => x.Stage).Where(x => x.TenantId == DemoId).ToListAsync();
        Assert.Equal(
            [ProjectStageCategories.Active, ProjectStageCategories.Handover, ProjectStageCategories.Planning],
            projects.Select(x => x.Stage!.Category).Order().ToArray());
        Assert.All(projects, project => Assert.Equal(DemoId, project.TenantId));

        var solar = projects.Single(x => x.Stage!.Category == ProjectStageCategories.Active);
        Assert.Equal(19_150_000m, solar.RevisedValue);
        Assert.Equal(30m, solar.ProgressPercent);
        Assert.True(await db.ProjectMusterEntries.AnyAsync(x => x.ProjectId == solar.Id));
        Assert.True(await db.ProjectDiaryEntries.AnyAsync(x => x.ProjectId == solar.Id && x.SubmittedAt == null));

        // Every valuation that reached an invoice points at an invoice on the same project, for its amount due.
        var valuations = await db.ProjectValuations.Where(x => x.TenantId == DemoId).ToListAsync();
        Assert.Equal(3, valuations.Count);
        foreach (var valuation in valuations.Where(x => x.InvoiceId.HasValue))
        {
            var invoice = await db.TenantInvoices.SingleAsync(x => x.Id == valuation.InvoiceId);
            Assert.Equal((valuation.ProjectId, valuation.Id), (invoice.ProjectId, invoice.ProjectValuationId));
            Assert.Equal(valuation.AmountDue, invoice.Subtotal);
        }

        // Both awarded projects trace back to their quote, marked converted as "Award as project" leaves it.
        foreach (var project in projects.Where(x => x.SourceQuotationId.HasValue))
        {
            var quote = await db.TenantQuotations.SingleAsync(x => x.Id == project.SourceQuotationId);
            Assert.Equal(("Converted", project.Id), (quote.Status, quote.ConvertedToProjectId));
        }

        // The Financials tab reads the seeded records: certified value, retention held, cash received and labour cost.
        var financials = new ProjectFinancialsService(db);
        var solarMoney = await financials.ComputeAsync(DemoId, solar);
        Assert.Equal(730_000m, solarMoney.RetentionHeld);
        Assert.True(solarMoney.CashReceived > 0);
        Assert.True(solarMoney.ActualCost > 10_000_000m);
        var theatre = projects.Single(x => x.Stage!.Category == ProjectStageCategories.Handover);
        Assert.Equal(155_000m, (await financials.ComputeAsync(DemoId, theatre)).RetentionDueNow);

        var numbering = await db.NumberingSettings.Where(x => x.TenantId == DemoId).ToDictionaryAsync(x => x.DocumentType, x => x.NextNumber);
        Assert.Equal(4, numbering[DocumentTypes.Project]);
        Assert.Equal(4, numbering[DocumentTypes.ProjectValuation]);
    }

    /// <summary>
    /// The purge runs raw SQL over every tenant table, so a reset that leaves project rows behind (or trips over
    /// their foreign keys) only shows up on PostgreSQL. Reset twice against a migrated database.
    /// </summary>
    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task Postgres_ResetTwice_ReplacesTheProjectsCleanly()
    {
        var serverConnection = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_demo_{Guid.NewGuid():N}";
        var databaseConnection = new NpgsqlConnectionStringBuilder(serverConnection) { Database = databaseName, Pooling = false }.ConnectionString;
        var adminConnection = new NpgsqlConnectionStringBuilder(serverConnection) { Database = "postgres", Pooling = false }.ConnectionString;

        await ExecuteAdminAsync(adminConnection, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using (var setup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(databaseConnection).Options))
            {
                await setup.Database.MigrateAsync();
            }

            for (var run = 0; run < 2; run++)
            {
                await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(databaseConnection).Options);
                await CreateService(db).ResetAsync(null, null, CancellationToken.None);
            }

            await using var verify = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(databaseConnection).Options);
            Assert.Equal(3, await verify.Projects.IgnoreQueryFilters().CountAsync(x => x.TenantId == DemoId));
            Assert.Equal(3, await verify.ProjectValuations.IgnoreQueryFilters().CountAsync(x => x.TenantId == DemoId));
            Assert.Equal(7, await verify.ProjectStages.IgnoreQueryFilters().CountAsync(x => x.TenantId == DemoId));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAdminAsync(adminConnection, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task SalesCrm_HasAPipelineAtEveryStage_TiedIntoTheJobsAndQuotes()
    {
        await using var db = CreateDbContext();
        await CreateService(db).ResetAsync(null, null, CancellationToken.None);

        var deals = await db.SalesOpportunities.Where(x => x.TenantId == DemoId).ToListAsync();
        Assert.Equal(["Lead", "Lost", "Negotiation", "Proposal", "Qualified", "Won"], deals.Select(x => x.Stage).Distinct().Order());
        Assert.All(deals, deal => Assert.NotNull(deal.OwnerUserId));

        // The won lighting deal carries the whole chain: accepted quote and closed job link back to it.
        var lighting = deals.Single(x => x.Title == "Emergency lighting – corridor B");
        Assert.Equal("Accepted", (await db.TenantQuotations.SingleAsync(x => x.SalesOpportunityId == lighting.Id)).Status);
        Assert.Equal(WorkOrderStatuses.Closed, (await db.WorkOrders.SingleAsync(x => x.SalesOpportunityId == lighting.Id)).Status);
        // …and one won deal is still waiting for its work order, so the hand-off button has something to do.
        var wonDealIds = deals.Where(x => x.Stage == "Won").Select(x => x.Id).ToList();
        Assert.Contains(wonDealIds, id => !db.WorkOrders.Any(w => w.SalesOpportunityId == id));

        var tenders = await db.SalesTenders.Where(x => x.TenantId == DemoId).ToListAsync();
        Assert.Equal(["NoBid", "Preparing", "Received", "Submitted"], tenders.Select(x => x.Status).Order());
        Assert.NotNull(tenders.Single(x => x.Status == "Preparing").OpportunityId);
        Assert.True(await db.SalesActivities.AnyAsync(x => x.TenantId == DemoId && x.DueAt < DateTime.UtcNow && x.CompletedAt == null),
            "The demo should show an overdue follow-up.");

        // The USD deal converts, because the demo carries a USD rate.
        Assert.True(await db.ExchangeRates.AnyAsync(x => x.TenantId == DemoId && x.BaseCurrency == "USD" && x.QuoteCurrency == "KES"));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-demo-workspace-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        db.LicensePlans.Add(new LicensePlan { PlanCode = ServiceOpsPlans.Enterprise, DisplayName = "Enterprise", IsActive = true });
        db.SaveChanges();
        return db;
    }

    private static DemoWorkspaceService CreateService(AppDbContext db) =>
        new(db, new PasswordHasher<User>(), new UnusedPortalAuthService(), new UnusedFileStorage(), new NoOpAuditLogService(), NullLogger<DemoWorkspaceService>.Instance);

    private sealed class UnusedPortalAuthService : IPortalAuthService
    {
        public Task RequestMagicLinkAsync(string email, string magicLinkBaseUrl, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortalMagicLinkResult> VerifyMagicLinkAsync(string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> IssueSignInLinkAsync(Guid portalUserId, string magicLinkBaseUrl, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendInvitationAsync(Guid portalUserId, string portalBaseUrl, Guid? invitedByUserId, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GetAccessLinkAsync(Guid portalUserId, string portalBaseUrl, Guid? actorUserId, bool reset, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PortalMagicLinkResult> OpenAccessLinkAsync(string link, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnusedFileStorage : IFileStorageService
    {
        public Task<UploadResult> UploadAsync(Stream stream, string fileName, string mimeType, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Stream> DownloadAsync(string storagePath, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string storagePath, CancellationToken ct) => throw new NotSupportedException();
        public string GetPublicUrl(string storagePath) => throw new NotSupportedException();
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
