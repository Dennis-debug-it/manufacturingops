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

public sealed partial class AuthEmailSafetyTests
{
    [Theory]
    [InlineData("operations")]
    [InlineData("finance")]
    [InlineData("suite")]
    public async Task MinimalProvisioning_CreatesSelectedPlanIdentityAdminAndInvitation(string plan)
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));
        var result = await controller.Create(MinimalProvisioningRequest(plan), CancellationToken.None);
        var response = Assert.IsType<PlatformTenantDetailResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        var tenant = await db.Tenants.SingleAsync(x => x.Id == response.TenantId);
        var identity = await db.EmailSettings.SingleAsync(x => x.TenantId == tenant.Id);
        var admin = await db.Users.SingleAsync(x => x.TenantId == tenant.Id);
        var invitation = await db.PasswordResetTokens.SingleAsync(x => x.UserId == admin.Id);
        var license = await db.TenantLicenses.Include(x => x.LicensePlan).SingleAsync(x => x.TenantId == tenant.Id);
        Assert.Equal("acme-engineering", tenant.Slug);
        Assert.Equal("Africa/Nairobi", tenant.TimeZoneId);
        Assert.Equal("KES", tenant.Currency);
        Assert.Equal("acme-engineering@serviceops.ecosysdigital.co.ke", identity.ManagedEmailAddress);
        Assert.Equal(identity.ManagedEmailAddress, response.ServiceOpsEmail);
        Assert.Equal(identity.ManagedEmailAddress, identity.SenderAddress);
        Assert.True(identity.InboundEnabled);
        Assert.Equal("admin@acme.test", admin.Email);
        Assert.Equal(AppRoles.Admin, admin.Role);
        Assert.True(admin.MustChangePassword);
        Assert.Equal(plan, license.LicensePlan!.PlanCode);
        Assert.Equal(PasswordLinkPurposes.Invitation, invitation.Purpose);
        Assert.InRange((invitation.ExpiresAt - invitation.CreatedAt).TotalHours, 71.99, 72.01);
        Assert.Equal(DateTimeKind.Utc, invitation.ExpiresAt.Kind);
        Assert.Equal(tenant.Id, await TenantEmailIdentity.ResolveRecipientAsync(db, identity.ManagedEmailAddress!));
    }

    [Fact]
    public async Task Provisioning_DeliveryFailureRetainsTenantAdminAndInvitation()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, false, false));
        var result = await controller.Create(MinimalProvisioningRequest(), CancellationToken.None);
        var response = Assert.IsType<PlatformTenantDetailResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.False(response.InitialAdminInvitationSent);
        var admin = await db.Users.SingleAsync(x => x.TenantId == response.TenantId);
        Assert.True(await db.PasswordResetTokens.AnyAsync(x => x.UserId == admin.Id && x.Purpose == PasswordLinkPurposes.Invitation));
        Assert.True(await db.EmailSettings.AnyAsync(x => x.TenantId == response.TenantId));
    }

    [Fact]
    public async Task Provisioning_GivesThePlatformCreatedWorkspaceTheBasicPmChecklists()
    {
        // A self-signup workspace always had these; one created from the platform started with none.
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));

        var result = await controller.Create(MinimalProvisioningRequest(), CancellationToken.None);

        var response = Assert.IsType<PlatformTenantDetailResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        var names = await db.PmTemplates.IgnoreQueryFilters().Where(x => x.TenantId == response.TenantId).Select(x => x.Name).OrderBy(x => x).ToListAsync();
        Assert.Equal(["Generator PM Checklist", "HVAC PM Checklist", "Solar PM Checklist", "UPS PM Checklist"], names);
    }

    [Fact]
    public async Task Provisioning_RejectsDuplicateSlugBeforePersistingSecondTenant()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));
        await controller.Create(MinimalProvisioningRequest(), CancellationToken.None);
        var duplicate = MinimalProvisioningRequest() with { CompanyName = "Another Company", Slug = "ACME ENGINEERING", AdminEmail = "other@acme.test" };
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Create(duplicate, CancellationToken.None));
        Assert.Equal(2, await db.Tenants.CountAsync());
    }

    [Theory]
    [InlineData("Acme Engineering Ltd", "acme-engineering")]
    [InlineData(" Prime & Facilities Limited ", "prime-facilities")]
    public void TenantSlug_NormalizesOnBackend(string value, string expected) =>
        Assert.Equal(expected, TenantEmailIdentity.NormalizeSlug(value, fromCompanyName: true));

    [Theory]
    [InlineData("admin")]
    [InlineData("API")]
    [InlineData("postmaster")]
    [InlineData("@@@")]
    [InlineData("ab")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void TenantSlug_RejectsReservedEmptyAndOversized(string value) =>
        Assert.Throws<BusinessRuleException>(() => TenantEmailIdentity.NormalizeSlug(value));

    [Fact]
    public async Task TenantEmailIdentity_IsTenantScopedAndAddressCannotBeUpdated()
    {
        var databaseName = $"managed-email-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options;
        var tenantA = new Tenant { Name = "Tenant A", CompanyName = "Tenant A", Slug = "tenant-a", Email = "a@test.local" };
        var tenantB = new Tenant { Name = "Tenant B", CompanyName = "Tenant B", Slug = "tenant-b", Email = "b@test.local" };
        await using (var seed = new AppDbContext(options))
        {
            seed.Tenants.AddRange(tenantA, tenantB);
            seed.EmailSettings.AddRange(TenantEmailIdentity.Create(tenantA), TenantEmailIdentity.Create(tenantB));
            await seed.SaveChangesAsync();
        }
        var context = new FakeTenantContext(tenantA.Id, Guid.NewGuid(), isAdmin: true);
        await using var db = new AppDbContext(options, context);
        Assert.Null(await TenantEmailIdentity.ResolveRecipientAsync(db, "tenant-b@serviceops.ecosysdigital.co.ke"));
        Assert.Equal(tenantA.Id, await TenantEmailIdentity.ResolveRecipientAsync(db, "tenant-a@serviceops.ecosysdigital.co.ke"));
        var controller = new TenantController(db, context, new NoOpAuditLogService(), null!, new NoOpUserAccessService());
        var request = JsonSerializer.Deserialize<UpdateTenantEmailIdentityRequest>("""
            { "DisplayName": "New Display", "ReplyToAddress": "reply@test.local", "InboundEnabled": false,
              "OutboundEnabled": true, "EmailAddress": "tenant-b@serviceops.ecosysdigital.co.ke", "TenantId": "00000000-0000-0000-0000-000000000001" }
            """)!;
        var result = await controller.UpdateEmailIdentity(request, CancellationToken.None);
        var identity = Assert.IsType<TenantEmailIdentityResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal("tenant-a@serviceops.ecosysdigital.co.ke", identity.EmailAddress);
        Assert.Equal("New Display", identity.DisplayName);
        Assert.Null(await TenantEmailIdentity.ResolveRecipientAsync(db, identity.EmailAddress!));
        await using var verify = new AppDbContext(options);
        Assert.Equal("Tenant B", (await verify.EmailSettings.SingleAsync(x => x.TenantId == tenantB.Id)).SenderName);
    }

    [Fact]
    public async Task Provisioning_AppliesCountryDefaultsAndHonorsTimeZoneCorrection()
    {
        await using var db = CreateDbContext();
        await SeedPlatformUserAsync(db, "owner@platform.test");
        var controller = CreatePlatformTenantsController(db, new FakeUserCredentialDeliveryService(db, true, true));
        var request = MinimalProvisioningRequest() with { Country = "United Kingdom", TimeZoneId = "Europe/London" };
        var result = await controller.Create(request, CancellationToken.None);
        var response = Assert.IsType<PlatformTenantDetailResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal("Europe/London", response.TimeZoneId);
        Assert.Equal("GBP", response.Currency);
    }

    [Fact]
    public async Task EmailOutbox_UsesManagedSenderAndRetainsPlatformTransportOwnership()
    {
        await using var db = CreateDbContext();
        var tenant = new Tenant { Name = "Acme", CompanyName = "Acme", Slug = "acme", Email = "contact@acme.test" };
        db.Tenants.Add(tenant);
        db.EmailSettings.Add(TenantEmailIdentity.Create(tenant));
        await db.SaveChangesAsync();
        var outbox = new EmailOutboxService(db, new EmailDeliveryLogService(db));
        var message = await outbox.QueueEmailAsync(new QueueEmailRequest(tenant.Id, "test", "test", "recipient@test.local",
            null, "Platform", "platform@test.local", null, "Hello", "Hello", "Hello", null));
        Assert.Equal("acme@serviceops.ecosysdigital.co.ke", message.SenderEmail);
        Assert.Equal("Acme", message.SenderName);
        Assert.Equal(string.Empty, (await db.EmailSettings.SingleAsync()).Host);
    }

    /// <summary>Mirrors exactly what the Create Tenant form sends, including the required licensed-users count.</summary>
    private static UpsertPlatformTenantRequest MinimalProvisioningRequest(string plan = "suite", int? maxUsers = 10) => new(
        Name: null, CompanyName: "Acme Engineering Ltd", Slug: null, CompanyEmail: null, CompanyPhone: null,
        Industry: null, ContactName: null, ContactEmail: null, ContactPhone: null,
        PrimaryContactName: null, PrimaryContactEmail: null, PrimaryContactPhone: null,
        Country: "Kenya", County: null, City: null, Address: null, TaxPin: null, PlanName: null,
        MaxUsers: maxUsers, MaxBranches: null, TrialEndsAt: null, SubscriptionEndsAt: null, StartsAt: null, ExpiresAt: null,
        Status: null, LicenseStatus: null, AdminFullName: "John Kamau", AdminEmail: "admin@acme.test", PlanCode: plan);
}
