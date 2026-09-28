using System.Security.Claims;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Security;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// Regression suite for the tenant-management remediation (phases 0–3): one lifecycle rule
/// (TenantLicense authoritative, tenant row projected by TenantLifecycleState), licensed seats, and the
/// security hardening. Companion to the audit proofs in TenantManagementAuditTests.
/// </summary>
public sealed partial class AuthEmailSafetyTests
{
    // ------------------------------------------------------------------ lifecycle

    [Fact]
    public async Task Lifecycle_WorkerSuspension_RecordsNonPayment_AndBlocksTheTenantRow()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Lapsed Co", "ops@lapsed.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(-30));

        await RunSubscriptionLifecycleCycleAsync(db);

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        Assert.Equal(LicenseSuspensionReasons.NonPayment, license.SuspensionReason);
        Assert.Equal(TenantStatuses.Suspended, tenant.Status);
        Assert.Equal(LicenseStatuses.Suspended, tenant.LicenseStatus);
        Assert.False(tenant.IsActive);
    }

    [Fact]
    public async Task Lifecycle_Payment_LiftsANonPaymentSuspension()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Lapsed Co", "ops@lapsed.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(-30));
        await RunSubscriptionLifecycleCycleAsync(db);

        await CreateBillingService(db).ApplyPaymentReceivedAsync(
            new PlatformPayment { TenantId = tenantId, Amount = 100m, PaymentNumber = "PAY-1" }, null, null, CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        var snapshot = await new LicenseGuardService(db).GetSnapshotAsync(tenantId);
        Assert.False(snapshot.IsSuspended);
        Assert.Equal(TenantStatuses.Active, tenant.Status);
        Assert.True(tenant.IsActive);
    }

    [Fact]
    public async Task Lifecycle_Payment_DoesNotLiftAManualSuspension()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Disputed Co", "ops@disputed.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(60));
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));
        await controller.Suspend(tenantId, new UpdatePlatformTenantStatusReasonRequest("Fraud review"), CancellationToken.None);

        await CreateBillingService(db).ApplyPaymentReceivedAsync(
            new PlatformPayment { TenantId = tenantId, Amount = 100m, PaymentNumber = "PAY-2" }, null, null, CancellationToken.None);

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal(LicenseStatuses.Suspended, license.Status);
        Assert.Equal(LicenseSuspensionReasons.Manual, license.SuspensionReason);
        Assert.False((await db.Tenants.SingleAsync(x => x.Id == tenantId)).IsActive);
    }

    [Fact]
    public async Task Lifecycle_ExtendingAManuallySuspendedLicense_KeepsItSuspendedUntilReactivated()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Held Co", "ops@held.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(60));
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));
        await controller.Suspend(tenantId, new UpdatePlatformTenantStatusReasonRequest("Contract dispute"), CancellationToken.None);

        await controller.ExtendLicense(tenantId, new ExtendTenantLicenseRequest(DateTime.UtcNow.AddYears(1)), CancellationToken.None);
        Assert.Equal(LicenseStatuses.Suspended, (await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId)).Status);

        await controller.ReactivateLicense(tenantId, CancellationToken.None);
        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        Assert.Equal(TenantStatuses.Active, tenant.Status);
        Assert.True(tenant.IsActive);
        Assert.Null((await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId)).SuspensionReason);
    }

    [Fact]
    public async Task Lifecycle_LicensingConsoleSuspend_ProjectsTheTenantRow()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Console Co", "ops@console.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(60));
        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId);
        var controller = new PlatformLicensingController(
            db,
            new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true),
            new NoOpAuditLogService(),
            new NoOpLicenseGuardService(db));

        await controller.Suspend(license.Id, CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        Assert.Equal(TenantStatuses.Suspended, tenant.Status);
        Assert.False(tenant.IsActive);
        Assert.Equal(LicenseSuspensionReasons.Manual, (await db.TenantLicenses.SingleAsync(x => x.Id == license.Id)).SuspensionReason);
    }

    [Fact]
    public async Task Lifecycle_SubscriptionUpdate_CannotMoveALicenseToAnotherTenant()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Owner Co", "ops@owner.test");
        await SeedTenantAsync(db, otherTenantId, "Other Co", "ops@other.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(60));
        var license = await db.TenantLicenses.Include(x => x.LicensePlan).SingleAsync(x => x.TenantId == tenantId);
        var suite = new LicensePlan { PlanCode = "suite", DisplayName = "Suite", IsActive = true };
        db.LicensePlans.Add(suite);
        await db.SaveChangesAsync();
        var controller = new PlatformLicensingController(
            db,
            new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true),
            new NoOpAuditLogService(),
            new NoOpLicenseGuardService(db));

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.UpdateSubscription(
            license.Id,
            new UpsertPlatformSubscriptionRequest(otherTenantId, suite.Id, "Active", "Monthly", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, null),
            CancellationToken.None));
        Assert.Equal(tenantId, (await db.TenantLicenses.SingleAsync(x => x.Id == license.Id)).TenantId);
    }

    [Fact]
    public async Task Lifecycle_DeactivatingATenant_LeavesItsAssetsUntouched()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Leaving Co", "ops@leaving.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(60));
        db.Assets.Add(new Asset { TenantId = tenantId, AssetName = "Chiller 1", AssetCode = "CH-1", Status = "Active" });
        await db.SaveChangesAsync();
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        await controller.DeactivatePost(tenantId, new UpdatePlatformTenantStatusReasonRequest("Closed account"), CancellationToken.None);

        var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId);
        Assert.Equal(TenantStatuses.Inactive, tenant.Status);
        Assert.False(tenant.IsActive);
        Assert.Equal("Active", (await db.Assets.SingleAsync(x => x.TenantId == tenantId)).Status);
    }

    [Fact]
    public async Task Lifecycle_ExtendTrial_IsAllowedForTrialLicenses()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var guard = new LicenseGuardService(db);
        var controller = CreateAuditPlatformTenantsController(db, guard);
        var created = Assert.IsType<PlatformTenantDetailResponse>(
            Assert.IsType<CreatedAtActionResult>((await controller.Create(MinimalProvisioningRequest(), CancellationToken.None)).Result).Value);

        var extended = Assert.IsType<PlatformTenantDetailResponse>(
            Assert.IsType<OkObjectResult>((await controller.ExtendTrial(created.TenantId, CancellationToken.None)).Result).Value);

        Assert.Equal(TenantStatuses.Trial, extended.Status);
        Assert.True(extended.TrialExtensionUsed);
    }

    // ------------------------------------------------------------------ middleware messages

    [Fact]
    public async Task Middleware_SuspendedTenant_GetsOneLicenseSuspendedError_AndCanStillReadItsLicense()
    {
        var (http, tenantContext, db) = await CreateMiddlewareHarnessAsync(LicenseStatuses.Suspended, tenantIsActive: false, "/api/work-orders", "WorkOrders");
        await using var _ = db;
        var middleware = new TenantLicenseEnforcementMiddleware(_ => Task.CompletedTask);
        var guard = new LicenseGuardService(db);

        var error = await Assert.ThrowsAsync<LicenseException>(() => middleware.InvokeAsync(http, tenantContext, guard, db));
        Assert.Equal("license_suspended", error.ErrorCode);

        http.Request.Path = "/api/tenant/license";
        var reached = false;
        await new TenantLicenseEnforcementMiddleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(http, tenantContext, guard, db);
        Assert.True(reached);
    }

    [Fact]
    public async Task Middleware_DeactivatedTenant_GetsTheDeactivatedError()
    {
        var (http, tenantContext, db) = await CreateMiddlewareHarnessAsync(LicenseStatuses.Cancelled, tenantIsActive: false, "/api/work-orders", "WorkOrders");
        await using var _ = db;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new TenantLicenseEnforcementMiddleware(_ => Task.CompletedTask).InvokeAsync(http, tenantContext, new LicenseGuardService(db), db));
    }

    // ------------------------------------------------------------------ seats

    [Fact]
    public async Task Seats_CreatingATenantWithoutLicensedUsers_IsRejected()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreateAuditPlatformTenantsController(db, new LicenseGuardService(db));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Create(MinimalProvisioningRequest(maxUsers: null), CancellationToken.None));

        Assert.StartsWith("Licensed user limit is required.", error.Message);
        Assert.False(await db.Tenants.AnyAsync(x => x.Slug == "acme-engineering"));
    }

    [Fact]
    public async Task Seats_UpdateBelowUsage_DeactivatesNobody_AndBlocksFurtherAdditions()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Shrinking Co", "ops@shrinking.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(60));
        db.Users.AddRange(
            new User { TenantId = tenantId, FullName = "One", Email = "one@shrinking.test", Role = AppRoles.Admin, IsActive = true },
            new User { TenantId = tenantId, FullName = "Two", Email = "two@shrinking.test", Role = AppRoles.Viewer, IsActive = true },
            new User { TenantId = tenantId, FullName = "Three", Email = "three@shrinking.test", Role = AppRoles.Viewer, IsActive = true });
        await db.SaveChangesAsync();
        var guard = new LicenseGuardService(db);
        var controller = CreateAuditPlatformTenantsController(db, guard);

        var detail = Assert.IsType<PlatformTenantDetailResponse>(
            Assert.IsType<OkObjectResult>((await controller.UpdateSeats(tenantId, new UpdateTenantSeatsRequest(2), CancellationToken.None)).Result).Value);

        Assert.Equal(2, detail.SeatLimit);
        Assert.Equal(3, detail.SeatsUsed);
        Assert.Equal(3, await db.Users.CountAsync(x => x.TenantId == tenantId && x.IsActive));
        await Assert.ThrowsAsync<LicenseException>(() => guard.EnsureCanCreateUserAsync(tenantId));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.UpdateSeats(tenantId, new UpdateTenantSeatsRequest(0), CancellationToken.None));
    }

    [Fact]
    public async Task Seats_EditingATenantWithoutSeatsOrStatus_KeepsThem()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreateAuditPlatformTenantsController(db, new LicenseGuardService(db));
        var created = Assert.IsType<PlatformTenantDetailResponse>(
            Assert.IsType<CreatedAtActionResult>((await controller.Create(MinimalProvisioningRequest(maxUsers: 12), CancellationToken.None)).Result).Value);
        await controller.Suspend(created.TenantId, new UpdatePlatformTenantStatusReasonRequest("Audit"), CancellationToken.None);

        await controller.Update(created.TenantId, MinimalProvisioningRequest(maxUsers: null) with { CompanyName = "Acme Engineering Group", Slug = "acme-engineering" }, CancellationToken.None);

        var license = await db.TenantLicenses.SingleAsync(x => x.TenantId == created.TenantId);
        var tenant = await db.Tenants.SingleAsync(x => x.Id == created.TenantId);
        Assert.Equal(12, license.LicensedUsers);
        Assert.Equal(LicenseStatuses.Suspended, license.Status);
        Assert.Equal(TenantStatuses.Suspended, tenant.Status);
        Assert.Equal("Acme Engineering Group", tenant.CompanyName);
    }

    [Fact]
    public async Task Seats_GuardChecksABatch_AgainstTheRemainingSeats()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Batch Co", "ops@batch.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(60));
        (await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId)).LicensedUsers = 5;
        db.Users.Add(new User { TenantId = tenantId, FullName = "Admin", Email = "admin@batch.test", Role = AppRoles.Admin, IsActive = true });
        await db.SaveChangesAsync();
        var guard = new LicenseGuardService(db);

        await guard.EnsureCanAddUsersAsync(tenantId, 4);
        var error = await Assert.ThrowsAsync<LicenseException>(() => guard.EnsureCanAddUsersAsync(tenantId, 5));
        Assert.Contains("licensed user limit of 5: 5 active users requested but only 4 seats remain", error.Message);
        await guard.EnsureCanAddUsersAsync(tenantId, 0);
    }

    [Fact]
    public async Task Seats_DetailAndListExposeUsageLimitModulesAndInvitationState()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreateAuditPlatformTenantsController(db, new LicenseGuardService(db));
        var created = Assert.IsType<PlatformTenantDetailResponse>(
            Assert.IsType<CreatedAtActionResult>((await controller.Create(MinimalProvisioningRequest("finance", 7), CancellationToken.None)).Result).Value);

        Assert.Equal(7, created.SeatLimit);
        Assert.Equal(1, created.SeatsUsed);
        Assert.Equal(["finance"], created.Modules);

        var list = Assert.IsAssignableFrom<IReadOnlyCollection<PlatformTenantListResponse>>(
            Assert.IsType<OkObjectResult>((await controller.GetAll(CancellationToken.None)).Result).Value);
        var row = Assert.Single(list);
        Assert.Equal(7, row.SeatLimit);
        Assert.Equal("admin@acme.test", row.PrimaryAdminEmail);

        var users = Assert.IsAssignableFrom<IReadOnlyCollection<PlatformTenantUserResponse>>(
            Assert.IsType<OkObjectResult>((await controller.GetUsers(created.TenantId, CancellationToken.None)).Result).Value);
        var admin = Assert.Single(users);
        Assert.Equal("Invited", admin.InvitationStatus);
        Assert.NotNull(admin.InvitationExpiresAt);
    }

    [Fact]
    public async Task Plans_RetiredFeatures_AreNotStored_WhenAnOldClientSendsThem()
    {
        await using var db = CreateDbContext();
        var controller = new PlatformLicensingController(
            db,
            new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true),
            new NoOpAuditLogService(),
            new NoOpLicenseGuardService(db));

        var operationsPlanId = Guid.Parse("55555555-5555-5555-5555-555555555551");
        var created = Assert.IsType<PlatformLicensePlanResponse>(Assert.IsType<OkObjectResult>((await controller.UpdatePlan(
            operationsPlanId,
            new UpsertPlatformLicensePlanRequest("Ops", "operations", 0, 0, null, ["EmailIngestion", "AdvancedReports", "ClientPortal"], true),
            CancellationToken.None)).Result).Value);

        Assert.DoesNotContain("AdvancedReports", created.ModulesIncluded);
        Assert.DoesNotContain("ClientPortal", created.ModulesIncluded);
        Assert.Contains("EmailIngestion", created.ModulesIncluded);
    }

    // ------------------------------------------------------------------ security

    [Fact]
    public async Task Security_AnonymousSignup_IsDisabledByDefault()
    {
        await using var db = CreateDbContext();
        var auth = CreateAuthService(db);

        await Assert.ThrowsAsync<NotFoundException>(() => auth.SignupAsync("Mallory", "mallory@example.test", "Str0ng!Passw0rd", "Mallory Ltd", null, "Kenya"));
        Assert.False(await db.Tenants.AnyAsync(x => x.CompanyName == "Mallory Ltd"));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Security_PlatformFilterBypass_RequiresThePlatformRootTenant(bool rootTenant, bool expectedBypass)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(TenantClaimTypes.TenantId, (rootTenant ? PlatformConstants.RootTenantId : Guid.NewGuid()).ToString()),
                new Claim(TenantClaimTypes.UserId, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, AppRoles.Support)
            ], "test"))
        };

        var context = new HttpTenantContext(new HttpContextAccessor { HttpContext = http });

        Assert.Equal(expectedBypass, context.IsSuperAdmin);
    }

    [Fact]
    public void Security_EveryTenantControllerHasAnExplicitModuleClassification()
    {
        var unclassified = typeof(PlatformTenantsController).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Select(type => type.Name[..^"Controller".Length])
            .Where(name => !name.StartsWith("Platform", StringComparison.Ordinal))
            .Where(name => !ServiceOpsPlans.IsClassified(name))
            .OrderBy(name => name)
            .ToList();

        Assert.True(unclassified.Count == 0,
            $"Classify these controllers in ServiceOpsPlans (Operations, Finance or Shared): {string.Join(", ", unclassified)}");
    }

    // ------------------------------------------------------------------ helpers

    private static PlatformBillingLifecycleService CreateBillingService(AppDbContext db) =>
        new(db, new FakeEmailTemplateService(), new FakeEmailSubjectRuleService(), new EmailOutboxService(db, new EmailDeliveryLogService(db)));

    private static async Task<(DefaultHttpContext Http, ITenantContext TenantContext, AppDbContext Db)> CreateMiddlewareHarnessAsync(
        string licenseStatus, bool tenantIsActive, string path, string controller)
    {
        var tenantId = Guid.NewGuid();
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Request.Method = "GET";
        http.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(TenantClaimTypes.TenantId, tenantId.ToString()),
            new Claim(TenantClaimTypes.UserId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Admin)
        ], "test"));
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = controller }), "test"));
        // HttpContextAccessor keeps the context in an AsyncLocal that would not flow back out of this async
        // helper, so the harness pins it with a plain accessor.
        var tenantContext = new HttpTenantContext(new FixedHttpContextAccessor(http));
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantContext);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Harness", CompanyName = "Harness", Slug = $"harness-{tenantId:N}", Country = "Kenya", IsActive = tenantIsActive });
        var plan = new LicensePlan { PlanCode = "suite", DisplayName = "Suite", IsActive = true };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense
        {
            TenantId = tenantId, LicensePlan = plan, Status = licenseStatus, StartsAt = DateTime.UtcNow.AddMonths(-1),
            ExpiresAt = DateTime.UtcNow.AddMonths(6), SuspendedAt = licenseStatus == LicenseStatuses.Suspended ? DateTime.UtcNow : null,
        });
        await db.SaveChangesAsync();
        return (http, tenantContext, db);
    }

    private sealed class FixedHttpContextAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext
        {
            get => context;
            set { }
        }
    }
}
