using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public static class LicenseStatuses
{
    public const string Trial = "Trial";
    public const string TrialExpiringSoon = "TrialExpiringSoon";
    public const string TrialExpired = "TrialExpired";
    public const string Active = "Active";
    public const string Expired = "Expired";
    public const string Suspended = "Suspended";
    public const string Cancelled = "Cancelled";

    public static bool IsTrialStatus(string? status) =>
        string.Equals(status, Trial, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, TrialExpiringSoon, StringComparison.OrdinalIgnoreCase);
}

public static class LicenseFeatures
{
    public const string EmailIngestion = "EmailIngestion";
    public const string MonitoringIntegration = "MonitoringIntegration";
    public const string AdvancedReports = "AdvancedReports";
    public const string ClientPortal = "ClientPortal";
}

public sealed record LicenseUsageSnapshot(
    int ActiveUsers,
    int ActiveBranches,
    int ActiveAssets,
    int MonthlyWorkOrders);

public sealed record TenantLicenseSnapshot(
    Guid TenantId,
    Guid LicensePlanId,
    string PlanCode,
    string PlanName,
    string Status,
    DateTime StartsAt,
    DateTime? TrialEndsAt,
    DateTime? ExpiresAt,
    DateTime? GraceEndsAt,
    bool IsReadOnly,
    bool IsSuspended,
    bool IsGracePeriod,
    int? MaxUsers,
    int? MaxBranches,
    int? MaxAssets,
    int? MonthlyWorkOrders,
    bool EmailIngestion,
    bool MonitoringIntegration,
    bool AdvancedReports,
    bool ClientPortal,
    string? WarningMessage);

public sealed record PlatformLicenseUsageSnapshot(
    Guid TenantId,
    string CompanyName,
    string PlanCode,
    string Status,
    int ActiveUsers,
    int ActiveBranches,
    int ActiveAssets,
    int MonthlyWorkOrders,
    int? MaxUsers,
    int? MaxBranches,
    int? MaxAssets,
    int? MonthlyWorkOrdersLimit,
    bool IsReadOnly,
    bool IsSuspended,
    DateTime? ExpiresAt,
    DateTime? GraceEndsAt);

public interface ILicenseGuardService
{
    Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default);
    Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureCanCreateAssetAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureCanCreateWorkOrderAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default);
    Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public interface ITenantSecurityPolicyService
{
    Task<TenantSecurityPolicy> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task ValidatePasswordAsync(Guid tenantId, string password, CancellationToken cancellationToken = default);
    void ValidatePassword(string password, TenantSecurityPolicy policy);
}

