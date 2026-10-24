using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecosys.Tests;

public sealed partial class AuthEmailSafetyTests
{
    [Fact]
    public async Task TenantAdminPasswordReset_Success_SendsLinkWithoutChangingPassword()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "operator@tenant.test", mustChangePassword: false);
        var originalHash = user.PasswordHash;

        var controller = CreateUsersController(dbContext, tenantId, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true));

        var actionResult = await controller.ResetPassword(user.Id, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<CredentialDeliveryResponse>(ok.Value);
        Assert.True(response.Success);

        var updatedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(originalHash, updatedUser.PasswordHash);
        Assert.False(updatedUser.MustChangePassword);
        Assert.NotNull(updatedUser.LastCredentialSentAt);

        var log = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.RecipientEmail == user.Email);
        Assert.Equal("Sent", log.Status);
        Assert.Equal("password-reset-link", log.TemplateKey);
    }

    [Fact]
    public async Task TenantAdminPasswordReset_Failure_DoesNotChangePasswordAndWritesFailedLog()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "dispatcher@tenant.test", mustChangePassword: false);
        var originalHash = user.PasswordHash;
        var originalMustChangePassword = user.MustChangePassword;

        var controller = CreateUsersController(dbContext, tenantId, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: false));

        var actionResult = await controller.ResetPassword(user.Id, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<CredentialDeliveryResponse>(ok.Value);
        Assert.False(response.Success);
        Assert.Equal("Password reset email could not be sent. The user's password remains unchanged.", response.Message);

        var updatedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(originalHash, updatedUser.PasswordHash);
        Assert.Equal(originalMustChangePassword, updatedUser.MustChangePassword);
        Assert.Null(updatedUser.LastCredentialSentAt);

        var log = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.RecipientEmail == user.Email);
        Assert.Equal("Failed", log.Status);
        Assert.Equal("password-reset-link", log.TemplateKey);
    }

    [Fact]
    public async Task PlatformAdminPasswordReset_Failure_DoesNotChangePassword()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedPlatformUserAsync(dbContext, "platform.user@ecosys.test");
        var originalHash = user.PasswordHash;
        user.MustChangePassword = false;
        await dbContext.SaveChangesAsync();

        var controller = CreatePlatformUsersController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: false));

        var actionResult = await controller.ResetPassword(user.Id, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<PlatformResetPasswordResponse>(ok.Value);
        Assert.False(response.Success);
        Assert.Equal("Password reset email could not be sent.", response.Message);

        var updatedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(originalHash, updatedUser.PasswordHash);
        Assert.Null(updatedUser.LastCredentialSentAt);
    }

    [Fact]
    public async Task TenantCreateUser_EmailFailure_StillCreatesUser()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "Acme", "admin@acme.test");

        var controller = CreateUsersController(dbContext, tenantId, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: false));

        var request = new CreateUserRequest(
            "New User",
            "new.user@acme.test",
            null,
            null,
            AppRoles.User,
            null,
            null,
            true,
            "TemporaryPassword",
            null,
            [],
            null,
            false,
            []);

        var actionResult = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var response = Assert.IsType<UserResponse>(created.Value);
        Assert.Equal("new.user@acme.test", response.Email);
        Assert.False(response.CredentialDelivery?.Success ?? true);

        var createdUser = await dbContext.Users.SingleAsync(x => x.Email == "new.user@acme.test");
        Assert.NotEqual(Guid.Empty, createdUser.Id);

        var log = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.RecipientEmail == createdUser.Email);
        Assert.Equal("Failed", log.Status);
        Assert.Equal("account-setup-link", log.TemplateKey);
    }

    [Theory]
    [InlineData(AppRoles.Technician, true)]
    [InlineData(AppRoles.User, false)]
    public async Task TenantCreateUser_TechnicianRole_GetsTechnicianProfileWithoutPhoneOrGroups(string role, bool expectProfile)
    {
        // Without a technician profile the user never appears in the work order "Assigned To" list.
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "Acme", "admin@acme.test");

        var controller = CreateUsersController(dbContext, tenantId, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true));

        var actionResult = await controller.Create(
            new CreateUserRequest(
                "Grace Muthoni",
                "grace@acme.test",
                null,
                null,
                role,
                null,
                null,
                true,
                "TemporaryPassword",
                null,
                [],
                null,
                true,
                []),
            CancellationToken.None);

        var response = Assert.IsType<UserResponse>(Assert.IsType<CreatedAtActionResult>(actionResult.Result).Value);
        var technician = await dbContext.Technicians.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == response.Id);
        if (expectProfile)
        {
            Assert.NotNull(technician);
            Assert.Equal("Grace Muthoni", technician.FullName);
            Assert.True(technician.IsActive);
        }
        else
        {
            Assert.Null(technician);
        }
    }

    [Fact]
    public async Task TenantCreateUser_SendsAccountSetupLink_InsteadOfPasswordResetTemplate()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "Acme", "admin@acme.test");

        var controller = CreateUsersController(dbContext, tenantId, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true));

        var actionResult = await controller.Create(
            new CreateUserRequest(
                "Invite Pending",
                "invite.pending@acme.test",
                null,
                null,
                AppRoles.User,
                null,
                null,
                true,
                "InviteEmail",
                null,
                [],
                null,
                false,
                []),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var response = Assert.IsType<UserResponse>(created.Value);
        Assert.True(response.CredentialDelivery?.Success);
        Assert.True(response.MustChangePassword);

        var log = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.RecipientEmail == "invite.pending@acme.test");
        Assert.Equal("account-setup-link", log.TemplateKey);
        Assert.Equal("Welcome to Ecosys — Set up your account", log.Subject);
    }

    [Fact]
    public async Task TenantUpdateStatus_ActivatingUser_EnforcesSeatLimit()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "inactive@acme.test", mustChangePassword: false);
        user.IsActive = false;
        await dbContext.SaveChangesAsync();

        var controller = CreateUsersController(
            dbContext,
            tenantId,
            new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true),
            new ThrowingLicenseGuardService());

        var error = await Assert.ThrowsAsync<LicenseException>(() =>
            controller.UpdateStatus(user.Id, new UpdateUserStatusRequest(true), CancellationToken.None));

        Assert.Equal("license_users_limit_reached", error.ErrorCode);

        var storedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        Assert.False(storedUser.IsActive);
    }

    [Fact]
    public async Task PlatformUserCreate_SendsAccountSetupLink()
    {
        await using var dbContext = CreateDbContext();
        await SeedTenantAsync(dbContext, PlatformConstants.RootTenantId, "Ecosys Platform", "support@ecosys.local");
        var controller = CreatePlatformUsersController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true));

        var actionResult = await controller.Create(
            new UpsertPlatformUserRequest("Platform Invite", "platform.invite@ecosys.test", null, "PlatformAdmin", "Active", null),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var response = Assert.IsType<PlatformUserResponse>(created.Value);
        Assert.True(response.CredentialDelivery?.Success);

        var log = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.RecipientEmail == "platform.invite@ecosys.test");
        Assert.Equal("account-setup-link", log.TemplateKey);
        Assert.Equal("Welcome to Ecosys — Set up your account", log.Subject);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsGenericMessage_ForExistingUser_AndStoresHashedToken()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "existing@tenant.test", mustChangePassword: false);
        var service = CreatePasswordResetService(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        var message = await service.RequestAsync(user.Email, CancellationToken.None);

        Assert.Equal("If an account exists for this email, password reset instructions will be sent shortly.", message);

        var token = await dbContext.PasswordResetTokens.SingleAsync(x => x.UserId == user.Id);
        Assert.NotEmpty(token.TokenHash);
        Assert.DoesNotContain("existing", token.TokenHash, StringComparison.OrdinalIgnoreCase);

        var log = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.TemplateKey == "password-reset-link");
        Assert.Equal("Sent", log.Status);
    }

    [Fact]
    public async Task ForgotPassword_ReturnsSameGenericMessage_ForMissingUser()
    {
        await using var dbContext = CreateDbContext();
        var service = CreatePasswordResetService(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        var message = await service.RequestAsync("missing@tenant.test", CancellationToken.None);

        Assert.Equal("If an account exists for this email, password reset instructions will be sent shortly.", message);
        Assert.Empty(await dbContext.PasswordResetTokens.ToListAsync());
    }

    [Fact]
    public async Task ForgotPassword_StopsReportingSuccess_WhenNoMailboxIsConfiguredAnywhere()
    {
        // EC-P1-06d: on a fresh database there is no settings row, and with no Smtp:* configuration
        // either nothing can be delivered. Reporting "instructions will be sent shortly" there is
        // worse than failing — the user waits for mail that will never arrive.
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "existing@tenant.test", mustChangePassword: false);
        var service = CreatePasswordResetService(
            dbContext,
            new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true),
            configuredSmtpHost: string.Empty);

        var failure = await Assert.ThrowsAsync<PasswordResetUnavailableException>(
            () => service.RequestAsync(user.Email, CancellationToken.None));

        Assert.Equal(503, failure.StatusCode);
        Assert.Equal("EMAIL_NOT_CONFIGURED", failure.ErrorCode);
        Assert.Empty(await dbContext.PasswordResetTokens.ToListAsync());
    }

    [Fact]
    public async Task ForgotPassword_UnconfiguredMailbox_AnswersIdenticallyForAnAddressThatDoesNotExist()
    {
        // The readiness check must not become an account enumeration oracle: it runs before the
        // lookup and reads only this server's configuration, so a missing account gets exactly the
        // failure an existing one gets.
        await using var dbContext = CreateDbContext();
        await SeedTenantUserAsync(dbContext, Guid.NewGuid(), "existing@tenant.test", mustChangePassword: false);
        var service = CreatePasswordResetService(
            dbContext,
            new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true),
            configuredSmtpHost: string.Empty);

        var forExisting = await Assert.ThrowsAsync<PasswordResetUnavailableException>(
            () => service.RequestAsync("existing@tenant.test", CancellationToken.None));
        var forMissing = await Assert.ThrowsAsync<PasswordResetUnavailableException>(
            () => service.RequestAsync("missing@tenant.test", CancellationToken.None));

        Assert.Equal(forExisting.StatusCode, forMissing.StatusCode);
        Assert.Equal(forExisting.ErrorCode, forMissing.ErrorCode);
        Assert.Equal(forExisting.Message, forMissing.Message);
    }

    [Fact]
    public async Task ResetPassword_Succeeds_WithValidToken_AndRevokesSessions()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "resetme@tenant.test", mustChangePassword: true);
        dbContext.UserSessions.Add(new UserSession
        {
            TenantId = tenantId,
            UserId = user.Id,
            LoginAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
            IsRevoked = false
        });
        var rawToken = "valid-reset-token";
        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = ToTokenHash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        });
        await dbContext.SaveChangesAsync();

        var originalHash = user.PasswordHash;
        var service = CreatePasswordResetService(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        var message = await service.ResetAsync(rawToken, "NewStrongPass123!", "NewStrongPass123!", CancellationToken.None);

        Assert.Equal("Your password has been reset. You can now sign in.", message);

        var updatedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        Assert.NotEqual(originalHash, updatedUser.PasswordHash);
        Assert.False(updatedUser.MustChangePassword);

        var token = await dbContext.PasswordResetTokens.SingleAsync(x => x.UserId == user.Id);
        Assert.NotNull(token.UsedAt);

        var session = await dbContext.UserSessions.SingleAsync(x => x.UserId == user.Id);
        Assert.True(session.IsRevoked);
    }

    [Fact]
    public async Task ResetPassword_RejectsExpiredOrUsedTokens_AndPasswordMismatch()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "expired@tenant.test", mustChangePassword: true);
        dbContext.PasswordResetTokens.AddRange(
            new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = ToTokenHash("expired-token"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = ToTokenHash("used-token"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                UsedAt = DateTime.UtcNow.AddMinutes(-1)
            });
        await dbContext.SaveChangesAsync();

        var service = CreatePasswordResetService(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        await Assert.ThrowsAsync<Shared.Errors.BusinessRuleException>(() =>
            service.ResetAsync("expired-token", "Mismatch123!", "Different123!", CancellationToken.None));

        await Assert.ThrowsAsync<Shared.Errors.ForbiddenException>(() =>
            service.ResetAsync("expired-token", "NewStrongPass123!", "NewStrongPass123!", CancellationToken.None));

        await Assert.ThrowsAsync<Shared.Errors.ForbiddenException>(() =>
            service.ResetAsync("used-token", "NewStrongPass123!", "NewStrongPass123!", CancellationToken.None));
    }

    [Fact]
    public async Task PlatformResendWorkspaceInvitation_IssuesAFreshLink_AndRetiresTheOldOne()
    {
        // The recovery path for a tenant admin whose first-login invitation lapsed: they cannot sign in
        // to ask for a new one, and the tenant-side resend needs an admin already inside the workspace.
        await using var dbContext = CreateDbContext();
        await SeedPlatformUserAsync(dbContext, "owner@ecosys.test");
        var tenantId = Guid.NewGuid();
        var admin = await SeedTenantUserAsync(dbContext, tenantId, "pending.admin@tenant.test", mustChangePassword: true);
        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = admin.Id,
            TokenHash = ToTokenHash("stale-invitation"),
            Purpose = PasswordLinkPurposes.Invitation,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await dbContext.SaveChangesAsync();

        var controller = CreatePlatformTenantsController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        var actionResult = await controller.ResendWorkspaceUserInvitation(tenantId, admin.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<PlatformTenantCredentialDeliveryResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal("pending.admin@tenant.test", response.Email);
        Assert.NotNull(response.LastCredentialSentAt);

        var stale = await dbContext.PasswordResetTokens.SingleAsync(x => x.TokenHash == ToTokenHash("stale-invitation"));
        Assert.NotNull(stale.SupersededAt);

        var fresh = await dbContext.PasswordResetTokens.SingleAsync(x => x.UserId == admin.Id && !x.UsedAt.HasValue && !x.SupersededAt.HasValue);
        Assert.True(fresh.ExpiresAt > DateTime.UtcNow.AddHours(24), "A resent setup link must use the invitation window, not the short reset window.");

        var log = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.RecipientEmail == "pending.admin@tenant.test");
        Assert.Equal("account-setup-link", log.TemplateKey);
        Assert.Equal("Sent", log.Status);
    }

    [Fact]
    public async Task PlatformResendWorkspaceInvitation_RejectsUsersFromAnotherTenant()
    {
        await using var dbContext = CreateDbContext();
        await SeedPlatformUserAsync(dbContext, "owner2@ecosys.test");
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var outsider = await SeedTenantUserAsync(dbContext, otherTenantId, "outsider@other.test", mustChangePassword: true);
        await SeedTenantAsync(dbContext, tenantId, "Target Workspace", "support@target.test");

        var controller = CreatePlatformTenantsController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.ResendWorkspaceUserInvitation(tenantId, outsider.Id, CancellationToken.None));
    }

    [Fact]
    public async Task PlatformTenantCreate_CreatesInitialTenantWorkspaceAdmin_WithoutCreatingPlatformUser()
    {
        await using var dbContext = CreateDbContext();
        var existingPlatformUser = await SeedPlatformUserAsync(dbContext, "owner@ecosys.test");
        var controller = CreatePlatformTenantsController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));
        var beforeCreate = DateTime.UtcNow;

        var request = new UpsertPlatformTenantRequest(
            Name: "Acme Facilities",
            CompanyName: null,
            Slug: "acme-facilities",
            CompanyEmail: "info@acme.test",
            CompanyPhone: "+254700000001",
            Industry: "Facilities",
            ContactName: null,
            ContactEmail: null,
            ContactPhone: null,
            PrimaryContactName: "Ada Contact",
            PrimaryContactEmail: "ada.contact@acme.test",
            PrimaryContactPhone: "+254700000002",
            Country: "Kenya",
            County: null,
            City: null,
            Address: null,
            TaxPin: null,
            PlanName: null,
            OfficeSeats: 12,
            MaxBranches: 3,
            TrialEndsAt: null,
            SubscriptionEndsAt: null,
            StartsAt: null,
            ExpiresAt: null,
            Status: "Active",
            LicenseStatus: "Trial",
            CreateDefaultAdmin: false,
            UsePrimaryContactAsWorkspaceAdmin: false,
            AdminFullName: "Grace Admin",
            AdminEmail: "grace.admin@acme.test",
            AdminPhone: "+254700000003");

        var actionResult = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var response = Assert.IsType<PlatformTenantDetailResponse>(created.Value);
        Assert.True(response.InitialAdminInvitationSent);

        var tenant = await dbContext.Tenants.SingleAsync(x => x.Id == response.TenantId);
        Assert.Equal("info@acme.test", tenant.Email);
        Assert.Equal("+254700000001", tenant.Phone);
        Assert.Equal("Facilities", tenant.Industry);
        Assert.Equal("Trial", tenant.Status);
        Assert.Equal("Trial", tenant.LicenseStatus);
        Assert.False(tenant.TrialExtensionUsed);
        Assert.NotNull(tenant.TrialStartsAt);
        Assert.NotNull(tenant.TrialEndsAt);
        Assert.InRange((tenant.TrialStartsAt!.Value - beforeCreate).TotalMinutes, -1, 1);
        Assert.Equal(14, (tenant.TrialEndsAt!.Value - tenant.TrialStartsAt.Value).Days);
        Assert.Equal("TrialActive", response.TrialStatus);

        var tenantAdmin = await dbContext.Users.SingleAsync(x => x.TenantId == tenant.Id);
        Assert.Equal(AppRoles.Admin, tenantAdmin.Role);
        Assert.Equal("grace.admin@acme.test", tenantAdmin.Email);
        Assert.Equal("+254700000003", tenantAdmin.Phone);
        Assert.True(tenantAdmin.MustChangePassword);

        var platformUserCount = await dbContext.Users.CountAsync(x => x.TenantId == PlatformConstants.RootTenantId);
        Assert.Equal(1, platformUserCount);
        Assert.Equal(existingPlatformUser.Id, await dbContext.Users.Where(x => x.TenantId == PlatformConstants.RootTenantId).Select(x => x.Id).SingleAsync());

        var resetLog = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.TemplateKey == "account-setup-link" && x.RecipientEmail == "grace.admin@acme.test");
        Assert.Equal("Sent", resetLog.Status);

        var onboarding = await dbContext.EmailOutboxMessages.SingleAsync(x => x.TemplateKey == "tenant-onboarding" && x.RecipientEmail == "grace.admin@acme.test");
        Assert.Equal("Pending", onboarding.Status);
    }

    [Fact]
    public async Task PlatformTenantCreate_SetsAutomaticFourteenDayTrial()
    {
        await using var dbContext = CreateDbContext();
        await SeedPlatformUserAsync(dbContext, "owner2@ecosys.test");
        var controller = CreatePlatformTenantsController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        var actionResult = await controller.Create(
            new UpsertPlatformTenantRequest(
                Name: "Trial First Tenant",
                CompanyName: null,
                Slug: "trial-first-tenant",
                CompanyEmail: "hello@trial-first.test",
                CompanyPhone: "+254700100001",
                Industry: "Services",
                ContactName: null,
                ContactEmail: null,
                ContactPhone: null,
                PrimaryContactName: "Trial Owner",
                PrimaryContactEmail: "trial.owner@trial-first.test",
                PrimaryContactPhone: "+254700100002",
                Country: "Kenya",
                County: null,
                City: null,
                Address: null,
                TaxPin: null,
                PlanName: null,
                OfficeSeats: 8,
                MaxBranches: 2,
                TrialEndsAt: DateTime.UtcNow.AddDays(60),
                SubscriptionEndsAt: null,
                StartsAt: null,
                ExpiresAt: null,
                Status: "Active",
                LicenseStatus: "Active",
                CreateDefaultAdmin: false,
                UsePrimaryContactAsWorkspaceAdmin: true,
                AdminFullName: null,
                AdminEmail: null,
                AdminPhone: null),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var response = Assert.IsType<PlatformTenantDetailResponse>(created.Value);
        Assert.Equal("Trial", response.Status);
        Assert.Equal("Trial", response.LicenseStatus);
        Assert.False(response.TrialExtensionUsed);
        Assert.NotNull(response.TrialStartsAt);
        Assert.NotNull(response.TrialEndsAt);
        Assert.Equal(14, (response.TrialEndsAt!.Value - response.TrialStartsAt!.Value).Days);
        Assert.Equal("TrialActive", response.TrialStatus);
        Assert.InRange(response.TrialDaysRemaining ?? 0, 13, 14);
    }

    [Fact]
    public async Task PlatformTenantExtendTrial_AddsFourteenDays_AndCannotBeUsedTwice()
    {
        await using var dbContext = CreateDbContext();
        await SeedPlatformUserAsync(dbContext, "owner3@ecosys.test");
        var controller = CreatePlatformTenantsController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        var createResult = await controller.Create(
            new UpsertPlatformTenantRequest(
                Name: "Extendable Tenant",
                CompanyName: null,
                Slug: "extendable-tenant",
                CompanyEmail: "hello@extendable.test",
                CompanyPhone: "+254700200001",
                Industry: "Logistics",
                ContactName: null,
                ContactEmail: null,
                ContactPhone: null,
                PrimaryContactName: "Extension Owner",
                PrimaryContactEmail: "owner@extendable.test",
                PrimaryContactPhone: "+254700200002",
                Country: "Kenya",
                County: null,
                City: null,
                Address: null,
                TaxPin: null,
                PlanName: null,
                OfficeSeats: 10,
                MaxBranches: 4,
                TrialEndsAt: null,
                SubscriptionEndsAt: null,
                StartsAt: null,
                ExpiresAt: null,
                Status: "Trial",
                LicenseStatus: "Trial",
                CreateDefaultAdmin: false,
                UsePrimaryContactAsWorkspaceAdmin: true,
                AdminFullName: null,
                AdminEmail: null,
                AdminPhone: null),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        var tenant = Assert.IsType<PlatformTenantDetailResponse>(created.Value);

        var extendResult = await controller.ExtendTrial(tenant.TenantId, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(extendResult.Result);
        var extended = Assert.IsType<PlatformTenantDetailResponse>(ok.Value);

        Assert.True(extended.TrialExtensionUsed);
        Assert.NotNull(extended.TrialExtendedAt);
        Assert.Equal(28, (extended.TrialEndsAt!.Value - extended.TrialStartsAt!.Value).Days);
        Assert.Equal("TrialExtended", extended.TrialStatus);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => controller.ExtendTrial(tenant.TenantId, CancellationToken.None));
        Assert.Equal("This tenant has already used the trial extension.", error.Message);
    }

    [Fact]
    public async Task PlatformTenantCreate_UsePrimaryContactAsWorkspaceAdmin_CopiesPrimaryContactDetails()
    {
        await using var dbContext = CreateDbContext();
        await SeedTenantAsync(dbContext, PlatformConstants.RootTenantId, "Ecosys Platform", "support@ecosys.local");
        var controller = CreatePlatformTenantsController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        var request = new UpsertPlatformTenantRequest(
            Name: "Northwind ServiceOps",
            CompanyName: null,
            Slug: "northwind-serviceops",
            CompanyEmail: "hello@northwind.test",
            CompanyPhone: "+254700000010",
            Industry: "Manufacturing",
            ContactName: null,
            ContactEmail: null,
            ContactPhone: null,
            PrimaryContactName: "Nia Contact",
            PrimaryContactEmail: "nia.contact@northwind.test",
            PrimaryContactPhone: "+254700000011",
            Country: "Kenya",
            County: null,
            City: null,
            Address: null,
            TaxPin: null,
            PlanName: null,
            OfficeSeats: 10,
            MaxBranches: 2,
            TrialEndsAt: DateTime.UtcNow.Date.AddDays(7),
            SubscriptionEndsAt: null,
            StartsAt: null,
            ExpiresAt: null,
            Status: "Active",
            LicenseStatus: "Trial",
            CreateDefaultAdmin: false,
            UsePrimaryContactAsWorkspaceAdmin: true,
            AdminFullName: null,
            AdminEmail: null,
            AdminPhone: null);

        var actionResult = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var response = Assert.IsType<PlatformTenantDetailResponse>(created.Value);
        Assert.True(response.InitialAdminInvitationSent);

        var tenantAdmin = await dbContext.Users.SingleAsync(x => x.TenantId == response.TenantId);
        Assert.Equal("Nia Contact", tenantAdmin.FullName);
        Assert.Equal("nia.contact@northwind.test", tenantAdmin.Email);
        Assert.Equal("+254700000011", tenantAdmin.Phone);
    }

    [Fact]
    public void PlatformFinanceSummary_UsesPublishedRoute_AndAuthorizationPolicy()
    {
        var controllerRoute = typeof(PlatformFinanceController).GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .OfType<RouteAttribute>()
            .Single();
        Assert.Equal("api/platform/finance", controllerRoute.Template);

        var authorize = typeof(PlatformFinanceController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .OfType<AuthorizeAttribute>()
            .Single();
        Assert.Equal("PlatformFinanceAccess", authorize.Policy);

        var summaryAction = typeof(PlatformFinanceController).GetMethod(nameof(PlatformFinanceController.GetSummary));
        Assert.NotNull(summaryAction);

        var summaryRoute = summaryAction!
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .OfType<HttpGetAttribute>()
            .Single();
        Assert.Equal("summary", summaryRoute.Template);
    }

    [Fact]
    public async Task PlatformFinanceSummary_EmptyDatabase_ReturnsZeroState()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreatePlatformFinanceController(dbContext);

        var actionResult = await controller.GetSummary(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<PlatformFinanceDashboardResponse>(ok.Value);
        Assert.Equal(0m, response.TotalRevenue);
        Assert.Equal(0m, response.OutstandingInvoices);
        Assert.Equal(0m, response.OverdueInvoices);
        Assert.Equal(0m, response.ExpensesThisMonth);
        Assert.Equal(0m, response.ProfitEstimate);
        Assert.Equal(0m, response.QuotationConversionRate);
        Assert.Empty(response.RecentPayments);
        Assert.Empty(response.RecentInvoices);
        Assert.Empty(response.OverdueAccounts);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorizedException()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantUserAsync(dbContext, tenantId, "login.user@tenant.test", mustChangePassword: false);
        var service = CreateAuthService(dbContext);

        var error = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync("login.user@tenant.test", "WrongPassword123!", cancellationToken: CancellationToken.None));

        Assert.Equal("Invalid email or password.", error.Message);
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public async Task Login_FailedAttempt_IncrementsFailedLoginCount()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "inactive.user@tenant.test", mustChangePassword: false);
        var service = CreateAuthService(dbContext);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync("inactive.user@tenant.test", "WrongPassword123!", cancellationToken: CancellationToken.None));

        var refreshedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(1, refreshedUser.FailedLoginCount);
        Assert.NotNull(refreshedUser.LastFailedLoginAt);
        Assert.Null(refreshedUser.LockedOutUntil);
    }

    [Fact]
    public async Task Login_AccountLocksAfterConfiguredThreshold()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "lockout.user@tenant.test", mustChangePassword: false);
        var service = CreateAuthService(dbContext);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                service.LoginAsync("lockout.user@tenant.test", "WrongPassword123!", cancellationToken: CancellationToken.None));
        }

        var refreshedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(5, refreshedUser.FailedLoginCount);
        Assert.NotNull(refreshedUser.LastFailedLoginAt);
        Assert.NotNull(refreshedUser.LockedOutUntil);
        Assert.True(refreshedUser.LockedOutUntil > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_Success_ResetsFailedLoginState()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "reset.login@tenant.test", mustChangePassword: false);
        user.FailedLoginCount = 3;
        user.LastFailedLoginAt = DateTime.UtcNow.AddMinutes(-2);
        user.LockedOutUntil = DateTime.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();
        var service = CreateAuthService(dbContext);

        var result = await service.LoginAsync("reset.login@tenant.test", "OriginalPass123!", cancellationToken: CancellationToken.None);

        Assert.NotEmpty(result.Token);

        var refreshedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(0, refreshedUser.FailedLoginCount);
        Assert.Null(refreshedUser.LastFailedLoginAt);
        Assert.Null(refreshedUser.LockedOutUntil);
    }

    [Fact]
    public async Task Login_Response_DoesNotRevealWhetherEmailExists()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantUserAsync(dbContext, tenantId, "known.user@tenant.test", mustChangePassword: false);
        var service = CreateAuthService(dbContext);

        var wrongPasswordError = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync("known.user@tenant.test", "WrongPassword123!", cancellationToken: CancellationToken.None));
        var unknownUserError = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync("missing.user@tenant.test", "WrongPassword123!", cancellationToken: CancellationToken.None));

        Assert.Equal("Invalid email or password.", wrongPasswordError.Message);
        Assert.Equal(wrongPasswordError.Message, unknownUserError.Message);
    }

    [Fact]
    public async Task LoginResponse_AndMePayload_IncludeMustChangePassword()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "first.login@tenant.test", mustChangePassword: true);
        var controller = CreateAuthController(dbContext, tenantId, user.Id, CreateAuthService(dbContext));

        var loginResult = await controller.Login(new LoginRequest(user.Email, "OriginalPass123!"), CancellationToken.None);
        var loginOk = Assert.IsType<OkObjectResult>(loginResult.Result);
        var loginResponse = Assert.IsType<LoginResponse>(loginOk.Value);
        Assert.True(loginResponse.User.MustChangePassword);

        var meResult = await controller.GetCurrentUser(CancellationToken.None);
        var meOk = Assert.IsType<OkObjectResult>(meResult.Result);
        var meResponse = Assert.IsType<AuthenticatedContextResponse>(meOk.Value);
        Assert.True(meResponse.User.MustChangePassword);
    }

    [Fact]
    public async Task ChangePassword_Succeeds_WithCorrectCurrentPassword_AndClearsMustChangePassword()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "change.me@tenant.test", mustChangePassword: true);
        var originalHash = user.PasswordHash;
        var currentSessionId = Guid.NewGuid();
        dbContext.UserSessions.AddRange(
            new UserSession
            {
                Id = currentSessionId,
                TenantId = tenantId,
                UserId = user.Id,
                LoginAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IsRevoked = false
            },
            new UserSession
            {
                TenantId = tenantId,
                UserId = user.Id,
                LoginAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
                IsRevoked = false
            });
        await dbContext.SaveChangesAsync();

        var controller = CreateAuthController(dbContext, tenantId, user.Id, CreateAuthService(dbContext), currentSessionId);

        var actionResult = await controller.ChangePassword(
            new ChangePasswordRequest("OriginalPass123!", "NewStrongPass123!", "NewStrongPass123!"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<MessageResponse>(ok.Value);
        Assert.Equal("Your password has been changed successfully.", response.Message);

        var updatedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        Assert.False(updatedUser.MustChangePassword);
        Assert.NotEqual(originalHash, updatedUser.PasswordHash);

        var sessions = await dbContext.UserSessions.Where(x => x.UserId == user.Id).ToListAsync();
        Assert.False(sessions.Single(x => x.Id == currentSessionId).IsRevoked);
        Assert.True(sessions.Single(x => x.Id != currentSessionId).IsRevoked);
    }

    [Fact]
    public async Task ChangePassword_Fails_WithWrongCurrentPassword()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "wrong.current@tenant.test", mustChangePassword: true);
        var controller = CreateAuthController(dbContext, tenantId, user.Id, CreateAuthService(dbContext));

        var error = await Assert.ThrowsAsync<Shared.Errors.BusinessRuleException>(async () =>
            await controller.ChangePassword(
                new ChangePasswordRequest("WrongPassword123!", "NewStrongPass123!", "NewStrongPass123!"),
                CancellationToken.None));

        Assert.Equal("The current password is incorrect.", error.Message);
    }

    [Fact]
    public async Task ChangePassword_RejectsWeakPassword()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "weak.password@tenant.test", mustChangePassword: true);
        var controller = CreateAuthController(dbContext, tenantId, user.Id, CreateAuthService(dbContext));

        var error = await Assert.ThrowsAsync<Shared.Errors.BusinessRuleException>(async () =>
            await controller.ChangePassword(
                new ChangePasswordRequest("OriginalPass123!", "weak", "weak"),
                CancellationToken.None));

        Assert.Contains("Password", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlatformCanCreateAdditionalWorkspaceAdmins_ForOneTenant()
    {
        // Tenant creation seeds exactly one admin; a workspace usually needs more than one.
        await using var dbContext = CreateDbContext();
        await SeedPlatformUserAsync(dbContext, "owner3@ecosys.test");
        var tenantId = Guid.NewGuid();
        var firstAdmin = await SeedTenantUserAsync(dbContext, tenantId, "first.admin@tenant.test", mustChangePassword: false);
        var controller = CreatePlatformTenantsController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        var actionResult = await controller.CreateWorkspaceUser(
            tenantId,
            new CreatePlatformTenantUserRequest("Second Admin", "second.admin@tenant.test", "+254700000009", AppRoles.Admin, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<PlatformTenantUserCreatedResponse>(ok.Value);
        Assert.True(response.InvitationSent);
        Assert.Equal(AppRoles.Admin, response.User.Role);

        var admins = await dbContext.Users
            .Where(x => x.TenantId == tenantId && x.Role == AppRoles.Admin)
            .ToListAsync();
        Assert.Equal(2, admins.Count);
        Assert.Contains(admins, x => x.Id == firstAdmin.Id);

        var created = admins.Single(x => x.Email == "second.admin@tenant.test");
        Assert.True(created.MustChangePassword);
        Assert.True(created.HasAllBranchAccess);
        Assert.NotNull(created.LastCredentialSentAt);

        var log = await dbContext.EmailDeliveryLogs.SingleAsync(x => x.RecipientEmail == "second.admin@tenant.test");
        Assert.Equal("account-setup-link", log.TemplateKey);
    }

    [Fact]
    public async Task PlatformCannotPlaceAPlatformRoleInsideATenantWorkspace()
    {
        await using var dbContext = CreateDbContext();
        await SeedPlatformUserAsync(dbContext, "owner4@ecosys.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantUserAsync(dbContext, tenantId, "existing@tenant.test", mustChangePassword: false);
        var controller = CreatePlatformTenantsController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.CreateWorkspaceUser(
            tenantId,
            new CreatePlatformTenantUserRequest("Sneaky Staff", "staff@tenant.test", null, AppRoles.PlatformAdmin, null),
            CancellationToken.None));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.CreateWorkspaceUser(
            tenantId,
            new CreatePlatformTenantUserRequest("Sneaky Root", "root@tenant.test", null, AppRoles.SuperAdmin, null),
            CancellationToken.None));

        Assert.Empty(await dbContext.Users.Where(x => x.Email == "staff@tenant.test" || x.Email == "root@tenant.test").ToListAsync());
    }

    [Fact]
    public async Task PlatformCannotReuseAnEmailAlreadyTakenAnywhere()
    {
        await using var dbContext = CreateDbContext();
        await SeedPlatformUserAsync(dbContext, "taken@ecosys.test");
        var tenantId = Guid.NewGuid();
        await SeedTenantUserAsync(dbContext, tenantId, "existing2@tenant.test", mustChangePassword: false);
        var controller = CreatePlatformTenantsController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true));

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.CreateWorkspaceUser(
            tenantId,
            new CreatePlatformTenantUserRequest("Clashing Admin", "taken@ecosys.test", null, AppRoles.Admin, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task PlatformUserListing_ExcludesRootTenantAccountsHoldingTenantRoles()
    {
        // Platform staff is an allowlist of platform roles. A root-tenant account carrying a tenant
        // role is not staff and must not be administrable from the platform users screen.
        await using var dbContext = CreateDbContext();
        await SeedPlatformUserAsync(dbContext, "staff@ecosys.test");
        var strayUser = new User
        {
            TenantId = PlatformConstants.RootTenantId,
            FullName = "Stray Tenant Role",
            Email = "stray@ecosys.test",
            Role = AppRoles.Manager,
            IsActive = true
        };
        strayUser.PasswordHash = new PasswordHasher<User>().HashPassword(strayUser, "OriginalPass123!");
        dbContext.Users.Add(strayUser);
        await dbContext.SaveChangesAsync();

        var controller = CreatePlatformUsersController(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true));

        var result = Assert.IsType<OkObjectResult>((await controller.GetAll(CancellationToken.None)).Result);
        var users = Assert.IsType<List<PlatformUserResponse>>(result.Value);

        Assert.Contains(users, x => x.Email == "staff@ecosys.test");
        Assert.DoesNotContain(users, x => x.Email == "stray@ecosys.test");
    }

    private static UsersController CreateUsersController(AppDbContext dbContext, Guid tenantId, FakeUserCredentialDeliveryService deliveryService)
    {
        return CreateUsersController(dbContext, tenantId, deliveryService, new NoOpLicenseGuardService(dbContext));
    }

    private static UsersController CreateUsersController(AppDbContext dbContext, Guid tenantId, FakeUserCredentialDeliveryService deliveryService, ILicenseGuardService licenseGuardService)
    {
        return new UsersController(
            dbContext,
            new FakeTenantContext(tenantId, Guid.NewGuid(), isAdmin: true),
            new NoOpUserAccessService(),
            new FakeUserPermissionTemplateService(),
            new PasswordHasher<User>(),
            licenseGuardService,
            new FakeTenantSecurityPolicyService(),
            new NoOpAuditLogService(),
            deliveryService,
            new TemporaryPasswordService(),
            Options.Create(new AuthSecurityOptions()),
            new FakeUserSessionService(dbContext));
    }

    private static AuthController CreateAuthController(AppDbContext dbContext, Guid tenantId, Guid userId, MvpAuthService authService, Guid? sessionId = null)
    {
        return new AuthController(
            authService,
            new FakeTenantContext(tenantId, userId, isAdmin: true, sessionId: sessionId),
            new FakeUserSessionService(dbContext),
            dbContext,
            new NoOpAuditLogService(),
            CreatePasswordResetService(dbContext, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true, resetSendSucceeds: true)),
            new PasswordHasher<User>(),
            new FakeTenantSecurityPolicyService(),
            TestTimeZones.Create(dbContext));
    }

    private static PlatformUsersController CreatePlatformUsersController(AppDbContext dbContext, FakeUserCredentialDeliveryService deliveryService)
    {
        return new PlatformUsersController(
            dbContext,
            new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true),
            new NoOpAuditLogService(),
            new PasswordHasher<User>(),
            deliveryService,
            new TemporaryPasswordService(),
            Options.Create(new AuthSecurityOptions()));
    }

    private static PasswordResetService CreatePasswordResetService(
        AppDbContext dbContext,
        FakeUserCredentialDeliveryService deliveryService,
        string configuredSmtpHost = "smtp.test")
    {
        return new PasswordResetService(
            dbContext,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new PasswordHasher<User>(),
            new FakeTenantSecurityPolicyService(),
            new FakeUserSessionService(dbContext),
            deliveryService,
            new NoOpAuditLogService(),
            new EmailSettingsResolver(dbContext),
            Options.Create(new AuthSecurityOptions()),
            Options.Create(new SmtpOptions { Host = configuredSmtpHost }),
            NullLogger<PasswordResetService>.Instance);
    }

    private static PlatformTenantsController CreatePlatformTenantsController(AppDbContext dbContext, FakeUserCredentialDeliveryService deliveryService)
    {
        var controller = new PlatformTenantsController(
            dbContext,
            new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true),
            new NoOpLicenseGuardService(dbContext),
            new NoOpAuditLogService(),
            new FakeEmailTemplateService(),
            new FakeEmailSubjectRuleService(),
            new EmailOutboxService(dbContext, new EmailDeliveryLogService(dbContext)),
            new PasswordHasher<User>(),
            new FakeUserPermissionTemplateService(),
            deliveryService,
            Options.Create(new AuthSecurityOptions()),
            TestTimeZones.Create(dbContext),
            CreateAuthService(dbContext));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.ControllerContext.HttpContext.Request.Scheme = "https";
        controller.ControllerContext.HttpContext.Request.Host = new HostString("app.ecosys.test");
        controller.ControllerContext.HttpContext.Request.Headers.Origin = "https://app.ecosys.test";

        return controller;
    }

    private static PlatformFinanceController CreatePlatformFinanceController(AppDbContext dbContext)
    {
        return new PlatformFinanceController(
            dbContext,
            new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true),
            new NoOpAuditLogService(),
            new PlatformBillingLifecycleService(
                dbContext,
                new FakeEmailTemplateService(),
                new FakeEmailSubjectRuleService(),
                new EmailOutboxService(dbContext, new EmailDeliveryLogService(dbContext))));
    }

    private static MvpAuthService CreateAuthService(AppDbContext dbContext)
    {
        return new MvpAuthService(
            dbContext,
            new PasswordHasher<User>(),
            Options.Create(new JwtOptions
            {
                Issuer = "ecosys-tests",
                Audience = "ecosys-tests",
                SigningKey = "12345678901234567890123456789012",
                ExpiryMinutes = 60
            }),
            Options.Create(new AuthSecurityOptions
            {
                FailedLoginThreshold = 5,
                LockoutMinutes = 15,
                RateLimitPermitLimit = 20,
                RateLimitWindowSeconds = 60,
                RateLimitQueueLimit = 0
            }),
            new NoOpAuditLogService(),
            new FakeUserPermissionTemplateService(),
            new FakeRolePermissionService(),
            new FakeDynamicAccessService(),
            new FakeUserSessionService(dbContext),
            new NoOpLicenseGuardService(dbContext),
            new FakeTenantSecurityPolicyService());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-tests-{Guid.NewGuid():N}")
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task SeedTenantAsync(AppDbContext dbContext, Guid tenantId, string companyName, string email)
    {
        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = companyName,
            Slug = companyName.ToLowerInvariant(),
            CompanyName = companyName,
            Email = email,
            ContactEmail = email,
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        });

        dbContext.EmailSettings.Add(new EmailSetting
        {
            TenantId = tenantId,
            IsEnabled = true,
            Provider = EmailDeliveryMode.Smtp.ToString(),
            Host = "smtp.local.test",
            Port = 587,
            SenderName = companyName,
            SenderAddress = email,
            ReplyToEmail = email
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task<User> SeedTenantUserAsync(AppDbContext dbContext, Guid tenantId, string email, bool mustChangePassword)
    {
        await SeedTenantAsync(dbContext, tenantId, "Tenant Workspace", "support@tenant.test");
        var user = new User
        {
            TenantId = tenantId,
            FullName = "Tenant User",
            Email = email,
            Role = AppRoles.Admin,
            IsActive = true,
            MustChangePassword = mustChangePassword
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "OriginalPass123!");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private static async Task<User> SeedPlatformUserAsync(AppDbContext dbContext, string email)
    {
        await SeedTenantAsync(dbContext, PlatformConstants.RootTenantId, "Ecosys Platform", "superadmin@ecosys.local");
        var user = new User
        {
            TenantId = PlatformConstants.RootTenantId,
            FullName = "Platform User",
            Email = email,
            Role = AppRoles.PlatformAdmin,
            IsActive = true,
            MustChangePassword = false,
            HasAllBranchAccess = true
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "OriginalPass123!");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private static string ToTokenHash(string rawToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public FakeTenantContext(Guid tenantId, Guid userId, bool isAdmin, bool isSuperAdmin = false, Guid? sessionId = null)
        {
            TenantId = tenantId;
            UserId = userId;
            SessionId = sessionId ?? Guid.NewGuid();
            IsAdmin = isAdmin;
            IsSuperAdmin = isSuperAdmin;
        }

        public Guid? TenantId { get; }
        public Guid? UserId { get; }
        public Guid? SessionId { get; }
        public string? Email => "tester@ecosys.local";
        public string? Role => IsSuperAdmin ? AppRoles.SuperAdmin : AppRoles.Admin;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin { get; }
        public bool IsAdmin { get; }
        public bool HasRole(string role) => string.Equals(Role, role, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => TenantId!.Value;
        public Guid GetRequiredUserId() => UserId!.Value;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }

    private sealed class FakeTenantSecurityPolicyService : ITenantSecurityPolicyService
    {
        public Task<TenantSecurityPolicy> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantSecurityPolicy { TenantId = tenantId });

        public Task ValidatePasswordAsync(Guid tenantId, string password, CancellationToken cancellationToken = default)
        {
            ValidatePassword(password, new TenantSecurityPolicy { TenantId = tenantId });
            return Task.CompletedTask;
        }

        public void ValidatePassword(string password, TenantSecurityPolicy policy)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new Shared.Errors.BusinessRuleException("Password is required.");
            }

            if (password.Length < 12)
            {
                throw new Shared.Errors.BusinessRuleException("Password must be at least 12 characters long.");
            }

            if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
            {
                throw new Shared.Errors.BusinessRuleException("Password must contain uppercase, lowercase, number, and special character.");
            }
        }
    }

    private sealed class FakeUserSessionService(AppDbContext dbContext) : IUserSessionService
    {
        public UserSession StartSession(User user, string? jwtId)
        {
            var session = new UserSession
            {
                TenantId = user.TenantId,
                UserId = user.Id,
                JwtId = jwtId,
                LoginAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };
            dbContext.UserSessions.Add(session);
            return session;
        }

        public Task<bool> TouchAsync(Guid sessionId, TimeSpan minimumInterval, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<UserSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            dbContext.UserSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        public async Task<int> RevokeAllForUserAsync(Guid userId, string? reason = null, CancellationToken cancellationToken = default)
        {
            var sessions = await dbContext.UserSessions.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
            foreach (var session in sessions)
            {
                session.IsRevoked = true;
                session.RevokedAt = DateTime.UtcNow;
                session.LogoutAt = session.RevokedAt;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return sessions.Count;
        }

        public async Task<int> RevokeOthersForUserAsync(Guid userId, Guid currentSessionId, string? reason = null, CancellationToken cancellationToken = default)
        {
            var sessions = await dbContext.UserSessions
                .Where(x => x.UserId == userId && x.Id != currentSessionId)
                .ToListAsync(cancellationToken);

            foreach (var session in sessions)
            {
                session.IsRevoked = true;
                session.RevokedAt = DateTime.UtcNow;
                session.LogoutAt = session.RevokedAt;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return sessions.Count;
        }

        public async Task<int> RevokeSessionsForRolesAsync(Guid tenantId, IReadOnlyCollection<string> roles, string? reason = null, CancellationToken cancellationToken = default)
        {
            if (roles.Count == 0)
            {
                return 0;
            }

            var normalized = roles.Select(r => r.ToLower()).ToArray();
            var userIds = await dbContext.Users
                .Where(x => x.TenantId == tenantId && normalized.Contains(x.Role.ToLower()))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            return await RevokeSessionsForUsersAsync(userIds, reason, cancellationToken);
        }

        public async Task<int> RevokeSessionsForUsersAsync(IReadOnlyCollection<Guid> userIds, string? reason = null, CancellationToken cancellationToken = default)
        {
            if (userIds.Count == 0)
            {
                return 0;
            }

            var sessions = await dbContext.UserSessions
                .Where(x => userIds.Contains(x.UserId) && !x.IsRevoked)
                .ToListAsync(cancellationToken);
            foreach (var session in sessions)
            {
                session.IsRevoked = true;
                session.RevokedAt = DateTime.UtcNow;
                session.LogoutAt = session.RevokedAt;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return sessions.Count;
        }
    }

    private sealed class NoOpUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private sealed class FakeDynamicAccessService : IDynamicAccessService
    {
        public Task<IReadOnlyList<EffectiveCapability>> GetEffectiveAccessAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EffectiveCapability>>([]);
        public Task<UserPermissionsModel> ResolveLegacyClaimsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(new UserPermissionsModel());
    }

    private sealed class FakeRolePermissionService : IRolePermissionService
    {
        public Task<IReadOnlyList<RolePermissionEntry>> GetMatrixAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RolePermissionEntry>>([]);

        public Task SaveMatrixAsync(Guid tenantId, IReadOnlyDictionary<string, UserPermissionsModel> updates, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<UserPermissionsModel> ResolveAsync(Guid tenantId, string role, CancellationToken cancellationToken = default) =>
            Task.FromResult(new FakeUserPermissionTemplateService().GetDefaultPermissions(role, null));
    }

    private sealed class FakeUserPermissionTemplateService : IUserPermissionTemplateService
    {
        public UserPermissionsModel GetDefaultPermissions(string role, string? jobTitle) => new UserPermissionsModel
        {
            CanViewWorkOrders = true,
            CanCreateWorkOrders = true,
            CanAssignWorkOrders = true,
            CanCompleteWorkOrders = true,
            CanViewKnowledge = true,
            CanManageKnowledge = true,
            CanViewMaterials = true,
            CanRequestMaterials = true,
            CanManageMaterials = true,
            CanApproveMaterials = true,
            CanIssueMaterials = true,
            CanManageAssets = true,
            CanManageSettings = true,
            CanViewReports = true,
            CanViewFinance = true,
            CanManageFinance = true,
            CanViewFinanceRates = true,
            CanManageFinanceRates = true,
            CanViewFinanceApprovals = true,
            CanManageFinanceApprovals = true,
            CanViewBillableWork = true,
            CanManageBillableWork = true,
            CanOverrideMaterialStock = true
        };
        public UserPermissionsModel GetFullTenantPermissions() => GetDefaultPermissions(string.Empty, null);
        public UserPermissionsModel GetSuperAdminPermissions() => new UserPermissionsModel();
        public UserPermissionsModel Merge(string role, string? jobTitle, UserPermissionsModel? customPermissions) => customPermissions ?? GetFullTenantPermissions();
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
            if (existing is not null)
            {
                return existing;
            }

            var license = new TenantLicense
            {
                TenantId = tenantId,
                LicensePlanId = Guid.NewGuid(),
                Status = "Trial",
                StartsAt = DateTime.UtcNow
            };

            dbContext.TenantLicenses.Add(license);
            await dbContext.SaveChangesAsync(cancellationToken);
            return license;
        }
    }

    private sealed class ThrowingLicenseGuardService : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromException(new LicenseException("The current plan limit for users has been reached.", "license_users_limit_reached", 409));
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeUserCredentialDeliveryService(AppDbContext dbContext, bool credentialSendSucceeds, bool? resetSendSucceeds = null) : IUserCredentialDeliveryService
    {
        public string BuildLoginUrl() => "https://app.ecosysdigital.co.ke/login";
        public string BuildInviteUrl(string token) => $"https://app.ecosysdigital.co.ke/reset-password?token={token}&mode=invite";
        public string BuildResetPasswordUrl(string token) => $"https://app.ecosysdigital.co.ke/reset-password?token={token}";

        public async Task<UserCredentialDeliveryResult> SendAsync(Guid tenantId, User user, UserCredentialDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            var success = credentialSendSucceeds;
            dbContext.EmailDeliveryLogs.Add(new EmailDeliveryLog
            {
                TenantId = tenantId,
                EventKey = request.TemplateEventKey,
                TemplateKey = request.TemplateEventKey,
                RecipientEmail = user.Email,
                Subject = $"{request.TemplateEventKey} subject",
                Status = success ? "Sent" : "Failed",
                ErrorMessage = success ? null : "SMTP failure",
                SentAt = success ? DateTime.UtcNow : null
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return success
                ? new UserCredentialDeliveryResult(true, "Sent", "User credentials sent.")
                : new UserCredentialDeliveryResult(false, "Failed", "Credential email could not be sent.");
        }

        public async Task<UserCredentialDeliveryResult> SendPasswordResetLinkAsync(Guid tenantId, User user, PasswordResetLinkDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            var success = resetSendSucceeds ?? credentialSendSucceeds;
            var isAccountSetup = request.Purpose == PasswordLinkDeliveryPurpose.AccountSetup;
            dbContext.EmailDeliveryLogs.Add(new EmailDeliveryLog
            {
                TenantId = tenantId,
                EventKey = isAccountSetup ? "users.invited" : "auth.password-reset.requested",
                TemplateKey = isAccountSetup ? "account-setup-link" : "password-reset-link",
                RecipientEmail = user.Email,
                Subject = isAccountSetup ? "Welcome to Ecosys — Set up your account" : "Reset your Ecosys password",
                Status = success ? "Sent" : "Failed",
                ErrorMessage = success ? null : "SMTP failure",
                SentAt = success ? DateTime.UtcNow : null
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return success
                ? new UserCredentialDeliveryResult(true, "Sent", isAccountSetup ? "Account setup email sent." : "Password reset email sent.")
                : new UserCredentialDeliveryResult(false, "Failed", isAccountSetup ? "Account setup email could not be sent." : "Password reset email could not be sent.");
        }
    }

    private sealed class FakeEmailTemplateService : IEmailTemplateService
    {
        public Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListPlatformTemplatesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListTenantTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> GetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> GetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> SavePlatformTemplateAsync(string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> SaveTenantTemplateAsync(Guid tenantId, string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> ResetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> ResetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RenderedEmailTemplate> RenderPlatformTemplateAsync(string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenderedEmailTemplate(
                eventKey,
                "Tenant onboarding",
                "Workspace ready",
                "<p>Workspace ready</p>",
                "Workspace ready",
                null,
                null,
                true));

        public Task<RenderedEmailTemplate> RenderTenantTemplateAsync(Guid tenantId, string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenderedEmailTemplate(
                eventKey,
                "Tenant template",
                "Tenant message",
                "<p>Tenant message</p>",
                "Tenant message",
                null,
                null,
                true));
    }

    private sealed class FakeEmailSubjectRuleService : IEmailSubjectRuleService
    {
        public Task<EmailSubjectRuleOptions> GetSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(EmailSubjectRuleOptions.Default);

        public Task<string> BuildFinalSubjectAsync(Guid? tenantId, string eventKey, string templateSubject, string? tenantNameOverride = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(templateSubject);
    }
}
