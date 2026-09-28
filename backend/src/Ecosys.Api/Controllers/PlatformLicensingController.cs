using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformAdminAccess")]
[Route("api/platform/licensing")]
public sealed class PlatformLicensingController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    ILicenseGuardService licenseGuardService) : ControllerBase
{
    [HttpGet("usage")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformLicenseUsageSnapshot>>> GetUsage(CancellationToken cancellationToken)
    {
        var snapshots = await licenseGuardService.GetPlatformUsageAsync(cancellationToken);
        return Ok(snapshots);
    }

    [HttpGet("plans")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformLicensePlanResponse>>> GetPlans(CancellationToken cancellationToken)
    {
        var plans = await dbContext.LicensePlans
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        return Ok(plans.Select(MapPlan).ToList());
    }

    [HttpPost("plans")]
    public async Task<ActionResult<PlatformLicensePlanResponse>> CreatePlan([FromBody] UpsertPlatformLicensePlanRequest request, CancellationToken cancellationToken)
    {
        ValidatePlanRequest(request);

        var normalizedCode = request.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ServiceOpsPlans.IsCommercial(normalizedCode))
            throw new BusinessRuleException("Select ServiceOps Operations, Finance, or Suite.");

        var exists = await dbContext.LicensePlans.AnyAsync(x => x.PlanCode.ToLower() == normalizedCode.ToLower(), cancellationToken);
        if (exists)
        {
            throw new BusinessRuleException("Plan code already exists.");
        }

        var plan = new LicensePlan
        {
            PlanCode = normalizedCode,
            DisplayName = ServiceOpsPlans.DisplayName(normalizedCode),
            MonthlyPrice = request.MonthlyPrice,
            AnnualPrice = request.AnnualPrice,
            MaxUsers = request.MaxUsers,
            MaxBranches = request.MaxBranches,
            ModulesIncluded = NormalizePlanModules(normalizedCode, request.ModulesIncluded),
            IsActive = request.IsActive,
            EmailIngestion = HasModule(request.ModulesIncluded, "EmailIngestion"),
            MonitoringIntegration = HasModule(request.ModulesIncluded, "MonitoringIntegration")
        };

        dbContext.LicensePlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            "platform.license.plan.created",
            nameof(LicensePlan),
            plan.Id.ToString(),
            $"License plan '{plan.DisplayName}' created.",
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(GetPlans), new { id = plan.Id }, MapPlan(plan));
    }

    [HttpPut("plans/{id:guid}")]
    public async Task<ActionResult<PlatformLicensePlanResponse>> UpdatePlan(Guid id, [FromBody] UpsertPlatformLicensePlanRequest request, CancellationToken cancellationToken)
    {
        ValidatePlanRequest(request);

        var plan = await dbContext.LicensePlans.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("License plan was not found.");

        plan.DisplayName = ServiceOpsPlans.IsCommercial(plan.PlanCode) ? ServiceOpsPlans.DisplayName(plan.PlanCode) : request.Name.Trim();
        plan.MonthlyPrice = request.MonthlyPrice;
        plan.AnnualPrice = request.AnnualPrice;
        plan.MaxUsers = request.MaxUsers;
        plan.MaxBranches = request.MaxBranches;
        plan.ModulesIncluded = NormalizePlanModules(plan.PlanCode, request.ModulesIncluded);
        plan.IsActive = ServiceOpsPlans.IsCommercial(plan.PlanCode) && request.IsActive;
        plan.EmailIngestion = HasModule(request.ModulesIncluded, "EmailIngestion");
        plan.MonitoringIntegration = HasModule(request.ModulesIncluded, "MonitoringIntegration");

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            "platform.license.plan.updated",
            nameof(LicensePlan),
            plan.Id.ToString(),
            $"License plan '{plan.DisplayName}' updated.",
            cancellationToken: cancellationToken);

