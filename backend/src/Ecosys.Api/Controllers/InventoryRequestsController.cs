using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The stores' single requests queue. Three kinds of record ask stores for parts — a technician's
/// parts request on a job, a stores issue slip, and the retired restock request (still listed until
/// its last open ones are finished) — and each keeps its own actions on its own controller. This
/// reads all three into one list with one set of stages, so stores work from one queue.
/// </summary>
[ApiController]
[Authorize]
[Route("api/inventory-requests")]
public sealed class InventoryRequestsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>The most recent records of each kind returned; older history is in each record's own list.</summary>
    private const int MaxPerSource = 500;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<InventoryRequestResponse>>> List([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(
            PermissionNames.CanViewMaterials,
            PermissionNames.CanRequestMaterials,
            PermissionNames.CanManageMaterials,
            PermissionNames.CanApproveMaterials,
            PermissionNames.CanIssueMaterials);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var technicianOnly = IsTechnicianOnlyWorkspaceUser();
        var userId = UserId;

        var partsQuery = dbContext.PartsRequests.AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.WorkOrder)
            .Include(x => x.Technician)
            .Include(x => x.Items).ThenInclude(x => x.InventoryItem)
            .Where(x => x.TenantId == TenantId)
            .WhereAccessible(scope, x => x.BranchId);
        if (technicianOnly) partsQuery = partsQuery.Where(x => x.TechnicianId == userId);
        var parts = await partsQuery.OrderByDescending(x => x.CreatedAt).Take(MaxPerSource).ToListAsync(cancellationToken);

        var issuesQuery = dbContext.MaterialIssues.AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.WorkOrder)
            .Include(x => x.RequestedByUser)
            .Include(x => x.Lines).ThenInclude(x => x.MaterialItem)
            .Where(x => x.TenantId == TenantId)
            .WhereAccessible(scope, x => x.BranchId);
        if (technicianOnly) issuesQuery = issuesQuery.Where(x => x.RequestedByUserId == userId);
        var issues = await issuesQuery.OrderByDescending(x => x.CreatedAt).Take(MaxPerSource).ToListAsync(cancellationToken);

        var restockQuery = dbContext.MaterialRequests.AsNoTracking()
            .Include(x => x.Branch)
            .Include(x => x.WorkOrder)
            .Include(x => x.RequestedByUser)
            .Include(x => x.Lines).ThenInclude(x => x.MaterialItem)
            .Where(x => x.TenantId == TenantId)
            .WhereAccessible(scope, x => x.BranchId);
        if (technicianOnly) restockQuery = restockQuery.Where(x => x.RequestedByUserId == userId);
        var restock = await restockQuery.OrderByDescending(x => x.CreatedAt).Take(MaxPerSource).ToListAsync(cancellationToken);

        var rows = new List<InventoryRequestResponse>(parts.Count + issues.Count + restock.Count);

        rows.AddRange(parts.Select(x => new InventoryRequestResponse(
            x.Id,
            InventoryRequestSources.PartsRequest,
            null,
            InventoryRequestStages.ForPartsRequest(x.Status),
            x.Status,
            x.WorkOrderId,
            x.WorkOrder?.WorkOrderNumber,
            x.Technician?.FullName,
            x.BranchId,
            x.Branch?.Name,
            x.Notes,
            x.RejectionReason,
            x.Items.Sum(i => (i.QuantityApproved ?? i.QuantityRequested) * (i.InventoryItem?.UnitCost ?? 0)),
            x.CreatedAt,
            x.Items.Select(i => new InventoryRequestLineResponse(
                i.Id,
                i.InventoryItemId,
                i.InventoryItem?.ItemCode,
                i.InventoryItem?.ItemName ?? i.Description,
                i.InventoryItem?.UnitOfMeasure,
                i.QuantityRequested,
                i.QuantityApproved,
                x.Status is "Dispatched" or "Delivered" ? i.QuantityApproved ?? i.QuantityRequested : 0,
                0)).ToList())));

        rows.AddRange(issues.Select(x => new InventoryRequestResponse(
            x.Id,
            InventoryRequestSources.IssueSlip,
            x.IssueNumber,
            InventoryRequestStages.ForIssueSlip(x.IssueStatus),
            x.IssueStatus,
            x.WorkOrderId,
            x.WorkOrder?.WorkOrderNumber,
            x.RequestedByUser?.FullName,
            x.BranchId,
            x.Branch?.Name,
            x.Notes,
            x.RejectionReason,
            x.IssueStatus is "issued" or "returned" ? x.TotalIssuedCost : x.TotalEstimatedCost,
            x.CreatedAt,
            x.Lines.Select(l => new InventoryRequestLineResponse(
                l.Id,
                l.MaterialItemId,
                l.MaterialItem?.ItemCode,
                l.MaterialItem?.ItemName,
                l.Unit,
                l.QuantityRequested,
                null,
                l.QuantityIssued,
                l.QuantityReturned)).ToList())));

        rows.AddRange(restock.Select(x => new InventoryRequestResponse(
            x.Id,
            InventoryRequestSources.Restock,
            x.RequestNumber,
            InventoryRequestStages.ForRestock(x.Status),
            x.Status,
            x.WorkOrderId,
            x.WorkOrder?.WorkOrderNumber,
            x.RequestedByUser?.FullName,
            x.BranchId,
            x.Branch?.Name,
            null,
            null,
            x.Lines.Sum(l => (l.QuantityIssued > 0 ? l.QuantityIssued : l.QuantityRequested) * (l.MaterialItem?.UnitCost ?? 0)),
            x.CreatedAt,
            x.Lines.Select(l => new InventoryRequestLineResponse(
                l.Id,
                l.MaterialItemId,
                l.MaterialItem?.ItemCode,
                l.MaterialItem?.ItemName,
                l.MaterialItem?.UnitOfMeasure,
                l.QuantityRequested,
                null,
                l.QuantityIssued,
                l.QuantityReturned)).ToList())));

        return Ok(rows.OrderByDescending(x => x.CreatedAt).ToList());
    }
}

