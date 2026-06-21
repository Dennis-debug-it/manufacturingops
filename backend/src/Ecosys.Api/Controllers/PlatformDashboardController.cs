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

        return Ok(new PlatformDashboardSummaryResponse(totalTenants, activeTenants, deactivatedTenants, suspendedTenants, totalUsers, openWorkOrders, closedWorkOrders, overdueWorkOrders, totalInvoices, overdueInvoices, totalRevenue, outstandingBalance, mrr));
    }
}

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
    decimal MonthlyRecurringRevenue);
