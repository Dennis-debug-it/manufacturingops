using System.Reflection;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecosys.Tests;

/// <summary>
/// Tenant-management audit (2026-09-24, docs/audits/TENANT_MANAGEMENT_AUDIT_2026-09-24.md). Each test
/// states the CORRECT business behaviour. Tests marked <c>Audit=Defect</c> reproduced a finding against
/// the pre-remediation code (all of them failed at audit time) and now guard the fix; they must not be
/// deleted or weakened. Tests marked <c>Audit=Verified</c> prove behaviour the audit found working.
/// Run just these with: <c>--filter "Audit=Defect|Audit=Verified"</c>.
/// </summary>
public sealed partial class AuthEmailSafetyTests
{
    // ---------------------------------------------------------------- Lifecycle / status source of truth

    /// <summary>
    /// Finding TM-P1-03. "Extend trial" is offered for any tenant that has not used its one extension,
    /// including a paying Active tenant. The action rewrites the license to Trial and replaces the paid
    /// expiry with now+14d.
    /// </summary>
    [Fact]
    [Trait("Audit", "Defect")]
    public async Task Audit_ExtendTrial_OnPaidActiveTenant_MustNotDowngradeOrShortenThePaidLicense()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Paying Co", "ops@paying.test");
        var paidUntil = DateTime.UtcNow.AddDays(300);
        await SeedActiveLicenseAsync(db, tenantId, paidUntil);
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.ExtendTrial(tenantId, CancellationToken.None));

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal(LicenseStatuses.Active, license.Status);
        Assert.True(license.ExpiresAt >= paidUntil.AddSeconds(-1),
            $"Paid expiry {paidUntil:u} was replaced with {license.ExpiresAt:u}.");
    }

    /// <summary>
    /// Finding TM-P1-01 (the "still suspended after reactivation" incident). SubscriptionLifecycleWorker
    /// suspends TenantLicense.Status and Tenant.LicenseStatus but leaves Tenant.Status = "Active". The
    /// platform UI keys its lifecycle buttons off Tenant.Status, so it shows the tenant as Active, hides
    /// Reactivate and offers Suspend instead.
    /// </summary>
    [Fact]
    [Trait("Audit", "Defect")]
    public async Task Audit_WorkerSuspendedTenant_IsReportedAsSuspendedToThePlatform()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Lapsed Co", "ops@lapsed.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(-30));

        await RunSubscriptionLifecycleCycleAsync(db);

        var snapshot = await new LicenseGuardService(db).GetSnapshotAsync(tenantId);
        Assert.True(snapshot.IsSuspended, "Precondition: the worker suspends a license 30 days past expiry with a 7-day grace period.");

        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));
        var detail = Assert.IsType<PlatformTenantDetailResponse>(Assert.IsType<OkObjectResult>((await controller.Get(tenantId, CancellationToken.None)).Result).Value);
        Assert.Equal("Suspended", detail.Status);
    }

    /// <summary>
    /// Finding TM-P1-01, second half. After worker suspension the operator's available actions are
    /// Extend (only moves ExpiresAt, never clears Suspended) and Reactivate (rejected because
    /// Tenant.Status is not "Suspended"). Neither restores access, which reproduces the production
    /// report "Tenant license is suspended" after the tenant had been "reactivated".
    /// </summary>
    [Fact]
    [Trait("Audit", "Defect")]
    public async Task Audit_WorkerSuspendedTenant_ExtendThenReactivate_RestoresAccess()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Lapsed Co", "ops@lapsed.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(-30));
        await RunSubscriptionLifecycleCycleAsync(db);
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        await controller.ExtendLicense(tenantId, new ExtendTenantLicenseRequest(DateTime.UtcNow.AddYears(1)), CancellationToken.None);
        string? reactivateError = null;
        try
        {
            await controller.ReactivateLicense(tenantId, CancellationToken.None);
        }
        catch (BusinessRuleException ex)
        {
            reactivateError = ex.Message;
        }

        var snapshot = await new LicenseGuardService(db).GetSnapshotAsync(tenantId);
        Assert.False(snapshot.IsSuspended,
            $"Tenant is still blocked with 'license_suspended' after Extend + Reactivate. Reactivate said: {reactivateError ?? "(succeeded)"}");
    }

    /// <summary>
    /// Finding TM-P1-02. /api/platform/tenant-licenses/{id}/suspend writes TenantLicense.Status and
    /// Tenant.Status but not Tenant.LicenseStatus. The platform tenant page resolves the license badge
    /// from Tenant.LicenseStatus first, so it keeps showing "Active" for a tenant that is blocked.
    /// </summary>
    [Fact]
    [Trait("Audit", "Defect")]
    public async Task Audit_TenantLicensesSuspend_KeepsThePlatformLicenseBadgeInSync()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Badge Co", "ops@badge.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(200));
        var licenses = new PlatformTenantLicensesController(
            db,
            new LicenseGuardService(db),
            new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true),
            new NoOpAuditLogService());

        await licenses.Suspend(tenantId, CancellationToken.None);

        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));
        var detail = Assert.IsType<PlatformTenantDetailResponse>(Assert.IsType<OkObjectResult>((await controller.Get(tenantId, CancellationToken.None)).Result).Value);
        Assert.Equal(LicenseStatuses.Suspended, detail.LicenseStatus);
    }

    /// <summary>
    /// Finding TM-P1-04. PATCH /status with "Trial" (an accepted value) sets Tenant.IsActive =
    /// (status == "Active"), i.e. false, and the Trial branch never restores it. The enforcement
    /// middleware then blocks the whole workspace with "Tenant access is deactivated".
    /// </summary>
    [Fact]
    [Trait("Audit", "Defect")]
    public async Task Audit_PatchStatusToTrial_KeepsTheWorkspaceAccessible()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Trial Co", "ops@trial.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        await controller.UpdateStatus(tenantId, new UpdatePlatformTenantStatusRequest("Trial", null), CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        Assert.Equal("Trial", tenant.Status);
        Assert.True(tenant.IsActive, "A Trial tenant was marked IsActive=false and is locked out by TenantLicenseEnforcementMiddleware.");
    }

    /// <summary>
    /// Finding TM-P1-05. Recording a payment reactivates Trial/TrialExpiringSoon/Suspended/Expired
    /// licenses, but not TrialExpired — the status TrialLifecycleWorker assigns to every lapsed trial.
    /// A trial customer who pays stays read-only.
    /// </summary>
    [Fact]
    [Trait("Audit", "Defect")]
    public async Task Audit_PaymentOnTrialExpiredLicense_RestoresWriteAccess()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Converting Co", "ops@converting.test");
        var plan = new LicensePlan { PlanCode = $"suite-{tenantId:N}", DisplayName = "Suite", IsActive = true };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense
        {
            TenantId = tenantId, LicensePlan = plan, Status = LicenseStatuses.TrialExpired,
            StartsAt = DateTime.UtcNow.AddDays(-20), TrialEndsAt = DateTime.UtcNow.AddDays(-6), ExpiresAt = DateTime.UtcNow.AddDays(-6),
        });
        await db.SaveChangesAsync();
        var billing = new PlatformBillingLifecycleService(
            db, new FakeEmailTemplateService(), new FakeEmailSubjectRuleService(),
            new EmailOutboxService(db, new EmailDeliveryLogService(db)));

        await billing.ApplyPaymentReceivedAsync(
            new PlatformPayment { TenantId = tenantId, Amount = 1000m, PaymentNumber = "PAY-AUDIT-1" }, null, null, CancellationToken.None);

        var snapshot = await new LicenseGuardService(db).GetSnapshotAsync(tenantId);
        Assert.False(snapshot.IsReadOnly, $"Paid tenant is still read-only; license status is '{snapshot.Status}'.");
    }

    // ---------------------------------------------------------------- Authorization

    private static readonly string[] AdminLevelPolicies = ["PlatformAdminAccess", "PlatformOwnerOnly", "PlatformSettingsAccess"];

    /// <summary>
    /// Finding TM-P0-01. PlatformAccess admits every platform role, including ReadOnlyAuditor, Auditor,
    /// Support and PlatformFinance. Every state-changing tenant/license endpoint must require an
    /// admin-level policy on top of it.
    /// </summary>
    [Theory]
    [Trait("Audit", "Defect")]
    [InlineData(typeof(PlatformTenantsController))]
    [InlineData(typeof(PlatformTenantLicensesController))]
    [InlineData(typeof(PlatformLicensingController))]
    [InlineData(typeof(PlatformUsersController))]
    [InlineData(typeof(PlatformSupportToolsController))]
    public void Audit_TenantLifecycleMutations_RequireAnAdminLevelPlatformPolicy(Type controllerType)
    {
        var classPolicies = controllerType.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy);
        var offenders = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>()
                .SelectMany(x => x.HttpMethods)
                .Any(verb => verb is "POST" or "PUT" or "PATCH" or "DELETE"))
            .Where(method => !classPolicies
                .Concat(method.GetCustomAttributes<AuthorizeAttribute>().Select(x => x.Policy))
                .Any(policy => AdminLevelPolicies.Contains(policy)))
            .Select(method => method.Name)
            .OrderBy(x => x)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"{controllerType.Name} actions reachable by read-only/support/finance platform roles: {string.Join(", ", offenders)}");
    }

    // ---------------------------------------------------------------- Licensed seats

    /// <summary>
    /// Finding TM-P1-06. The Create Tenant form used to send no seat limit and every commercial plan is
    /// seeded with MaxUsers = null, so tenants were created with unlimited workspace users. Licensed users
    /// is now a required field of the form (mirrored by MinimalProvisioningRequest) and of the API.
    /// </summary>
    [Fact]
    [Trait("Audit", "Defect")]
    public async Task Audit_TenantCreatedFromCurrentForm_HasAFiniteSeatLimit()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var guard = new LicenseGuardService(db);
        var controller = CreateAuditPlatformTenantsController(db, guard);

        var created = Assert.IsType<PlatformTenantDetailResponse>(
            Assert.IsType<CreatedAtActionResult>((await controller.Create(MinimalProvisioningRequest(), CancellationToken.None)).Result).Value);

        var snapshot = await guard.GetSnapshotAsync(created.TenantId);
        Assert.Equal(10, snapshot.OfficeSeats);
    }

    /// <summary>
    /// Verifies the existing seat rule in LicenseGuardService: active users (including pending, never
    /// signed-in invitees, which are IsActive=true) consume seats; deactivated users do not.
    /// </summary>
    [Fact]
    [Trait("Audit", "Verified")]
    public async Task Audit_SeatGuard_CountsActiveAndPendingUsers_AndIgnoresDeactivatedUsers()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Seat Co", "ops@seat.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(100));
        (await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId)).OfficeSeats = 3;
        db.Users.AddRange(
            new User { TenantId = tenantId, FullName = "Active", Email = "a@seat.test", Role = AppRoles.Admin, IsActive = true },
            new User { TenantId = tenantId, FullName = "Pending", Email = "p@seat.test", Role = AppRoles.Dispatcher, IsActive = true, MustChangePassword = true },
            new User { TenantId = tenantId, FullName = "Gone", Email = "g@seat.test", Role = AppRoles.Dispatcher, IsActive = false },
            new User { TenantId = tenantId, FullName = "Free", Email = "v@seat.test", Role = AppRoles.Viewer, IsActive = true });
        await db.SaveChangesAsync();
        var guard = new LicenseGuardService(db);

        await guard.EnsureCanCreateUserAsync(tenantId);

        db.Users.Add(new User { TenantId = tenantId, FullName = "Third", Email = "t@seat.test", Role = AppRoles.Manager, IsActive = true });
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<LicenseException>(() => guard.EnsureCanCreateUserAsync(tenantId));
        Assert.Equal("license_office_seats_reached", ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
    }

    /// <summary>Verifies the platform "Add workspace user" path goes through the same seat guard.</summary>
    [Fact]
    [Trait("Audit", "Verified")]
    public async Task Audit_PlatformAddWorkspaceUser_IsRejectedAtTheSeatLimit()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Full Co", "ops@full.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(100));
        (await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId)).OfficeSeats = 1;
        db.Users.Add(new User { TenantId = tenantId, FullName = "Only Admin", Email = "admin@full.test", Role = AppRoles.Admin, IsActive = true });
        await db.SaveChangesAsync();
        var controller = CreateAuditPlatformTenantsController(db, new LicenseGuardService(db));

        var ex = await Assert.ThrowsAsync<LicenseException>(() => controller.CreateWorkspaceUser(
            tenantId, new CreatePlatformTenantUserRequest("Second Admin", "second@full.test", null, null, null), CancellationToken.None));

        Assert.Equal("license_office_seats_reached", ex.ErrorCode);
        Assert.False(await db.Users.AnyAsync(x => x.Email == "second@full.test"));
    }

    // ---------------------------------------------------------------- helpers

    private static PlatformTenantsController CreateAuditPlatformTenantsController(AppDbContext dbContext, ILicenseGuardService licenseGuard)
    {
        var controller = new PlatformTenantsController(
            dbContext,
            new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true),
            licenseGuard,
            new NoOpAuditLogService(),
            new FakeEmailTemplateService(),
            new FakeEmailSubjectRuleService(),
            new EmailOutboxService(dbContext, new EmailDeliveryLogService(dbContext)),
            new PasswordHasher<User>(),
            new FakeUserPermissionTemplateService(),
            new FakeUserCredentialDeliveryService(dbContext, true, true),
            Options.Create(new AuthSecurityOptions()),
            TestTimeZones.Create(dbContext),
            CreateAuthService(dbContext));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.ControllerContext.HttpContext.Request.Scheme = "https";
        controller.ControllerContext.HttpContext.Request.Host = new HostString("app.ecosys.test");
        return controller;
    }

    /// <summary>Runs exactly one SubscriptionLifecycleWorker sweep — the same code that runs at every API start-up.</summary>
    private static async Task RunSubscriptionLifecycleCycleAsync(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IPlatformBillingLifecycleService>(new PlatformBillingLifecycleService(
            db, new FakeEmailTemplateService(), new FakeEmailSubjectRuleService(),
            new EmailOutboxService(db, new EmailDeliveryLogService(db))));
        await using var provider = services.BuildServiceProvider();
        var worker = new SubscriptionLifecycleWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<SubscriptionLifecycleWorker>.Instance);
        var runCycle = typeof(SubscriptionLifecycleWorker).GetMethod("RunCycleAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SubscriptionLifecycleWorker.RunCycleAsync was renamed; update the audit helper.");
        await (Task)runCycle.Invoke(worker, [CancellationToken.None])!;
    }
}
