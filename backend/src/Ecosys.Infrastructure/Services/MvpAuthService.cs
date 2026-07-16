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
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<ImpersonationResult> ImpersonateTenantAdminAsync(Guid tenantId, Guid actingUserId, string actingUserName, CancellationToken cancellationToken = default);
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
    bool ShowPoweredByEcosys);

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
            Industry = string.IsNullOrWhiteSpace(industry) ? null : industry.Trim(),
            Status = "Active",
            LicenseStatus = LicenseStatuses.Trial,
            PlanName = "Trial",
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
        dbContext.EmailSettings.Add(new EmailSetting
        {
            Tenant = tenant,
            UsePlatformDefaults = true,
            OverrideSmtpSettings = false,
            Host = "localhost",
            Port = 25,
            UseSsl = false,
            SenderName = tenant.CompanyName,
            SenderAddress = normalizedEmail
        });

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
        return new SignupResult(token, tenant.Id, user.Id, tenant.CompanyName, user.Role, user.JobTitle, permissions);
    }

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessRuleException("Email and password are required.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .Include(x => x.Tenant)
            .Include(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user is null)
        {
            await LogFailedLoginAsync(
                tenantId: null,
                userId: null,
                entityId: normalizedEmail,
                actorName: normalizedEmail,
                details: $"Failed login attempt for '{normalizedEmail}'.",
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
                user.Email,
                $"Login attempt blocked for '{user.Email}' because the account is temporarily locked.",
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
            $"User '{user.Email}' signed in.",
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
            user.Tenant.ShowPoweredByEcosys);
    }

    public async Task<ImpersonationResult> ImpersonateTenantAdminAsync(Guid tenantId, Guid actingUserId, string actingUserName, CancellationToken cancellationToken = default)
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

        var permissions = await ResolvePermissionsAsync(targetUser, cancellationToken);
        var jwtId = Guid.NewGuid().ToString("N");
        var session = userSessionService.StartSession(targetUser, jwtId);
        var startedAt = DateTime.UtcNow;
        var token = CreateToken(targetUser, tenant, permissions, session.Id, jwtId, mustChangePassword: false, actingUserId, actingUserName);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            tenantId,
            actingUserId,
            "impersonation.started",
            nameof(User),
            targetUser.Id.ToString(),
            $"Platform admin '{actingUserName}' started an impersonation session as tenant admin '{targetUser.Email}'.",
            severity: "Warning",
            cancellationToken: cancellationToken);

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            actingUserId,
            "platform.tenant.impersonation.started",
            nameof(Tenant),
            tenantId.ToString(),
            $"Started impersonating '{targetUser.Email}' in tenant '{tenant.Name}'.",
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

        var details = user.LockedOutUntil.HasValue && user.LockedOutUntil.Value > utcNow
            ? $"Failed login attempt for '{user.Email}'. {reason}. Account locked temporarily."
            : $"Failed login attempt for '{user.Email}'. {reason}.";

        await LogFailedLoginAsync(
            user.TenantId,
            user.Id,
            user.Id.ToString(),
            user.Email,
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

        return await rolePermissionService.ResolveAsync(user.TenantId, user.Role, cancellationToken);
    }

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
        var baseSlug = Slugify(companyName);
        var slug = baseSlug;
        var counter = 2;

        while (await dbContext.Tenants.AnyAsync(x => x.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{counter}";
            counter += 1;
        }

        return slug;
    }

    private static string Slugify(string value)
    {
        var normalized = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "tenant" : normalized;
    }

    private async Task ProvisionTenantDefaultsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await licenseGuardService.GetOrCreateTenantLicenseAsync(tenantId, cancellationToken);
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
