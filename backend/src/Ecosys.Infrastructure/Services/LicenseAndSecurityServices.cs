using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
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

/// <summary>
/// Billing frequency is distinct from license validity (see TenantLicense.StartsAt/ExpiresAt) — this
/// only governs how often the tenant is billed. The single definition, replacing what used to be
/// hardcoded independently in PlatformBillingLifecycleService and PlatformLicensingController.
/// </summary>
public static class BillingCycles
{
    public const string Monthly = "Monthly";
    public const string Quarterly = "Quarterly";
    public const string SemiAnnual = "SemiAnnual";
    public const string Annual = "Annual";
    public const string Custom = "Custom";

    private static readonly string[] All = [Monthly, Quarterly, SemiAnnual, Annual, Custom];

    /// <summary>
    /// Approximate cycle lengths in days, used to stack a full-cycle extension and prorate a partial
    /// payment against it. <see cref="Custom"/> has no fixed length — its NextBillingDate is
    /// administrator-supplied rather than computed, so it is deliberately absent here.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, double> FixedCycleDays =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            [Monthly] = 30,
            [Quarterly] = 90,
            [SemiAnnual] = 182,
            [Annual] = 365,
        };

    public static bool TryGetFixedCycleDays(string? billingCycle, out double days) =>
        FixedCycleDays.TryGetValue(billingCycle ?? string.Empty, out days);

    /// <summary>Validates and canonicalizes a billing cycle value, defaulting to Monthly when blank.</summary>
    public static string Normalize(string? billingCycle)
    {
        var candidate = string.IsNullOrWhiteSpace(billingCycle) ? Monthly : billingCycle.Trim();
        var match = Array.Find(All, x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new BusinessRuleException($"Billing cycle must be one of: {string.Join(", ", All)}.");
    }
}

public static class LicenseFeatures
{
    public const string EmailIngestion = "EmailIngestion";
    public const string MonitoringIntegration = "MonitoringIntegration";
    /// <summary>The tenant feature flag <c>whatsapp</c> (<see cref="TenantLicense.WhatsAppEnabled"/>), on any plan.</summary>
    public const string WhatsApp = "WhatsApp";
}

public sealed record LicenseUsageSnapshot(
    int ActiveUsers,
    int ActiveBranches);

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
    int MaxUsers,
    int? MaxBranches,
    bool EmailIngestion,
    bool MonitoringIntegration,
    string? WarningMessage)
{
    public IReadOnlyList<string> Modules { get; init; } = [];

    /// <summary>The tenant feature flag <c>whatsapp</c>: WhatsApp reminders from the shared Ecosys number.</summary>
    public bool WhatsApp { get; init; }
}

public sealed record PlatformLicenseUsageSnapshot(
    Guid TenantId,
    string CompanyName,
    string PlanCode,
    string Status,
    int ActiveUsers,
    int ActiveBranches,
    int MaxUsers,
    int? MaxBranches,
    bool IsReadOnly,
    bool IsSuspended,
    DateTime? ExpiresAt,
    DateTime? GraceEndsAt,
    string PlanName,
    IReadOnlyList<string> Modules,
    DateTime StartsAt,
    DateTime? TrialEndsAt);