        return Ok(MapPlan(plan));
    }

    [HttpGet("subscriptions")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformSubscriptionResponse>>> GetSubscriptions(CancellationToken cancellationToken)
    {
        var subscriptions = await dbContext.TenantLicenses
            .Include(x => x.Tenant)
            .Include(x => x.LicensePlan)
            .Where(x => x.TenantId != PlatformConstants.RootTenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(subscriptions.Select(MapSubscription).ToList());
    }

    [HttpGet("subscriptions/{id:guid}")]
    public async Task<ActionResult<PlatformSubscriptionResponse>> GetSubscription(Guid id, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.TenantLicenses
            .Include(x => x.Tenant)
            .Include(x => x.LicensePlan)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Subscription was not found.");

        return Ok(MapSubscription(subscription));
    }

    [HttpPost("subscriptions")]
    public async Task<ActionResult<PlatformSubscriptionResponse>> CreateSubscription([FromBody] UpsertPlatformSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");
        if (request.TenantId == PlatformConstants.RootTenantId)
            throw new BusinessRuleException("The platform root tenant cannot receive a commercial ServiceOps subscription.");

        var plan = await dbContext.LicensePlans.SingleOrDefaultAsync(x => x.Id == request.PlanId, cancellationToken)
            ?? throw new NotFoundException("License plan was not found.");
        if (!plan.IsActive || !ServiceOpsPlans.IsCommercial(plan.PlanCode))
            throw new BusinessRuleException("Only active commercial ServiceOps plans can be assigned.");

        var existing = await dbContext.TenantLicenses.SingleOrDefaultAsync(x => x.TenantId == request.TenantId, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessRuleException("Tenant already has a subscription. Use update instead.");
        }

        ValidateSubscriptionDates(request);

        var subscription = new TenantLicense
        {
            TenantId = request.TenantId,
            LicensePlanId = request.PlanId,
            Status = NormalizeSubscriptionStatus(request.Status),
            BillingCycle = NormalizeBillingCycle(request.BillingCycle),
            StartsAt = request.StartsAt,
            ExpiresAt = request.EndsAt,
            TrialEndsAt = request.TrialEndsAt,
            NextBillingDate = request.NextBillingDate,
            LicensePlan = plan,
            Tenant = tenant
        };

        if (subscription.Status == LicenseStatuses.Suspended)
        {
            TenantLifecycleState.Suspend(subscription, LicenseSuspensionReasons.Manual, DateTime.UtcNow);
        }

        dbContext.TenantLicenses.Add(subscription);
        TenantLifecycleState.Sync(tenant, subscription, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            request.TenantId,
            tenantContext.GetRequiredUserId(),
            "platform.subscription.created",
            nameof(TenantLicense),
            subscription.Id.ToString(),
            $"Subscription for tenant '{tenant.Name}' created.",
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(GetSubscription), new { id = subscription.Id }, MapSubscription(subscription));
    }

    [HttpPut("subscriptions/{id:guid}")]
    public async Task<ActionResult<PlatformSubscriptionResponse>> UpdateSubscription(Guid id, [FromBody] UpsertPlatformSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.TenantLicenses
            .Include(x => x.Tenant)
            .Include(x => x.LicensePlan)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Subscription was not found.");

        var plan = await dbContext.LicensePlans.SingleOrDefaultAsync(x => x.Id == request.PlanId, cancellationToken)
            ?? throw new NotFoundException("License plan was not found.");
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == request.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");
        if (request.TenantId == PlatformConstants.RootTenantId)
            throw new BusinessRuleException("The platform root tenant cannot receive a commercial ServiceOps subscription.");
        if (!plan.IsActive || !ServiceOpsPlans.IsCommercial(plan.PlanCode))
            throw new BusinessRuleException("Only active commercial ServiceOps plans can be assigned.");

        if (request.TenantId != subscription.TenantId)
        {
            // A license row is the tenant's entitlement record; re-pointing it would silently strip one
            // tenant's access and hand it to another.
            throw new BusinessRuleException("A subscription cannot be moved to a different tenant.");
        }

        ValidateSubscriptionDates(request);

        subscription.LicensePlanId = request.PlanId;
        subscription.LicensePlan = plan;
        subscription.Tenant = tenant;
        ApplyStatus(subscription, NormalizeSubscriptionStatus(request.Status), DateTime.UtcNow);
        subscription.BillingCycle = NormalizeBillingCycle(request.BillingCycle);
        subscription.StartsAt = request.StartsAt;
        subscription.ExpiresAt = request.EndsAt;
        subscription.TrialEndsAt = request.TrialEndsAt;
        subscription.NextBillingDate = request.NextBillingDate;
        TenantLifecycleState.Sync(tenant, subscription, DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            subscription.TenantId,
            tenantContext.GetRequiredUserId(),
            "platform.subscription.updated",
            nameof(TenantLicense),
            subscription.Id.ToString(),
            $"Subscription '{subscription.Id}' updated.",
            cancellationToken: cancellationToken);

        return Ok(MapSubscription(subscription));
    }

    [HttpPost("subscriptions/{id:guid}/activate")]
    public Task<ActionResult<PlatformSubscriptionResponse>> Activate(Guid id, CancellationToken cancellationToken) =>
        UpdateSubscriptionStatus(id, "Active", cancellationToken);

    [HttpPost("subscriptions/{id:guid}/suspend")]
    public Task<ActionResult<PlatformSubscriptionResponse>> Suspend(Guid id, CancellationToken cancellationToken) =>
        UpdateSubscriptionStatus(id, "Suspended", cancellationToken);

    [HttpPost("subscriptions/{id:guid}/cancel")]
    public Task<ActionResult<PlatformSubscriptionResponse>> Cancel(Guid id, CancellationToken cancellationToken) =>
        UpdateSubscriptionStatus(id, "Cancelled", cancellationToken);

    private async Task<ActionResult<PlatformSubscriptionResponse>> UpdateSubscriptionStatus(Guid id, string status, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.TenantLicenses
            .Include(x => x.Tenant)
            .Include(x => x.LicensePlan)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Subscription was not found.");

        var now = DateTime.UtcNow;
        ApplyStatus(subscription, NormalizeSubscriptionStatus(status), now);

        // Same projection every other lifecycle writer uses (TM-P1-02): Status, IsActive and
        // LicenseStatus on the tenant row all follow the license.
        if (subscription.Tenant is { } tenant)
        {
            TenantLifecycleState.Sync(tenant, subscription, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            subscription.TenantId,
            tenantContext.GetRequiredUserId(),
            $"platform.subscription.{status.ToLowerInvariant()}",
            nameof(TenantLicense),
            subscription.Id.ToString(),
            $"Subscription status changed to {status}.",
            cancellationToken: cancellationToken);

        return Ok(MapSubscription(subscription));
    }

    private static void ApplyStatus(TenantLicense subscription, string status, DateTime now)
    {
        if (string.Equals(subscription.Status, status, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (status == LicenseStatuses.Suspended)
        {
            TenantLifecycleState.Suspend(subscription, LicenseSuspensionReasons.Manual, now);
            return;
        }

        subscription.Status = status;
        subscription.SuspendedAt = null;
        subscription.SuspensionReason = null;
        subscription.CancelledAt = status == LicenseStatuses.Cancelled ? now : null;
    }

    private static PlatformLicensePlanResponse MapPlan(LicensePlan plan) =>
        new(
            plan.Id,
            plan.DisplayName,
            plan.PlanCode,
            plan.MonthlyPrice,
            plan.AnnualPrice,
            plan.MaxUsers,
            plan.MaxBranches,
            SplitModules(plan.ModulesIncluded),
            plan.IsActive);

    private static PlatformSubscriptionResponse MapSubscription(TenantLicense subscription) =>
        new(
            subscription.Id,
            subscription.TenantId,
            subscription.Tenant?.Name,
            subscription.LicensePlanId,
            subscription.LicensePlan?.DisplayName,
            subscription.Status,
            subscription.BillingCycle,
            subscription.StartsAt,
            subscription.ExpiresAt,
            subscription.TrialEndsAt,
            subscription.NextBillingDate);

    private static void ValidatePlanRequest(UpsertPlatformLicensePlanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Plan name is required.");
        }

        if (request.MonthlyPrice < 0 || request.AnnualPrice < 0)
        {
            throw new BusinessRuleException("Plan prices cannot be negative.");
        }
    }

    private static void ValidateSubscriptionDates(UpsertPlatformSubscriptionRequest request)
    {
        if (request.EndsAt.HasValue && request.EndsAt.Value <= request.StartsAt)
        {
            throw new BusinessRuleException("License expiry must be after the license start date.");
        }

        if (request.NextBillingDate.HasValue && request.NextBillingDate.Value < request.StartsAt)
        {
            throw new BusinessRuleException("Next billing date cannot be before the license start date.");
        }
    }

    private static string NormalizeSubscriptionStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "Active" : status.Trim();
        return normalized switch
        {
            "Trial" => "Trial",
            "Active" => "Active",
            "Suspended" => "Suspended",
            "Cancelled" => "Cancelled",
            "Expired" => "Expired",
            _ => throw new BusinessRuleException("Subscription status must be Trial, Active, Suspended, Cancelled, or Expired.")
        };
    }

    private static string NormalizeBillingCycle(string? billingCycle) => BillingCycles.Normalize(billingCycle);

    /// <summary>Plan features that were retired (never enforced); dropped if an old client still sends them.</summary>
    private static readonly HashSet<string> RetiredModules = new(StringComparer.OrdinalIgnoreCase) { "AdvancedReports", "ClientPortal" };

    private static string NormalizeModules(IReadOnlyCollection<string>? modulesIncluded)
    {
        if (modulesIncluded is null || modulesIncluded.Count == 0)
        {
            return "Core";
        }

        return string.Join(',', modulesIncluded
            .Where(x => !string.IsNullOrWhiteSpace(x) && !RetiredModules.Contains(x.Trim()))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizePlanModules(string code, IReadOnlyCollection<string>? modules)
    {
        if (!ServiceOpsPlans.IsCommercial(code)) return NormalizeModules(modules);
        var flags = (modules ?? []).Where(x => !string.Equals(x, "operations", StringComparison.OrdinalIgnoreCase) && !string.Equals(x, "finance", StringComparison.OrdinalIgnoreCase));
        return NormalizeModules(flags.Concat(ServiceOpsPlans.Modules(new LicensePlan { PlanCode = code })).Prepend("Core").ToArray());
    }

    private static IReadOnlyCollection<string> SplitModules(string? modulesIncluded)
    {
        if (string.IsNullOrWhiteSpace(modulesIncluded))
        {
            return [];
        }

        return modulesIncluded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool HasModule(IReadOnlyCollection<string>? modulesIncluded, string moduleName) =>
        modulesIncluded?.Any(x => x.Equals(moduleName, StringComparison.OrdinalIgnoreCase)) == true;
}

public sealed record UpsertPlatformLicensePlanRequest(
    string Name,
    string? Code,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int? MaxUsers,
    int? MaxBranches,
    IReadOnlyCollection<string>? ModulesIncluded,
    bool IsActive);

public sealed record PlatformLicensePlanResponse(
    Guid Id,
    string Name,
    string Code,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int? MaxUsers,
    int? MaxBranches,
    IReadOnlyCollection<string> ModulesIncluded,
    bool IsActive);

public sealed record UpsertPlatformSubscriptionRequest(
    Guid TenantId,
    Guid PlanId,
    string Status,
    string? BillingCycle,
    DateTime StartsAt,
    DateTime? EndsAt,
    DateTime? TrialEndsAt,
    DateTime? NextBillingDate);

public sealed record PlatformSubscriptionResponse(
    Guid Id,
    Guid TenantId,
    string? TenantName,
    Guid PlanId,
    string? PlanName,
    string Status,
    string BillingCycle,
    DateTime StartsAt,
    DateTime? EndsAt,
    DateTime? TrialEndsAt,
    DateTime? NextBillingDate);
