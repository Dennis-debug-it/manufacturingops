using System.Data;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Parts going back from a job: the technician raises a return on the work order (Pending), stores
/// receives or rejects it. Only receiving touches stock and usage, per line condition — Unused parts
/// are restocked and taken off the job's usage, Faulty parts are taken off usage but not restocked,
/// Removed (old) parts are recorded only. See <see cref="PartsReturnConditions"/>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/parts-returns")]
public sealed class PartsReturnsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IDocumentNumberingService documentNumberingService,
    IStockLedgerService stockLedgerService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PartsReturnResponse>>> List([FromQuery] string? status, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var query = Query(await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken));
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = status.Trim();
            query = query.Where(x => x.Status == normalized);
        }

        var returns = await query.OrderByDescending(x => x.RequestedAt).Take(500).ToListAsync(cancellationToken);
        return Ok(returns.Select(Map).ToList());
    }

    [HttpGet("/api/workorders/{workOrderId:guid}/parts-returns")]
    public async Task<ActionResult<IReadOnlyCollection<PartsReturnResponse>>> GetForWorkOrder(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var returns = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken))
            .Where(x => x.WorkOrderId == workOrderId)
            .OrderByDescending(x => x.RequestedAt)
            .ToListAsync(cancellationToken);
        return Ok(returns.Select(Map).ToList());
    }

    /// <summary>Parts booked on the job that can still be sent back: used quantity less anything already pending return.</summary>
    [HttpGet("/api/workorders/{workOrderId:guid}/parts-returns/returnable")]
    public async Task<ActionResult<IReadOnlyCollection<ReturnablePartResponse>>> GetReturnable(Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await LoadWorkOrderAsync(workOrderId, cancellationToken);

        var usages = await dbContext.WorkOrderMaterialUsages
            .AsNoTracking()
            .Include(x => x.MaterialItem)
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.QuantityUsed > 0)
            .OrderBy(x => x.UsedAt)
            .ToListAsync(cancellationToken);
        var pending = await PendingQuantitiesByUsageAsync(workOrderId, cancellationToken);

        return Ok(usages
            .Select(x => new ReturnablePartResponse(
                x.Id, x.MaterialItemId, x.MaterialItem?.ItemCode, x.MaterialItem?.ItemName ?? "Part", x.MaterialItem?.UnitOfMeasure,
                x.QuantityUsed, pending.GetValueOrDefault(x.Id), Math.Max(0, x.QuantityUsed - pending.GetValueOrDefault(x.Id)), x.UsedAt))
            .Where(x => x.ReturnableQuantity > 0)
            .ToList());
    }

    [HttpPost("/api/workorders/{workOrderId:guid}/parts-returns")]
    public async Task<ActionResult<PartsReturnResponse>> Create(Guid workOrderId, [FromBody] CreatePartsReturnRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var workOrder = await LoadWorkOrderAsync(workOrderId, cancellationToken);
        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new BusinessRuleException("Add at least one part to return.");
        }

        var usageIds = request.Lines.Where(x => x.WorkOrderMaterialUsageId.HasValue).Select(x => x.WorkOrderMaterialUsageId!.Value).ToList();
        if (usageIds.Count != usageIds.Distinct().Count())
        {
            throw new BusinessRuleException("Each used part can only appear once in a return.");
        }

        var usages = await dbContext.WorkOrderMaterialUsages
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && usageIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var pending = await PendingQuantitiesByUsageAsync(workOrderId, cancellationToken);

        var partsReturn = new PartsReturn
        {
            TenantId = TenantId,
            BranchId = workOrder.BranchId,
            WorkOrderId = workOrderId,
            RequestedByUserId = UserId,
            RequestedAt = DateTime.UtcNow,
            Notes = Trim(request.Notes, 2000),
        };

        foreach (var line in request.Lines)
        {
            var condition = PartsReturnConditions.All.FirstOrDefault(x => string.Equals(x, line.Condition?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException("Condition must be Unused, Faulty, or Removed.");
            if (line.Quantity <= 0)
            {
                throw new BusinessRuleException("Return quantity must be greater than zero.");
            }

            Guid materialItemId;
            if (condition == PartsReturnConditions.Removed)
            {
                // An old part taken out of the client's equipment was never booked on this job.
                materialItemId = line.MaterialItemId
                    ?? throw new BusinessRuleException("Select which part was removed from the equipment.");
                if (!await dbContext.MaterialItems.AnyAsync(x => x.TenantId == TenantId && x.Id == materialItemId, cancellationToken))
                {
                    throw new BusinessRuleException("Inventory item was not found for this tenant.");
                }
            }
            else
            {
                var usageId = line.WorkOrderMaterialUsageId
                    ?? throw new BusinessRuleException("Unused and faulty returns must reference a part used on this job.");
                var usage = usages.GetValueOrDefault(usageId)
                    ?? throw new BusinessRuleException("That part was not used on this work order.");
                var returnable = usage.QuantityUsed - pending.GetValueOrDefault(usageId);
                if (line.Quantity > returnable)
                {
                    throw new BusinessRuleException($"Only {returnable:0.##} of that part can still be returned.");
                }

                materialItemId = usage.MaterialItemId;
            }

            partsReturn.Lines.Add(new PartsReturnLine
            {
                MaterialItemId = materialItemId,
                WorkOrderMaterialUsageId = condition == PartsReturnConditions.Removed ? null : line.WorkOrderMaterialUsageId,
                Quantity = line.Quantity,
                Condition = condition,
                Notes = Trim(line.Notes, 1000),
            });
        }

        partsReturn.ReturnNumber = await documentNumberingService.GenerateAsync(TenantId, workOrder.BranchId, DocumentTypes.PartsReturn, cancellationToken);
        dbContext.PartsReturns.Add(partsReturn);
        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId,
            WorkOrderId = workOrderId,
            ActorUserId = UserId,
            EventType = "PartsReturnRequested",
            Status = WorkOrderStatuses.Normalize(workOrder.Status),
            Message = $"Parts return {partsReturn.ReturnNumber} raised ({partsReturn.Lines.Count} line(s)).",
            OccurredAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "parts_return.requested", nameof(PartsReturn), partsReturn.Id.ToString(),
            $"Raised parts return {partsReturn.ReturnNumber} for work order {workOrder.WorkOrderNumber}.", cancellationToken);

        return CreatedAtAction(nameof(GetForWorkOrder), new { workOrderId }, Map(await LoadAsync(partsReturn.Id, cancellationToken)));
    }

    [HttpPost("{id:guid}/receive")]
    public async Task<ActionResult<PartsReturnResponse>> Receive(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanIssueMaterials);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;

        var partsReturn = await LoadAsync(id, cancellationToken);
        EnsurePending(partsReturn);
        var workOrder = partsReturn.WorkOrder!;

        foreach (var line in partsReturn.Lines)
        {
            if (line.Condition == PartsReturnConditions.Removed)
            {
                continue;
            }

            // Re-checked here, not just at request time: usage can have been edited or returned since.
            var usage = await dbContext.WorkOrderMaterialUsages.SingleOrDefaultAsync(
                    x => x.TenantId == TenantId && x.Id == line.WorkOrderMaterialUsageId && x.WorkOrderId == partsReturn.WorkOrderId, cancellationToken)
                ?? throw new BusinessRuleException($"The job no longer has the usage this {line.MaterialItem?.ItemName ?? "part"} was returned against.");
            if (usage.QuantityUsed < line.Quantity)
            {
                throw new BusinessRuleException($"{line.MaterialItem?.ItemName ?? "A part"} now has only {usage.QuantityUsed:0.##} booked on the job; reject this return and raise a new one.");
            }

            usage.QuantityUsed -= line.Quantity;
            if (line.Condition == PartsReturnConditions.Unused)
            {
                var material = await dbContext.MaterialItems.SingleAsync(x => x.TenantId == TenantId && x.Id == line.MaterialItemId, cancellationToken);
                await stockLedgerService.RecordReturnAsync(material, partsReturn.BranchId, line.Quantity, UserId,
                    $"Unused part returned from work order {workOrder.WorkOrderNumber}", partsReturn.ReturnNumber, workOrder.Id, null, cancellationToken);
            }
        }

        partsReturn.Status = PartsReturnStatuses.Received;
        partsReturn.ProcessedByUserId = UserId;
        partsReturn.ProcessedAt = DateTime.UtcNow;
        dbContext.WorkOrderEvents.Add(new WorkOrderEvent
        {
            TenantId = TenantId,
            WorkOrderId = workOrder.Id,
            ActorUserId = UserId,
            EventType = "PartsReturnReceived",
            Status = WorkOrderStatuses.Normalize(workOrder.Status),
            Message = $"Parts return {partsReturn.ReturnNumber} received by stores.",
            OccurredAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "parts_return.received", nameof(PartsReturn), partsReturn.Id.ToString(),
            $"Received parts return {partsReturn.ReturnNumber} for work order {workOrder.WorkOrderNumber}.", cancellationToken);
        return Ok(Map(partsReturn));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<PartsReturnResponse>> Reject(Guid id, [FromBody] RejectPartsReturnRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanIssueMaterials);
        var reason = Trim(request.Reason, 1000) ?? throw new BusinessRuleException("A reason is required to reject a return.");
        var partsReturn = await LoadAsync(id, cancellationToken);
        EnsurePending(partsReturn);

        partsReturn.Status = PartsReturnStatuses.Rejected;
        partsReturn.RejectionReason = reason;
        partsReturn.ProcessedByUserId = UserId;
        partsReturn.ProcessedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "parts_return.rejected", nameof(PartsReturn), partsReturn.Id.ToString(),
            $"Rejected parts return {partsReturn.ReturnNumber}: {reason}", cancellationToken);
        return Ok(Map(partsReturn));
    }

    private static void EnsurePending(PartsReturn partsReturn)
    {
        if (partsReturn.Status != PartsReturnStatuses.Pending)
        {
            throw new BusinessRuleException($"Only pending returns can be processed (this one is {partsReturn.Status}).");
        }
    }

    private async Task<Dictionary<Guid, decimal>> PendingQuantitiesByUsageAsync(Guid workOrderId, CancellationToken cancellationToken) =>
        await dbContext.PartsReturnLines
            .Where(x => x.PartsReturn!.TenantId == TenantId && x.PartsReturn.WorkOrderId == workOrderId
                && x.PartsReturn.Status == PartsReturnStatuses.Pending && x.WorkOrderMaterialUsageId != null)
            .GroupBy(x => x.WorkOrderMaterialUsageId!.Value)
            .Select(g => new { UsageId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.UsageId, x => x.Quantity, cancellationToken);

    private async Task<WorkOrder> LoadWorkOrderAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        return await dbContext.WorkOrders
                .Where(x => x.TenantId == TenantId && x.Id == workOrderId)
                .WhereAccessible(scope, x => x.BranchId)
                .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");
    }

    private async Task<PartsReturn> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await Query(await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken)).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Parts return was not found.");

    private IQueryable<PartsReturn> Query(BranchQueryScope scope) =>
        dbContext.PartsReturns
            .Include(x => x.WorkOrder)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ProcessedByUser)
            .Include(x => x.Lines).ThenInclude(x => x.MaterialItem)
            .Where(x => x.TenantId == TenantId)
            .WhereAccessible(scope, x => x.BranchId);

    private static string? Trim(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];

    private static PartsReturnResponse Map(PartsReturn value) => new(
        value.Id,
        value.ReturnNumber,
        value.WorkOrderId,
        value.WorkOrder?.WorkOrderNumber,
        value.Status,
        value.RequestedByUser?.FullName,
        value.RequestedAt,
        value.ProcessedByUser?.FullName,
        value.ProcessedAt,
        value.Notes,
        value.RejectionReason,
        value.Lines.Select(x => new PartsReturnLineResponse(
            x.Id, x.MaterialItemId, x.MaterialItem?.ItemCode, x.MaterialItem?.ItemName ?? "Part",
            x.WorkOrderMaterialUsageId, x.Quantity, x.Condition, x.Notes)).ToList());
}

