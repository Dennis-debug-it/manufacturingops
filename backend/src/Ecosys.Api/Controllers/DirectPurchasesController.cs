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
/// Parts bought straight from a supplier for a job, waiting to be reconciled. Their use took the
/// branch balance down (below zero if stores had none); reconciling records the supplier invoice and
/// books the same quantity back in, so the balance returns to what stores really holds.
/// </summary>
[ApiController]
[Authorize]
[Route("api/direct-purchases")]
public sealed class DirectPurchasesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IStockLedgerService stockLedgerService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<DirectPurchaseResponse>>> List([FromQuery] string? status, CancellationToken cancellationToken)
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
    /// Reconciles pending direct purchases against one supplier invoice: each is booked back into its
    /// branch as a receipt referencing the invoice, with the invoiced unit cost where given.
    /// </summary>
    [HttpPost("reconcile")]
    public async Task<ActionResult<IReadOnlyCollection<DirectPurchaseResponse>>> Reconcile([FromBody] ReconcileDirectPurchasesRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials, PermissionNames.CanIssueMaterials);
        var invoice = request.InvoiceReference?.Trim();
        if (string.IsNullOrWhiteSpace(invoice))
            throw new BusinessRuleException("Enter the supplier's invoice number.");
        if (request.Ids.Count == 0)
            throw new BusinessRuleException("Choose at least one purchase to reconcile.");

        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var ids = request.Ids.Distinct().ToList();
        List<DirectPurchase> purchases = [];
        await stockLedgerService.ExecuteAsync(async ct =>
        {
            purchases = await Query().WhereAccessible(scope, x => x.BranchId).Where(x => ids.Contains(x.Id)).ToListAsync(ct);
            if (purchases.Count != ids.Count)
                throw new NotFoundException("A purchase was not found.");
            if (purchases.FirstOrDefault(x => x.Status != DirectPurchaseStatuses.Pending) is { } done)
                throw new BusinessRuleException($"{done.MaterialItem?.ItemName ?? "A purchase"} was already reconciled.");
            if (request.UnitCosts?.Values.Any(x => x < 0) == true)
                throw new BusinessRuleException("Unit costs cannot be negative.");

            foreach (var purchase in purchases)
            {
                var material = await dbContext.MaterialItems.SingleAsync(x => x.TenantId == TenantId && x.Id == purchase.MaterialItemId, ct);
                if (request.UnitCosts?.TryGetValue(purchase.Id, out var cost) == true)
                    purchase.UnitCost = cost;
                await stockLedgerService.RecordReceiptAsync(material, purchase.BranchId, purchase.Quantity, UserId,
                    $"Direct purchase{(purchase.WorkOrder is null ? string.Empty : $" for {purchase.WorkOrder.WorkOrderNumber}")} reconciled with invoice {invoice}",
                    invoice, purchase.WorkOrderId, cancellationToken: ct);
                purchase.Status = DirectPurchaseStatuses.Reconciled;
                purchase.InvoiceReference = invoice;
                purchase.ReconciledByUserId = UserId;
                purchase.ReconciledAt = DateTime.UtcNow;
                if (request.SupplierId is Guid supplierId && purchase.SupplierId is null)
                    purchase.SupplierId = supplierId;
            }

            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "inventory.direct_purchase.reconciled", nameof(DirectPurchase), invoice,
            $"Reconciled {purchases.Count} direct purchase(s) with supplier invoice {invoice}.", cancellationToken);
        var reloaded = await Query().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        return Ok(reloaded.Select(Map).ToList());
    }

    private IQueryable<DirectPurchase> Query() =>
        dbContext.DirectPurchases
            .Include(x => x.MaterialItem)
            .Include(x => x.Branch)
            .Include(x => x.WorkOrder)
            .Include(x => x.Supplier)
            .Include(x => x.PurchasedByUser)
            .Include(x => x.ReconciledByUser)
            .Where(x => x.TenantId == TenantId);

    private static DirectPurchaseResponse Map(DirectPurchase x) => new(
        x.Id, x.MaterialItemId, x.MaterialItem?.ItemCode, x.MaterialItem?.ItemName, x.MaterialItem?.UnitOfMeasure,
        x.Quantity, x.UnitCost, x.Status, x.BranchId, x.Branch?.Name, x.WorkOrderId, x.WorkOrder?.WorkOrderNumber,
        x.SupplierId, x.Supplier?.SupplierName ?? x.SupplierName, x.SupplierReference, x.PurchasedByUser?.FullName,
        x.InvoiceReference, x.ReconciledByUser?.FullName, x.ReconciledAt, x.CreatedAt);
}

/// <param name="UnitCosts">The invoiced unit cost per purchase, by id, where it differs from (or fills in) what was logged.</param>
/// <param name="SupplierId">Links purchases logged with only a typed supplier name to this supplier.</param>
public sealed record ReconcileDirectPurchasesRequest(IReadOnlyList<Guid> Ids, string? InvoiceReference, IReadOnlyDictionary<Guid, decimal>? UnitCosts = null, Guid? SupplierId = null);

public sealed record DirectPurchaseResponse(
    Guid Id, Guid MaterialItemId, string? ItemCode, string? ItemName, string? UnitOfMeasure,
    decimal Quantity, decimal? UnitCost, string Status, Guid? BranchId, string? BranchName,
    Guid? WorkOrderId, string? WorkOrderNumber, Guid? SupplierId, string? SupplierName, string? SupplierReference,
    string? PurchasedByName, string? InvoiceReference, string? ReconciledByName, DateTime? ReconciledAt, DateTime CreatedAt);