public interface ILicenseGuardService
{
    Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default);
    Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The single licensed-seat rule for every path that adds or reactivates workspace users (tenant
    /// create/reactivate, platform add-user, CSV import). Active users — including invitees who have not
    /// signed in yet — consume a seat; deactivated users and client-portal contacts do not. When called
    /// inside a transaction it row-locks the tenant's license so concurrent callers are serialised.
    /// </summary>
    Task EnsureCanAddUsersAsync(Guid tenantId, int count, CancellationToken cancellationToken = default) =>
        count <= 0 ? Task.CompletedTask : EnsureCanCreateUserAsync(tenantId, cancellationToken);
    Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default);
    Task EnsureModuleEnabledAsync(Guid tenantId, string moduleName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
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
        var activeUsers = await dbContext.Users.CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);
        var activeBranches = await dbContext.Branches.CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

        return new LicenseUsageSnapshot(activeUsers, activeBranches);
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
                snapshot.MaxUsers,
                snapshot.MaxBranches,
                snapshot.IsReadOnly,
                snapshot.IsSuspended,
                snapshot.ExpiresAt,
                snapshot.GraceEndsAt,
                snapshot.PlanName,
                snapshot.Modules,
                snapshot.StartsAt,
                snapshot.TrialEndsAt));
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
        EnsureCanAddUsersAsync(tenantId, 1, cancellationToken);

    public async Task EnsureCanAddUsersAsync(Guid tenantId, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return;
        }

        await EnsureTenantCanMutateAsync(tenantId, cancellationToken);
        await LockLicenseRowAsync(tenantId, cancellationToken);

        var snapshot = await GetSnapshotAsync(tenantId, cancellationToken);
        var used = await CountSeatsUsedAsync(tenantId, cancellationToken);
        var limit = snapshot.MaxUsers;
        if (used + count > limit)
        {
            var remaining = Math.Max(0, limit - used);
            throw new LicenseException(
                count == 1
                    ? $"This tenant has reached its licensed user limit of {limit}. {used} seats are in use; deactivate a user or contact Ecosys to add seats."
                    : $"This would exceed the tenant's licensed user limit of {limit}: {count} active users requested but only {remaining} seat{(remaining == 1 ? "" : "s")} remain. Deactivate users or contact Ecosys to add seats.",
                "license_users_limit_reached",
                409);
        }
    }

    /// <summary>Seats in use: every active workspace user of the tenant (pending invitees included).</summary>
    public Task<int> CountSeatsUsedAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        dbContext.Users.CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

    /// <summary>
    /// Serialises seat checks for one tenant. Only meaningful inside a caller-owned transaction on a
    /// relational provider; elsewhere (EF InMemory in unit tests) it is a no-op.
    /// </summary>
    private async Task LockLicenseRowAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational() || dbContext.Database.CurrentTransaction is null)
        {
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM tenant_licenses WHERE \"TenantId\" = {tenantId} FOR UPDATE",
            cancellationToken);
    }

    public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        EnsureWithinLimitAsync(tenantId, "branches", cancellationToken);


    public async Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(tenantId, cancellationToken);
        if (featureName is LicenseFeatures.EmailIngestion or LicenseFeatures.MonitoringIntegration)
            ServiceOpsPlans.EnsureModules(snapshot.Modules, [ServiceOpsPlans.Operations]);
        var enabled = featureName switch
        {
            // Included with Operations (checked just above).
            LicenseFeatures.EmailIngestion => true,
            LicenseFeatures.MonitoringIntegration => snapshot.MonitoringIntegration,
            LicenseFeatures.WhatsApp => snapshot.WhatsApp,
            _ => false
        };

        if (!enabled)
        {
            throw new LicenseException($"The current plan does not include {featureName}.", $"license_feature_{featureName.ToLowerInvariant()}", 403);
        }
    }

    public async Task EnsureModuleEnabledAsync(Guid tenantId, string moduleName, CancellationToken cancellationToken = default)
    {
        var normalized = moduleName.Trim().ToLowerInvariant();
        if (normalized is not (ServiceOpsPlans.Operations or ServiceOpsPlans.Finance or ServiceOpsPlans.Projects))
        {
            throw new BusinessRuleException("Unknown ServiceOps module.");
        }

        var snapshot = await GetSnapshotAsync(tenantId, cancellationToken);
        ServiceOpsPlans.EnsureModules(snapshot.Modules, [normalized]);
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

        // A new workspace starts as a trial of the full Suite with the default licensed-user count. "Trial" is
        // a license status, not a plan; callers that know the real plan/seat count overwrite both.
        var trialPlan = await dbContext.LicensePlans.SingleAsync(x => x.PlanCode == ServiceOpsPlans.Suite, cancellationToken);
        var now = DateTime.UtcNow;
        var trialEndsAt = now.AddDays(14);
        var tenantLicense = new TenantLicense
        {
            TenantId = tenantId,
            LicensePlanId = trialPlan.Id,
            LicensedUsers = TenantLicense.DefaultLicensedUsers,
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
            "branches" => (usage.ActiveBranches, snapshot.MaxBranches, "license_branches_limit_reached", "branches"),
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
        // The grace period only cushions the automatic, date-driven expiry — it must not shield an
        // explicit Expired/TrialExpired status override. Without the isExplicitExpired guard, an
        // operator force-expiring a license ahead of its scheduled ExpiresAt/TrialEndsAt (e.g. for a
        // billing dispute) would have that override silently ignored until the original date, plus
        // the grace period, actually passed — the tenant would keep full write access in the
        // meantime despite being marked Expired.
        var isGrace = !isExplicitExpired && isExpired && graceEndsAt.HasValue && now <= graceEndsAt.Value && !isSuspended && !isCancelled;
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
            tenantLicense.LicensedUsers,
            tenantLicense.MaxBranchesOverride ?? plan.MaxBranches,
            // Email intake comes with the Operations module; there is no separate switch for it.
            ServiceOpsPlans.Modules(plan).Contains(ServiceOpsPlans.Operations),
            tenantLicense.MonitoringIntegrationOverride ?? plan.MonitoringIntegration,
            warning) { Modules = ServiceOpsPlans.Modules(plan), WhatsApp = tenantLicense.WhatsAppEnabled };
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
        tenantLicense.OverdueWarningSentAt = EnsureUtc(tenantLicense.OverdueWarningSentAt);
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
        var snapshot = await licenseGuardService.GetSnapshotAsync(tenantId, context.RequestAborted);
        if (!string.IsNullOrWhiteSpace(snapshot.WarningMessage))
        {
            context.Response.Headers["X-License-Warning"] = snapshot.WarningMessage;
        }

        var isLicenseReadEndpoint =
            path.StartsWith("/api/tenant/license", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/settings/license", StringComparison.OrdinalIgnoreCase);

        // One business state, one message: a suspended tenant (operator or non-payment) always gets
        // license_suspended — which the frontend shows once as a banner — and can still read its license.
        // Only a deactivated (cancelled) tenant gets the deactivated error.
        if (snapshot.IsSuspended)
        {
            if (!isLicenseReadEndpoint)
            {
                throw new LicenseException("Tenant license is suspended. Contact Ecosys to restore access.", "license_suspended", 403);
            }
        }
        else if (!tenantIsActive)
        {
            throw new ForbiddenException("Tenant access is deactivated. Contact Ecosys platform support.");
        }

        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
        ServiceOpsPlans.EnsureModules(snapshot.Modules, ServiceOpsPlans.RequiredModules(action?.ControllerName, path));

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
