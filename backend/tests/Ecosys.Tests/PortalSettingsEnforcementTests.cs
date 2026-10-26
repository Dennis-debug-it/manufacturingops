using System.Text.Json;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// The tenant's "Client Portal" settings page used to save switches that nothing read. These tests pin that the
/// portal API now honours them server-side, that declining a quote requires a reason, and that money totals are
/// reported per currency instead of being summed across currencies.
/// </summary>
public sealed class PortalSettingsEnforcementTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _clientId = Guid.NewGuid();

    [Fact]
    public void Parse_FallsBackToEnabledDefaults_ForMissingOrInvalidJson()
    {
        Assert.Equal(PortalModuleSettings.Default, PortalModuleSettings.Parse(null));
        Assert.Equal(PortalModuleSettings.Default, PortalModuleSettings.Parse("not json"));
        Assert.Equal(PortalModuleSettings.Default, PortalModuleSettings.Parse("{}"));

        var parsed = PortalModuleSettings.Parse("""{"showInvoices":false,"AllowQuoteResponses":"false","welcomeMessage":"  Hi  "}""");
        Assert.False(parsed.ShowInvoices);
        Assert.False(parsed.AllowQuoteResponses);
        Assert.True(parsed.AllowRequestSubmission);
        Assert.Equal("Hi", parsed.WelcomeMessage);
    }

    [Fact]
    public async Task RejectQuotation_WithoutReason_IsRefusedAndLeavesQuoteSent()
    {
        await using var db = CreateDbContext();
        var quote = await SeedQuoteAsync(db);
        var controller = CreateController(db);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.RejectQuotation(quote.Id, new PortalRejectQuotationRequest("   "), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.RejectQuotation(quote.Id, null, CancellationToken.None));

        Assert.Equal("Sent", (await db.TenantQuotations.SingleAsync()).Status);
    }

    [Fact]
    public async Task RejectQuotation_WithReason_StoresTrimmedReason()
    {
        await using var db = CreateDbContext();
        var quote = await SeedQuoteAsync(db);

        await CreateController(db).RejectQuotation(quote.Id, new PortalRejectQuotationRequest("  Too expensive  "), CancellationToken.None);

        var stored = await db.TenantQuotations.SingleAsync();
        Assert.Equal("Rejected", stored.Status);
        Assert.Equal("Too expensive", stored.RejectionReason);
    }

    [Fact]
    public async Task QuoteResponses_AreRefused_WhenDisabled()
    {
        await using var db = CreateDbContext();
        var quote = await SeedQuoteAsync(db);
        await SaveSettingsAsync(db, new { allowQuoteResponses = false });
        var controller = CreateController(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.AcceptQuotation(quote.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => controller.RejectQuotation(quote.Id, new PortalRejectQuotationRequest("No"), CancellationToken.None));
        Assert.Equal("Sent", (await db.TenantQuotations.SingleAsync()).Status);
    }

    [Fact]
    public async Task RequestSubmission_IsRefused_WhenDisabled()
    {
        await using var db = CreateDbContext();
        await SaveSettingsAsync(db, new { allowRequestSubmission = false });

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateController(db).CreateWorkOrder(
            new PortalCreateWorkOrderRequest("Leak", null, null, null, null), CancellationToken.None));
        Assert.Empty(db.WorkOrders);
    }

    [Fact]
    public async Task HiddenSections_AreRefused()
    {
        await using var db = CreateDbContext();
        await SaveSettingsAsync(db, new { showInvoices = false, showContracts = false, showAssetRegister = false });
        var controller = CreateController(db);

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.ListInvoices(CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetInvoice(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => controller.ListContracts(CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => controller.ListAssets(new UtcZone(), CancellationToken.None));
    }

    [Fact]
    public async Task Settings_ExposeWhatThePortalMayShow()
    {
        await using var db = CreateDbContext();
        await SaveSettingsAsync(db, new { showContracts = false, welcomeMessage = "Karibu" });

        await SeedLicenseAsync(db, ServiceOpsPlans.Essentials);

        var result = await CreateController(db).GetSettings(new LicenseGuardService(db), new ProjectSettingsService(db), CancellationToken.None);
        var settings = Assert.IsType<PortalSettingsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.False(settings.ShowContracts);
        Assert.True(settings.ShowInvoices);
        Assert.Equal("Karibu", settings.WelcomeMessage);
        Assert.False(settings.ShowProjects);
    }

    [Theory]
    [InlineData(ServiceOpsPlans.Professional, true, true)]
    [InlineData(ServiceOpsPlans.Professional, false, false)]
    [InlineData(ServiceOpsPlans.Essentials, true, false)]
    public async Task Settings_ShowProjects_NeedsTheModuleAndThePortalSetting(string edition, bool clientPortal, bool expected)
    {
        await using var db = CreateDbContext();
        await SeedLicenseAsync(db, edition);
        await new ProjectSettingsService(db).SaveAsync(_tenantId, new ProjectSettingsModel { ClientPortal = clientPortal });

        var result = await CreateController(db).GetSettings(new LicenseGuardService(db), new ProjectSettingsService(db), CancellationToken.None);

        Assert.Equal(expected, Assert.IsType<PortalSettingsResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).ShowProjects);
    }

    private async Task SeedLicenseAsync(AppDbContext db, string edition)
    {
        var plan = new LicensePlan { PlanCode = edition, DisplayName = edition };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense { TenantId = _tenantId, LicensePlanId = plan.Id, LicensePlan = plan, Status = "Active" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task DashboardAndReports_GroupMoneyByCurrency_AndSkipVoidInvoices()
    {
        await using var db = CreateDbContext();
        db.TenantInvoices.AddRange(
            Invoice("KES", total: 1000, paid: 400),
            Invoice("KES", total: 500, paid: 0),
            Invoice("USD", total: 80, paid: 80),
            Invoice("USD", total: 999, paid: 0, status: "Void"));
        var overdue = DateTime.UtcNow.AddDays(-2);
        db.WorkOrders.AddRange(
            WorkOrder(WorkOrderStatuses.New),
            WorkOrder(WorkOrderStatuses.InProgress, overdue),
            WorkOrder(WorkOrderStatuses.Completed, overdue),
            WorkOrder(WorkOrderStatuses.ClientAccepted),
            WorkOrder(WorkOrderStatuses.Closed, overdue),
            WorkOrder(WorkOrderStatuses.Cancelled),
            WorkOrder(WorkOrderStatuses.Rejected));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var dashboard = Assert.IsType<PortalDashboardSummaryResponse>(Assert.IsType<OkObjectResult>((await controller.GetDashboardSummary(new UtcZone(), CancellationToken.None)).Result).Value);
        var balance = Assert.Single(dashboard.OutstandingBalances);
        Assert.Equal(new PortalCurrencyAmount("KES", 1100), balance);
        Assert.Equal("EUR", dashboard.Currency);
        // Completed, closed, cancelled and rejected jobs are not open, and a finished job past its due date is not overdue.
        Assert.Equal(2, dashboard.OpenWorkOrders);
        Assert.Equal(1, dashboard.OverdueWorkOrders);

        var report = Assert.IsType<PortalReportSummaryResponse>(Assert.IsType<OkObjectResult>((await controller.GetReportSummary(CancellationToken.None)).Result).Value);
        Assert.Equal(7, report.TotalRequests);
        Assert.Equal(2, report.OpenRequests);
        Assert.Equal(2, report.CompletedRequests);
        Assert.Equal(1, report.ClosedRequests);
        Assert.Equal(2, report.CancelledRequests);
        Assert.Equal(report.TotalRequests, report.OpenRequests + report.CompletedRequests + report.ClosedRequests + report.CancelledRequests);
        Assert.Equal(1, report.OverdueRequests);
        Assert.Collection(
            report.InvoiceTotals,
            kes => Assert.Equal(new PortalInvoiceCurrencyTotals("KES", 1500, 400, 1100), kes),
            usd => Assert.Equal(new PortalInvoiceCurrencyTotals("USD", 80, 80, 0), usd));
    }

    [Fact]
    public async Task DashboardAndReports_OmitInvoiceMoney_WhenInvoicesHidden()
    {
        await using var db = CreateDbContext();
        db.TenantInvoices.Add(Invoice("KES", total: 1000, paid: 0));
        await db.SaveChangesAsync();
        await SaveSettingsAsync(db, new { showInvoices = false });
        var controller = CreateController(db);

        var dashboard = Assert.IsType<PortalDashboardSummaryResponse>(Assert.IsType<OkObjectResult>((await controller.GetDashboardSummary(new UtcZone(), CancellationToken.None)).Result).Value);
        var report = Assert.IsType<PortalReportSummaryResponse>(Assert.IsType<OkObjectResult>((await controller.GetReportSummary(CancellationToken.None)).Result).Value);

        Assert.Empty(dashboard.OutstandingBalances);
        Assert.Empty(report.InvoiceTotals);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-portal-settings-{Guid.NewGuid():N}")
            .Options;
        var db = new AppDbContext(options);
        db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Acme", Slug = "acme", CompanyName = "Acme", Email = "ops@acme.test", Currency = "EUR" });
        db.SaveChanges();
        return db;
    }

    private PortalController CreateController(AppDbContext db) =>
        new(db, new FakePortalContext(_tenantId, _clientId), null!, new NoOpAuditLogService());

    private async Task SaveSettingsAsync(AppDbContext db, object values)
    {
        db.TenantModuleSettings.Add(new TenantModuleSetting { TenantId = _tenantId, ModuleKey = PortalModuleSettings.ModuleKey, ValueJson = JsonSerializer.Serialize(values) });
        await db.SaveChangesAsync();
    }

    private async Task<TenantQuotation> SeedQuoteAsync(AppDbContext db)
    {
        var quote = new TenantQuotation { TenantId = _tenantId, ClientId = _clientId, QuoteNumber = "QT-1", Status = "Sent", QuoteDate = DateTime.UtcNow, ValidUntil = DateTime.UtcNow.AddDays(14) };
        db.TenantQuotations.Add(quote);
        await db.SaveChangesAsync();
        return quote;
    }

    private TenantInvoice Invoice(string currency, decimal total, decimal paid, string status = "Sent") => new()
    {
        TenantId = _tenantId,
        ClientId = _clientId,
        InvoiceNumber = $"INV-{Guid.NewGuid():N}",
        Status = status,
        InvoiceDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(30),
        TotalAmount = total,
        AmountPaid = paid,
        Balance = total - paid,
        Currency = currency,
    };

    private WorkOrder WorkOrder(string status, DateTime? dueDate = null) => new()
    {
        TenantId = _tenantId,
        ClientId = _clientId,
        WorkOrderNumber = $"WO-{Guid.NewGuid():N}",
        Title = "Job",
        Status = status,
        DueDate = dueDate,
    };

    private sealed class UtcZone : ITenantTimeZoneService
    {
        public Task<string> GetPlatformTimeZoneIdAsync(CancellationToken cancellationToken = default) => Task.FromResult("UTC");
        public Task<string> GetTimeZoneIdAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult("UTC");
        public Task<TimeZoneInfo> GetTimeZoneAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(TimeZoneInfo.Utc);
        public Task<DateTime> ToTenantTimeAsync(DateTime utcValue, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(utcValue);
        public Task<DateTime> TenantTodayAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(DateTime.UtcNow.Date);
        public Task<DateTime> TenantDayEndUtcAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(DateTime.UtcNow.Date.AddDays(1));
        public Task<string> FormatForTenantAsync(DateTime utcValue, Guid tenantId, string format = "dd MMM yyyy 'at' HH:mm", CancellationToken cancellationToken = default) => Task.FromResult(utcValue.ToString(format));
        public string? NormalizeTimeZoneId(string? timeZoneId) => timeZoneId;
        public void Invalidate(Guid? tenantId = null) { }
    }
    private sealed class FakePortalContext(Guid tenantId, Guid clientId) : IPortalContext
    {
        public Guid? TenantId => tenantId;
        public Guid? PortalUserId { get; } = Guid.NewGuid();
        public Guid? ClientId => clientId;
        public string? Email => "contact@client.test";
        public string? FullName => "Client Contact";
        public bool IsAuthenticated => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredPortalUserId() => PortalUserId!.Value;
        public Guid GetRequiredClientId() => clientId;
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
