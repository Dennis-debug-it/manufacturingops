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

        var normalizedCode = string.IsNullOrWhiteSpace(request.Code)
            ? request.Name.Trim().Replace(" ", string.Empty)
            : request.Code.Trim();

        var exists = await dbContext.LicensePlans.AnyAsync(x => x.PlanCode.ToLower() == normalizedCode.ToLower(), cancellationToken);
        if (exists)
        {
            throw new BusinessRuleException("Plan code already exists.");
        }

        var plan = new LicensePlan
        {
            PlanCode = normalizedCode,
            DisplayName = request.Name.Trim(),
            MonthlyPrice = request.MonthlyPrice,
            AnnualPrice = request.AnnualPrice,
            MaxUsers = request.MaxUsers,
            MaxAssets = request.MaxAssets,
            MonthlyWorkOrders = request.MaxWorkOrders,
            ModulesIncluded = NormalizeModules(request.ModulesIncluded),
            IsActive = request.IsActive,
            EmailIngestion = HasModule(request.ModulesIncluded, "EmailIngestion"),
            MonitoringIntegration = HasModule(request.ModulesIncluded, "MonitoringIntegration"),
            AdvancedReports = HasModule(request.ModulesIncluded, "AdvancedReports"),
            ClientPortal = HasModule(request.ModulesIncluded, "ClientPortal")
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

        plan.DisplayName = request.Name.Trim();
        plan.MonthlyPrice = request.MonthlyPrice;
        plan.AnnualPrice = request.AnnualPrice;
        plan.MaxUsers = request.MaxUsers;
        plan.MaxAssets = request.MaxAssets;
        plan.MonthlyWorkOrders = request.MaxWorkOrders;
        plan.ModulesIncluded = NormalizeModules(request.ModulesIncluded);
        plan.IsActive = request.IsActive;
        plan.EmailIngestion = HasModule(request.ModulesIncluded, "EmailIngestion");
        plan.MonitoringIntegration = HasModule(request.ModulesIncluded, "MonitoringIntegration");
        plan.AdvancedReports = HasModule(request.ModulesIncluded, "AdvancedReports");
        plan.ClientPortal = HasModule(request.ModulesIncluded, "ClientPortal");

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

        var plan = await dbContext.LicensePlans.SingleOrDefaultAsync(x => x.Id == request.PlanId, cancellationToken)
            ?? throw new NotFoundException("License plan was not found.");

        var existing = await dbContext.TenantLicenses.SingleOrDefaultAsync(x => x.TenantId == request.TenantId, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessRuleException("Tenant already has a subscription. Use update instead.");
        }

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

        dbContext.TenantLicenses.Add(subscription);
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

        subscription.TenantId = request.TenantId;
        subscription.LicensePlanId = request.PlanId;
        subscription.LicensePlan = plan;
        subscription.Status = NormalizeSubscriptionStatus(request.Status);
        subscription.BillingCycle = NormalizeBillingCycle(request.BillingCycle);
        subscription.StartsAt = request.StartsAt;
        subscription.ExpiresAt = request.EndsAt;
        subscription.TrialEndsAt = request.TrialEndsAt;
        subscription.NextBillingDate = request.NextBillingDate;

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

        subscription.Status = NormalizeSubscriptionStatus(status);
        subscription.SuspendedAt = status.Equals("Suspended", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null;
        subscription.CancelledAt = status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null;

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

    private static PlatformLicensePlanResponse MapPlan(LicensePlan plan) =>
        new(
            plan.Id,
            plan.DisplayName,
            plan.PlanCode,
            plan.MonthlyPrice,
            plan.AnnualPrice,
            plan.MaxUsers,
            plan.MaxAssets,
            plan.MonthlyWorkOrders,
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

    private static string NormalizeBillingCycle(string? billingCycle)
    {
        var normalized = string.IsNullOrWhiteSpace(billingCycle) ? "Monthly" : billingCycle.Trim();
        return normalized switch
        {
            "Monthly" => "Monthly",
            "Annual" => "Annual",
            "Quarterly" => "Quarterly",
            _ => throw new BusinessRuleException("Billing cycle must be Monthly, Quarterly, or Annual.")
        };
    }

    private static string NormalizeModules(IReadOnlyCollection<string>? modulesIncluded)
    {
        if (modulesIncluded is null || modulesIncluded.Count == 0)
        {
            return "Core";
        }

        return string.Join(',', modulesIncluded
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
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
    int? MaxAssets,
    int? MaxWorkOrders,
    IReadOnlyCollection<string>? ModulesIncluded,
    bool IsActive);

public sealed record PlatformLicensePlanResponse(
    Guid Id,
    string Name,
    string Code,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int? MaxUsers,
    int? MaxAssets,
    int? MaxWorkOrders,
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
