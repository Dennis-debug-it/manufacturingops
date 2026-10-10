using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecosys.Tests;

/// <summary>
/// EC-002 and EC-011.
///
/// EC-002: login 500'd for every non-Admin role because permission resolution reached six
/// dynamic-access tables that no migration ever created. These tests exercise the real
/// <see cref="DynamicAccessService"/> and <see cref="RolePermissionService"/> against a database
/// where those tables exist but are empty — the state of every freshly migrated tenant — so a
/// regression that makes resolution depend on rows nobody has yet fails here.
///
/// EC-011: <c>POST /api/auth/login</c> computed permissions while <c>GET /api/auth/me</c> mapped the
/// stored <c>user_permissions</c> row, so the two disagreed. The SPA re-bootstraps its session from
/// /me on every page load, which meant a refresh could silently change what the user could do.
/// Every test here seeds a deliberately contradictory <c>user_permissions</c> row: if either
/// endpoint ever reads it again, these assertions break.
/// </summary>
public sealed class RoleLoginTests
{
    private const string Password = "Str0ng!TestPass1";

    public static TheoryData<string> AssignableTenantRoles()
    {
        var data = new TheoryData<string>();
        foreach (var role in AppRoles.AssignableTenantRoles)
        {
            data.Add(role);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AssignableTenantRoles))]
    public async Task Login_Succeeds_ForEveryAssignableTenantRole(string role)
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, role);

        var login = await LoginAsync(dbContext, fixture);

        Assert.False(string.IsNullOrWhiteSpace(login.Token));
        Assert.Equal(role, login.User.Role);
    }

    [Theory]
    [MemberData(nameof(AssignableTenantRoles))]
    public async Task Login_And_Me_ReportTheSamePermissions_ForEveryAssignableTenantRole(string role)
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, role);

        var login = await LoginAsync(dbContext, fixture);
        var me = await GetCurrentUserAsync(dbContext, fixture);

        Assert.Equal(login.User.Permissions, me.User.Permissions);
    }

    [Theory]
    [MemberData(nameof(AssignableTenantRoles))]
    public async Task Login_GrantsSomething_WhenDynamicAccessTablesAreEmpty(string role)
    {
        // A brand-new tenant has no permission groups, overrides, scopes or thresholds. Resolution
        // must still fall back to the role matrix rather than leaving the user able to sign in and
        // do nothing at all.
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, role);

        Assert.Empty(await dbContext.PermissionGroups.ToListAsync());
        Assert.Empty(await dbContext.UserPermissionGroups.ToListAsync());
        Assert.Empty(await dbContext.GroupCapabilities.ToListAsync());
        Assert.Empty(await dbContext.UserPermissionOverrides.ToListAsync());
        Assert.Empty(await dbContext.AccessScopes.ToListAsync());
        Assert.Empty(await dbContext.PermissionThresholds.ToListAsync());

        var login = await LoginAsync(dbContext, fixture);

        Assert.True(login.User.Permissions.CanViewWorkOrders, $"Role '{role}' resolved to no work-order access at all.");
    }

    [Fact]
    public async Task MaterialsController_RunsTheStores_ButNotDispatchOrFinance()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, AppRoles.MaterialsController);

        var permissions = (await LoginAsync(dbContext, fixture)).User.Permissions;

        // Stock, purchase orders, transfers and vans (manage); handing out parts (issue); approving requests.
        Assert.True(permissions.CanViewMaterials);
        Assert.True(permissions.CanManageMaterials);
        Assert.True(permissions.CanIssueMaterials);
        Assert.True(permissions.CanApproveMaterials);
        Assert.True(permissions.CanViewWorkOrders);
        // Overriding stock levels is left for an admin to grant.
        Assert.False(permissions.CanOverrideMaterialStock);
        Assert.False(permissions.CanAssignWorkOrders);
        Assert.False(permissions.CanCreateWorkOrders);
        Assert.False(permissions.CanViewFinance);
        Assert.False(permissions.CanManageSettings);
    }

    [Fact]
    public async Task Login_Succeeds_ForLegacyUserRole()
    {
        // "User" predates the role matrix and normalises to Viewer. It has no matrix row of its own,
        // so it is the case most likely to resolve to nothing.
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, AppRoles.User);

        var login = await LoginAsync(dbContext, fixture);
        var me = await GetCurrentUserAsync(dbContext, fixture);

        Assert.Equal(login.User.Permissions, me.User.Permissions);
        Assert.True(login.User.Permissions.CanViewWorkOrders);
    }

    [Fact]
    public async Task Login_And_Me_Agree_ForTenantAdmin_EvenWhenTheStoredRowIsStale()
    {
        // The observed EC-011 case: an Admin whose stored user_permissions row said
        // CanViewKnowledge=false, while login handed back the full tenant permission set.
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, AppRoles.Admin);

        var login = await LoginAsync(dbContext, fixture);
        var me = await GetCurrentUserAsync(dbContext, fixture);

        Assert.Equal(login.User.Permissions, me.User.Permissions);
        Assert.True(login.User.Permissions.CanViewKnowledge);
        Assert.True(login.User.Permissions.CanManageSettings);
    }

    [Fact]
    public async Task RolePermissionMatrix_IsTheSourceOfTruth_NotTheStoredUserPermissionRow()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, AppRoles.Technician);

        // The tenant's matrix grants asset management and withholds knowledge, the exact opposite of
        // the stale row seeded against the user.
        dbContext.RolePermissions.Add(new RolePermission
        {
            TenantId = fixture.TenantId,
            Role = AppRoles.Technician,
            CanViewWorkOrders = true,
            CanManageAssets = true,
            CanViewKnowledge = false
        });
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync(dbContext, fixture);
        var me = await GetCurrentUserAsync(dbContext, fixture);

        Assert.True(login.User.Permissions.CanManageAssets);
        Assert.False(login.User.Permissions.CanViewKnowledge);
        Assert.Equal(login.User.Permissions, me.User.Permissions);
    }

    [Fact]
    public async Task PermissionGroupMembership_AddsToTheRoleBaseline()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, AppRoles.Technician);

        dbContext.RolePermissions.Add(new RolePermission
        {
            TenantId = fixture.TenantId,
            Role = AppRoles.Technician,
            CanViewWorkOrders = true
        });

        var group = new PermissionGroup { TenantId = fixture.TenantId, Name = "Stock keepers" };
        group.Capabilities.Add(new GroupCapability
        {
            TenantId = fixture.TenantId,
            Capability = PermissionNames.CanIssueMaterials
        });
        dbContext.PermissionGroups.Add(group);
        dbContext.UserPermissionGroups.Add(new UserPermissionGroup
        {
            TenantId = fixture.TenantId,
            UserId = fixture.UserId,
            PermissionGroupId = group.Id
        });
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync(dbContext, fixture);
        var me = await GetCurrentUserAsync(dbContext, fixture);

        Assert.True(login.User.Permissions.CanViewWorkOrders);
        Assert.True(login.User.Permissions.CanIssueMaterials);
        Assert.Equal(login.User.Permissions, me.User.Permissions);
    }

    [Fact]
    public async Task UserPermissionOverride_DeniesACapabilityTheRoleGrants()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedAsync(dbContext, AppRoles.Manager);

        dbContext.RolePermissions.Add(new RolePermission
        {
            TenantId = fixture.TenantId,
            Role = AppRoles.Manager,
            CanViewWorkOrders = true,
            CanViewFinance = true
        });
        dbContext.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = fixture.TenantId,
            UserId = fixture.UserId,
            Capability = PermissionNames.FinanceView,
            IsAllowed = false
        });
        await dbContext.SaveChangesAsync();

        var login = await LoginAsync(dbContext, fixture);
        var me = await GetCurrentUserAsync(dbContext, fixture);

        Assert.True(login.User.Permissions.CanViewWorkOrders);
        Assert.False(login.User.Permissions.CanViewFinance);
        Assert.Equal(login.User.Permissions, me.User.Permissions);
    }

    private static async Task<LoginResponse> LoginAsync(AppDbContext dbContext, Fixture fixture)
    {
        var controller = CreateAuthController(dbContext, fixture);
        var result = await controller.Login(new LoginRequest(fixture.Email, Password), CancellationToken.None);
        return Assert.IsType<LoginResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private static async Task<AuthenticatedContextResponse> GetCurrentUserAsync(AppDbContext dbContext, Fixture fixture)
    {
        var controller = CreateAuthController(dbContext, fixture);
        var result = await controller.GetCurrentUser(CancellationToken.None);
        return Assert.IsType<AuthenticatedContextResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private static AuthController CreateAuthController(AppDbContext dbContext, Fixture fixture) =>
        new(
            CreateAuthService(dbContext),
            new FakeTenantContext(fixture.TenantId, fixture.UserId, fixture.Role),
            new FakeUserSessionService(dbContext),
            dbContext,
            new NoOpAuditLogService(),
            new UnusedPasswordResetService(),
            new PasswordHasher<User>(),
            new FakeTenantSecurityPolicyService());

    private static MvpAuthService CreateAuthService(AppDbContext dbContext) =>
        new(
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
            new UserPermissionTemplateService(),
            new RolePermissionService(dbContext),
            new DynamicAccessService(dbContext, new RolePermissionService(dbContext)),
            new FakeUserSessionService(dbContext),
            new UnusedLicenseGuardService(),
            new FakeTenantSecurityPolicyService());

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-role-login-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed record Fixture(Guid TenantId, Guid UserId, string Email, string Role);

    private static async Task<Fixture> SeedAsync(AppDbContext dbContext, string role)
    {
        var tenantId = Guid.NewGuid();
        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Role Login Co",
            Slug = $"role-login-{tenantId:N}",
            CompanyName = "Role Login Co",
            Email = "ops@rolelogin.test",
            ContactEmail = "ops@rolelogin.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            IsActive = true
        });

        var email = $"{role.ToLowerInvariant()}@rolelogin.test";
        var user = new User
        {
            TenantId = tenantId,
            FullName = $"{role} User",
            Email = email,
            Role = role,
            IsActive = true,
            HasAllBranchAccess = string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase),
            // Deliberately contradictory: nothing but the stale row grants finance management, and
            // nothing but the stale row withholds knowledge. Neither endpoint may read it.
            Permission = new UserPermission
            {
                CanViewKnowledge = false,
                CanManageFinance = true,
                CanManageFinanceRates = true,
                CanOverrideMaterialStock = true
            }
        };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Password);
        dbContext.Users.Add(user);

        await dbContext.SaveChangesAsync();
        return new Fixture(tenantId, user.Id, email, role);
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId, string role) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId { get; } = Guid.NewGuid();
        public string? Email => null;
        public string? Role => role;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => string.Equals(role, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase);
        public bool IsAdmin => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
        public bool HasRole(string candidate) => string.Equals(role, candidate, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
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

        public Task<int> RevokeAllForUserAsync(Guid userId, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> RevokeOthersForUserAsync(Guid userId, Guid currentSessionId, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> RevokeSessionsForRolesAsync(Guid tenantId, IReadOnlyCollection<string> roles, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> RevokeSessionsForUsersAsync(IReadOnlyCollection<Guid> userIds, string? reason = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeTenantSecurityPolicyService : ITenantSecurityPolicyService
    {
        public Task<TenantSecurityPolicy> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TenantSecurityPolicy { TenantId = tenantId });

        public Task ValidatePasswordAsync(Guid tenantId, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void ValidatePassword(string password, TenantSecurityPolicy policy)
        {
        }
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task LogAsync(
            Guid? tenantId,
            Guid? userId,
            string action,
            string entityName,
            string entityId,
            string? details,
            string severity = "Info",
            string? actorName = null,
            string? ipAddress = null,
            string? userAgent = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>Constructor ballast: /me and login never touch password reset.</summary>
    private sealed class UnusedPasswordResetService : IPasswordResetService
    {
        public Task<string> RequestAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> ResetAsync(string token, string newPassword, string confirmPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> ValidateInvitationAsync(string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> ResendInvitationAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>Constructor ballast: only signup provisions licences.</summary>
    private sealed class UnusedLicenseGuardService : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
