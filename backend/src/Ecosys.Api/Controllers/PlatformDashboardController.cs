using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformReadOnlyAccess")]
[Route("api/platform/dashboard")]
public sealed class PlatformDashboardController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<PlatformDashboardSummaryResponse>> GetSummary(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var totalTenants = await dbContext.Tenants.CountAsync(x => x.Id != PlatformConstants.RootTenantId, cancellationToken);
        var activeTenants = await dbContext.Tenants.CountAsync(x => x.Id != PlatformConstants.RootTenantId && x.IsActive, cancellationToken);
        var deactivatedTenants = await dbContext.Tenants.CountAsync(x => x.Id != PlatformConstants.RootTenantId && (x.DeactivatedAt.HasValue || x.Status == "Inactive"), cancellationToken);
        var suspendedTenants = await dbContext.Tenants.CountAsync(x => x.Id != PlatformConstants.RootTenantId && (x.SuspendedAt.HasValue || x.Status == "Suspended"), cancellationToken);

        var totalUsers = await dbContext.Users.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId, cancellationToken);
        var openWorkOrders = await dbContext.WorkOrders.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId && x.Status != "Closed" && x.Status != "Cancelled", cancellationToken);
        var closedWorkOrders = await dbContext.WorkOrders.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId && x.Status == "Closed", cancellationToken);
        var overdueWorkOrders = await dbContext.WorkOrders.CountAsync(x => x.TenantId != PlatformConstants.RootTenantId && x.DueDate.HasValue && x.DueDate.Value.Date < now.Date && x.Status != "Closed" && x.Status != "Cancelled", cancellationToken);

        var totalInvoices = await dbContext.PlatformInvoices.CountAsync(cancellationToken);
        var overdueInvoices = await dbContext.PlatformInvoices.CountAsync(x => x.Status == "Overdue" || (x.DueDate.HasValue && x.DueDate.Value.Date < now.Date && x.Status != "Paid" && x.Status != "Void"), cancellationToken);
        var totalRevenue = await dbContext.PlatformPayments.Where(x => x.Status == "Paid").SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var outstandingBalance = await dbContext.PlatformInvoices.Where(x => x.Status != "Paid" && x.Status != "Void").SumAsync(x => (decimal?)x.Balance, cancellationToken) ?? 0m;
        var mrr = await dbContext.TenantLicenses.Include(x => x.LicensePlan).Where(x => x.Status == "Active" || x.Status == "Trial").SumAsync(x => (decimal?)x.LicensePlan!.MonthlyPrice, cancellationToken) ?? 0m;

        // Conversion metrics (4b).
        var totalLeads = await dbContext.PlatformLeads.CountAsync(cancellationToken);
        var convertedLeads = await dbContext.PlatformLeads.CountAsync(x => x.ConvertedTenantId != null || x.Status == "Converted to Workspace", cancellationToken);
        var leadConversionRate = totalLeads > 0 ? Math.Round((decimal)convertedLeads / totalLeads * 100, 1) : 0m;

        var paidLicenses = await dbContext.TenantLicenses.CountAsync(x => x.Status == "Active", cancellationToken);
        var trialLicenses = await dbContext.TenantLicenses.CountAsync(x => x.Status == "Trial", cancellationToken);
        var trialToPaidRate = (paidLicenses + trialLicenses) > 0 ? Math.Round((decimal)paidLicenses / (paidLicenses + trialLicenses) * 100, 1) : 0m;

        return Ok(new PlatformDashboardSummaryResponse(totalTenants, activeTenants, deactivatedTenants, suspendedTenants, totalUsers, openWorkOrders, closedWorkOrders, overdueWorkOrders, totalInvoices, overdueInvoices, totalRevenue, outstandingBalance, mrr, leadConversionRate, trialToPaidRate));
    }

    /// <summary>Cumulative tenant count at the end of each of the last 12 months (for a growth sparkline).</summary>
    [HttpGet("tenant-growth")]
    public async Task<ActionResult<IReadOnlyList<TenantGrowthPoint>>> GetTenantGrowth(CancellationToken cancellationToken)
    {
        var createdDates = await dbContext.Tenants
            .Where(x => x.Id != PlatformConstants.RootTenantId)
            .Select(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var firstMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);

        var points = Enumerable.Range(0, 12)
            .Select(offset => firstMonth.AddMonths(offset))
            .Select(month => new TenantGrowthPoint(
                month.ToString("yyyy-MM"),
                createdDates.Count(created => created < month.AddMonths(1))))
            .ToList();

        return Ok(points);
    }

    /// <summary>Daily MRR snapshots over the requested window (written by the MRR snapshot job).</summary>
    [HttpGet("mrr-trend")]
    public async Task<ActionResult<IReadOnlyList<MrrTrendPoint>>> GetMrrTrend([FromQuery] int days = 90, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.Date.AddDays(-Math.Clamp(days, 7, 365));
        var points = await dbContext.MrrSnapshots
            .Where(x => x.SnapshotDate >= since)
            .OrderBy(x => x.SnapshotDate)
            .Select(x => new MrrTrendPoint(x.SnapshotDate, x.Mrr, x.PaidTenants, x.TrialTenants))
            .ToListAsync(cancellationToken);
        return Ok(points);
    }
}

public sealed record TenantGrowthPoint(string Month, int TenantCount);
public sealed record MrrTrendPoint(DateTime Date, decimal Mrr, int PaidTenants, int TrialTenants);

public sealed record PlatformDashboardSummaryResponse(
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
    decimal LeadConversionRate,
    decimal TrialToPaidRate);
