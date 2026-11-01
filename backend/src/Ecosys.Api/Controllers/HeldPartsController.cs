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
/// Faulty parts stores is holding (from parts returns received as Faulty) and what happened to them:
/// sent back to the supplier, written off, or put back into stock once found sound.
/// </summary>
[ApiController]
[Authorize]
[Route("api/held-parts")]
public sealed class HeldPartsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IStockLedgerService stockLedgerService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<HeldPartResponse>>> List([FromQuery] string? status, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(
            PermissionNames.CanViewMaterials, PermissionNames.CanManageMaterials, PermissionNames.CanApproveMaterials, PermissionNames.CanIssueMaterials);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var query = Query().WhereAccessible(scope, x => x.BranchId);
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == status.Trim());
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync(cancellationToken);
        return Ok(rows.Select(Map).ToList());
    }

    /// <summary>
    /// Resolves some or all of a held part. ReturnToSupplier records the supplier (and RMA reference);
    /// WriteOff needs a reason; Restock puts the part back into a branch's stock. A smaller quantity
    /// than is held splits the row, leaving the rest held.
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    public async Task<ActionResult<HeldPartResponse>> Resolve(Guid id, [FromBody] ResolveHeldPartRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanIssueMaterials, PermissionNames.CanManageMaterials);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        HeldPart resolved = null!;
        await stockLedgerService.ExecuteAsync(async ct =>
        {
            var held = await Query().WhereAccessible(scope, x => x.BranchId).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Held part was not found.");
            if (held.Status != HeldPartStatuses.Held)
                throw new BusinessRuleException("This part has already been dealt with.");

            var quantity = request.Quantity ?? held.Quantity;
            if (quantity <= 0 || quantity > held.Quantity)
                throw new BusinessRuleException($"Quantity must be more than 0 and at most {held.Quantity:0.##}.");
            var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            var reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();

            // Every check before anything changes.
            MaterialItem? material = null;
            switch (request.Action)
            {
                case HeldPartActions.ReturnToSupplier:
                    if (request.SupplierId is not Guid supplierToCheck)
                        throw new BusinessRuleException("Choose the supplier it is going back to.");
                    if (!await dbContext.Suppliers.AnyAsync(x => x.TenantId == TenantId && x.Id == supplierToCheck, ct))
                        throw new BusinessRuleException("Supplier was not found.");
                    break;
                case HeldPartActions.WriteOff:
                    if (note is null)
                        throw new BusinessRuleException("Give a reason for writing it off.");
                    break;
                case HeldPartActions.Restock:
                    material = await dbContext.MaterialItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == held.MaterialItemId && x.IsActive, ct)
                        ?? throw new BusinessRuleException("The part is no longer active, so it can't go back into stock.");
                    break;
                default:
                    throw new BusinessRuleException("Action must be ReturnToSupplier, WriteOff or Restock.");
            }

            // Resolve part of the row: the resolved share becomes its own record, the rest stays held.
            resolved = held;
            if (quantity < held.Quantity)
            {
                held.Quantity -= quantity;
                resolved = new HeldPart
                {
                    TenantId = TenantId, MaterialItemId = held.MaterialItemId, BranchId = held.BranchId, Quantity = quantity,
                    UnitCost = held.UnitCost, SourceReference = held.SourceReference, PartsReturnId = held.PartsReturnId,
                    WorkOrderId = held.WorkOrderId, Notes = held.Notes,
                };
                dbContext.HeldParts.Add(resolved);
            }

            switch (request.Action)
            {
                case HeldPartActions.ReturnToSupplier:
                    resolved.Status = HeldPartStatuses.ReturnedToSupplier;
                    resolved.SupplierId = request.SupplierId;
                    break;
                case HeldPartActions.WriteOff:
                    resolved.Status = HeldPartStatuses.WrittenOff;
                    break;
                case HeldPartActions.Restock:
                    var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId ?? held.BranchId, ct);
                    await stockLedgerService.RecordReturnAsync(material!, branchId, quantity, UserId,
                        $"Faulty part back in stock ({held.SourceReference}){(note is null ? string.Empty : $": {note}")}", held.SourceReference, held.WorkOrderId, null, ct);
                    resolved.Status = HeldPartStatuses.Restocked;
                    break;
            }

            resolved.ResolutionReference = reference;
            resolved.ResolutionNote = note;
            resolved.ResolvedByUserId = UserId;
            resolved.ResolvedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "inventory.held_part.resolved", nameof(HeldPart), resolved.Id.ToString(),
            $"Faulty part {resolved.SourceReference}: {resolved.Quantity:0.##} {resolved.Status}{(resolved.ResolutionReference is null ? string.Empty : $" ({resolved.ResolutionReference})")}.", cancellationToken);
        return Ok(Map(await Query().SingleAsync(x => x.Id == resolved.Id, cancellationToken)));
    }

    private IQueryable<HeldPart> Query() =>
        dbContext.HeldParts
            .Include(x => x.MaterialItem)
            .Include(x => x.Branch)
            .Include(x => x.WorkOrder)
            .Include(x => x.Supplier)
            .Include(x => x.ResolvedByUser)
            .Where(x => x.TenantId == TenantId);

    private static HeldPartResponse Map(HeldPart x) => new(
        x.Id, x.MaterialItemId, x.MaterialItem?.ItemCode, x.MaterialItem?.ItemName, x.MaterialItem?.UnitOfMeasure,
        x.Quantity, x.UnitCost, x.Status, x.BranchId, x.Branch?.Name, x.SourceReference, x.WorkOrderId, x.WorkOrder?.WorkOrderNumber,
        x.Notes, x.SupplierId, x.Supplier?.SupplierName, x.ResolutionReference, x.ResolutionNote, x.ResolvedByUser?.FullName, x.ResolvedAt, x.CreatedAt);
}

public static class HeldPartActions
{
    public const string ReturnToSupplier = "ReturnToSupplier";
    public const string WriteOff = "WriteOff";
    public const string Restock = "Restock";
}

/// <param name="Quantity">How much to resolve; all of it when omitted.</param>
/// <param name="Reference">The supplier's RMA number or a write-off reference.</param>
/// <param name="BranchId">Restock: the branch it goes back into (the holding branch when omitted).</param>
public sealed record ResolveHeldPartRequest(string Action, decimal? Quantity = null, Guid? SupplierId = null, string? Reference = null, string? Note = null, Guid? BranchId = null);

public sealed record HeldPartResponse(
    Guid Id, Guid MaterialItemId, string? ItemCode, string? ItemName, string? UnitOfMeasure,
    decimal Quantity, decimal? UnitCost, string Status, Guid? BranchId, string? BranchName,
    string? SourceReference, Guid? WorkOrderId, string? WorkOrderNumber, string? Notes,
    Guid? SupplierId, string? SupplierName, string? ResolutionReference, string? ResolutionNote,
    string? ResolvedByName, DateTime? ResolvedAt, DateTime CreatedAt);
