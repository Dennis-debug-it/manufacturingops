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
    /// <summary>Any WhatsApp add-on (<see cref="TenantLicense.WhatsAppAddOn"/>), on any edition.</summary>
    public const string WhatsApp = "WhatsApp";
}

/// <summary>
/// What a license is worth per month: office seats and field seats at the edition's prices, plus the
/// WhatsApp add-on. An annual license counts its annual price spread over twelve months; a price left at
/// zero for the annual cycle falls back to the monthly price.
/// </summary>
public static class LicensePricing
{
    public static decimal MonthlyValue(TenantLicense license, IReadOnlyCollection<LicenseAddOn> addOns)
    {
        var plan = license.LicensePlan ?? throw new InvalidOperationException("Tenant license plan was not loaded.");
        var annual = string.Equals(license.BillingCycle, BillingCycles.Annual, StringComparison.OrdinalIgnoreCase);
        decimal PerMonth(decimal monthly, decimal yearly) => annual && yearly > 0 ? yearly / 12m : monthly;

        var value = license.OfficeSeats * PerMonth(plan.OfficeSeatMonthlyPrice, plan.OfficeSeatAnnualPrice)
            + license.FieldSeats * PerMonth(plan.FieldSeatMonthlyPrice, plan.FieldSeatAnnualPrice);
        if (addOns.FirstOrDefault(x => x.Code == license.WhatsAppAddOn) is { } addOn)
        {
            value += PerMonth(addOn.MonthlyPrice, addOn.AnnualPrice);
        }

        return decimal.Round(value, 2);
    }

    public static async Task<decimal> MonthlyRecurringRevenueAsync(
        AppDbContext dbContext,
        System.Linq.Expressions.Expression<Func<TenantLicense, bool>> filter,
        CancellationToken cancellationToken)
    {
        var addOns = await dbContext.LicenseAddOns.AsNoTracking().ToListAsync(cancellationToken);
        var licenses = await dbContext.TenantLicenses.AsNoTracking().Include(x => x.LicensePlan)
            .Where(x => x.TenantId != PlatformConstants.RootTenantId)
            .Where(filter)
            .ToListAsync(cancellationToken);
        return licenses.Sum(x => MonthlyValue(x, addOns));
    }
}

/// <summary>The seat counts a license may hold on an edition.</summary>
public static class SeatRules
{
    public static void Validate(LicensePlan plan, int officeSeats, int fieldSeats)
    {
        var minimum = Math.Max(1, plan.MinOfficeSeats);
        if (officeSeats < minimum)
        {
            throw new BusinessRuleException($"{plan.DisplayName} needs at least {minimum} office seat{(minimum == 1 ? "" : "s")}.");
        }

        if (fieldSeats < 0)
        {
            throw new BusinessRuleException("Field seats cannot be negative.");
        }
    }
}

/// <summary>
/// Which seat a workspace role takes. Technicians take a field seat; Viewers (and the legacy User role)
/// are free; every other workspace role takes an office seat. Client-portal contacts are not users.
/// </summary>
public static class SeatTypes
{
    public const string Office = "Office";
    public const string Field = "Field";
    public const string Free = "Free";

    public static string For(string? role) =>
        string.Equals(role, AppRoles.Technician, StringComparison.OrdinalIgnoreCase) ? Field
        : string.Equals(role, AppRoles.Viewer, StringComparison.OrdinalIgnoreCase) || string.Equals(role, AppRoles.User, StringComparison.OrdinalIgnoreCase) ? Free
        : Office;
}

public sealed record LicenseUsageSnapshot(
    int ActiveUsers,
    int ActiveBranches)
{
    public int OfficeSeatsUsed { get; init; }
    public int FieldSeatsUsed { get; init; }
}

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
    int OfficeSeats,
    int FieldSeats,
    int? MaxBranches,
    bool EmailIngestion,
    bool MonitoringIntegration,
    string? WarningMessage)
{
    public IReadOnlyList<string> Modules { get; init; } = [];

    /// <summary>The WhatsApp add-on code, or null when the tenant has none.</summary>
    public string? WhatsAppAddOn { get; init; }

    /// <summary>Any WhatsApp add-on is on: reminders from the shared Ecosys number may be sent.</summary>
    public bool WhatsApp => WhatsAppAddOn is not null;
}

public sealed record PlatformLicenseUsageSnapshot(
    Guid TenantId,
    string CompanyName,
    string PlanCode,
    string Status,
    int ActiveUsers,
    int ActiveBranches,
    int OfficeSeats,
    int FieldSeats,
    int OfficeSeatsUsed,
    int FieldSeatsUsed,
    int? MaxBranches,
    bool IsReadOnly,
    bool IsSuspended,
    DateTime? ExpiresAt,
    DateTime? GraceEndsAt,
    string PlanName,
    IReadOnlyList<string> Modules,
    DateTime StartsAt,
    DateTime? TrialEndsAt,
    string? WhatsAppAddOn);

