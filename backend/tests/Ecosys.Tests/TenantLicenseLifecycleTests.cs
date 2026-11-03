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
/// Covers the licensing-lifecycle gap identified in the systematic cleanup: a normal Activate used to
/// take no term at all (silently perpetual), and Renew/Extend/Reactivate as distinct, validated
/// actions didn't exist. These exercise <see cref="PlatformTenantsController"/> directly (the HTTP
/// pipeline needs a real database this sandbox doesn't have) via the same
/// <c>CreatePlatformTenantsController</c>/<c>NoOpLicenseGuardService</c> test doubles
/// <see cref="TenantProvisioningTests"/> already uses.
/// </summary>
public sealed partial class AuthEmailSafetyTests
{
    [Fact]
    public async Task Activate_RequiresExpiry_UnlessExplicitlyPerpetual()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var request = new ActivateTenantLicenseRequest(DateTime.UtcNow, null, false, "Monthly", null, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Activate(tenantId, request, CancellationToken.None));
    }

    [Fact]
    public async Task Activate_AllowsPerpetual_WhenExplicitlyChecked()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var request = new ActivateTenantLicenseRequest(DateTime.UtcNow, null, true, "Monthly", null, 7);
        await controller.Activate(tenantId, request, CancellationToken.None);

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        Assert.True(license.IsPerpetual);
        Assert.Null(license.ExpiresAt);
        Assert.Equal(LicenseStatuses.Active, license.Status);
        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        Assert.True(tenant.IsActive);
    }

    [Fact]
    public async Task Activate_AssignsTheSelectedPlan()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        var plan = new LicensePlan { PlanCode = "finance", DisplayName = "ServiceOps Finance", IsActive = true };
        db.LicensePlans.Add(plan);
        await db.SaveChangesAsync();
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var request = new ActivateTenantLicenseRequest(DateTime.UtcNow, DateTime.UtcNow.AddYears(1), false, "Monthly", null, 7, plan.Id);
        await controller.Activate(tenantId, request, CancellationToken.None);

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        Assert.Equal(plan.Id, license.LicensePlanId);
        Assert.Equal("ServiceOps Finance", tenant.PlanName);
    }

    [Fact]
    public async Task Activate_RejectsAnInactivePlan()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        var inactivePlan = new LicensePlan { PlanCode = "retired", DisplayName = "Retired Plan", IsActive = false };
        db.LicensePlans.Add(inactivePlan);
        await db.SaveChangesAsync();
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var request = new ActivateTenantLicenseRequest(DateTime.UtcNow, DateTime.UtcNow.AddYears(1), false, "Monthly", null, 7, inactivePlan.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Activate(tenantId, request, CancellationToken.None));
    }

    [Fact]
    public async Task Activate_RejectsExpiryNotAfterStart()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var startsAt = DateTime.UtcNow;
        var request = new ActivateTenantLicenseRequest(startsAt, startsAt, false, "Monthly", null, 7);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Activate(tenantId, request, CancellationToken.None));
    }

    [Fact]
    public async Task Activate_RejectsNegativeGracePeriod()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var request = new ActivateTenantLicenseRequest(DateTime.UtcNow, DateTime.UtcNow.AddYears(1), false, "Monthly", null, -1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Activate(tenantId, request, CancellationToken.None));
    }

    [Fact]
    public async Task Activate_RejectsNextBillingDateBeforeStart()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var startsAt = DateTime.UtcNow;
        var request = new ActivateTenantLicenseRequest(startsAt, startsAt.AddYears(1), false, "Monthly", startsAt.AddDays(-1), 7);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Activate(tenantId, request, CancellationToken.None));
    }

    [Theory]
    [InlineData(BillingCycles.Monthly)]
    [InlineData(BillingCycles.Quarterly)]
    [InlineData(BillingCycles.SemiAnnual)]
    [InlineData(BillingCycles.Annual)]
    [InlineData(BillingCycles.Custom)]
    public async Task Activate_AcceptsEveryBillingCycle(string billingCycle)
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var startsAt = new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc);
        var request = new ActivateTenantLicenseRequest(startsAt, startsAt.AddYears(1), false, billingCycle, startsAt.AddMonths(1), 7);
        await controller.Activate(tenantId, request, CancellationToken.None);

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal(billingCycle, license.BillingCycle);
    }

    [Fact]
    public async Task Suspend_SetsTenantAndLicenseSuspended_AndRequiresAReason()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddYears(1));
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Suspend(tenantId, null, CancellationToken.None));

        await controller.Suspend(tenantId, new UpdatePlatformTenantStatusReasonRequest("Non-payment"), CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal("Suspended", tenant.Status);
        Assert.False(tenant.IsActive);
        Assert.NotNull(tenant.SuspendedAt);
        Assert.Equal(LicenseStatuses.Suspended, license.Status);
        Assert.NotNull(license.SuspendedAt);
        // A manually suspended tenant is blocked even though its entitlement (ExpiresAt) is still valid —
        // suspension and expiry are independent (Phase 6/7).
        Assert.True(license.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Cancel_CancelsLicenseAndSyncsTenant_WithoutDeletingData()
    {
        // Exercised through PlatformLicensingController rather than PlatformTenantsController.DeactivatePost:
        // the latter also bulk-updates Assets via ExecuteUpdateAsync, which EF's InMemory provider (this
        // sandbox has no real Postgres) does not support — a test-infrastructure limit, not a product one.
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddYears(1));
        var user = new User { TenantId = tenantId, FullName = "Staff User", Email = "staff@acme.test", Role = AppRoles.Admin, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        var controller = new PlatformLicensingController(
            db,
            new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true),
            new NoOpAuditLogService(),
            new NoOpLicenseGuardService(db));

        await controller.Cancel(license.Id, CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        var reloadedLicense = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal(LicenseStatuses.Cancelled, reloadedLicense.Status);
        Assert.NotNull(reloadedLicense.CancelledAt);
        Assert.Equal(LicenseStatuses.Cancelled, tenant.LicenseStatus);
        Assert.False(tenant.IsActive);
        // Cancellation must never delete the tenant's own data (Phase 6/7).
        Assert.True(await db.Users.AnyAsync(x => x.Id == user.Id));
        Assert.True(await db.Tenants.AnyAsync(x => x.Id == tenantId));
    }

    [Fact]
    public async Task Renew_RestoresSuspendedTenantToActive_WithNewTerm()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        await SeedSuspendedLicenseAsync(db, tenantId);
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var startsAt = DateTime.UtcNow;
        var request = new RenewTenantLicenseRequest(startsAt, startsAt.AddYears(2), false, BillingCycles.Quarterly, startsAt.AddMonths(3), 10);
        await controller.RenewLicense(tenantId, request, CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal("Active", tenant.Status);
        Assert.True(tenant.IsActive);
        Assert.Null(tenant.SuspendedAt);
        Assert.Equal(LicenseStatuses.Active, license.Status);
        Assert.Null(license.SuspendedAt);
        Assert.Null(license.CancelledAt);
        Assert.Equal(10, license.GracePeriodDays);
        Assert.Equal(BillingCycles.Quarterly, license.BillingCycle);
    }

    [Fact]
    public async Task Renew_CanUpgradeThePlanAtTheSameTime()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        await SeedSuspendedLicenseAsync(db, tenantId);
        var upgradedPlan = new LicensePlan { PlanCode = "suite", DisplayName = "ServiceOps Suite", IsActive = true };
        db.LicensePlans.Add(upgradedPlan);
        await db.SaveChangesAsync();
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var startsAt = DateTime.UtcNow;
        var request = new RenewTenantLicenseRequest(startsAt, startsAt.AddYears(1), false, BillingCycles.Annual, null, 7, upgradedPlan.Id);
        await controller.RenewLicense(tenantId, request, CancellationToken.None);

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        Assert.Equal(upgradedPlan.Id, license.LicensePlanId);
        Assert.Equal("ServiceOps Suite", tenant.PlanName);
    }

    [Fact]
    public async Task Renew_RestoresCancelledTenantToActive()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        await SeedCancelledLicenseAsync(db, tenantId);
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var startsAt = DateTime.UtcNow;
        var request = new RenewTenantLicenseRequest(startsAt, startsAt.AddYears(1), false, BillingCycles.Annual, null, 7);
        await controller.RenewLicense(tenantId, request, CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal("Active", tenant.Status);
        Assert.Null(license.CancelledAt);
        Assert.Equal(LicenseStatuses.Active, license.Status);
    }

    [Fact]
    public async Task Extend_PushesExpiryFurtherOut()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        var currentExpiry = new DateTime(2027, 9, 14, 0, 0, 0, DateTimeKind.Utc);
        await SeedActiveLicenseAsync(db, tenantId, currentExpiry);
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var newExpiry = new DateTime(2027, 12, 14, 0, 0, 0, DateTimeKind.Utc);
        await controller.ExtendLicense(tenantId, new ExtendTenantLicenseRequest(newExpiry), CancellationToken.None);

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal(newExpiry, license.ExpiresAt);
    }

    [Fact]
    public async Task Extend_RejectsWhenNewExpiryNotAfterCurrent()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        var currentExpiry = DateTime.UtcNow.AddYears(1);
        await SeedActiveLicenseAsync(db, tenantId, currentExpiry);
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            controller.ExtendLicense(tenantId, new ExtendTenantLicenseRequest(currentExpiry.AddDays(-1)), CancellationToken.None));
    }

    [Fact]
    public async Task Extend_RejectsOnPerpetualLicense()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        await SeedPerpetualLicenseAsync(db, tenantId);
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            controller.ExtendLicense(tenantId, new ExtendTenantLicenseRequest(DateTime.UtcNow.AddYears(5)), CancellationToken.None));
    }

    [Fact]
    public async Task Reactivate_RejectsWhenTenantIsNotManuallySuspended()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddYears(1));
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.ReactivateLicense(tenantId, CancellationToken.None));
    }

    [Fact]
    public async Task Reactivate_RejectsWhenEntitlementHasExpired()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        await SeedSuspendedLicenseAsync(db, tenantId, expiresAt: DateTime.UtcNow.AddDays(-1));
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.ReactivateLicense(tenantId, CancellationToken.None));

        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        Assert.Equal("Suspended", tenant.Status);
    }

    [Fact]
    public async Task Reactivate_SucceedsWhenEntitlementStillValid()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        await SeedSuspendedLicenseAsync(db, tenantId, expiresAt: DateTime.UtcNow.AddMonths(6));
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        await controller.ReactivateLicense(tenantId, CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal("Active", tenant.Status);
        Assert.True(tenant.IsActive);
        Assert.Null(license.SuspendedAt);
    }

    [Fact]
    public async Task Reactivate_SucceedsForPerpetualLicense_EvenWithoutAnExpiry()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Acme Co", "ops@acme.test");
        await SeedSuspendedLicenseAsync(db, tenantId, expiresAt: null, isPerpetual: true);
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        await controller.ReactivateLicense(tenantId, CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        Assert.Equal("Active", tenant.Status);
    }

    /// <summary>
    /// Phase 25 (legacy compatibility): a pre-existing tenant with a null ExpiresAt and the new
    /// IsPerpetual column defaulting to false (i.e. not yet backfilled by the migration) must keep
    /// behaving exactly as it already did — never-expiring — since BuildSnapshot's read path never
    /// consults IsPerpetual at all; only the write-time Activate/Renew validation does.
    /// </summary>
    [Fact]
    public async Task LegacyNullExpiry_StaysNeverExpiring_RegardlessOfIsPerpetualColumn()
    {
        await using var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-legacy-license-{Guid.NewGuid():N}").Options);
        var tenantId = Guid.NewGuid();
        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId, Name = "Legacy Co", Slug = "legacy-co", CompanyName = "Legacy Co",
            Email = "ops@legacy.test", ContactEmail = "ops@legacy.test", Country = "Kenya",
            Status = "Active", LicenseStatus = "Active", IsActive = true
        });
        var plan = new LicensePlan { PlanCode = "suite", DisplayName = "Suite", IsActive = true };
        dbContext.LicensePlans.Add(plan);
        dbContext.TenantLicenses.Add(new TenantLicense
        {
            TenantId = tenantId, LicensePlan = plan, Status = LicenseStatuses.Active,
            StartsAt = DateTime.UtcNow.AddYears(-3), ExpiresAt = null, IsPerpetual = false,
        });
        await dbContext.SaveChangesAsync();

        var service = new LicenseGuardService(dbContext);
        var snapshot = await service.GetSnapshotAsync(tenantId);

        Assert.False(snapshot.IsReadOnly);
        await service.EnsureTenantCanMutateAsync(tenantId);
    }

    private static async Task SeedSuspendedLicenseAsync(AppDbContext db, Guid tenantId, DateTime? expiresAt = null, bool isPerpetual = false)
    {
        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        tenant.Status = "Suspended";
        tenant.IsActive = false;
        tenant.SuspendedAt = DateTime.UtcNow;
        var plan = new LicensePlan { PlanCode = $"suite-{tenantId:N}", DisplayName = "Suite", IsActive = true };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense
        {
            TenantId = tenantId, LicensePlan = plan, Status = LicenseStatuses.Suspended,
            StartsAt = DateTime.UtcNow.AddYears(-1), ExpiresAt = expiresAt, IsPerpetual = isPerpetual,
            SuspendedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCancelledLicenseAsync(AppDbContext db, Guid tenantId)
    {
        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        tenant.Status = "Inactive";
        tenant.IsActive = false;
        var plan = new LicensePlan { PlanCode = $"suite-{tenantId:N}", DisplayName = "Suite", IsActive = true };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense
        {
            TenantId = tenantId, LicensePlan = plan, Status = LicenseStatuses.Cancelled,
            StartsAt = DateTime.UtcNow.AddYears(-1), CancelledAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedActiveLicenseAsync(AppDbContext db, Guid tenantId, DateTime expiresAt)
    {
        var plan = new LicensePlan { PlanCode = $"suite-{tenantId:N}", DisplayName = "Suite", IsActive = true };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense
        {
            TenantId = tenantId, LicensePlan = plan, Status = LicenseStatuses.Active,
            StartsAt = DateTime.UtcNow.AddMonths(-1), ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedPerpetualLicenseAsync(AppDbContext db, Guid tenantId)
    {
        var plan = new LicensePlan { PlanCode = $"suite-{tenantId:N}", DisplayName = "Suite", IsActive = true };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense
        {
            TenantId = tenantId, LicensePlan = plan, Status = LicenseStatuses.Active,
            StartsAt = DateTime.UtcNow.AddMonths(-1), ExpiresAt = null, IsPerpetual = true,
        });
        await db.SaveChangesAsync();
    }
}
