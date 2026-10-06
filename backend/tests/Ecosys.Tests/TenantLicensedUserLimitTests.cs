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
/// Licensed-user limit, end to end: the canonical field is <c>MaxUsers</c> on the tenant create/update and
/// seat requests, stored as <see cref="TenantLicense.LicensedUsers"/> and enforced by
/// <see cref="LicenseGuardService"/> for every path that adds or reactivates a workspace user.
/// </summary>
public sealed partial class AuthEmailSafetyTests
{
    [Fact]
    public async Task LicensedUsers_NewTenantWithTen_IsCreatedWithThatLimit()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var guard = new LicenseGuardService(db);
        var controller = CreateAuditPlatformTenantsController(db, guard);

        var created = Assert.IsType<PlatformTenantDetailResponse>(
            Assert.IsType<CreatedAtActionResult>((await controller.Create(MinimalProvisioningRequest(maxUsers: 10), CancellationToken.None)).Result).Value);

        Assert.Equal(10, created.SeatLimit);
        Assert.Equal(1, created.SeatsUsed);
        Assert.Equal(10, (await db.TenantLicenses.SingleAsync(x => x.TenantId == created.TenantId)).LicensedUsers);
        Assert.Equal(10, (await db.Tenants.SingleAsync(x => x.Id == created.TenantId)).MaxUsers);
        Assert.Equal(10, (await guard.GetSnapshotAsync(created.TenantId)).MaxUsers);
    }

    [Fact]
    public async Task LicensedUsers_MissingOnCreate_IsRejectedWithAClearMessage()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreateAuditPlatformTenantsController(db, new LicenseGuardService(db));

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Create(MinimalProvisioningRequest(maxUsers: null), CancellationToken.None));

        Assert.StartsWith("Licensed user limit is required.", error.Message);
        Assert.False(await db.Tenants.AnyAsync(x => x.CompanyName == "Acme Engineering Ltd"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task LicensedUsers_ZeroOrNegative_IsRejectedOnCreateAndSeatUpdate(int value)
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreateAuditPlatformTenantsController(db, new LicenseGuardService(db));

        var createError = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Create(MinimalProvisioningRequest(maxUsers: value), CancellationToken.None));
        Assert.Contains("greater than zero", createError.Message);
        Assert.False(await db.Tenants.AnyAsync(x => x.CompanyName == "Acme Engineering Ltd"));

        var created = Assert.IsType<PlatformTenantDetailResponse>(
            Assert.IsType<CreatedAtActionResult>((await controller.Create(MinimalProvisioningRequest(maxUsers: 10), CancellationToken.None)).Result).Value);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.UpdateSeats(created.TenantId, new UpdateTenantSeatsRequest(value), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Update(created.TenantId, MinimalProvisioningRequest(maxUsers: value), CancellationToken.None));
        Assert.Equal(10, (await db.TenantLicenses.SingleAsync(x => x.TenantId == created.TenantId)).LicensedUsers);
    }

    [Fact]
    public async Task PlatformEdit_DoesNotOverwriteCompanyDetailsTheTenantManagesItself()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreateAuditPlatformTenantsController(db, new LicenseGuardService(db));
        var created = Assert.IsType<PlatformTenantDetailResponse>(
            Assert.IsType<CreatedAtActionResult>((await controller.Create(MinimalProvisioningRequest(maxUsers: 10), CancellationToken.None)).Result).Value);

        // The tenant updates its own Company Profile...
        var tenant = await db.Tenants.SingleAsync(x => x.Id == created.TenantId);
        tenant.CompanyName = "Acme Engineering (K) Ltd";
        tenant.Email = "info@acme.co.ke";
        tenant.Phone = "+254700000001";
        tenant.Country = "Uganda";
        tenant.TimeZoneId = "Africa/Kampala";
        tenant.Address = "Plot 5, Kampala Road";
        await db.SaveChangesAsync();

        // ...then a platform admin edits the tenant with stale company details in the request.
        await controller.Update(created.TenantId, MinimalProvisioningRequest(maxUsers: 12) with
        {
            CompanyEmail = "stale@acme.test", CompanyPhone = "+254799999999", Country = "Kenya", Address = "Old address",
            PrimaryContactName = "New Contact",
        }, CancellationToken.None);

        var reloaded = await db.Tenants.AsNoTracking().SingleAsync(x => x.Id == created.TenantId);
        Assert.Equal("Acme Engineering (K) Ltd", reloaded.CompanyName);
        Assert.Equal("info@acme.co.ke", reloaded.Email);
        Assert.Equal("+254700000001", reloaded.Phone);
        Assert.Equal("Uganda", reloaded.Country);
        Assert.Equal("Africa/Kampala", reloaded.TimeZoneId);
        Assert.Equal("Plot 5, Kampala Road", reloaded.Address);
        Assert.Equal("New Contact", reloaded.ContactName);
    }

    [Fact]
    public async Task LicensedUsers_LimitOfTen_BlocksTheEleventhUser_UntilTheLimitIsRaised()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var tenantId = Guid.NewGuid();
        await SeedLicensedTenantAsync(db, tenantId, limit: 10, activeUsers: 10);
        var guard = new LicenseGuardService(db);
        var users = CreateUsersController(db, tenantId, new FakeUserCredentialDeliveryService(db, credentialSendSucceeds: true), guard);

        var error = await Assert.ThrowsAsync<LicenseException>(() => users.Create(NewActiveUser("eleventh@seats.test"), CancellationToken.None));

        Assert.Equal("license_users_limit_reached", error.ErrorCode);
        Assert.Equal(409, error.StatusCode);
        Assert.StartsWith("This tenant has reached its licensed user limit of 10.", error.Message);
        Assert.Equal(10, await db.Users.CountAsync(x => x.TenantId == tenantId && x.IsActive));

        var platform = CreateAuditPlatformTenantsController(db, guard);
        await platform.UpdateSeats(tenantId, new UpdateTenantSeatsRequest(11), CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>((await users.Create(NewActiveUser("eleventh@seats.test"), CancellationToken.None)).Result);
        Assert.Equal(11, await db.Users.CountAsync(x => x.TenantId == tenantId && x.IsActive));
        await Assert.ThrowsAsync<LicenseException>(() => users.Create(NewActiveUser("twelfth@seats.test"), CancellationToken.None));
    }

    [Fact]
    public async Task LicensedUsers_DeactivatedUsers_FreeTheirSeat_AndReactivationIsChecked()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedLicensedTenantAsync(db, tenantId, limit: 2, activeUsers: 2);
        var guard = new LicenseGuardService(db);
        var users = CreateUsersController(db, tenantId, new FakeUserCredentialDeliveryService(db, credentialSendSucceeds: true), guard);
        var departing = await db.Users.FirstAsync(x => x.TenantId == tenantId);

        await users.UpdateStatus(departing.Id, new UpdateUserStatusRequest(false), CancellationToken.None);
        await guard.EnsureCanCreateUserAsync(tenantId);

        // An inactive account can be created while full: it does not take a seat.
        Assert.IsType<CreatedAtActionResult>((await users.Create(NewActiveUser("replacement@seats.test"), CancellationToken.None)).Result);
        Assert.IsType<CreatedAtActionResult>((await users.Create(NewActiveUser("parked@seats.test") with { IsActive = false }, CancellationToken.None)).Result);

        var error = await Assert.ThrowsAsync<LicenseException>(() => users.UpdateStatus(departing.Id, new UpdateUserStatusRequest(true), CancellationToken.None));
        Assert.Equal("license_users_limit_reached", error.ErrorCode);
        Assert.False((await db.Users.SingleAsync(x => x.Id == departing.Id)).IsActive);
    }

    [Fact]
    public async Task LicensedUsers_PortalContacts_DoNotConsumeSeats()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedLicensedTenantAsync(db, tenantId, limit: 2, activeUsers: 1);
        for (var i = 0; i < 5; i++)
        {
            db.ClientPortalUsers.Add(new ClientPortalUser { TenantId = tenantId, ClientId = Guid.NewGuid(), FullName = $"Requester {i}", Email = $"requester{i}@customer.test", IsActive = true });
        }
        await db.SaveChangesAsync();
        var guard = new LicenseGuardService(db);

        await guard.EnsureCanCreateUserAsync(tenantId);
        Assert.Equal(1, (await guard.GetUsageAsync(tenantId)).ActiveUsers);
        await Assert.ThrowsAsync<LicenseException>(() => guard.EnsureCanAddUsersAsync(tenantId, 2));
    }

    /// <summary>
    /// Licenses created without an explicit seat count (self-signup and first-access provisioning go through
    /// GetOrCreateTenantLicenseAsync) start as a Suite trial with the default limit, never unlimited.
    /// </summary>
    [Fact]
    public async Task LicensedUsers_AutoProvisionedLicense_IsASuiteTrialWithTheDefaultLimit()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Signup Co", "ops@signup.test");
        var guard = new LicenseGuardService(db);

        var license = await guard.GetOrCreateTenantLicenseAsync(tenantId);

        Assert.Equal(ServiceOpsPlans.Suite, license.LicensePlan!.PlanCode);
        Assert.Equal(LicenseStatuses.Trial, license.Status);
        Assert.Equal(TenantLicense.DefaultLicensedUsers, license.LicensedUsers);
        Assert.Equal(TenantLicense.DefaultLicensedUsers, (await guard.GetSnapshotAsync(tenantId)).MaxUsers);
        Assert.False(await db.LicensePlans.AnyAsync(x => x.PlanCode == "Trial" || x.PlanCode == "Starter" || x.PlanCode == "Professional" || x.PlanCode == "Enterprise"));
    }

    [Fact]
    public async Task LicensedUsers_RenewingOntoAnotherPlan_KeepsTheLimit()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreateAuditPlatformTenantsController(db, new LicenseGuardService(db));
        var created = Assert.IsType<PlatformTenantDetailResponse>(
            Assert.IsType<CreatedAtActionResult>((await controller.Create(MinimalProvisioningRequest("operations", 7), CancellationToken.None)).Result).Value);
        var suiteId = (await db.LicensePlans.SingleAsync(x => x.PlanCode == ServiceOpsPlans.Suite)).Id;

        var renewed = Assert.IsType<PlatformTenantDetailResponse>(Assert.IsType<OkObjectResult>((await controller.RenewLicense(
            created.TenantId,
            new RenewTenantLicenseRequest(DateTime.UtcNow, DateTime.UtcNow.AddYears(1), false, "Annual", null, null, suiteId),
            CancellationToken.None)).Result).Value);

        Assert.Equal(7, renewed.SeatLimit);
        Assert.Equal(["operations", "finance"], renewed.Modules);
    }

    [Fact]
    public async Task LicensedUsers_SubscriptionApi_SetsAnExplicitLimit_AndRejectsInvalidOnes()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        var defaultTenantId = Guid.NewGuid();
        var chosenTenantId = Guid.NewGuid();
        await SeedTenantAsync(db, defaultTenantId, "Default Co", "ops@default.test");
        await SeedTenantAsync(db, chosenTenantId, "Chosen Co", "ops@chosen.test");
        var suiteId = (await db.LicensePlans.SingleAsync(x => x.PlanCode == ServiceOpsPlans.Suite)).Id;
        var controller = new PlatformLicensingController(
            db,
            new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true),
            new NoOpAuditLogService(),
            new NoOpLicenseGuardService(db));
        UpsertPlatformSubscriptionRequest Request(Guid tenantId, int? maxUsers) =>
            new(tenantId, suiteId, "Active", "Monthly", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, null, maxUsers);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.CreateSubscription(Request(chosenTenantId, 0), CancellationToken.None));
        var defaulted = Assert.IsType<PlatformSubscriptionResponse>(Assert.IsType<CreatedAtActionResult>(
            (await controller.CreateSubscription(Request(defaultTenantId, null), CancellationToken.None)).Result).Value);
        var chosen = Assert.IsType<PlatformSubscriptionResponse>(Assert.IsType<CreatedAtActionResult>(
            (await controller.CreateSubscription(Request(chosenTenantId, 25), CancellationToken.None)).Result).Value);

        Assert.Equal(TenantLicense.DefaultLicensedUsers, defaulted.MaxUsers);
        Assert.Equal(25, chosen.MaxUsers);
        var kept = Assert.IsType<PlatformSubscriptionResponse>(Assert.IsType<OkObjectResult>(
            (await controller.UpdateSubscription(chosen.Id, Request(chosenTenantId, null), CancellationToken.None)).Result).Value);
        Assert.Equal(25, kept.MaxUsers);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.UpdateSubscription(chosen.Id, Request(chosenTenantId, -3), CancellationToken.None));
    }

    private static async Task SeedLicensedTenantAsync(AppDbContext db, Guid tenantId, int limit, int activeUsers)
    {
        await SeedTenantAsync(db, tenantId, $"Seats {tenantId:N}", $"ops-{tenantId:N}@seats.test");
        await SeedActiveLicenseAsync(db, tenantId, DateTime.UtcNow.AddDays(60));
        (await db.TenantLicenses.SingleAsync(x => x.TenantId == tenantId)).LicensedUsers = limit;
        for (var i = 0; i < activeUsers; i++)
        {
            db.Users.Add(new User { TenantId = tenantId, FullName = $"User {i}", Email = $"user{i}-{tenantId:N}@seats.test", Role = i == 0 ? AppRoles.Admin : AppRoles.User, IsActive = true });
        }
        await db.SaveChangesAsync();
    }

    private static CreateUserRequest NewActiveUser(string email) => new(
        "Seat Holder", email, null, null, AppRoles.User, null, null, true, "TemporaryPassword", null, [], null, false, []);
}