public sealed record CreatePartsReturnRequest(string? Notes, IReadOnlyCollection<CreatePartsReturnLineRequest> Lines);

/// <summary>Unused/Faulty: <c>WorkOrderMaterialUsageId</c> is required. Removed: <c>MaterialItemId</c> is required.</summary>
public sealed record CreatePartsReturnLineRequest(Guid? WorkOrderMaterialUsageId, Guid? MaterialItemId, decimal Quantity, string Condition, string? Notes);

public sealed record RejectPartsReturnRequest(string? Reason);

public sealed record ReturnablePartResponse(
    Guid WorkOrderMaterialUsageId, Guid MaterialItemId, string? ItemCode, string ItemName, string? UnitOfMeasure,
    decimal QuantityUsed, decimal PendingReturnQuantity, decimal ReturnableQuantity, DateTime UsedAt);

public sealed record PartsReturnResponse(
    Guid Id, string ReturnNumber, Guid WorkOrderId, string? WorkOrderNumber, string Status,
    string? RequestedByName, DateTime RequestedAt, string? ProcessedByName, DateTime? ProcessedAt,
    string? Notes, string? RejectionReason, IReadOnlyList<PartsReturnLineResponse> Lines);

public sealed record PartsReturnLineResponse(
    Guid Id, Guid MaterialItemId, string? ItemCode, string ItemName, Guid? WorkOrderMaterialUsageId,
    decimal Quantity, string Condition, string? Notes);
