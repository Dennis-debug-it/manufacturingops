using System.Security.Claims;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(
    AppDbContext dbContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    ILogger<DashboardController> logger,
    ITenantContext tenantContext) : TenantAwareControllerBase(tenantContext)
{
    private static readonly string[] ClosedWorkOrderStatuses = ["Completed", "Acknowledged", "Closed"];
    private static readonly string[] NonOpenWorkOrderStatuses = ["Completed", "Acknowledged", "Closed", "Cancelled"];

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var tenantId = TenantId;
        var userId = TryGetUserId();
        var scope = await branchAccessService.GetQueryScopeAsync(tenantId, branchId, cancellationToken);

        try
        {
            var summary = await BuildSummaryAsync(tenantId, scope, cancellationToken);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed generating dashboard summary for tenant {TenantId}, user {UserId}, branch {BranchId}.",
                tenantId,
                userId,
                branchId);

            return StatusCode(500, new { error = "Dashboard data could not be loaded. Please try again." });
        }
    }

    private async Task<DashboardSummaryDto> BuildSummaryAsync(
        Guid tenantId,
        BranchQueryScope scope,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var workOrders = dbContext.WorkOrders
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .WhereAccessible(scope, x => x.BranchId);

        var workOrderAssignments = dbContext.WorkOrderAssignments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        var technicianAssignments = dbContext.WorkOrderTechnicianAssignments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        var assets = dbContext.Assets
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .WhereAccessible(scope, x => x.BranchId);

        var openWorkOrders = await workOrders.CountAsync(
            x => !NonOpenWorkOrderStatuses.Contains(x.Status ?? string.Empty),
            cancellationToken);

        var closedWorkOrders = await workOrders.CountAsync(
            x => ClosedWorkOrderStatuses.Contains(x.Status ?? string.Empty),
            cancellationToken);

        var overdueWorkOrders = await workOrders.CountAsync(
            x => x.DueDate.HasValue
                && x.DueDate.Value < now
                && !NonOpenWorkOrderStatuses.Contains(x.Status ?? string.Empty),
            cancellationToken);

        var assetCount = await assets.CountAsync(
            x => (x.Status ?? string.Empty) != "Inactive",
            cancellationToken);

        var clientCount = await dbContext.Clients
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

        var today = DateTime.UtcNow.Date;
        var contracts = dbContext.Contracts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsArchived);

        var contractsExpiringIn90Days = await contracts.CountAsync(
            x => x.EndDate.Date >= today && x.EndDate.Date <= today.AddDays(90),
            cancellationToken);

        var contractsExpiringIn30Days = await contracts.CountAsync(
            x => x.EndDate.Date >= today && x.EndDate.Date <= today.AddDays(30),
            cancellationToken);

        var criticalRenewalRiskContracts = await contracts.CountAsync(
            x => x.RenewalRisk == Domain.Enums.RenewalRisk.Critical,
            cancellationToken);

        var expiredContracts = await contracts.CountAsync(
            x => x.Status == Domain.Enums.ContractStatus.Expired || x.EndDate.Date < today,
            cancellationToken);

        var renewalInProgressContracts = await contracts.CountAsync(
            x => x.Status == Domain.Enums.ContractStatus.RenewalInProgress,
            cancellationToken);

        var materialsLowStock = scope.HasConfiguredBranches
            ? await dbContext.BranchMaterialStocks
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .WhereAccessible(scope, x => (Guid?)x.BranchId)
                .CountAsync(x => x.QuantityOnHand <= x.ReorderLevel && (x.QuantityOnHand != 0 || x.ReorderLevel > 0), cancellationToken)
            : await dbContext.MaterialItems
                .AsNoTracking()
                .CountAsync(
                    x => x.TenantId == tenantId
                        && x.IsActive
                        && x.QuantityOnHand <= x.ReorderLevel
                        && (x.QuantityOnHand != 0 || x.ReorderLevel > 0),
                    cancellationToken);

        var accessibleWorkOrderIds = await workOrders
            .Where(x => !NonOpenWorkOrderStatuses.Contains(x.Status ?? string.Empty))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var accessibleAssignments = await workOrderAssignments
            .Include(x => x.AssignmentGroup)
            .Where(x => accessibleWorkOrderIds.Contains(x.WorkOrderId))
            .ToListAsync(cancellationToken);

        var accessibleTechnicianAssignments = await technicianAssignments
            .Include(x => x.Technician)
            .Where(x => accessibleWorkOrderIds.Contains(x.WorkOrderId))
            .ToListAsync(cancellationToken);

        var unassignedWorkOrders = accessibleWorkOrderIds.Count(id =>
            !accessibleAssignments.Any(assignment => assignment.WorkOrderId == id && assignment.AssignmentGroupId.HasValue)
            && !accessibleTechnicianAssignments.Any(assignment => assignment.WorkOrderId == id));

        var assignedToGroup = accessibleAssignments.Count(x => x.AssignmentGroupId.HasValue && !accessibleTechnicianAssignments.Any(assignment => assignment.WorkOrderId == x.WorkOrderId));
        var assignedToTechnicians = accessibleWorkOrderIds.Count(id => accessibleTechnicianAssignments.Any(assignment => assignment.WorkOrderId == id));
        var awaitingAcceptance = accessibleWorkOrderIds.Count(id =>
            accessibleTechnicianAssignments.Any(assignment => assignment.WorkOrderId == id)
            && !accessibleTechnicianAssignments.Any(assignment => assignment.WorkOrderId == id && assignment.Status != "Pending"));
        var techniciansOnSite = accessibleTechnicianAssignments.Count(x => x.ArrivalAt.HasValue && !x.DepartureAt.HasValue);

        // Call .AsEnumerable() explicitly to guarantee LINQ-to-Objects evaluation.
        // accessibleAssignments and accessibleTechnicianAssignments are already List<T>
        // (materialised above via ToListAsync), but the explicit cast prevents any future
        // IQueryable wrapping from accidentally routing these groupings through EF Core.
        var workOrdersByGroup = accessibleAssignments
            .AsEnumerable()
            .Where(x => x.AssignmentGroupId.HasValue)
            .GroupBy(x => x.AssignmentGroup?.Name ?? "Unassigned")
            .Select(group => new DashboardGroupMetricDto(group.Key, group.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var technicianWorkload = accessibleTechnicianAssignments
            .AsEnumerable()
            .GroupBy(x => x.Technician?.FullName ?? "Technician")
            .Select(group => new DashboardTechnicianWorkloadDto(
                group.Key,
                group.Count(),
                group.Count(x => x.Status == "Pending"),
                group.Count(x => x.ArrivalAt.HasValue && !x.DepartureAt.HasValue)))
            .OrderByDescending(x => x.ActiveWorkOrders)
            .ThenBy(x => x.TechnicianName)
            .ToList();

        return new DashboardSummaryDto(
            openWorkOrders,
            closedWorkOrders,
            overdueWorkOrders,
            assetCount,
            clientCount,
            materialsLowStock,
            unassignedWorkOrders,
            assignedToGroup,
            assignedToTechnicians,
            awaitingAcceptance,
            techniciansOnSite,
            contractsExpiringIn90Days,
            contractsExpiringIn30Days,
            criticalRenewalRiskContracts,
            expiredContracts,
            renewalInProgressContracts,
            workOrdersByGroup,
            technicianWorkload);
    }

    private Guid? TryGetUserId()
    {
        var rawUserId = User.FindFirstValue(TenantClaimTypes.UserId);
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }
}

public sealed record DashboardSummaryDto(
    int OpenWorkOrders,
    int ClosedWorkOrders,
    int OverdueWorkOrders,
    int Assets,
    int Clients,
    int MaterialsLowStock,
    int UnassignedWorkOrders,
    int AssignedToGroup,
    int AssignedToTechnicians,
    int AwaitingAcceptance,
    int TechniciansOnSite,
    int ContractsExpiringIn90Days,
    int ContractsExpiringIn30Days,
    int CriticalRenewalRiskContracts,
    int ExpiredContracts,
    int RenewalInProgressContracts,
    IReadOnlyCollection<DashboardGroupMetricDto> WorkOrdersByGroup,
    IReadOnlyCollection<DashboardTechnicianWorkloadDto> TechnicianWorkload)
{
    public static DashboardSummaryDto Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], []);
}

public sealed record DashboardGroupMetricDto(string GroupName, int Count);
public sealed record DashboardTechnicianWorkloadDto(string TechnicianName, int ActiveWorkOrders, int PendingResponses, int OnSiteCount);
