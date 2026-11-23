using Ecosys.Api.Common;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Ecosys.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("work-order-performance")]
    public async Task<IActionResult> WorkOrderPerformance(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? branchId,
        [FromQuery] string? status, [FromQuery] string? priority,
        CancellationToken ct)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewReports);

        var (start, end) = NormalizeDateRange(from, to);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, ct);

        var query = dbContext.WorkOrders.Where(x =>
            x.TenantId == TenantId &&
            x.CreatedAt >= start &&
            x.CreatedAt <= end);

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(x => x.Priority == priority);
        if (scope.RequestedBranchId.HasValue)
            query = query.Where(x => x.BranchId == scope.RequestedBranchId);

        var wos = await query
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.Priority,
                x.CreatedAt,
                x.CompletedAt,
                x.DueDate,
                x.IsPreventiveMaintenance,
                IsFollowUp = x.FollowUpOfWorkOrderId != null
            })
            .ToListAsync(ct);

        var total = wos.Count;
        // Return visits raised from a finished job ("Create follow-up"): a high share points at
        // first-time-fix problems.
        var followUps = wos.Count(x => x.IsFollowUp);
        var completed = wos.Count(x => x.Status == "Completed");
        var completedOnTime = wos.Count(x => x.Status == "Completed" && x.DueDate.HasValue && x.CompletedAt <= x.DueDate);
        var overdue = wos.Count(x => x.Status != "Completed" && x.Status != "Cancelled" && x.DueDate.HasValue && x.DueDate < DateTime.UtcNow);
        var avgCompletionHours = wos
            .Where(x => x.CompletedAt.HasValue)
            .Select(x => (x.CompletedAt!.Value - x.CreatedAt).TotalHours)
            .DefaultIfEmpty(0)
            .Average();

        var byStatus = wos.GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() });

        var byPriority = wos.GroupBy(x => x.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() });

        var byDay = wos.GroupBy(x => x.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.Count() });

        return Ok(new
        {
            DateRange = new { From = start, To = end },
            Total = total,
            Completed = completed,
            CompletedOnTime = completedOnTime,
            Overdue = overdue,
            OnTimeRate = completed > 0 ? Math.Round((double)completedOnTime / completed * 100, 1) : 0,
            AvgCompletionHours = Math.Round(avgCompletionHours, 1),
            FollowUps = followUps,
            FollowUpRate = total > 0 ? Math.Round((double)followUps / total * 100, 1) : 0,
            ByStatus = byStatus,
            ByPriority = byPriority,
            ByDay = byDay
        });
    }

    [HttpGet("work-order-performance/export")]
    public async Task<IActionResult> WorkOrderPerformanceExport(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? branchId,
        [FromQuery] string? status, [FromQuery] string? priority,
        CancellationToken ct)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewReports);

        var (start, end) = NormalizeDateRange(from, to);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, ct);

        var query = dbContext.WorkOrders
            .Include(x => x.AssignedTechnician)
            .Include(x => x.FollowUpOfWorkOrder)
            .Where(x => x.TenantId == TenantId && x.CreatedAt >= start && x.CreatedAt <= end);

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(x => x.Priority == priority);
        if (scope.RequestedBranchId.HasValue)
            query = query.Where(x => x.BranchId == scope.RequestedBranchId);

        var wos = await query.OrderBy(x => x.CreatedAt).ToListAsync(ct);

        var csv = new StringBuilder();
        csv.AppendLine("WO Number,Title,Priority,Status,Created At,Due Date,Completed At,On Time,PM,Follow-up Of");
        foreach (var wo in wos)
        {
            var onTime = wo.CompletedAt.HasValue && wo.DueDate.HasValue ? (wo.CompletedAt <= wo.DueDate ? "Yes" : "No") : "";
            csv.AppendLine($"\"{Csv.Escape(wo.WorkOrderNumber)}\",\"{Csv.Escape(wo.Title)}\",{wo.Priority},{wo.Status},{wo.CreatedAt:yyyy-MM-dd},{wo.DueDate?.ToString("yyyy-MM-dd")},{wo.CompletedAt?.ToString("yyyy-MM-dd")},{onTime},{(wo.IsPreventiveMaintenance ? "Yes" : "No")},\"{Csv.Escape(wo.FollowUpOfWorkOrder?.WorkOrderNumber)}\"");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"wo-performance-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }

    [HttpGet("technician-productivity")]
    public async Task<IActionResult> TechnicianProductivity(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? technicianId, [FromQuery] Guid? branchId,
        CancellationToken ct)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewReports);

        var (start, end) = NormalizeDateRange(from, to);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, ct);

        var query = dbContext.WorkOrders
            .Include(x => x.AssignedTechnician)
            .Where(x => x.TenantId == TenantId && x.CreatedAt >= start && x.CreatedAt <= end && x.AssignedTechnicianId.HasValue);

        if (technicianId.HasValue) query = query.Where(x => x.AssignedTechnicianId == technicianId.Value);
        if (scope.RequestedBranchId.HasValue)
            query = query.Where(x => x.BranchId == scope.RequestedBranchId);

        var wos = await query.ToListAsync(ct);

        var report = wos
            .GroupBy(x => new { x.AssignedTechnicianId, Name = x.AssignedTechnician?.FullName ?? "Unknown" })
            .Select(g =>
            {
                var completed = g.Where(x => x.Status == "Completed").ToList();
                var onTime = completed.Count(x => x.DueDate.HasValue && x.CompletedAt <= x.DueDate);

                return new
                {
                    TechnicianId = g.Key.AssignedTechnicianId,
                    Name = g.Key.Name,
                    TotalJobs = g.Count(),
                    Completed = completed.Count,
                    OnTimeCount = onTime,
                    OnTimeRate = completed.Count > 0 ? Math.Round((double)onTime / completed.Count * 100, 1) : 0,
                    PmJobs = g.Count(x => x.IsPreventiveMaintenance),
                    CorrectiveJobs = g.Count(x => !x.IsPreventiveMaintenance)
                };
            })
            .OrderByDescending(x => x.Completed)
            .ToList();

        return Ok(new { DateRange = new { From = start, To = end }, Technicians = report });
    }

    [HttpGet("technician-productivity/export")]
    public async Task<IActionResult> TechnicianProductivityExport(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? technicianId, [FromQuery] Guid? branchId,
        CancellationToken ct)
    {
        var result = await TechnicianProductivity(from, to, technicianId, branchId, ct) as OkObjectResult;
        var data = result?.Value as dynamic;

        var csv = new StringBuilder();
        csv.AppendLine("Technician,Total Jobs,Completed,On Time Rate %,PM Jobs,Corrective Jobs");
        if (data?.Technicians is IEnumerable<dynamic> techs)
        {
            foreach (var t in techs)
                csv.AppendLine($"\"{Csv.Escape((object?)t.Name)}\",{t.TotalJobs},{t.Completed},{t.OnTimeRate},{t.PmJobs},{t.CorrectiveJobs}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "technician-productivity.csv");
    }

    [HttpGet("asset-reliability")]
    public async Task<IActionResult> AssetReliability(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? categoryId,
        CancellationToken ct)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewReports);

        var (start, end) = NormalizeDateRange(from, to);

        var assetsQuery = dbContext.Assets.Where(x => x.TenantId == TenantId);
        if (categoryId.HasValue) assetsQuery = assetsQuery.Where(x => x.AssetCategoryId == categoryId.Value);

        var assets = await assetsQuery
            .Include(x => x.AssetCategory)
            .ToListAsync(ct);

        var assetIds = assets.Select(a => a.Id).ToList();

        var wosByAsset = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.AssetId.HasValue && assetIds.Contains(x.AssetId.Value) && x.CreatedAt >= start && x.CreatedAt <= end)
            .GroupBy(x => x.AssetId!.Value)
            .Select(g => new
            {
                AssetId = g.Key,
                CorrectiveCount = g.Count(x => !x.IsPreventiveMaintenance),
                PmCount = g.Count(x => x.IsPreventiveMaintenance),
                PmCompleted = g.Count(x => x.IsPreventiveMaintenance && x.Status == "Completed"),
                FollowUpCount = g.Count(x => x.FollowUpOfWorkOrderId != null)
            })
            .ToDictionaryAsync(x => x.AssetId, ct);

        var report = assets.Select(a =>
        {
            var wos = wosByAsset.GetValueOrDefault(a.Id);
            var pmCompliance = wos?.PmCount > 0 ? Math.Round((double)wos.PmCompleted / wos.PmCount * 100, 1) : 100.0;
            var isRecurringFault = (wos?.CorrectiveCount ?? 0) >= 3;

            return new
            {
                a.Id,
                a.AssetName,
                a.AssetCode,
                a.Status,
                CategoryName = a.AssetCategory?.Name,
                a.LastPmDate,
                a.NextPmDate,
                a.WarrantyExpiryDate,
                WarrantyStatus = a.WarrantyExpiryDate.HasValue
                    ? (a.WarrantyExpiryDate >= DateTime.UtcNow ? "In Warranty" : "Expired")
                    : "Unknown",
                CorrectiveWos = wos?.CorrectiveCount ?? 0,
                FollowUpWos = wos?.FollowUpCount ?? 0,
                PmCompliance = pmCompliance,
                IsRecurringFault = isRecurringFault
            };
        })
        .OrderByDescending(x => x.CorrectiveWos)
        .ToList();

        return Ok(new
        {
            DateRange = new { From = start, To = end },
            Assets = report,
            RecurringFaultCount = report.Count(x => x.IsRecurringFault)
        });
    }

    [HttpGet("asset-reliability/export")]
    public async Task<IActionResult> AssetReliabilityExport(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? categoryId,
        CancellationToken ct)
    {
        var result = await AssetReliability(from, to, categoryId, ct) as OkObjectResult;
        var data = result?.Value as dynamic;

        var csv = new StringBuilder();
        csv.AppendLine("Asset Code,Asset Name,Category,Status,Last PM,Next PM,Warranty Status,Corrective WOs,Follow-up WOs,PM Compliance %,Recurring Fault");
        if (data?.Assets is IEnumerable<dynamic> items)
        {
            foreach (var a in items)
                csv.AppendLine($"\"{Csv.Escape((object?)a.AssetCode)}\",\"{Csv.Escape((object?)a.AssetName)}\",\"{Csv.Escape((object?)a.CategoryName)}\",{a.Status},{a.LastPmDate:yyyy-MM-dd},{a.NextPmDate:yyyy-MM-dd},{a.WarrantyStatus},{a.CorrectiveWos},{a.FollowUpWos},{a.PmCompliance},{(a.IsRecurringFault ? "Yes" : "No")}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "asset-reliability.csv");
    }

    [HttpGet("pm-compliance")]
    public async Task<IActionResult> PmCompliance(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? categoryId,
        CancellationToken ct)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewReports);

        var (start, end) = NormalizeDateRange(from, to);

        var plansQuery = dbContext.PreventiveMaintenancePlans
            .Include(x => x.Asset)
                .ThenInclude(a => a!.AssetCategory)
            .Where(x => x.TenantId == TenantId && x.Status == "Active");

        if (categoryId.HasValue)
            plansQuery = plansQuery.Where(x => x.Asset != null && x.Asset.AssetCategoryId == categoryId.Value);

        var plans = await plansQuery.ToListAsync(ct);
        var scopedAssetIds = plans.Select(x => x.AssetId).Distinct().ToList();

        var pmWosQuery = dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.IsPreventiveMaintenance && x.DueDate >= start && x.DueDate <= end);

        if (categoryId.HasValue)
            pmWosQuery = pmWosQuery.Where(x => x.AssetId.HasValue && scopedAssetIds.Contains(x.AssetId.Value));

        var pmWos = await pmWosQuery.ToListAsync(ct);

        var duePlans = plans.Where(p => p.NextPmDate.HasValue && p.NextPmDate >= start && p.NextPmDate <= end).ToList();
        var completedOnTime = pmWos.Count(x => x.Status == "Completed" && x.DueDate.HasValue && x.CompletedAt <= x.DueDate);
        var overduePlans = plans.Where(p => p.NextPmDate.HasValue && p.NextPmDate < DateTime.UtcNow).ToList();

        return Ok(new
        {
            DateRange = new { From = start, To = end },
            ActivePlans = plans.Count,
            DueInPeriod = duePlans.Count,
            CompletedOnTime = completedOnTime,
            Overdue = overduePlans.Count,
            ComplianceRate = pmWos.Count > 0 ? Math.Round((double)completedOnTime / pmWos.Count * 100, 1) : 100.0,
            OverduePlans = overduePlans.Select(p => new
            {
                p.Id,
                AssetName = p.Asset?.AssetName,
                AssetCode = p.Asset?.AssetCode,
                NextPmDate = p.NextPmDate,
                DaysOverdue = p.NextPmDate.HasValue ? (int)(DateTime.UtcNow - p.NextPmDate.Value).TotalDays : 0
            }).OrderByDescending(x => x.DaysOverdue)
        });
    }

    [HttpGet("pm-compliance/export")]
    public async Task<IActionResult> PmComplianceExport(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] Guid? categoryId,
        CancellationToken ct)
    {
        var result = await PmCompliance(from, to, categoryId, ct) as OkObjectResult;
        var data = result?.Value as dynamic;

        var csv = new StringBuilder();
        csv.AppendLine("Asset,Asset Code,Next PM Date,Days Overdue");
        if (data?.OverduePlans is IEnumerable<dynamic> items)
        {
            foreach (var p in items)
                csv.AppendLine($"\"{Csv.Escape((object?)p.AssetName)}\",\"{Csv.Escape((object?)p.AssetCode)}\",{p.NextPmDate:yyyy-MM-dd},{p.DaysOverdue}");
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "pm-compliance.csv");
    }

    private static (DateTime start, DateTime end) NormalizeDateRange(DateTime? from, DateTime? to)
    {
        var end = to?.ToUniversalTime() ?? DateTime.UtcNow;
        var start = from?.ToUniversalTime() ?? end.AddDays(-30);
        return (start, end);
    }
}