public static class InventoryRequestSources
{
    /// <summary>A technician's request for parts on a work order.</summary>
    public const string PartsRequest = "PartsRequest";
    /// <summary>A stores issue slip, with or without a work order.</summary>
    public const string IssueSlip = "IssueSlip";
    /// <summary>The retired restock request; listed until its open records are finished.</summary>
    public const string Restock = "Restock";
}

/// <summary>
/// One flow for every request: Requested → Approved → Issued (handed over or dispatched) →
/// Completed (delivered, closed, or fully returned). Rejected and Cancelled end it early.
/// </summary>
public static class InventoryRequestStages
{
    public const string Requested = "Requested";
    public const string Approved = "Approved";
    public const string Issued = "Issued";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static string ForPartsRequest(string status) => status switch
    {
        "Pending" => Requested,
        "Approved" => Approved,
        "Dispatched" => Issued,
        "Delivered" => Completed,
        "Rejected" => Rejected,
        _ => status,
    };

    public static string ForIssueSlip(string status) => status.ToLowerInvariant() switch
    {
        "draft" or "pending_approval" => Requested,
        "approved" => Approved,
        "issued" => Issued,
        "returned" => Completed,
        "rejected" => Rejected,
        "cancelled" => Cancelled,
        _ => status,
    };

    public static string ForRestock(string status) => status switch
    {
        "Pending" => Requested,
        "Approved" => Approved,
        "Issued" => Issued,
        "Closed" => Completed,
        _ => status,
    };
}

/// <param name="Number">The document number; null for a job parts request, which is known by its work order.</param>
/// <param name="Status">The record's own status, for the actions its controller accepts.</param>
public sealed record InventoryRequestResponse(
    Guid Id,
    string Source,
    string? Number,
    string Stage,
    string Status,
    Guid? WorkOrderId,
    string? WorkOrderNumber,
    string? RequestedByName,
    Guid? BranchId,
    string? BranchName,
    string? Notes,
    string? RejectionReason,
    decimal Value,
    DateTime CreatedAt,
    IReadOnlyCollection<InventoryRequestLineResponse> Lines);

/// <param name="QuantityApproved">Job parts requests only: what was approved (null until then).</param>
public sealed record InventoryRequestLineResponse(
    Guid Id,
    Guid? MaterialItemId,
    string? ItemCode,
    string? ItemName,
    string? UnitOfMeasure,
    decimal QuantityRequested,
    decimal? QuantityApproved,
    decimal QuantityIssued,
    decimal QuantityReturned);