public interface ILicenseGuardService
{
    Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default);
    Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The single seat rule for every path that adds, reactivates or changes the role of workspace users
    /// (tenant create/reactivate/role change, platform add-user, CSV import). <paramref name="roles"/> are
    /// the roles of the users about to take a seat; each checks the office or field pool its role uses
    /// (<see cref="SeatTypes"/>). Active users — including invitees who have not signed in yet — consume a
    /// seat; deactivated users, Viewers and client-portal contacts do not. When called inside a
    /// transaction it row-locks the tenant's license so concurrent callers are serialised.
    /// </summary>
    Task EnsureSeatsAvailableAsync(Guid tenantId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default) =>
        roles.Count == 0 ? Task.CompletedTask : EnsureCanCreateUserAsync(tenantId, cancellationToken);
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
        var (officeUsed, fieldUsed) = await CountSeatsUsedAsync(tenantId, cancellationToken);

        return new LicenseUsageSnapshot(activeUsers, activeBranches) { OfficeSeatsUsed = officeUsed, FieldSeatsUsed = fieldUsed };
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
                snapshot.OfficeSeats,
                snapshot.FieldSeats,
                usage.OfficeSeatsUsed,
                usage.FieldSeatsUsed,
                snapshot.MaxBranches,
                snapshot.IsReadOnly,
                snapshot.IsSuspended,
                snapshot.ExpiresAt,
                snapshot.GraceEndsAt,
                snapshot.PlanName,
                snapshot.Modules,
                snapshot.StartsAt,
                snapshot.TrialEndsAt,
                snapshot.WhatsAppAddOn));
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
        EnsureSeatsAvailableAsync(tenantId, [AppRoles.Admin], cancellationToken);

    public async Task EnsureSeatsAvailableAsync(Guid tenantId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        var officeRequested = roles.Count(x => SeatTypes.For(x) == SeatTypes.Office);
        var fieldRequested = roles.Count(x => SeatTypes.For(x) == SeatTypes.Field);
        if (officeRequested == 0 && fieldRequested == 0)
        {
            return;
        }

        await EnsureTenantCanMutateAsync(tenantId, cancellationToken);
        await LockLicenseRowAsync(tenantId, cancellationToken);

        var snapshot = await GetSnapshotAsync(tenantId, cancellationToken);
        var (officeUsed, fieldUsed) = await CountSeatsUsedAsync(tenantId, cancellationToken);
        EnsurePool("office", officeRequested, officeUsed, snapshot.OfficeSeats, "license_office_seats_reached");
        EnsurePool("field", fieldRequested, fieldUsed, snapshot.FieldSeats, "license_field_seats_reached");
    }

    private static void EnsurePool(string pool, int requested, int used, int limit, string code)
    {
        if (requested == 0 || used + requested <= limit)
        {
            return;
        }

        var remaining = Math.Max(0, limit - used);
        throw new LicenseException(
            requested == 1
                ? $"This workspace has used all {limit} of its {pool} seats. Deactivate a user or contact Ecosys to add {pool} seats."
                : $"This would exceed the workspace's {limit} {pool} seats: {requested} requested but only {remaining} remain. Deactivate users or contact Ecosys to add {pool} seats.",
            code,
            409);
    }

    /// <summary>Seats in use: active workspace users (pending invitees included), by seat type.</summary>
    public async Task<(int Office, int Field)> CountSeatsUsedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var byRole = await dbContext.Users
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .GroupBy(x => x.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        return (
            byRole.Where(x => SeatTypes.For(x.Role) == SeatTypes.Office).Sum(x => x.Count),
            byRole.Where(x => SeatTypes.For(x.Role) == SeatTypes.Field).Sum(x => x.Count));
    }

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
        if (featureName is LicenseFeatures.EmailIngestion)
            ServiceOpsPlans.EnsureModules(snapshot.Modules, [ServiceOpsPlans.Advanced]);
        var enabled = featureName switch
        {
            // Included with the Advanced module (checked just above).
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
        if (!ServiceOpsPlans.EditionModules(ServiceOpsPlans.Enterprise).Contains(normalized) && normalized != ServiceOpsPlans.WhatsAppBusiness)
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

        // A new workspace starts as a trial of Professional with the default seats. "Trial" is a license
        // status, not an edition; callers that know the real edition/seats overwrite both.
        var trialPlan = await dbContext.LicensePlans.SingleAsync(x => x.PlanCode == ServiceOpsPlans.Professional, cancellationToken);
        var now = DateTime.UtcNow;
        var trialEndsAt = now.AddDays(14);
        var tenantLicense = new TenantLicense
        {
            TenantId = tenantId,
            LicensePlanId = trialPlan.Id,
            OfficeSeats = TenantLicense.DefaultOfficeSeats,
            FieldSeats = TenantLicense.DefaultFieldSeats,
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
        var modules = ServiceOpsPlans.Modules(plan, tenantLicense.WhatsAppAddOn);
        var isEnterprise = modules.Contains(ServiceOpsPlans.EnterpriseModule);
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
            tenantLicense.OfficeSeats,
            tenantLicense.FieldSeats,
            // Enterprise has unlimited branches; every other edition has one unless the platform overrides it.
            tenantLicense.MaxBranchesOverride ?? (isEnterprise ? null : 1),
            // Email intake comes with the Advanced module; there is no separate switch for it.
            modules.Contains(ServiceOpsPlans.Advanced),
            tenantLicense.MonitoringIntegrationOverride ?? isEnterprise,
            warning) { Modules = modules, WhatsAppAddOn = tenantLicense.WhatsAppAddOn };
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