internal sealed class LicenseGuardService(AppDbContext dbContext) : ILicenseGuardService
{
    public async Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenantLicense = await LoadLicenseAsync(tenantId, cancellationToken);
        var usage = await GetUsageAsync(tenantId, cancellationToken);
        return BuildSnapshot(tenantLicense, usage);
    }

    public async Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var activeUsers = await dbContext.Users.CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);
        var activeBranches = await dbContext.Branches.CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);
        var activeAssets = await dbContext.Assets.CountAsync(x => x.TenantId == tenantId && x.Status != "Inactive", cancellationToken);
        var monthlyWorkOrders = await dbContext.WorkOrders.CountAsync(x => x.TenantId == tenantId && x.CreatedAt >= monthStart, cancellationToken);

        return new LicenseUsageSnapshot(activeUsers, activeBranches, activeAssets, monthlyWorkOrders);
    }

    public async Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default)
    {
        var licenses = await dbContext.TenantLicenses
            .Include(x => x.Tenant)
            .Include(x => x.LicensePlan)
            .Where(x => x.TenantId != PlatformConstants.RootTenantId)
            .OrderBy(x => x.Tenant!.CompanyName)
            .ToListAsync(cancellationToken);

        var snapshots = new List<PlatformLicenseUsageSnapshot>(licenses.Count);
        foreach (var tenantLicense in licenses)
        {
            var usage = await GetUsageAsync(tenantLicense.TenantId, cancellationToken);
            var snapshot = BuildSnapshot(tenantLicense, usage);
            snapshots.Add(new PlatformLicenseUsageSnapshot(
                tenantLicense.TenantId,
                tenantLicense.Tenant?.CompanyName ?? string.Empty,
                snapshot.PlanCode,
                snapshot.Status,
                usage.ActiveUsers,
                usage.ActiveBranches,
                usage.ActiveAssets,
                usage.MonthlyWorkOrders,
                snapshot.MaxUsers,
                snapshot.MaxBranches,
                snapshot.MaxAssets,
                snapshot.MonthlyWorkOrders,
                snapshot.IsReadOnly,
                snapshot.IsSuspended,
                snapshot.ExpiresAt,
                snapshot.GraceEndsAt));
        }

        return snapshots;
    }

    public async Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(tenantId, cancellationToken);
        if (snapshot.IsSuspended)
        {
            throw new LicenseException("Tenant license is suspended. Contact Ecosys to restore access.", "license_suspended", 403);
        }

        if (snapshot.IsReadOnly)
        {
            throw new LicenseException("Tenant license has expired and the workspace is now read-only.", "license_read_only", 403);
        }
    }

    public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        EnsureWithinLimitAsync(tenantId, "users", cancellationToken);

    public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        EnsureWithinLimitAsync(tenantId, "branches", cancellationToken);

    public Task EnsureCanCreateAssetAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        EnsureWithinLimitAsync(tenantId, "assets", cancellationToken);

    public Task EnsureCanCreateWorkOrderAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        EnsureWithinLimitAsync(tenantId, "workorders", cancellationToken);

    public async Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(tenantId, cancellationToken);
        var enabled = featureName switch
        {
            LicenseFeatures.EmailIngestion => snapshot.EmailIngestion,
            LicenseFeatures.MonitoringIntegration => snapshot.MonitoringIntegration,
            LicenseFeatures.AdvancedReports => snapshot.AdvancedReports,
            LicenseFeatures.ClientPortal => snapshot.ClientPortal,
            _ => false
        };

        if (!enabled)
        {
            throw new LicenseException($"The current plan does not include {featureName}.", $"license_feature_{featureName.ToLowerInvariant()}", 403);
        }
    }

    public async Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.TenantLicenses
            .Include(x => x.LicensePlan)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (existing is not null)
        {
            NormalizeUtc(existing);
            return existing;
        }

        var trialPlan = await dbContext.LicensePlans.SingleAsync(x => x.PlanCode == LicenseStatuses.Trial, cancellationToken);
        var now = DateTime.UtcNow;
        var trialEndsAt = now.AddDays(14);
        var tenantLicense = new TenantLicense
        {
            TenantId = tenantId,
            LicensePlanId = trialPlan.Id,
            Status = LicenseStatuses.Trial,
            StartsAt = now,
            TrialEndsAt = trialEndsAt,
            ExpiresAt = trialEndsAt,
            BillingCycle = "Monthly",
            NextBillingDate = trialEndsAt
        };

        NormalizeUtc(tenantLicense);
        dbContext.TenantLicenses.Add(tenantLicense);
        await dbContext.SaveChangesAsync(cancellationToken);
        tenantLicense.LicensePlan = trialPlan;
        return tenantLicense;
    }

    private async Task EnsureWithinLimitAsync(Guid tenantId, string resourceType, CancellationToken cancellationToken)
    {
        await EnsureTenantCanMutateAsync(tenantId, cancellationToken);

        var snapshot = await GetSnapshotAsync(tenantId, cancellationToken);
        var usage = await GetUsageAsync(tenantId, cancellationToken);

        (int current, int? limit, string code, string label) = resourceType switch
        {
            "users" => (usage.ActiveUsers, snapshot.MaxUsers, "license_users_limit_reached", "users"),
            "branches" => (usage.ActiveBranches, snapshot.MaxBranches, "license_branches_limit_reached", "branches"),
            "assets" => (usage.ActiveAssets, snapshot.MaxAssets, "license_assets_limit_reached", "assets"),
            "workorders" => (usage.MonthlyWorkOrders, snapshot.MonthlyWorkOrders, "license_monthly_workorders_limit_reached", "monthly work orders"),
            _ => (0, null, "license_limit_reached", resourceType)
        };

        if (limit.HasValue && current >= limit.Value)
        {
            throw new LicenseException($"The current plan limit for {label} has been reached.", code, 409);
        }
    }

    private async Task<TenantLicense> LoadLicenseAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.TenantLicenses
            .Include(x => x.LicensePlan)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
        ?? await GetOrCreateTenantLicenseAsync(tenantId, cancellationToken);

    private static TenantLicenseSnapshot BuildSnapshot(TenantLicense tenantLicense, LicenseUsageSnapshot usage)
    {
        NormalizeUtc(tenantLicense);
        var plan = tenantLicense.LicensePlan ?? throw new InvalidOperationException("Tenant license plan was not loaded.");
        var now = DateTime.UtcNow;
        var effectiveExpiry = tenantLicense.ExpiresAt ?? tenantLicense.TrialEndsAt;
        var graceEndsAt = effectiveExpiry?.AddDays(tenantLicense.GracePeriodDays);
        var isSuspended = string.Equals(tenantLicense.Status, LicenseStatuses.Suspended, StringComparison.OrdinalIgnoreCase);
        var isCancelled = string.Equals(tenantLicense.Status, LicenseStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);
        var isExplicitExpired = string.Equals(tenantLicense.Status, LicenseStatuses.Expired, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tenantLicense.Status, LicenseStatuses.TrialExpired, StringComparison.OrdinalIgnoreCase);
        var isExpired = isExplicitExpired || (effectiveExpiry.HasValue && now > effectiveExpiry.Value);
        var isGrace = isExpired && graceEndsAt.HasValue && now <= graceEndsAt.Value && !isSuspended && !isCancelled;
        var isReadOnly = (isExpired && !isGrace) || isCancelled;

        var warning = isSuspended
            ? "License is suspended."
            : isReadOnly
                ? "License has expired. Tenant is in read-only mode."
                : isGrace
                    ? $"License expired. Grace period ends on {graceEndsAt:yyyy-MM-dd}."
                    : effectiveExpiry.HasValue && effectiveExpiry.Value <= now.AddDays(7)
                        ? $"License expires on {effectiveExpiry:yyyy-MM-dd}."
                        : null;

        return new TenantLicenseSnapshot(
            tenantLicense.TenantId,
            tenantLicense.LicensePlanId,
            plan.PlanCode,
            plan.DisplayName,
            isSuspended ? LicenseStatuses.Suspended : isCancelled ? LicenseStatuses.Cancelled : isReadOnly ? LicenseStatuses.Expired : tenantLicense.Status,
            tenantLicense.StartsAt,
            tenantLicense.TrialEndsAt,
            tenantLicense.ExpiresAt,
            graceEndsAt,
            isReadOnly,
            isSuspended,
            isGrace,
            tenantLicense.MaxUsersOverride ?? plan.MaxUsers,
            tenantLicense.MaxBranchesOverride ?? plan.MaxBranches,
            tenantLicense.MaxAssetsOverride ?? plan.MaxAssets,
            tenantLicense.MonthlyWorkOrdersOverride ?? plan.MonthlyWorkOrders,
            tenantLicense.EmailIngestionOverride ?? plan.EmailIngestion,
            tenantLicense.MonitoringIntegrationOverride ?? plan.MonitoringIntegration,
            tenantLicense.AdvancedReportsOverride ?? plan.AdvancedReports,
            tenantLicense.ClientPortalOverride ?? plan.ClientPortal,
            warning);
    }

    private static void NormalizeUtc(TenantLicense tenantLicense)
    {
        tenantLicense.CreatedAt = EnsureUtc(tenantLicense.CreatedAt);
        tenantLicense.UpdatedAt = EnsureUtc(tenantLicense.UpdatedAt);
        tenantLicense.StartsAt = EnsureUtc(tenantLicense.StartsAt);
        tenantLicense.TrialEndsAt = EnsureUtc(tenantLicense.TrialEndsAt);
        tenantLicense.ExpiresAt = EnsureUtc(tenantLicense.ExpiresAt);
        tenantLicense.NextBillingDate = EnsureUtc(tenantLicense.NextBillingDate);
        tenantLicense.SuspendedAt = EnsureUtc(tenantLicense.SuspendedAt);
        tenantLicense.CancelledAt = EnsureUtc(tenantLicense.CancelledAt);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.ToUniversalTime();
    }

    private static DateTime? EnsureUtc(DateTime? value) =>
        value.HasValue ? EnsureUtc(value.Value) : null;
}

