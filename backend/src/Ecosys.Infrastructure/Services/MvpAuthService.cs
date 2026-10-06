using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.RegularExpressions;

namespace Ecosys.Infrastructure.Services;

public interface IMvpAuthService
{
    Task<SignupResult> SignupAsync(string fullName, string email, string password, string companyName, string? industry, string country, CancellationToken cancellationToken = default);

    /// <summary>
    /// <paramref name="identifier"/> is either an email (globally unique, resolved directly) or a
    /// username (unique only within a tenant, per EC-041 — two tenants may each have a "jsmith").
    /// A username-shaped identifier therefore also needs <paramref name="workspace"/> — the user's
    /// company name, normalised and matched against the tenant's slug — to know which tenant to look
    /// in; an email-shaped identifier ignores it.
    /// </summary>
    Task<LoginResult> LoginAsync(string identifier, string password, string? workspace = null, CancellationToken cancellationToken = default);
    Task<ImpersonationResult> ImpersonateTenantAdminAsync(Guid tenantId, Guid actingUserId, string actingUserName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an impersonation session as the tenant's admin, recording the operator's stated
    /// justification. <paramref name="reason"/> is written into both audit entries: "who impersonated
    /// which tenant, and why" is the question an auditor actually asks (EC-028), and it cannot be
    /// reconstructed after the fact. A separate overload rather than an added parameter so the
    /// existing four-argument call sites keep binding to the overload above unchanged.
    /// </summary>
    Task<ImpersonationResult> ImpersonateTenantAdminAsync(Guid tenantId, Guid actingUserId, string actingUserName, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Starts an impersonation session as one specific active user of a tenant (the demo workspace's roles).</summary>
    Task<ImpersonationResult> ImpersonateUserAsync(Guid tenantId, Guid userId, Guid actingUserId, string actingUserName, string? reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tenant's role-permission matrix (plus group grants and per-user overrides) for
    /// <paramref name="user"/> — the same resolution <see cref="LoginAsync"/> uses. Callers that
    /// already hold a loaded <see cref="User"/> (e.g. the "current user" endpoint) must go through
    /// this rather than reading <see cref="User.Permission"/> directly: that stored row is the
    /// legacy per-user grant the role matrix replaced, and is no longer kept in sync.
    /// </summary>
    Task<UserPermissionsModel> ResolveCurrentPermissionsAsync(User user, CancellationToken cancellationToken = default);
}

public sealed record SignupResult(
    string Token,
    Guid TenantId,
    Guid UserId,
    string CompanyName,
    string Role,
    string? JobTitle,
    UserPermissionsModel Permissions);

public sealed record LoginResult(
    string Token,
    Guid TenantId,
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    string? JobTitle,
    string? Department,
    bool MustChangePassword,
    UserPermissionsModel Permissions,
    string CompanyName,
    string Country,
    string? Industry,
    string? LogoUrl,
    bool ShowPoweredByEcosys,
    string TimeZoneId = "Africa/Nairobi",
    string? Username = null,
    bool HasEmail = true);

public sealed record ImpersonationResult(
    string Token,
    Guid TenantId,
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    string? JobTitle,
    UserPermissionsModel Permissions,
    string CompanyName,
    string Country,
    string? Industry,
    string? LogoUrl,
    bool ShowPoweredByEcosys,
    Guid ImpersonatedByUserId,
    string ImpersonatedByName,
    DateTime StartedAt);

internal sealed class MvpAuthService(
    AppDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    IOptions<AuthSecurityOptions> authSecurityOptions,
    IAuditLogService auditLogService,
    IUserPermissionTemplateService permissionTemplateService,
    IRolePermissionService rolePermissionService,
    IDynamicAccessService dynamicAccessService,
    IUserSessionService userSessionService,
    ILicenseGuardService licenseGuardService,
    ITenantSecurityPolicyService tenantSecurityPolicyService) : IMvpAuthService
{
    private const string GenericLoginFailureMessage = "Invalid email or password.";

    public async Task<SignupResult> SignupAsync(
        string fullName,
        string email,
        string password,
        string companyName,
        string? industry,
        string country,
        CancellationToken cancellationToken = default)
    {
        // Production tenants are provisioned from the platform so plan, seats and the invitation are
        // always set; an anonymous caller must not be able to mint an active workspace.
        if (!authSecurityOptions.Value.AllowSelfSignup)
        {
            throw new NotFoundException("Self-service signup is not available.");
        }

        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(companyName) ||
            string.IsNullOrWhiteSpace(country))
        {
            throw new BusinessRuleException("Full name, email, password, company name, and country are required.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var emailExists = await dbContext.Users.AnyAsync(x => x.Email.ToLower() == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            throw new BusinessRuleException("Email is already in use.");
        }

        tenantSecurityPolicyService.ValidatePassword(password, new TenantSecurityPolicy());

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        var trimmedCompanyName = companyName.Trim();
        var slug = await GenerateUniqueSlugAsync(trimmedCompanyName, cancellationToken);
        var normalizedCountry = country.Trim();

        var tenant = new Tenant
        {
            Name = trimmedCompanyName,
            Slug = slug,
            CompanyName = trimmedCompanyName,
            Email = normalizedEmail,
            ContactEmail = normalizedEmail,
            Country = normalizedCountry,
            TimeZoneId = TenantTimeService.DefaultTimeZone(normalizedCountry),
            Currency = TenantEmailIdentity.DefaultCurrency(normalizedCountry),
            Industry = string.IsNullOrWhiteSpace(industry) ? null : industry.Trim(),
            Status = "Active",
            LicenseStatus = LicenseStatuses.Trial,
            PlanName = ServiceOpsPlans.DisplayName(ServiceOpsPlans.Suite),
            IsActive = true
        };

        var user = new User
        {
            Tenant = tenant,
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            Role = AppRoles.Admin,
            JobTitle = "Tenant Administrator",
            IsActive = true,
            HasAllBranchAccess = true
        };

        var permissions = permissionTemplateService.GetFullTenantPermissions();
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        user.Permission = ToEntity(permissions);

        dbContext.Tenants.Add(tenant);
        dbContext.Users.Add(user);
        dbContext.EmailSettings.Add(TenantEmailIdentity.Create(tenant));

        dbContext.NumberingSettings.AddRange(
            CreateNumberingSetting(tenant, null, DocumentTypes.WorkOrder, "WO"),
            CreateNumberingSetting(tenant, null, DocumentTypes.MaterialRequest, "MR"),
            CreateNumberingSetting(tenant, null, DocumentTypes.Asset, "AST"),
            CreateNumberingSetting(tenant, null, DocumentTypes.StockTransfer, "ST"));

        await dbContext.SaveChangesAsync(cancellationToken);
        await ProvisionTenantDefaultsAsync(tenant.Id, cancellationToken);

        await auditLogService.LogAsync(
            tenant.Id,
            user.Id,
            "Tenant signup",
            nameof(Tenant),
            tenant.Id.ToString(),
            $"Tenant '{tenant.CompanyName}' created with first admin '{user.FullName}'.",
            cancellationToken);

        var token = await CreateTokenAsync(user, tenant, permissions, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new SignupResult(token, tenant.Id, user.Id, tenant.CompanyName, user.Role, user.JobTitle, permissions);
    }

    public async Task<LoginResult> LoginAsync(string identifier, string password, string? workspace = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessRuleException("Your sign-in details and password are required.");
        }

        var trimmedIdentifier = identifier.Trim();
        User? user;
        string failureEntityId;

        if (trimmedIdentifier.Contains('@'))
        {
            var normalizedEmail = trimmedIdentifier.ToLowerInvariant();
            failureEntityId = normalizedEmail;
            user = await dbContext.Users
                .Include(x => x.Tenant)
                .Include(x => x.Permission)
                .SingleOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail, cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(workspace))
            {
                throw new BusinessRuleException("Enter your company name to sign in with a username.");
            }

            var normalizedUsername = trimmedIdentifier.ToLowerInvariant();

            // The field is labelled "Company Name" to users, but the tenant lookup still needs to
            // land on the guaranteed-unique Slug column (CompanyName isn't unique). Normalising the
            // typed value the same way signup turns a company name into a slug (TenantEmailIdentity
            // .NormalizeSlug) means "Acme Corp" and "acme-corp" both resolve — they were the same
            // input at signup time. A malformed/too-short value can't be a real slug, so it's treated
            // as a lookup miss rather than surfacing NormalizeSlug's validation message.
            string? normalizedWorkspace;
            try
            {
                normalizedWorkspace = TenantEmailIdentity.NormalizeSlug(workspace, fromCompanyName: true);
            }
            catch (BusinessRuleException)
            {
                normalizedWorkspace = null;
            }

            // The slug alone is not enough: a platform-provisioned tenant can have a custom workspace
            // slug, and a clashing name gets a "-2" suffix, so the typed company name would never
            // match. Also accept the tenant's company/display name verbatim (case-insensitive). If
            // that is ambiguous (two tenants share a name and both have this username) it's a miss.
            var typedWorkspace = workspace.Trim().ToLowerInvariant();
            failureEntityId = $"{normalizedUsername}@{normalizedWorkspace ?? typedWorkspace}";
            var candidates = await dbContext.Users
                .Include(x => x.Tenant)
                .Include(x => x.Permission)
                .Where(x => x.Username == normalizedUsername && x.Tenant != null &&
                    ((normalizedWorkspace != null && x.Tenant.Slug == normalizedWorkspace) ||
                     x.Tenant.CompanyName.ToLower() == typedWorkspace ||
                     x.Tenant.Name.ToLower() == typedWorkspace))
                .Take(3)
                .ToListAsync(cancellationToken);
            user = candidates.Count == 1
                ? candidates[0]
                : candidates.SingleOrDefault(x => normalizedWorkspace != null && x.Tenant!.Slug == normalizedWorkspace);
        }

        if (user is null)
        {
            await LogFailedLoginAsync(
                tenantId: null,
                userId: null,
                entityId: failureEntityId,
                actorName: failureEntityId,
                details: $"Failed login attempt for '{failureEntityId}'.",
                cancellationToken: cancellationToken);
            throw new UnauthorizedException(GenericLoginFailureMessage);
        }

        var utcNow = DateTime.UtcNow;
        ResetExpiredLockout(user, utcNow);

        if (IsLockedOut(user, utcNow))
        {
            await LogFailedLoginAsync(
                user.TenantId,
                user.Id,
                user.Id.ToString(),
                DisplayIdentifier(user),
                $"Login attempt blocked for '{DisplayIdentifier(user)}' because the account is temporarily locked.",
                cancellationToken);
            throw new UnauthorizedException(GenericLoginFailureMessage);
        }

        if (!user.IsActive)
        {
            await RegisterFailedLoginAttemptAsync(user, "Inactive account", cancellationToken);
            throw new UnauthorizedException(GenericLoginFailureMessage);
        }

        if (user.Tenant is null || !user.Tenant.IsActive)
        {
            await RegisterFailedLoginAttemptAsync(user, "Inactive tenant", cancellationToken);
            throw new UnauthorizedException(GenericLoginFailureMessage);
        }

        var passwordResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (passwordResult == PasswordVerificationResult.Failed)
        {
            await RegisterFailedLoginAttemptAsync(user, "Invalid credentials", cancellationToken);
            throw new UnauthorizedException(GenericLoginFailureMessage);
        }

        ResetFailedLoginState(user);
        var permissions = await ResolvePermissionsAsync(user, cancellationToken);
        var token = await CreateTokenAsync(user, user.Tenant, permissions, cancellationToken, user.MustChangePassword);

        await auditLogService.LogAsync(
            user.TenantId,
            user.Id,
            "Login",
            nameof(User),
            user.Id.ToString(),
            $"User '{DisplayIdentifier(user)}' signed in.",
            cancellationToken);

        return new LoginResult(
            token,
            user.TenantId,
            user.Id,
            user.FullName,
            user.Email,
            user.Role,
            user.JobTitle,
            user.Department,
            user.MustChangePassword,
            permissions,
            user.Tenant.CompanyName,
            user.Tenant.Country,
            user.Tenant.Industry,
            user.Tenant.LogoUrl,
            user.Tenant.ShowPoweredByEcosys,
            user.Tenant.TimeZoneId,
            user.Username,
            user.HasEmail);
    }

    public Task<ImpersonationResult> ImpersonateTenantAdminAsync(Guid tenantId, Guid actingUserId, string actingUserName, CancellationToken cancellationToken = default) =>
        ImpersonateTenantAdminAsync(tenantId, actingUserId, actingUserName, null, cancellationToken);

    public async Task<ImpersonationResult> ImpersonateTenantAdminAsync(Guid tenantId, Guid actingUserId, string actingUserName, string? reason, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == tenantId && x.Id != PlatformConstants.RootTenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");

        if (!tenant.IsActive)
        {
            throw new BusinessRuleException("This tenant is not active. Reactivate it before starting an impersonation session.");
        }

        var targetUser = await dbContext.Users
            .Include(x => x.Permission)
            .Where(x => x.TenantId == tenantId && x.IsActive && x.Role == AppRoles.Admin)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("This tenant has no active admin user to impersonate.");

        return await StartImpersonationAsync(tenant, targetUser, actingUserId, actingUserName, reason, cancellationToken);
    }

    public async Task<ImpersonationResult> ImpersonateUserAsync(Guid tenantId, Guid userId, Guid actingUserId, string actingUserName, string? reason, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(x => x.Id == tenantId && x.Id != PlatformConstants.RootTenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");
        if (!tenant.IsActive)
        {
            throw new BusinessRuleException("This tenant is not active. Reactivate it before starting an impersonation session.");
        }

        var targetUser = await dbContext.Users
            .Include(x => x.Permission)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("That user was not found or is inactive.");

        return await StartImpersonationAsync(tenant, targetUser, actingUserId, actingUserName, reason, cancellationToken);
    }

    private async Task<ImpersonationResult> StartImpersonationAsync(Tenant tenant, User targetUser, Guid actingUserId, string actingUserName, string? reason, CancellationToken cancellationToken)
    {
        var tenantId = tenant.Id;
        var permissions = await ResolvePermissionsAsync(targetUser, cancellationToken);
        var jwtId = Guid.NewGuid().ToString("N");
        var session = userSessionService.StartSession(targetUser, jwtId);
        var startedAt = DateTime.UtcNow;
        var token = CreateToken(targetUser, tenant, permissions, session.Id, jwtId, mustChangePassword: false, actingUserId, actingUserName);
        await dbContext.SaveChangesAsync(cancellationToken);

        // EC-028: both entries name the acting platform user, the target tenant, and the stated
        // reason. The reason is operator-supplied free text and is recorded verbatim; nothing about
        // the issued session (token, jwt id, session id) is logged.
        var reasonSuffix = string.IsNullOrWhiteSpace(reason)
            ? " No reason was given."
            : $" Reason: {reason.Trim()}";

        await auditLogService.LogAsync(
            tenantId,
            actingUserId,
            "impersonation.started",
            nameof(User),
            targetUser.Id.ToString(),
            $"Platform admin '{actingUserName}' started an impersonation session in tenant '{tenant.Name}' as '{DisplayIdentifier(targetUser)}' ({targetUser.Role}).{reasonSuffix}",
            severity: "Warning",
            cancellationToken: cancellationToken);

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            actingUserId,
            "platform.tenant.impersonation.started",
            nameof(Tenant),
            tenantId.ToString(),
            $"Platform admin '{actingUserName}' started impersonating '{DisplayIdentifier(targetUser)}' ({targetUser.Role}) in tenant '{tenant.Name}'.{reasonSuffix}",
            severity: "Warning",
            cancellationToken: cancellationToken);

        return new ImpersonationResult(
            token,
            tenantId,
            targetUser.Id,
            targetUser.FullName,
            targetUser.Email,
            targetUser.Role,
            targetUser.JobTitle,
            permissions,
            tenant.CompanyName,
            tenant.Country,
            tenant.Industry,
            tenant.LogoUrl,
            tenant.ShowPoweredByEcosys,
            actingUserId,
            actingUserName,
            startedAt);
    }

    private async Task RegisterFailedLoginAttemptAsync(User user, string reason, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        ResetExpiredLockout(user, utcNow);

        user.FailedLoginCount = Math.Max(0, user.FailedLoginCount) + 1;
        user.LastFailedLoginAt = utcNow;

        if (user.FailedLoginCount >= authSecurityOptions.Value.FailedLoginThreshold)
        {
            user.LockedOutUntil = utcNow.AddMinutes(authSecurityOptions.Value.LockoutMinutes);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var displayIdentifier = DisplayIdentifier(user);
        var details = user.LockedOutUntil.HasValue && user.LockedOutUntil.Value > utcNow
            ? $"Failed login attempt for '{displayIdentifier}'. {reason}. Account locked temporarily."
            : $"Failed login attempt for '{displayIdentifier}'. {reason}.";

        await LogFailedLoginAsync(
            user.TenantId,
            user.Id,
            user.Id.ToString(),
            displayIdentifier,
            details,
            cancellationToken);
    }

    private async Task LogFailedLoginAsync(
        Guid? tenantId,
        Guid? userId,
        string entityId,
        string actorName,
        string details,
        CancellationToken cancellationToken)
    {
        await auditLogService.LogAsync(
            tenantId,
            userId,
            "auth.login.failed",
            nameof(User),
            entityId,
            details,
            severity: "Warning",
            actorName: actorName,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// What to show in audit trails and log messages: <see cref="User.Email"/> is a system-generated
    /// placeholder for a no-email account, so <see cref="User.Username"/> reads far better there.
    /// </summary>
    private static string DisplayIdentifier(User user) =>
        !user.HasEmail && !string.IsNullOrWhiteSpace(user.Username) ? user.Username : user.Email;

    private static bool IsLockedOut(User user, DateTime utcNow) =>
        user.LockedOutUntil.HasValue && user.LockedOutUntil.Value > utcNow;

    private static void ResetExpiredLockout(User user, DateTime utcNow)
    {
        if (!user.LockedOutUntil.HasValue || user.LockedOutUntil.Value > utcNow)
        {
            return;
        }

        user.FailedLoginCount = 0;
        user.LockedOutUntil = null;
        user.LastFailedLoginAt = null;
    }

    private static void ResetFailedLoginState(User user)
    {
        user.FailedLoginCount = 0;
        user.LockedOutUntil = null;
        user.LastFailedLoginAt = null;
    }

    private async Task<string> CreateTokenAsync(User user, Tenant tenant, UserPermissionsModel permissions, CancellationToken cancellationToken, bool mustChangePassword = false)
    {
        var jwtId = Guid.NewGuid().ToString("N");
        var session = userSessionService.StartSession(user, jwtId);
        var token = CreateToken(user, tenant, permissions, session.Id, jwtId, mustChangePassword);
        await dbContext.SaveChangesAsync(cancellationToken);
        return token;
    }

    private string CreateToken(
        User user,
        Tenant tenant,
        UserPermissionsModel permissions,
        Guid sessionId,
        string jwtId,
        bool mustChangePassword = false,
        Guid? impersonatedByUserId = null,
        string? impersonatedByName = null)
    {
        var options = jwtOptions.Value;
        var expiresUtc = DateTime.UtcNow.AddMinutes(options.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(TenantClaimTypes.UserId, user.Id.ToString()),
            new(TenantClaimTypes.TenantId, tenant.Id.ToString()),
            new(TenantClaimTypes.SessionId, sessionId.ToString()),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };

        if (!string.IsNullOrWhiteSpace(user.JobTitle))
        {
            claims.Add(new Claim(TenantClaimTypes.JobTitle, user.JobTitle));
        }

        if (!string.IsNullOrWhiteSpace(user.Department))
        {
            claims.Add(new Claim(TenantClaimTypes.Department, user.Department));
        }

        if (mustChangePassword)
        {
            claims.Add(new Claim(TenantClaimTypes.Scope, "password_change_only"));
        }

        if (impersonatedByUserId.HasValue)
        {
            claims.Add(new Claim(TenantClaimTypes.ImpersonatedByUserId, impersonatedByUserId.Value.ToString()));
            claims.Add(new Claim(TenantClaimTypes.ImpersonatedByName, impersonatedByName ?? string.Empty));
        }

        claims.AddRange(ToClaims(permissions));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresUtc,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// The tenant's role-permission matrix is the source of truth for what a user may do.
    /// Admin is implicitly granted everything and has no matrix row; platform SuperAdmin is
    /// handled separately. Legacy per-user permission rows are no longer consulted.
    /// </summary>
    private async Task<UserPermissionsModel> ResolvePermissionsAsync(User user, CancellationToken cancellationToken)
    {
        if (string.Equals(user.Role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return permissionTemplateService.GetFullTenantPermissions();
        }

        if (string.Equals(user.Role, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return permissionTemplateService.GetSuperAdminPermissions();
        }

        return await dynamicAccessService.ResolveLegacyClaimsAsync(user.TenantId, user.Id, cancellationToken);
    }

    public Task<UserPermissionsModel> ResolveCurrentPermissionsAsync(User user, CancellationToken cancellationToken = default) =>
        ResolvePermissionsAsync(user, cancellationToken);

    private static UserPermission ToEntity(UserPermissionsModel permissions) =>
        new()
        {
            CanViewWorkOrders = permissions.CanViewWorkOrders,
            CanCreateWorkOrders = permissions.CanCreateWorkOrders,
            CanAssignWorkOrders = permissions.CanAssignWorkOrders,
            CanCompleteWorkOrders = permissions.CanCompleteWorkOrders,
            CanViewKnowledge = permissions.CanViewKnowledge,
            CanManageKnowledge = permissions.CanManageKnowledge,
            CanViewMaterials = permissions.CanViewMaterials,
            CanRequestMaterials = permissions.CanRequestMaterials,
            CanManageMaterials = permissions.CanManageMaterials,
            CanApproveMaterials = permissions.CanApproveMaterials,
            CanIssueMaterials = permissions.CanIssueMaterials,
            CanManageAssets = permissions.CanManageAssets,
            CanManageSettings = permissions.CanManageSettings,
            CanViewReports = permissions.CanViewReports,
            CanViewFinance = permissions.CanViewFinance,
            CanManageFinance = permissions.CanManageFinance,
            CanViewFinanceRates = permissions.CanViewFinanceRates,
            CanManageFinanceRates = permissions.CanManageFinanceRates,
            CanViewFinanceApprovals = permissions.CanViewFinanceApprovals,
            CanManageFinanceApprovals = permissions.CanManageFinanceApprovals,
            CanViewBillableWork = permissions.CanViewBillableWork,
            CanManageBillableWork = permissions.CanManageBillableWork,
            CanOverrideMaterialStock = permissions.CanOverrideMaterialStock
        };

    public static UserPermissionsModel FromEntity(UserPermission permission) =>
        new()
        {
            CanViewWorkOrders = permission.CanViewWorkOrders,
            CanCreateWorkOrders = permission.CanCreateWorkOrders,
            CanAssignWorkOrders = permission.CanAssignWorkOrders,
            CanCompleteWorkOrders = permission.CanCompleteWorkOrders,
            CanViewKnowledge = permission.CanViewKnowledge,
            CanManageKnowledge = permission.CanManageKnowledge,
            CanViewMaterials = permission.CanViewMaterials,
            CanRequestMaterials = permission.CanRequestMaterials,
            CanManageMaterials = permission.CanManageMaterials,
            CanApproveMaterials = permission.CanApproveMaterials,
            CanIssueMaterials = permission.CanIssueMaterials,
            CanManageAssets = permission.CanManageAssets,
            CanManageSettings = permission.CanManageSettings,
            CanViewReports = permission.CanViewReports,
            CanViewFinance = permission.CanViewFinance,
            CanManageFinance = permission.CanManageFinance,
            CanViewFinanceRates = permission.CanViewFinanceRates,
            CanManageFinanceRates = permission.CanManageFinanceRates,
            CanViewFinanceApprovals = permission.CanViewFinanceApprovals,
            CanManageFinanceApprovals = permission.CanManageFinanceApprovals,
            CanViewBillableWork = permission.CanViewBillableWork,
            CanManageBillableWork = permission.CanManageBillableWork,
            CanOverrideMaterialStock = permission.CanOverrideMaterialStock
        };

    private static IEnumerable<Claim> ToClaims(UserPermissionsModel permissions)
    {
        yield return new Claim(PermissionNames.CanViewWorkOrders, permissions.CanViewWorkOrders.ToString());
        yield return new Claim(PermissionNames.CanCreateWorkOrders, permissions.CanCreateWorkOrders.ToString());
        yield return new Claim(PermissionNames.CanAssignWorkOrders, permissions.CanAssignWorkOrders.ToString());
        yield return new Claim(PermissionNames.CanCompleteWorkOrders, permissions.CanCompleteWorkOrders.ToString());
        yield return new Claim(PermissionNames.CanViewKnowledge, permissions.CanViewKnowledge.ToString());
        yield return new Claim(PermissionNames.CanManageKnowledge, permissions.CanManageKnowledge.ToString());
        yield return new Claim(PermissionNames.CanViewMaterials, permissions.CanViewMaterials.ToString());
        yield return new Claim(PermissionNames.CanRequestMaterials, permissions.CanRequestMaterials.ToString());
        yield return new Claim(PermissionNames.CanManageMaterials, permissions.CanManageMaterials.ToString());
        yield return new Claim(PermissionNames.CanApproveMaterials, permissions.CanApproveMaterials.ToString());
        yield return new Claim(PermissionNames.CanIssueMaterials, permissions.CanIssueMaterials.ToString());
        yield return new Claim(PermissionNames.CanManageAssets, permissions.CanManageAssets.ToString());
        yield return new Claim(PermissionNames.CanManageSettings, permissions.CanManageSettings.ToString());
        yield return new Claim(PermissionNames.CanViewReports, permissions.CanViewReports.ToString());
        yield return new Claim(PermissionNames.FinanceView, permissions.CanViewFinance.ToString());
        yield return new Claim(PermissionNames.FinanceManage, permissions.CanManageFinance.ToString());
        yield return new Claim(PermissionNames.FinanceViewRates, permissions.CanViewFinanceRates.ToString());
        yield return new Claim(PermissionNames.FinanceManageRates, permissions.CanManageFinanceRates.ToString());
        yield return new Claim(PermissionNames.FinanceViewApprovals, permissions.CanViewFinanceApprovals.ToString());
        yield return new Claim(PermissionNames.FinanceManageApprovals, permissions.CanManageFinanceApprovals.ToString());
        yield return new Claim(PermissionNames.FinanceViewBillableWork, permissions.CanViewBillableWork.ToString());
        yield return new Claim(PermissionNames.FinanceManageBillableWork, permissions.CanManageBillableWork.ToString());
        yield return new Claim(PermissionNames.MaterialsOverrideStock, permissions.CanOverrideMaterialStock.ToString());
    }

    private static NumberingSetting CreateNumberingSetting(Tenant tenant, Guid? branchId, string documentType, string prefix) =>
        new()
        {
            Tenant = tenant,
            BranchId = branchId,
            DocumentType = documentType,
            Prefix = prefix,
            NextNumber = 1,
            PaddingLength = 6,
            ResetFrequency = NumberResetFrequencies.Never,
            IncludeYear = false,
            IncludeMonth = false,
            IsActive = true
        };

    private async Task<string> GenerateUniqueSlugAsync(string companyName, CancellationToken cancellationToken)
    {
        var baseSlug = TenantEmailIdentity.NormalizeSlug(companyName, fromCompanyName: true);
        var slug = baseSlug;
        var counter = 2;

        while (await dbContext.Tenants.AnyAsync(x => x.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug[..Math.Min(baseSlug.Length, 58)]}-{counter}";
            counter += 1;
        }

        return slug;
    }

    private async Task ProvisionTenantDefaultsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var license = await licenseGuardService.GetOrCreateTenantLicenseAsync(tenantId, cancellationToken);
        var suite = await dbContext.LicensePlans.SingleAsync(x => x.PlanCode == ServiceOpsPlans.Suite && x.IsActive, cancellationToken);
        license.LicensePlanId = suite.Id;
        license.LicensePlan = suite;
        await tenantSecurityPolicyService.GetOrCreateAsync(tenantId, cancellationToken);

        if (!await dbContext.NotificationSettings.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
        {
            dbContext.NotificationSettings.Add(new NotificationSetting { TenantId = tenantId });
        }

        if (!await dbContext.MonitoringSettings.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
        {
            dbContext.MonitoringSettings.Add(new MonitoringSetting { TenantId = tenantId });
        }

        if (!await dbContext.PmTemplates.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
        {
            var templates = new[]
            {
                CreatePmTemplate(tenantId, "HVAC", "HVAC PM Checklist",
                [
                    ("Airflow", "Confirm supply and return airflow is unobstructed.", "yesno", true),
                    ("Filters", "Inspect and clean or replace filters as needed.", "yesno", true),
                    ("Refrigerant / Cooling", "Verify cooling performance is within expected range.", "yesno", true),
                    ("Electrical", "Inspect contactors, terminals, and wiring for signs of overheating.", "yesno", true),
                    ("Condensate Drain", "Check condensate drain line and tray for blockage or leaks.", "yesno", true),
                    ("Recommendations", "Add technician recommendations for the client.", "text", false)
                ]),
                CreatePmTemplate(tenantId, "Generator", "Generator PM Checklist",
                [
                    ("General Inspection", "Inspect generator enclosure and mounting condition.", "yesno", true),
                    ("Fuel System", "Check fuel level, lines, and leaks.", "yesno", true),
                    ("Battery System", "Test starter battery condition and terminals.", "yesno", true),
                    ("Cooling System", "Inspect coolant level, hoses, and radiator condition.", "yesno", true),
                    ("Running Test", "Perform running test and confirm stable output.", "passfail", true),
                    ("Recommendations", "Capture follow-up actions or recommendations.", "text", false)
                ]),
                CreatePmTemplate(tenantId, "UPS", "UPS PM Checklist",
                [
                    ("Visual Inspection", "Inspect UPS enclosure, indicators, and cable terminations.", "yesno", true),
                    ("Electrical Checks", "Verify input and output voltages are within range.", "yesno", true),
                    ("Battery Checks", "Check battery health, voltage, and corrosion signs.", "yesno", true),
                    ("Alarms & Logs", "Review active alarms and event logs.", "yesno", true),
                    ("Recommendations", "Document service recommendations.", "text", false)
                ]),
                CreatePmTemplate(tenantId, "Solar", "Solar PM Checklist",
                [
                    ("Panels", "Inspect panel surfaces for dirt, cracks, or shading issues.", "yesno", true),
                    ("Inverter", "Check inverter status indicators and error logs.", "yesno", true),
                    ("Batteries", "Inspect battery bank condition where applicable.", "yesno", false),
                    ("Cabling", "Verify cable routing, glands, and terminations are secure.", "yesno", true),
                    ("Performance", "Confirm expected energy production or system output.", "yesno", true),
                    ("Recommendations", "Record optimization or repair recommendations.", "text", false)
                ])
            };

            dbContext.PmTemplates.AddRange(templates);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static PmTemplate CreatePmTemplate(
        Guid tenantId,
        string category,
        string name,
        IReadOnlyList<(string SectionName, string Prompt, string ResponseType, bool IsRequired)> questions)
    {
        var template = new PmTemplate
        {
            TenantId = tenantId,
            Category = category,
            Name = name,
            AutoScheduleByDefault = false
        };

        template.Questions = questions
            .Select((question, index) => new PmTemplateQuestion
            {
                SectionName = question.SectionName,
                Prompt = question.Prompt,
                ResponseType = question.ResponseType,
                DisplayOrder = index + 1,
                IsRequired = question.IsRequired
            })
            .ToList();

        return template;
    }
}
