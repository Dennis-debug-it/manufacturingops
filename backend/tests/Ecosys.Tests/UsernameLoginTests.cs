using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// EC-041: staff without an email address sign in with a username instead. A username is unique
/// only within its tenant (unlike email, which is unique platform-wide), so a username-shaped
/// login also needs the tenant's workspace slug to know where to look.
/// </summary>
public sealed partial class AuthEmailSafetyTests
{
    private const string UsernamePassword = "Str0ng!TestPass1";

    [Fact]
    public async Task Login_WithUsernameAndWorkspace_Succeeds()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "acmecorp", "admin@acmecorp.test");
        var user = await SeedUsernameOnlyUserAsync(dbContext, tenantId, "jsmith");

        var authService = CreateAuthService(dbContext);
        var controller = CreateAuthController(dbContext, tenantId, user.Id, authService);

        var result = await controller.Login(new LoginRequest("jsmith", UsernamePassword, "acmecorp"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal(user.Id, response.User.UserId);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
    }

    [Fact]
    public async Task Login_WithUsername_IsCaseInsensitiveForBothUsernameAndWorkspace()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "acmecorp", "admin@acmecorp.test");
        var user = await SeedUsernameOnlyUserAsync(dbContext, tenantId, "jsmith");

        var authService = CreateAuthService(dbContext);
        var controller = CreateAuthController(dbContext, tenantId, user.Id, authService);

        var result = await controller.Login(new LoginRequest("JSmith", UsernamePassword, "AcmeCorp"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal(user.Id, response.User.UserId);
    }

    [Fact]
    public async Task Login_WithUsername_ButNoWorkspace_Rejects()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "acmecorp", "admin@acmecorp.test");
        var user = await SeedUsernameOnlyUserAsync(dbContext, tenantId, "jsmith");

        var authService = CreateAuthService(dbContext);
        var controller = CreateAuthController(dbContext, tenantId, user.Id, authService);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            controller.Login(new LoginRequest("jsmith", UsernamePassword), CancellationToken.None));
    }

    [Fact]
    public async Task Login_WithUsername_DoesNotCrossTenantBoundaries()
    {
        // Two unrelated tenants each provision a "jsmith" — the same username must resolve to
        // whichever tenant the workspace slug names, never the other one.
        await using var dbContext = CreateDbContext();
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantAId, "acmecorp", "admin@acmecorp.test");
        await SeedTenantAsync(dbContext, tenantBId, "globex", "admin@globex.test");
        var userA = await SeedUsernameOnlyUserAsync(dbContext, tenantAId, "jsmith");
        await SeedUsernameOnlyUserAsync(dbContext, tenantBId, "jsmith");

        var authService = CreateAuthService(dbContext);
        var controller = CreateAuthController(dbContext, tenantAId, userA.Id, authService);

        var result = await controller.Login(new LoginRequest("jsmith", UsernamePassword, "acmecorp"), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal(userA.Id, response.User.UserId);
        Assert.Equal(tenantAId, response.Tenant.TenantId);
    }

    [Fact]
    public async Task Login_WithUsername_WrongWorkspace_FailsGenerically()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "acmecorp", "admin@acmecorp.test");
        var user = await SeedUsernameOnlyUserAsync(dbContext, tenantId, "jsmith");

        var authService = CreateAuthService(dbContext);
        var controller = CreateAuthController(dbContext, tenantId, user.Id, authService);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            controller.Login(new LoginRequest("jsmith", UsernamePassword, "no-such-workspace"), CancellationToken.None));
    }

    [Fact]
    public async Task Login_WithUsername_AcceptsTheActualCompanyNameNotJustTheRawSlug()
    {
        // The field is labelled "Company Name" to users, so typing the real, human name — spaces,
        // capitalisation, a trailing "Ltd" — must resolve the same way the raw slug would, because
        // that's exactly how the slug was derived from this same name at signup.
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "AcmeCorp", "admin@acmecorp.test");
        var tenant = await dbContext.Tenants.SingleAsync(x => x.Id == tenantId);
        tenant.Slug = "acme-corp";
        await dbContext.SaveChangesAsync();
        var user = await SeedUsernameOnlyUserAsync(dbContext, tenantId, "jsmith");

        var authService = CreateAuthService(dbContext);
        var controller = CreateAuthController(dbContext, tenantId, user.Id, authService);

        var result = await controller.Login(new LoginRequest("jsmith", UsernamePassword, "Acme Corp Ltd"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal(user.Id, response.User.UserId);
    }

    [Fact]
    public async Task Login_WithUsername_MalformedCompanyNameFailsGenericallyInsteadOfThrowingValidationError()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "acmecorp", "admin@acmecorp.test");
        var user = await SeedUsernameOnlyUserAsync(dbContext, tenantId, "jsmith");

        var authService = CreateAuthService(dbContext);
        var controller = CreateAuthController(dbContext, tenantId, user.Id, authService);

        // "!!" normalises to an empty slug candidate, which NormalizeSlug would otherwise reject
        // with a validation exception — that must not leak out of a login attempt.
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            controller.Login(new LoginRequest("jsmith", UsernamePassword, "!!"), CancellationToken.None));
    }

    [Fact]
    public async Task Create_WithoutEmail_RevealsGeneratedPasswordAndNeverEmails()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "acmecorp", "admin@acmecorp.test");
        var controller = CreateUsersController(dbContext, tenantId, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true));

        var request = new CreateUserRequest(
            "Field Tech", null, null, null, AppRoles.User, null, null, true, "None", null, [], null, false, []);

        var actionResult = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var response = Assert.IsType<UserResponse>(created.Value);
        Assert.False(response.HasEmail);
        Assert.False(string.IsNullOrWhiteSpace(response.Username));
        Assert.Equal("field.tech", response.Username);
        Assert.True(response.CredentialDelivery?.Success);
        Assert.False(string.IsNullOrWhiteSpace(response.CredentialDelivery?.TemporaryPassword));

        Assert.Empty(await dbContext.EmailDeliveryLogs.ToListAsync());
    }

    [Fact]
    public async Task Create_WithoutEmail_AndDeliveryMethodNotNone_Rejects()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "acmecorp", "admin@acmecorp.test");
        var controller = CreateUsersController(dbContext, tenantId, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true));

        var request = new CreateUserRequest(
            "Field Tech", null, null, null, AppRoles.User, null, null, true, "InviteEmail", null, [], null, false, []);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Create(request, CancellationToken.None));
    }

    [Fact]
    public async Task Create_WithoutEmail_AdminSuppliedPassword_DoesNotRevealIt()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(dbContext, tenantId, "acmecorp", "admin@acmecorp.test");
        var controller = CreateUsersController(dbContext, tenantId, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true));

        var request = new CreateUserRequest(
            "Field Tech", null, null, UsernamePassword, AppRoles.User, null, null, true, "None", null, [], null, false, []);

        var actionResult = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var response = Assert.IsType<UserResponse>(created.Value);
        Assert.True(response.CredentialDelivery?.Success);
        Assert.Null(response.CredentialDelivery?.TemporaryPassword);
    }

    [Fact]
    public async Task ResetPassword_GenerateAndReveal_ReturnsPasswordAndForcesChangeOnNextLogin()
    {
        await using var dbContext = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var user = await SeedTenantUserAsync(dbContext, tenantId, "operator2@tenant.test", mustChangePassword: false);
        var originalHash = user.PasswordHash;
        var controller = CreateUsersController(dbContext, tenantId, new FakeUserCredentialDeliveryService(dbContext, credentialSendSucceeds: true));

        var actionResult = await controller.ResetPassword(user.Id, new ResetPasswordRequest(null, null, GenerateAndReveal: true), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<CredentialDeliveryResponse>(ok.Value);
        Assert.True(response.Success);
        Assert.False(string.IsNullOrWhiteSpace(response.TemporaryPassword));

        var updatedUser = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        Assert.NotEqual(originalHash, updatedUser.PasswordHash);
        Assert.True(updatedUser.MustChangePassword);
        Assert.Empty(await dbContext.EmailDeliveryLogs.ToListAsync());
    }

    private static async Task<User> SeedUsernameOnlyUserAsync(AppDbContext dbContext, Guid tenantId, string username)
    {
        var user = new User
        {
            TenantId = tenantId,
            FullName = "No Email User",
            Email = $"{Guid.NewGuid():N}@no-email.invalid",
            HasEmail = false,
            Username = username,
            Role = AppRoles.Admin,
            IsActive = true,
            HasAllBranchAccess = true
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, UsernamePassword);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }
}
