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
[Authorize]
[Route("api/parts-requests")]
public sealed class PartsRequestsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IStockLedgerService stockLedgerService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("/api/workorders/{workOrderId:guid}/parts-requests")]
    public async Task<ActionResult<IReadOnlyCollection<PartsRequestResponse>>> GetForWorkOrder(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureTechnicianAssignedToWorkOrderAsync(dbContext, workOrderId, cancellationToken);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var requests = await Query(scope)
            .Where(x => x.WorkOrderId == workOrderId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(requests.Select(Map).ToList());
    }

    [HttpPost("/api/workorders/{workOrderId:guid}/parts-requests")]
    public async Task<ActionResult<PartsRequestResponse>> Create(Guid workOrderId, [FromBody] CreatePartsRequestRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await EnsureTechnicianAssignedToWorkOrderAsync(dbContext, workOrderId, cancellationToken);

        if (request.Items.Count == 0)
        {
            throw new BusinessRuleException("At least one part is required.");
        }

        var workOrderScope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrder = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == workOrderId)
            .WhereAccessible(workOrderScope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Work order was not found for this tenant.");

        var partsRequest = new PartsRequest
        {
            TenantId = TenantId,
            BranchId = workOrder.BranchId,
            WorkOrderId = workOrderId,
            TechnicianId = UserId,
            Status = "Pending",
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Items = new List<PartsRequestItem>()
        };

        foreach (var item in request.Items)
        {
            if (item.QuantityRequested <= 0)
            {
                throw new BusinessRuleException("Quantity requested must be greater than zero.");
            }

            if (item.InventoryItemId is null && string.IsNullOrWhiteSpace(item.Description))
            {
                throw new BusinessRuleException("Each part must reference an inventory item or include a description.");
            }

            if (item.InventoryItemId is Guid inventoryItemId)
            {
                var exists = await dbContext.MaterialItems.AnyAsync(x => x.TenantId == TenantId && x.Id == inventoryItemId, cancellationToken);
                if (!exists)
                {
                    throw new BusinessRuleException("Inventory item was not found for this tenant.");
                }
            }

            partsRequest.Items.Add(new PartsRequestItem
            {
                InventoryItemId = item.InventoryItemId,
                Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                QuantityRequested = item.QuantityRequested
            });
        }

        dbContext.PartsRequests.Add(partsRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Parts request submitted",
            nameof(PartsRequest),
            partsRequest.Id.ToString(),
            $"Requested parts for work order '{workOrder.WorkOrderNumber}'.",
            cancellationToken);

        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, partsRequest.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == partsRequest.Id, cancellationToken);
        return CreatedAtAction(nameof(GetForWorkOrder), new { workOrderId }, Map(persisted));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PartsRequestResponse>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? technicianId,
        [FromQuery] Guid? workOrderId,
        [FromQuery] Guid? branchId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);

        var query = Query(scope);
        if (IsTechnicianOnlyWorkspaceUser())
        {
            // Field technicians see their own requests, not the whole branch's stores queue.
            var userId = UserId;
            query = query.Where(x => x.TechnicianId == userId);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }
        if (technicianId is Guid technician)
        {
            query = query.Where(x => x.TechnicianId == technician);
        }
        if (workOrderId is Guid workOrder)
        {
            query = query.Where(x => x.WorkOrderId == workOrder);
        }
        if (fromDate is DateTime from)
        {
            query = query.Where(x => x.CreatedAt >= from.ToUniversalTime());
        }
        if (toDate is DateTime to)
        {
            query = query.Where(x => x.CreatedAt <= to.ToUniversalTime());
        }

        var requests = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Ok(requests.Select(Map).ToList());
    }

    [HttpPatch("{id:guid}/approve")]
    public async Task<ActionResult<PartsRequestResponse>> Approve(Guid id, [FromBody] ApprovePartsRequestRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanApproveMaterials);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var partsRequest = await dbContext.PartsRequests
            .Include(x => x.Items)
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Parts request was not found.");

        if (partsRequest.Status != "Pending")
        {
            throw new BusinessRuleException("Only pending parts requests can be approved.");
        }

        foreach (var item in partsRequest.Items)
        {
            var itemOverride = request.Items?.SingleOrDefault(x => x.PartsRequestItemId == item.Id);
            var quantityApproved = itemOverride?.QuantityApproved ?? item.QuantityRequested;

            if (quantityApproved < 0 || quantityApproved > item.QuantityRequested)
            {
                throw new BusinessRuleException("Quantity approved must be between zero and the quantity requested.");
            }

            item.QuantityApproved = quantityApproved;
        }

        partsRequest.Status = "Approved";
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Parts request approved",
            nameof(PartsRequest),
            partsRequest.Id.ToString(),
            "Approved parts request.",
            cancellationToken);

        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, partsRequest.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == partsRequest.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPatch("{id:guid}/reject")]
    public async Task<ActionResult<PartsRequestResponse>> Reject(Guid id, [FromBody] RejectPartsRequestRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanApproveMaterials);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException("A rejection reason is required.");
        }

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var partsRequest = await dbContext.PartsRequests
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Parts request was not found.");

        if (partsRequest.Status != "Pending")
        {
            throw new BusinessRuleException("Only pending parts requests can be rejected.");
        }

        partsRequest.Status = "Rejected";
        partsRequest.RejectionReason = request.Reason.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Parts request rejected",
            nameof(PartsRequest),
            partsRequest.Id.ToString(),
            $"Rejected parts request: {partsRequest.RejectionReason}",
            cancellationToken);

        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, partsRequest.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == partsRequest.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    /// <summary>
    /// Sends the approved parts out of stores: each catalogue item leaves the request's branch stock
    /// and is booked on the work order as used, so it shows in the job's parts and can come back
    /// through a parts return. Free-text items (no catalogue part) move no stock.
    /// </summary>
    [HttpPatch("{id:guid}/dispatch")]
    public async Task<ActionResult<PartsRequestResponse>> Dispatch(
        Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] DispatchPartsRequestRequest? request,
        CancellationToken cancellationToken)
    {
        // Parts normally leave the job's branch; stores can send them from another branch it can reach.
        var sourceBranchId = request?.SourceBranchId is Guid requestedBranch
            ? await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, requestedBranch, cancellationToken)
            : null;
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanIssueMaterials);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        PartsRequest partsRequest = null!;
        await stockLedgerService.ExecuteAsync(async ct =>
        {
            partsRequest = await dbContext.PartsRequests
                .Include(x => x.Items)
                    .ThenInclude(x => x.InventoryItem)
                .Include(x => x.WorkOrder)
                .Where(x => x.TenantId == TenantId && x.Id == id)
                .WhereAccessible(scope, x => x.BranchId)
                .SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException("Parts request was not found.");

            if (partsRequest.Status != "Approved")
            {
                throw new BusinessRuleException("Only approved parts requests can be dispatched.");
            }

            var workOrderNumber = partsRequest.WorkOrder?.WorkOrderNumber;
            foreach (var item in partsRequest.Items)
            {
                var quantity = item.QuantityApproved ?? item.QuantityRequested;
                if (item.InventoryItem is not { } material || quantity <= 0)
                    continue;

                await stockLedgerService.RecordIssueAsync(
                    material, sourceBranchId ?? partsRequest.BranchId, quantity, UserId,
                    $"Parts request dispatched for work order {workOrderNumber}", workOrderNumber,
                    partsRequest.WorkOrderId, cancellationToken: ct);
                dbContext.WorkOrderMaterialUsages.Add(new WorkOrderMaterialUsage
                {
                    TenantId = TenantId,
                    WorkOrderId = partsRequest.WorkOrderId,
                    MaterialItemId = material.Id,
                    AssetId = partsRequest.WorkOrder?.AssetId,
                    QuantityUsed = quantity,
                    UnitCost = material.UnitCost,
                    Chargeable = true,
                    UsedByUserId = partsRequest.TechnicianId,
                    Notes = "Parts request dispatch",
                });
            }

            partsRequest.Status = "Dispatched";
            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Parts request dispatched",
            nameof(PartsRequest),
            partsRequest.Id.ToString(),
            "Dispatched parts request from stores.",
            cancellationToken);

        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, partsRequest.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == partsRequest.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPatch("{id:guid}/deliver")]
    public async Task<ActionResult<PartsRequestResponse>> Deliver(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var partsRequest = await dbContext.PartsRequests
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Parts request was not found.");

        if (partsRequest.TechnicianId != UserId && !IsAdmin)
        {
            throw new ForbiddenException("Only the requesting technician or an admin can confirm delivery.");
        }

        if (partsRequest.Status != "Dispatched")
        {
            throw new BusinessRuleException("Only dispatched parts requests can be marked as delivered.");
        }

        partsRequest.Status = "Delivered";
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Parts request delivered",
            nameof(PartsRequest),
            partsRequest.Id.ToString(),
            "Technician confirmed delivery of parts.",
            cancellationToken);

        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, partsRequest.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == partsRequest.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    private IQueryable<PartsRequest> Query(BranchQueryScope scope) =>
        dbContext.PartsRequests
            .Include(x => x.Branch)
            .Include(x => x.WorkOrder)
            .Include(x => x.Technician)
            .Include(x => x.Items)
                .ThenInclude(x => x.InventoryItem)
            .Where(x => x.TenantId == TenantId)
            .WhereAccessible(scope, x => x.BranchId);

    private static PartsRequestResponse Map(PartsRequest request) =>
        new(
            request.Id,
            request.WorkOrderId,
            request.WorkOrder?.WorkOrderNumber,
            request.TechnicianId,
            request.Technician?.FullName,
            request.BranchId,
            request.Branch?.Name,
            request.Status,
            request.Notes,
            request.RejectionReason,
            request.CreatedAt,
            request.UpdatedAt,
            request.Items.Select(item => new PartsRequestItemResponse(
                item.Id,
                item.InventoryItemId,
                item.InventoryItem?.ItemCode,
                item.InventoryItem?.ItemName,
                item.Description,
                item.QuantityRequested,
                item.QuantityApproved)).ToList());
}

