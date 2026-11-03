using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// Phase 11: tenant-owned Finance Defaults, the settings-ownership gap identified in the cleanup —
/// the platform already had its own tax/finance defaults; tenants had none.
/// </summary>
public sealed class TenantFinanceDefaultsTests
{
    [Fact]
    public async Task GetFinanceDefaults_ReturnsSensibleDefaults_WhenNoRowExistsYet()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var controller = CreateController(db, tenantId);

        var result = await controller.GetFinanceDefaults(CancellationToken.None);

        var defaults = Assert.IsType<TenantFinanceDefaults>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.True(defaults.EnableVat);
        Assert.Equal(16, defaults.DefaultVatRate);
        Assert.Equal(30, defaults.DefaultInvoiceDueDays);
    }

    [Fact]
    public async Task UpdateFinanceDefaults_PersistsTheNewValues()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var controller = CreateController(db, tenantId);

        await controller.UpdateFinanceDefaults(
            new TenantFinanceDefaultsRequest(true, "GST", 18, "45 days", 45, 21, false),
            CancellationToken.None);

        var stored = await db.TenantFinanceDefaults.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal("GST", stored.TaxName);
        Assert.Equal(18, stored.DefaultVatRate);
        Assert.Equal("45 days", stored.DefaultPaymentTerms);
        Assert.Equal(45, stored.DefaultInvoiceDueDays);
        Assert.Equal(21, stored.DefaultQuotationValidityDays);
        Assert.False(stored.DefaultExpenseApprovalRequired);
    }

    [Theory]
    [InlineData(-1, 30, 14, "VAT")]
    [InlineData(101, 30, 14, "VAT")]
    [InlineData(16, -1, 14, "VAT")]
    [InlineData(16, 30, -1, "VAT")]
    [InlineData(16, 30, 14, "")]
    public async Task UpdateFinanceDefaults_RejectsInvalidValues(decimal vatRate, int dueDays, int validityDays, string taxName)
    {
        await using var db = CreateDbContext();
        var controller = CreateController(db, Guid.NewGuid());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            controller.UpdateFinanceDefaults(
                new TenantFinanceDefaultsRequest(true, taxName, vatRate, "30 days", dueDays, validityDays, true),
                CancellationToken.None));
    }

    [Fact]
    public async Task TenantA_CannotReachTenantBsFinanceDefaults()
    {
        await using var db = CreateDbContext();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var controllerB = CreateController(db, tenantB);
        await controllerB.UpdateFinanceDefaults(new TenantFinanceDefaultsRequest(true, "Tenant B Tax", 20, "60 days", 60, 30, true), CancellationToken.None);

        var controllerA = CreateController(db, tenantA);
        var result = await controllerA.GetFinanceDefaults(CancellationToken.None);
        var defaultsForA = Assert.IsType<TenantFinanceDefaults>(Assert.IsType<OkObjectResult>(result.Result).Value);

        // Tenant A, having never saved anything, gets the stock defaults — never tenant B's row.
        Assert.Equal("VAT", defaultsForA.TaxName);
        Assert.NotEqual("Tenant B Tax", defaultsForA.TaxName);
    }

    private static AdvancedSettingsController CreateController(AppDbContext db, Guid tenantId) =>
        new(db, new FakeTenantContext(tenantId), new NoOpUserAccessService(), new NoOpLicenseGuardService(db), new NoOpPdfRenderer());

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-finance-defaults-{Guid.NewGuid():N}")
            .Options);

    private sealed class FakeTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => Guid.NewGuid();
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "admin@acme.test";
        public string? Role => AppRoles.Admin;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => Guid.NewGuid();
        public Guid GetRequiredSessionId() => Guid.NewGuid();
    }

    private sealed class NoOpUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private sealed class NoOpLicenseGuardService(AppDbContext dbContext) : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var existing = await dbContext.TenantLicenses.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
            if (existing is not null) return existing;
            var license = new TenantLicense { TenantId = tenantId, LicensePlanId = Guid.NewGuid(), Status = "Trial", StartsAt = DateTime.UtcNow };
            dbContext.TenantLicenses.Add(license);
            await dbContext.SaveChangesAsync(cancellationToken);
            return license;
        }
    }

    private sealed class NoOpPdfRenderer : IPdfRenderer
    {
        public byte[] RenderWorkOrderReportPdf(string title, IReadOnlyCollection<string> sections) => [];
        public byte[] RenderWorkOrderReportPdf(WorkOrderReportPdfModel report) => [];
        public byte[] RenderInvoicePdf(InvoicePdfModel model) => [];
        public byte[] RenderQuotationPdf(QuotationPdfModel model) => [];
        public byte[] RenderReceiptPdf(ReceiptPdfModel model) => [];
        public byte[] RenderClientStatementPdf(ClientStatementPdfModel model) => [];
        public byte[] RenderPurchaseOrderPdf(PurchaseOrderPdfModel model) => [];
    }
}