internal sealed class TenantSecurityPolicyService(AppDbContext dbContext) : ITenantSecurityPolicyService
{
    public async Task<TenantSecurityPolicy> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var policy = await dbContext.TenantSecurityPolicies.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (policy is not null)
        {
            return policy;
        }

        policy = new TenantSecurityPolicy
        {
            TenantId = tenantId
        };
        dbContext.TenantSecurityPolicies.Add(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public async Task ValidatePasswordAsync(Guid tenantId, string password, CancellationToken cancellationToken = default)
    {
        var policy = await GetOrCreateAsync(tenantId, cancellationToken);
        ValidatePassword(password, policy);
    }

    public void ValidatePassword(string password, TenantSecurityPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessRuleException("Password is required.");
        }

        if (password.Length < policy.MinPasswordLength)
        {
            throw new BusinessRuleException($"Password must be at least {policy.MinPasswordLength} characters long.");
        }

        if (policy.RequireUppercase && !password.Any(char.IsUpper))
        {
            throw new BusinessRuleException("Password must contain an uppercase letter.");
        }

        if (policy.RequireLowercase && !password.Any(char.IsLower))
        {
            throw new BusinessRuleException("Password must contain a lowercase letter.");
        }

        if (policy.RequireDigit && !password.Any(char.IsDigit))
        {
            throw new BusinessRuleException("Password must contain a number.");
        }

        if (policy.RequireSpecialCharacter && !password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            throw new BusinessRuleException("Password must contain a special character.");
        }
    }
}

public sealed class TenantLicenseEnforcementMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> ExemptPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth",
        "/api/platform",
        "/api/health",
        "/health"
    };

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ILicenseGuardService licenseGuardService, AppDbContext dbContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (ExemptPrefixes.Any(path.StartsWith))
        {
            await next(context);
            return;
        }

        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) || !tenantContext.IsAuthenticated || tenantContext.IsSuperAdmin)
        {
            await next(context);
            return;
        }

        var tenantId = tenantContext.GetRequiredTenantId();
        var tenantIsActive = await dbContext.Tenants
            .Where(x => x.Id == tenantId)
            .Select(x => x.IsActive)
            .SingleOrDefaultAsync(context.RequestAborted);
        if (!tenantIsActive)
        {
            throw new ForbiddenException("Tenant access is deactivated. Contact Ecosys platform support.");
        }

        var snapshot = await licenseGuardService.GetSnapshotAsync(tenantId, context.RequestAborted);
        if (!string.IsNullOrWhiteSpace(snapshot.WarningMessage))
        {
            context.Response.Headers["X-License-Warning"] = snapshot.WarningMessage;
        }

        var isLicenseReadEndpoint =
            path.StartsWith("/api/tenant/license", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/settings/license", StringComparison.OrdinalIgnoreCase);

        if (snapshot.IsSuspended && !isLicenseReadEndpoint)
        {
            throw new LicenseException("Tenant license is suspended. Contact Ecosys to restore access.", "license_suspended", 403);
        }

        if (!HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method)
            && !isLicenseReadEndpoint)
        {
            await licenseGuardService.EnsureTenantCanMutateAsync(tenantId, context.RequestAborted);
        }

        await next(context);
    }
}