public sealed record CreatePartsRequestRequest(string? Notes, IReadOnlyCollection<CreatePartsRequestItemRequest> Items);

public sealed record CreatePartsRequestItemRequest(Guid? InventoryItemId, string? Description, decimal QuantityRequested);

public sealed record ApprovePartsRequestRequest(IReadOnlyCollection<ApprovePartsRequestItemRequest>? Items);

public sealed record ApprovePartsRequestItemRequest(Guid PartsRequestItemId, decimal QuantityApproved);

public sealed record RejectPartsRequestRequest(string Reason);
/// <summary>Optional branch the parts leave from; the request's own branch when omitted.</summary>
public sealed record DispatchPartsRequestRequest(Guid? SourceBranchId);

public sealed record PartsRequestResponse(
    Guid Id,
    Guid WorkOrderId,
    string? WorkOrderNumber,
    Guid TechnicianId,
    string? TechnicianName,
    Guid? BranchId,
    string? BranchName,
    string Status,
    string? Notes,
    string? RejectionReason,
    DateTime RequestedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<PartsRequestItemResponse> Items);

public sealed record PartsRequestItemResponse(
    Guid Id,
    Guid? InventoryItemId,
    string? ItemCode,
    string? ItemName,
    string? Description,
    decimal QuantityRequested,
    decimal? QuantityApproved);
