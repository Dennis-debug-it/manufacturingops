using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformReadOnlyAccess")]
[Route("api/platform/reports")]
public sealed class PlatformReportsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<PlatformReportsSummaryResponse>> GetSummary(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var tenantQuery = dbContext.Tenants.Where(x => x.Id != PlatformConstants.RootTenantId);
        var totalTenants = await tenantQuery.CountAsync(cancellationToken);
        var activeTenants = await tenantQuery.CountAsync(x => x.IsActive, cancellationToken);
        var deactivatedTenants = await tenantQuery.CountAsync(x => x.DeactivatedAt.HasValue || x.Status == "Inactive", cancellationToken);
        var suspendedTenants = await tenantQuery.CountAsync(x => x.SuspendedAt.HasValue || x.Status == "Suspended", cancellationToken);

        var totalUsers = await dbContext.Users.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId, cancellationToken);

        var openWorkOrders = await dbContext.WorkOrders.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId && x.Status != "Closed" && x.Status != "Cancelled", cancellationToken);
        var closedWorkOrders = await dbContext.WorkOrders.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId && x.Status == "Closed", cancellationToken);
        var overdueWorkOrders = await dbContext.WorkOrders.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId && x.DueDate.HasValue && x.DueDate.Value.Date < now.Date && x.Status != "Closed" && x.Status != "Cancelled", cancellationToken);

        var totalInvoices = await dbContext.PlatformInvoices.CountAsync(cancellationToken);
        var overdueInvoices = await dbContext.PlatformInvoices.CountAsync(x => x.Status == "Overdue" || (x.DueDate.HasValue && x.DueDate.Value.Date < now.Date && x.Status != "Paid" && x.Status != "Void"), cancellationToken);

        var totalRevenue = await dbContext.PlatformPayments.Where(x => x.Status == "Paid").SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var outstandingBalance = await dbContext.PlatformInvoices.Where(x => x.Status != "Paid" && x.Status != "Void").SumAsync(x => (decimal?)x.Balance, cancellationToken) ?? 0m;

        var mrr = await dbContext.TenantLicenses
            .Include(x => x.LicensePlan)
            .Where(x => x.Status == "Active" || x.Status == "Trial")
            .SumAsync(x => (decimal?)((x.BillingCycle == "Annual"
                ? x.LicensePlan!.AnnualPrice / 12m
                : x.BillingCycle == "Quarterly"
                    ? x.LicensePlan!.MonthlyPrice * 3m / 3m
                    : x.LicensePlan!.MonthlyPrice)), cancellationToken) ?? 0m;

        return Ok(new PlatformReportsSummaryResponse(
            totalTenants,
            activeTenants,
            deactivatedTenants,
            suspendedTenants,
            totalUsers,
            openWorkOrders,
            closedWorkOrders,
            overdueWorkOrders,
            totalInvoices,
            overdueInvoices,
            totalRevenue,
            outstandingBalance,
            mrr,
            monthStart));
    }

    [HttpGet("tenants")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformReportTenantPoint>>> GetTenants(CancellationToken cancellationToken)
    {
        var tenants = await dbContext.Tenants
            .Where(x => x.Id != PlatformConstants.RootTenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PlatformReportTenantPoint(x.Id, x.Name, x.Status, x.IsActive, x.CreatedAt, x.DeactivatedAt, x.SuspendedAt))
            .ToListAsync(cancellationToken);

        return Ok(tenants);
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<PlatformRevenueReportResponse>> GetRevenue(CancellationToken cancellationToken)
    {
        var revenue = await dbContext.PlatformPayments.Where(x => x.Status == "Paid").SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var outstanding = await dbContext.PlatformInvoices.Where(x => x.Status != "Paid" && x.Status != "Void").SumAsync(x => (decimal?)x.Balance, cancellationToken) ?? 0m;
        var expenses = await dbContext.PlatformExpenses.SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;
        return Ok(new PlatformRevenueReportResponse(revenue, outstanding, expenses, revenue - expenses));
    }

    [HttpGet("subscriptions")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformSubscriptionReportPoint>>> GetSubscriptions(CancellationToken cancellationToken)
    {
        var subscriptions = await dbContext.TenantLicenses
            .Include(x => x.Tenant)
            .Include(x => x.LicensePlan)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PlatformSubscriptionReportPoint(
                x.Id,
                x.TenantId,
                x.Tenant != null ? x.Tenant.Name : string.Empty,
                x.LicensePlanId,
                x.LicensePlan != null ? x.LicensePlan.DisplayName : string.Empty,
                x.Status,
                x.BillingCycle,
                x.StartsAt,
                x.ExpiresAt,
                x.TrialEndsAt,
                x.NextBillingDate))
            .ToListAsync(cancellationToken);

        return Ok(subscriptions);
    }

    [HttpGet("work-orders")]
    public async Task<ActionResult<PlatformWorkOrdersReportResponse>> GetWorkOrders(CancellationToken cancellationToken)
    {
        var total = await dbContext.WorkOrders.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId, cancellationToken);
        var open = await dbContext.WorkOrders.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId && x.Status != "Closed" && x.Status != "Cancelled", cancellationToken);
        var closed = await dbContext.WorkOrders.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId && x.Status == "Closed", cancellationToken);
        var overdue = await dbContext.WorkOrders.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId && x.DueDate.HasValue && x.DueDate.Value.Date < DateTime.UtcNow.Date && x.Status != "Closed" && x.Status != "Cancelled", cancellationToken);

        return Ok(new PlatformWorkOrdersReportResponse(total, open, closed, overdue));
    }

    [HttpGet("finance")]
    public async Task<ActionResult<PlatformFinanceReportResponse>> GetFinance(CancellationToken cancellationToken)
    {
        var quotations = await dbContext.PlatformQuotations.CountAsync(cancellationToken);
        var invoices = await dbContext.PlatformInvoices.CountAsync(cancellationToken);
        var payments = await dbContext.PlatformPayments.CountAsync(cancellationToken);
        var expenses = await dbContext.PlatformExpenses.CountAsync(cancellationToken);

        return Ok(new PlatformFinanceReportResponse(quotations, invoices, payments, expenses));
    }

    [HttpGet("audit")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformAuditReportPoint>>> GetAudit(CancellationToken cancellationToken)
    {
        var rows = await dbContext.AuditLogs
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new PlatformAuditReportPoint(
                x.Id,
                x.TenantId,
                x.UserId,
                x.ActorName,
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Details,
                x.Severity,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }
}

public sealed record PlatformReportsSummaryResponse(
    int TotalTenants,
    int ActiveTenants,
    int DeactivatedTenants,
    int SuspendedTenants,
    int TotalUsers,
    int OpenWorkOrders,
    int ClosedWorkOrders,
    int OverdueWorkOrders,
    int TotalInvoices,
    int OverdueInvoices,
    decimal TotalRevenue,
    decimal OutstandingBalance,
    decimal MonthlyRecurringRevenue,
    DateTime AsAtMonthStartUtc);

public sealed record PlatformReportTenantPoint(Guid TenantId, string Name, string Status, bool IsActive, DateTime CreatedAt, DateTime? DeactivatedAt, DateTime? SuspendedAt);
public sealed record PlatformRevenueReportResponse(decimal TotalRevenue, decimal OutstandingBalance, decimal TotalExpenses, decimal NetPosition);
public sealed record PlatformSubscriptionReportPoint(Guid SubscriptionId, Guid TenantId, string TenantName, Guid PlanId, string PlanName, string Status, string BillingCycle, DateTime StartsAt, DateTime? EndsAt, DateTime? TrialEndsAt, DateTime? NextBillingDate);
public sealed record PlatformWorkOrdersReportResponse(int TotalWorkOrders, int OpenWorkOrders, int ClosedWorkOrders, int OverdueWorkOrders);
public sealed record PlatformFinanceReportResponse(int Quotations, int Invoices, int Payments, int Expenses);
public sealed record PlatformAuditReportPoint(Guid Id, Guid? TenantId, Guid? ActorUserId, string? ActorName, string Action, string EntityType, string EntityId, string? Description, string Severity, DateTime CreatedAt);
