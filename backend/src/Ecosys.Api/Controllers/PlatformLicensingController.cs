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

    /// <summary>The three editions with their prices. Which modules each includes is fixed in code.</summary>
    [HttpGet("plans")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformLicensePlanResponse>>> GetPlans(CancellationToken cancellationToken)
    {
        var plans = await dbContext.LicensePlans.ToListAsync(cancellationToken);
        return Ok(plans.OrderBy(x => ServiceOpsPlans.Editions.ToList().IndexOf(x.PlanCode)).Select(MapPlan).ToList());
    }

    /// <summary>Changes an edition's prices, minimum office seats or availability. New prices apply to new licenses and renewals.</summary>
    [HttpPut("plans/{id:guid}")]
    public async Task<ActionResult<PlatformLicensePlanResponse>> UpdatePlan(Guid id, [FromBody] UpdatePlatformLicensePlanRequest request, CancellationToken cancellationToken)
    {
        if (new[] { request.OfficeSeatMonthlyPrice, request.OfficeSeatAnnualPrice, request.FieldSeatMonthlyPrice, request.FieldSeatAnnualPrice }.Any(x => x < 0))
        {
            throw new BusinessRuleException("Prices cannot be negative.");
        }

        if (request.MinOfficeSeats < 1)
        {
            throw new BusinessRuleException("Minimum office seats must be at least 1.");
        }

        var plan = await dbContext.LicensePlans.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Edition was not found.");

        plan.OfficeSeatMonthlyPrice = request.OfficeSeatMonthlyPrice;
        plan.OfficeSeatAnnualPrice = request.OfficeSeatAnnualPrice;
        plan.FieldSeatMonthlyPrice = request.FieldSeatMonthlyPrice;
        plan.FieldSeatAnnualPrice = request.FieldSeatAnnualPrice;
        plan.MinOfficeSeats = request.MinOfficeSeats;
        plan.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            "platform.license.plan.updated",
            nameof(LicensePlan),
            plan.Id.ToString(),
            $"Edition '{plan.DisplayName}' updated.",
            cancellationToken: cancellationToken);

        return Ok(MapPlan(plan));
    }

    [HttpGet("add-ons")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformLicenseAddOnResponse>>> GetAddOns(CancellationToken cancellationToken)
    {
        var addOns = await dbContext.LicenseAddOns.OrderBy(x => x.MonthlyPrice).ToListAsync(cancellationToken);
        return Ok(addOns.Select(MapAddOn).ToList());
    }

    /// <summary>Changes an add-on's prices and included messages. New prices apply to new licenses and renewals.</summary>
    [HttpPut("add-ons/{id:guid}")]
    public async Task<ActionResult<PlatformLicenseAddOnResponse>> UpdateAddOn(Guid id, [FromBody] UpdatePlatformLicenseAddOnRequest request, CancellationToken cancellationToken)
    {
        if (new[] { request.MonthlyPrice, request.AnnualPrice, request.ExtraMessagePrice, request.ExtraNumberPrice }.Any(x => x < 0) || request.IncludedMessages < 0)
        {
            throw new BusinessRuleException("Prices and included messages cannot be negative.");
        }

        var addOn = await dbContext.LicenseAddOns.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Add-on was not found.");

        addOn.MonthlyPrice = request.MonthlyPrice;
        addOn.AnnualPrice = request.AnnualPrice;
        addOn.IncludedMessages = request.IncludedMessages;
        addOn.ExtraMessagePrice = request.ExtraMessagePrice;
        addOn.ExtraNumberPrice = request.ExtraNumberPrice;
        addOn.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            "platform.license.addon.updated",
            nameof(LicenseAddOn),
            addOn.Id.ToString(),
            $"Add-on '{addOn.DisplayName}' updated.",
            cancellationToken: cancellationToken);

        return Ok(MapAddOn(addOn));
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
        if (!plan.IsActive || !ServiceOpsPlans.IsEdition(plan.PlanCode))
            throw new BusinessRuleException("Only an active edition can be assigned.");

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
            OfficeSeats = request.OfficeSeats ?? Math.Max(plan.MinOfficeSeats, TenantLicense.DefaultOfficeSeats),
            FieldSeats = request.FieldSeats ?? TenantLicense.DefaultFieldSeats,
            WhatsAppAddOn = ServiceOpsPlans.NormalizeWhatsAppAddOn(request.WhatsAppAddOn),
            Status = NormalizeSubscriptionStatus(request.Status),
            BillingCycle = NormalizeBillingCycle(request.BillingCycle),
            StartsAt = request.StartsAt,
            ExpiresAt = request.EndsAt,
            TrialEndsAt = request.TrialEndsAt,
            NextBillingDate = request.NextBillingDate,
            LicensePlan = plan,
            Tenant = tenant
        };

        SeatRules.Validate(plan, subscription.OfficeSeats, subscription.FieldSeats);
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
        if (!plan.IsActive || !ServiceOpsPlans.IsEdition(plan.PlanCode))
            throw new BusinessRuleException("Only an active edition can be assigned.");

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
        subscription.OfficeSeats = request.OfficeSeats ?? subscription.OfficeSeats;
        subscription.FieldSeats = request.FieldSeats ?? subscription.FieldSeats;
        // The WhatsApp add-on is turned on and off through PlatformWhatsAppController, not here.
        SeatRules.Validate(plan, subscription.OfficeSeats, subscription.FieldSeats);
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
            plan.OfficeSeatMonthlyPrice,
            plan.OfficeSeatAnnualPrice,
            plan.FieldSeatMonthlyPrice,
            plan.FieldSeatAnnualPrice,
            plan.MinOfficeSeats,
            ServiceOpsPlans.EditionModules(plan.PlanCode),
            plan.IsPriced,
            plan.IsActive);

    private static PlatformLicenseAddOnResponse MapAddOn(LicenseAddOn addOn) =>
        new(addOn.Id, addOn.Code, addOn.DisplayName, addOn.MonthlyPrice, addOn.AnnualPrice, addOn.IncludedMessages,
            addOn.ExtraMessagePrice, addOn.ExtraNumberPrice, addOn.IsActive);

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
            subscription.NextBillingDate,
            subscription.OfficeSeats,
            subscription.FieldSeats,
            subscription.WhatsAppAddOn);

    private static void ValidateSubscriptionDates(UpsertPlatformSubscriptionRequest request)
    {
        if (request.EndsAt.HasValue && request.EndsAt.Value <= request.StartsAt)
        {
            throw new BusinessRuleException("License expiry must be after the license start date.");
        }

        if (request.OfficeSeats is <= 0)
        {
            throw new BusinessRuleException("Office seats must be greater than zero.");
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
}

public sealed record UpdatePlatformLicensePlanRequest(
    decimal OfficeSeatMonthlyPrice,
    decimal OfficeSeatAnnualPrice,
    decimal FieldSeatMonthlyPrice,
    decimal FieldSeatAnnualPrice,
    int MinOfficeSeats,
    bool IsActive);

public sealed record PlatformLicensePlanResponse(
    Guid Id,
    string Name,
    string Code,
    decimal OfficeSeatMonthlyPrice,
    decimal OfficeSeatAnnualPrice,
    decimal FieldSeatMonthlyPrice,
    decimal FieldSeatAnnualPrice,
    int MinOfficeSeats,
    IReadOnlyCollection<string> ModulesIncluded,
    bool IsPriced,
    bool IsActive);

public sealed record UpdatePlatformLicenseAddOnRequest(
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int IncludedMessages,
    decimal ExtraMessagePrice,
    decimal ExtraNumberPrice,
    bool IsActive);

public sealed record PlatformLicenseAddOnResponse(
    Guid Id,
    string Code,
    string Name,
    decimal MonthlyPrice,
    decimal AnnualPrice,
    int IncludedMessages,
    decimal ExtraMessagePrice,
    decimal ExtraNumberPrice,
    bool IsActive);

public sealed record UpsertPlatformSubscriptionRequest(
    Guid TenantId,
    Guid PlanId,
    string Status,
    string? BillingCycle,
    DateTime StartsAt,
    DateTime? EndsAt,
    DateTime? TrialEndsAt,
    DateTime? NextBillingDate,
    int? OfficeSeats = null,
    int? FieldSeats = null,
    string? WhatsAppAddOn = null);

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
    DateTime? NextBillingDate,
    int OfficeSeats,
    int FieldSeats,
    string? WhatsAppAddOn);
