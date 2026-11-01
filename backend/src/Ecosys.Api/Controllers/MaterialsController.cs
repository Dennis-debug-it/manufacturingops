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
[Route("api/materials")]
public sealed class MaterialsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IStockLedgerService stockLedgerService,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService,
    IBranchAccessService branchAccessService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<MaterialItemResponse>>> GetAll(
        [FromQuery] bool lowStockOnly,
        [FromQuery] Guid? branchId,
        [FromQuery] string? search,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        EnsureMaterialsViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var normalizedStatus = NormalizeStatusFilter(status);
        var normalizedSearch = NormalizeSearch(search);
        var items = await LoadVisibleMaterialResponsesAsync(scope, normalizedStatus, normalizedSearch, cancellationToken);

        return Ok(lowStockOnly ? items.Where(x => x.IsLowStock).ToList() : items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaterialItemResponse>> Get(Guid id, [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        EnsureMaterialsViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var item = await dbContext.MaterialItems
            .Include(x => x.PreferredSupplier)
            .Include(x => x.BranchStocks)
                .ThenInclude(x => x.Branch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material item was not found.");

        return Ok(Map(item, scope, await SettingsAsync(cancellationToken)));
    }

    /// <summary>Where this part is held: each visible branch's balance and each van carrying it.</summary>
    [HttpGet("{id:guid}/stock")]
    public async Task<ActionResult<MaterialStockLocationsResponse>> GetStockLocations(Guid id, CancellationToken cancellationToken)
    {
        EnsureMaterialsViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        if (!await dbContext.MaterialItems.AnyAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken))
            throw new NotFoundException("Material item was not found.");

        var branches = await dbContext.BranchMaterialStocks.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.MaterialId == id)
            .WhereAccessible(scope, x => (Guid?)x.BranchId)
            .OrderBy(x => x.Branch!.Name)
            .Select(x => new MaterialBranchStockResponse(
                x.BranchId, x.Branch!.Name, x.QuantityOnHand, x.ReorderLevel, x.UnitCost, x.QuantityOnHand <= x.ReorderLevel && (x.QuantityOnHand != 0 || x.ReorderLevel > 0)))
            .ToListAsync(cancellationToken);
        var vans = await dbContext.VanMaterialStocks.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.MaterialId == id && x.QuantityOnHand != 0)
            .WhereAccessible(scope, x => x.Van!.HomeBranchId)
            .OrderBy(x => x.Van!.Name)
            .Select(x => new MaterialVanStockResponse(x.VanId, x.Van!.Name, x.QuantityOnHand))
            .ToListAsync(cancellationToken);

        return Ok(new MaterialStockLocationsResponse(branches, vans));
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyCollection<MaterialItemResponse>>> GetLowStock([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        EnsureMaterialsViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var items = await LoadVisibleMaterialResponsesAsync(scope, "active", null, cancellationToken);
        return Ok(items.Where(x => x.IsLowStock).ToList());
    }

    /// <summary>
    /// Every place stock sits, one row per part per location: each visible branch's balance and each
    /// van carrying it. Van rows carry no reorder level and are never low stock. A tenant without branches has one "Main stores" location holding the part
    /// totals. Branch rows with nothing on hand and no reorder level are left out (never stocked there).
    /// </summary>
    [HttpGet("stock-by-location")]
    public async Task<ActionResult<IReadOnlyCollection<StockLocationRowResponse>>> GetStockByLocation(CancellationToken cancellationToken)
    {
        EnsureMaterialsViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var rows = new List<StockLocationRowResponse>();

        if (scope.HasConfiguredBranches)
        {
            rows.AddRange(await dbContext.BranchMaterialStocks.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.Material!.IsActive && (x.QuantityOnHand != 0 || x.ReorderLevel > 0))
                .WhereAccessible(scope, x => (Guid?)x.BranchId)
                .Select(x => new StockLocationRowResponse(
                    x.MaterialId, x.Material!.ItemCode, x.Material.ItemName, x.Material.Category, x.Material.UnitOfMeasure,
                    StockLocationTypes.Branch, x.BranchId, x.Branch!.Name,
                    x.QuantityOnHand, x.ReorderLevel, x.UnitCost ?? x.Material.UnitCost,
                    x.QuantityOnHand <= x.ReorderLevel && (x.QuantityOnHand != 0 || x.ReorderLevel > 0)))
                .ToListAsync(cancellationToken));
        }
        else
        {
            // Without branches the part total is the stores balance; van stock is part of that total,
            // so it is taken back out here and shown against each van below.
            var vanTotals = await dbContext.VanMaterialStocks.AsNoTracking()
                .Where(x => x.TenantId == TenantId)
                .GroupBy(x => x.MaterialId)
                .Select(g => new { MaterialId = g.Key, Quantity = g.Sum(x => x.QuantityOnHand) })
                .ToDictionaryAsync(x => x.MaterialId, x => x.Quantity, cancellationToken);
            var items = await dbContext.MaterialItems.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var item in items)
            {
                var stores = item.QuantityOnHand - vanTotals.GetValueOrDefault(item.Id);
                if (stores == 0 && item.ReorderLevel <= 0) continue;
                rows.Add(new StockLocationRowResponse(
                    item.Id, item.ItemCode, item.ItemName, item.Category, item.UnitOfMeasure,
                    StockLocationTypes.Stores, null, "Main stores",
                    stores, item.ReorderLevel, item.UnitCost, StockRules.IsLow(stores, item.ReorderLevel)));
            }
        }

        rows.AddRange(await dbContext.VanMaterialStocks.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.QuantityOnHand != 0 && x.Material!.IsActive && x.Van!.IsActive)
            .WhereAccessible(scope, x => x.Van!.HomeBranchId)
            .Select(x => new StockLocationRowResponse(
                x.MaterialId, x.Material!.ItemCode, x.Material.ItemName, x.Material.Category, x.Material.UnitOfMeasure,
                StockLocationTypes.Van, x.VanId, x.Van!.Name,
                x.QuantityOnHand, 0, x.UnitCost ?? x.Material.UnitCost,
                // Vans have no reorder levels of their own (a van row copies the catalogue's when
                // created, and nothing edits it), so a van is never "low"; the part page agrees.
                false))
            .ToListAsync(cancellationToken));

        return Ok(rows.OrderBy(x => x.ItemName).ThenBy(x => x.LocationType).ThenBy(x => x.LocationName).ToList());
    }

    /// <summary>Per-branch stock totals for the branches the user can see: parts held, low stock and value.</summary>
    [HttpGet("branch-summary")]
    public async Task<ActionResult<IReadOnlyCollection<BranchStockSummaryResponse>>> GetBranchSummary(CancellationToken cancellationToken)
    {
        EnsureMaterialsViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var stocks = await dbContext.BranchMaterialStocks.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Material!.IsActive)
            .WhereAccessible(scope, x => (Guid?)x.BranchId)
            .Select(x => new { x.BranchId, x.QuantityOnHand, x.ReorderLevel, UnitCost = x.UnitCost ?? x.Material!.UnitCost })
            .ToListAsync(cancellationToken);

        return Ok(stocks
            .GroupBy(x => x.BranchId)
            .Select(g => new BranchStockSummaryResponse(
                g.Key,
                g.Count(x => x.QuantityOnHand != 0),
                g.Count(x => x.QuantityOnHand <= x.ReorderLevel && (x.QuantityOnHand != 0 || x.ReorderLevel > 0)),
                Math.Round(g.Sum(x => x.QuantityOnHand * (x.UnitCost ?? 0)), 2)))
            .ToList());
    }

    [HttpPost]
    public async Task<ActionResult<MaterialItemResponse>> Create([FromBody] UpsertMaterialItemRequest request, CancellationToken cancellationToken)
    {
        EnsureMaterialsManageAccess();
        Validate(request);
        await EnsureUniqueCodeAsync(request.ItemCode, null, cancellationToken);
        await ValidateMasterDataAsync(request, null, cancellationToken);

        var item = new MaterialItem
        {
            TenantId = TenantId,
            ItemCode = request.ItemCode.Trim(),
            ItemName = request.ItemName.Trim(),
            Category = request.Category?.Trim(),
            UnitOfMeasure = request.UnitOfMeasure.Trim(),
            QuantityOnHand = 0,
            ReorderLevel = request.ReorderLevel,
            UnitCost = request.UnitCost,
            Barcode = Blank(request.Barcode),
            PreferredSupplierId = request.PreferredSupplierId,
            ReorderQuantity = request.ReorderQuantity,
            IsActive = true
        };

        dbContext.MaterialItems.Add(item);

        Guid? branchId = null;
        await stockLedgerService.ExecuteAsync(async ct =>
        {
            if (request.QuantityOnHand > 0)
            {
                branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId, ct);
                await stockLedgerService.RecordReceiptAsync(item, branchId, request.QuantityOnHand, UserId, "Initial stock balance", "INITIAL-STOCK", cancellationToken: ct);
            }

            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        await dbContext.Entry(item).Reference(x => x.PreferredSupplier).LoadAsync(cancellationToken);
        var responseScope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, Map(item, responseScope, await SettingsAsync(cancellationToken)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaterialItemResponse>> Update(Guid id, [FromBody] UpsertMaterialItemRequest request, CancellationToken cancellationToken)
    {
        EnsureMaterialsManageAccess();
        Validate(request);
        var item = await dbContext.MaterialItems
            .Include(x => x.PreferredSupplier)
            .Include(x => x.BranchStocks)
                .ThenInclude(x => x.Branch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material item was not found.");

        await EnsureUniqueCodeAsync(request.ItemCode, item.Id, cancellationToken);
        await ValidateMasterDataAsync(request, item.Id, cancellationToken);

        var requestedBranchId = request.BranchId;
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, requestedBranchId, cancellationToken);
        var currentQuantity = GetQuantity(item, scope);
        var quantityDelta = request.QuantityOnHand - currentQuantity;

        if (scope.HasConfiguredBranches && !scope.RequestedBranchId.HasValue && quantityDelta != 0)
        {
            throw new BusinessRuleException("Branch selection is required when adjusting branch-based stock.");
        }

        item.ItemCode = request.ItemCode.Trim();
        item.ItemName = request.ItemName.Trim();
        item.Category = request.Category?.Trim();
        item.UnitOfMeasure = request.UnitOfMeasure.Trim();
        item.ReorderLevel = request.ReorderLevel;
        item.UnitCost = request.UnitCost;
        item.Barcode = Blank(request.Barcode);
        item.PreferredSupplierId = request.PreferredSupplierId;
        item.ReorderQuantity = request.ReorderQuantity;

        if (scope.RequestedBranchId.HasValue)
        {
            var branchStock = item.BranchStocks.SingleOrDefault(x => x.BranchId == scope.RequestedBranchId.Value);
            if (branchStock is null)
            {
                branchStock = new BranchMaterialStock
                {
                    TenantId = TenantId,
                    BranchId = scope.RequestedBranchId.Value,
                    MaterialId = item.Id,
                    ReorderLevel = request.ReorderLevel,
                    UnitCost = request.UnitCost
                };
                item.BranchStocks.Add(branchStock);
            }
            else
            {
                branchStock.ReorderLevel = request.ReorderLevel;
                branchStock.UnitCost = request.UnitCost;
            }
        }

        await stockLedgerService.ExecuteAsync(async ct =>
        {
            if (quantityDelta != 0)
            {
                await stockLedgerService.RecordAdjustmentAsync(item, scope.RequestedBranchId, quantityDelta, UserId, "Adjustment from material update", cancellationToken: ct);
            }

            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);
        await dbContext.Entry(item).Reference(x => x.PreferredSupplier).LoadAsync(cancellationToken);
        return Ok(Map(item, scope, await SettingsAsync(cancellationToken)));
    }

    [HttpPost("{id:guid}/replenish")]
    public async Task<ActionResult<MaterialItemResponse>> Replenish(Guid id, [FromBody] ReplenishMaterialRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials, PermissionNames.CanIssueMaterials);

        if (request.Quantity <= 0)
        {
            throw new BusinessRuleException("Replenishment quantity must be greater than zero.");
        }

        // This is a receipt with no purchase order behind it, so the ledger needs the why.
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException("Give a reason for receiving stock without a purchase order.");
        }

        var item = await dbContext.MaterialItems
            .Include(x => x.PreferredSupplier)
            .Include(x => x.BranchStocks)
                .ThenInclude(x => x.Branch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material item was not found.");

        var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId, cancellationToken);
        if (request.UnitCost.HasValue)
        {
            item.UnitCost = request.UnitCost.Value;
        }

        await stockLedgerService.ExecuteAsync(async ct =>
        {
            var movement = await stockLedgerService.RecordReceiptAsync(item, branchId, request.Quantity, UserId, request.Reason, request.ReferenceNumber, cancellationToken: ct);
            movement.BatchOrSerial = Blank(request.BatchOrSerial);
            var branchStock = item.BranchStocks.SingleOrDefault(x => x.BranchId == branchId);
            if (branchStock is not null)
            {
                branchStock.UnitCost = request.UnitCost ?? branchStock.UnitCost ?? item.UnitCost;
            }

            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Material received manually",
            nameof(MaterialItem),
            item.Id.ToString(),
            $"Manual receipt of {request.Quantity} {item.UnitOfMeasure} of '{item.ItemName}' (no purchase order): {request.Reason!.Trim()}.",
            cancellationToken);

        return Ok(Map(item, await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken), await SettingsAsync(cancellationToken)));
    }

    [HttpPost("{id:guid}/adjust")]
    public async Task<ActionResult<MaterialItemResponse>> Adjust(Guid id, [FromBody] AdjustMaterialRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials, PermissionNames.CanIssueMaterials);

        if (request.QuantityChange == 0)
        {
            throw new BusinessRuleException("Adjustment quantity change cannot be zero.");
        }

        // An adjustment changes stock with nothing else behind it, so the why is the only record.
        var reason = Blank(request.Reason) ?? throw new BusinessRuleException("Give a reason for the adjustment.");

        var item = await dbContext.MaterialItems
            .Include(x => x.PreferredSupplier)
            .Include(x => x.BranchStocks)
                .ThenInclude(x => x.Branch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material item was not found.");

        var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId, cancellationToken);
        await stockLedgerService.ExecuteAsync(async ct =>
        {
            var movement = await stockLedgerService.RecordAdjustmentAsync(item, branchId, request.QuantityChange, UserId, reason, cancellationToken: ct);
            movement.BatchOrSerial = Blank(request.BatchOrSerial);
            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Material adjusted",
            nameof(MaterialItem),
            item.Id.ToString(),
            $"Adjusted '{item.ItemName}' by {request.QuantityChange:+0.###;-0.###} {item.UnitOfMeasure}: {reason}",
            cancellationToken);

        return Ok(Map(item, await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken), await SettingsAsync(cancellationToken)));
    }

    [HttpGet("{id:guid}/movements")]
    public async Task<ActionResult<IReadOnlyCollection<StockMovementResponse>>> GetMovements(Guid id, [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        EnsureMaterialsViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);

        var materialExists = await dbContext.MaterialItems.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken);
        if (!materialExists)
        {
            throw new NotFoundException("Material item was not found.");
        }

        var movements = await dbContext.StockMovements
            .Include(x => x.Branch)
            .Where(x => x.TenantId == TenantId && x.MaterialId == id)
            .WhereAccessible(scope, x => x.BranchId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new StockMovementResponse(
                x.Id,
                x.BranchId,
                x.Branch != null ? x.Branch.Name : null,
                x.MaterialId,
                x.WorkOrderId,
                x.MaterialRequestId,
                x.MovementType,
                x.Quantity,
                x.BalanceAfter,
                x.Reason,
                x.ReferenceNumber,
                x.CreatedByUserId,
                x.CreatedAt,
                x.QuantityChange,
                x.BatchOrSerial))
            .ToListAsync(cancellationToken);

        return Ok(movements);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        EnsureMaterialsManageAccess();

        var item = await dbContext.MaterialItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material item was not found.");

        // An inactive part drops out of every stock view, so deactivating one that still has stock or
        // is still being asked for would make that stock and those requests vanish from sight.
        var blockers = new List<string>();
        var branchStock = await dbContext.BranchMaterialStocks.Where(x => x.TenantId == TenantId && x.MaterialId == id).SumAsync(x => x.QuantityOnHand, cancellationToken);
        var vanStock = await dbContext.VanMaterialStocks.Where(x => x.TenantId == TenantId && x.MaterialId == id).SumAsync(x => x.QuantityOnHand, cancellationToken);
        if (branchStock != 0 || vanStock != 0 || item.QuantityOnHand != 0)
            blockers.Add($"{(branchStock != 0 || vanStock != 0 ? branchStock + vanStock : item.QuantityOnHand):0.##} {item.UnitOfMeasure} still in stock{(vanStock != 0 ? $" ({vanStock:0.##} on vans)" : string.Empty)} — issue, transfer or adjust it to zero first");
        var openRequests =
            await dbContext.PartsRequestItems.CountAsync(x => x.InventoryItemId == id && x.PartsRequest!.TenantId == TenantId && (x.PartsRequest.Status == "Pending" || x.PartsRequest.Status == "Approved"), cancellationToken)
            + await dbContext.MaterialIssueLines.CountAsync(x => x.TenantId == TenantId && x.MaterialItemId == id && (x.MaterialIssue!.IssueStatus == "draft" || x.MaterialIssue.IssueStatus == "pending_approval" || x.MaterialIssue.IssueStatus == "approved"), cancellationToken)
            + await dbContext.MaterialRequestLines.CountAsync(x => x.MaterialItemId == id && x.MaterialRequest!.TenantId == TenantId && (x.MaterialRequest.Status == "Pending" || x.MaterialRequest.Status == "Approved"), cancellationToken);
        if (openRequests > 0)
            blockers.Add($"{openRequests} open request{(openRequests == 1 ? "" : "s")} for it under Inventory → Requests");
        var openOrders = await dbContext.PurchaseOrderLines.CountAsync(x => x.TenantId == TenantId && x.MaterialItemId == id
            && x.QuantityReceived < x.QuantityOrdered
            && (x.PurchaseOrder!.Status == "Draft" || x.PurchaseOrder.Status == "Sent" || x.PurchaseOrder.Status == "PartiallyReceived"), cancellationToken);
        if (openOrders > 0)
            blockers.Add($"{openOrders} purchase order line{(openOrders == 1 ? "" : "s")} still to be received");
        var openCounts = await dbContext.StockCountLines.CountAsync(x => x.TenantId == TenantId && x.MaterialItemId == id && x.StockCount!.Status == StockCountStatuses.Open, cancellationToken);
        if (openCounts > 0)
            blockers.Add("an open stock count that lists it");
        if (blockers.Count > 0)
            throw new BusinessRuleException($"{item.ItemName} can't be deactivated yet: {string.Join("; ", blockers)}.");

        item.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<List<MaterialItemResponse>> LoadVisibleMaterialResponsesAsync(
        BranchQueryScope scope,
        string statusFilter,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MaterialItems
            .Include(x => x.PreferredSupplier)
            .Include(x => x.BranchStocks)
                .ThenInclude(x => x.Branch)
            .Where(x => x.TenantId == TenantId)
            .AsQueryable();

        if (statusFilter == "active")
        {
            query = query.Where(x => x.IsActive);
        }
        else if (statusFilter == "inactive")
        {
            query = query.Where(x => !x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(x =>
                x.ItemName.ToLower().Contains(searchLower)
                || x.ItemCode.ToLower().Contains(searchLower)
                || (x.Category != null && x.Category.ToLower().Contains(searchLower))
                || (x.Barcode != null && x.Barcode.ToLower() == searchLower)
                || x.UnitOfMeasure.ToLower().Contains(searchLower));
        }

        var items = await query
            .OrderBy(x => x.ItemName)
            .ToListAsync(cancellationToken);

        var settings = await SettingsAsync(cancellationToken);
        return items.Select(x => Map(x, scope, settings)).ToList();
    }

    private InventoryModuleSettings? settings;

    private async Task<InventoryModuleSettings> SettingsAsync(CancellationToken cancellationToken) =>
        settings ??= await InventoryModuleSettings.LoadAsync(dbContext, TenantId, cancellationToken);

    private static void Validate(UpsertMaterialItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ItemCode) ||
            string.IsNullOrWhiteSpace(request.ItemName) ||
            string.IsNullOrWhiteSpace(request.UnitOfMeasure))
        {
            throw new BusinessRuleException("Item code, item name, and unit of measure are required.");
        }
    }

    private async Task ValidateMasterDataAsync(UpsertMaterialItemRequest request, Guid? currentId, CancellationToken cancellationToken)
    {
        if (request.ReorderLevel < 0)
            throw new BusinessRuleException("Reorder level cannot be negative.");
        if (request.ReorderQuantity is <= 0)
            throw new BusinessRuleException("Reorder quantity must be greater than zero, or left blank.");
        if (request.UnitCost is < 0)
            throw new BusinessRuleException("Unit cost cannot be negative.");

        if (Blank(request.Barcode) is { } barcode)
        {
            var normalized = barcode.ToLower();
            if (await dbContext.MaterialItems.AnyAsync(x => x.TenantId == TenantId && x.Id != currentId && x.IsActive && x.Barcode != null && x.Barcode.ToLower() == normalized, cancellationToken))
                throw new BusinessRuleException($"Another part already uses barcode {barcode}.");
        }

        if (request.PreferredSupplierId is { } supplierId
            && !await dbContext.Suppliers.AnyAsync(x => x.TenantId == TenantId && x.Id == supplierId, cancellationToken))
            throw new BusinessRuleException("Preferred supplier was not found.");
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task EnsureUniqueCodeAsync(string itemCode, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalized = itemCode.Trim().ToLowerInvariant();
        var exists = await dbContext.MaterialItems.AnyAsync(
            x => x.TenantId == TenantId && x.Id != currentId && x.ItemCode.ToLower() == normalized,
            cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException("Item code must be unique per tenant.");
        }
    }

    private static MaterialItemResponse Map(MaterialItem item, BranchQueryScope scope, InventoryModuleSettings settings)
    {
        decimal quantityOnHand;
        decimal reorderLevel;
        decimal? unitCost;
        bool isLowStock;
        bool isCritical;
        Guid? branchId = null;
        string? branchName = null;

        if (!scope.HasConfiguredBranches)
        {
            quantityOnHand = item.QuantityOnHand;
            reorderLevel = item.ReorderLevel;
            unitCost = item.UnitCost;
            isLowStock = StockRules.IsLow(quantityOnHand, reorderLevel);
            isCritical = settings.IsCritical(quantityOnHand, reorderLevel);
        }
        else if (scope.RequestedBranchId.HasValue)
        {
            var stock = item.BranchStocks.SingleOrDefault(x => x.BranchId == scope.RequestedBranchId.Value);
            quantityOnHand = stock?.QuantityOnHand ?? 0;
            reorderLevel = stock?.ReorderLevel ?? item.ReorderLevel;
            unitCost = stock?.UnitCost ?? item.UnitCost;
            isLowStock = StockRules.IsLow(quantityOnHand, reorderLevel);
            isCritical = settings.IsCritical(quantityOnHand, reorderLevel);
            branchId = scope.RequestedBranchId;
            branchName = stock?.Branch?.Name;
        }
        else
        {
            var visibleStocks = item.BranchStocks
                .Where(x => scope.AccessibleBranchIds.Contains(x.BranchId))
                .ToList();

            if (visibleStocks.Count == 0)
            {
                quantityOnHand = item.QuantityOnHand;
                reorderLevel = item.ReorderLevel;
                unitCost = item.UnitCost;
                isLowStock = StockRules.IsLow(quantityOnHand, reorderLevel);
                isCritical = settings.IsCritical(quantityOnHand, reorderLevel);
            }
            else
            {
                quantityOnHand = visibleStocks.Sum(x => x.QuantityOnHand);
                reorderLevel = visibleStocks.Sum(x => x.ReorderLevel);
                unitCost = item.UnitCost;
                isLowStock = visibleStocks.Any(x => StockRules.IsLow(x.QuantityOnHand, x.ReorderLevel));
                isCritical = visibleStocks.Any(x => settings.IsCritical(x.QuantityOnHand, x.ReorderLevel));
            }
        }

        return new MaterialItemResponse(
            item.Id,
            branchId,
            branchName,
            item.ItemCode,
            item.ItemName,
            item.Category,
            item.UnitOfMeasure,
            quantityOnHand,
            reorderLevel,
            unitCost,
            item.IsActive,
            isLowStock,
            item.CreatedAt,
            isCritical,
            item.Barcode,
            item.PreferredSupplierId,
            item.PreferredSupplier?.SupplierName,
            item.ReorderQuantity);
    }

    private static decimal GetQuantity(MaterialItem item, BranchQueryScope scope)
    {
        if (!scope.HasConfiguredBranches)
        {
            return item.QuantityOnHand;
        }

        if (scope.RequestedBranchId.HasValue)
        {
            return item.BranchStocks.SingleOrDefault(x => x.BranchId == scope.RequestedBranchId.Value)?.QuantityOnHand ?? 0;
        }

        return item.BranchStocks
            .Where(x => scope.AccessibleBranchIds.Contains(x.BranchId))
            .Sum(x => x.QuantityOnHand);
    }

    private static string NormalizeStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "active";
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "active" => "active",
            "inactive" => "inactive",
            "all" => "all",
            _ => throw new BusinessRuleException("Status must be active, inactive, or all.")
        };
    }

    private static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    private void EnsureMaterialsViewAccess()
    {
        userAccessService.EnsureAdminOrAnyPermission(
            PermissionNames.CanViewMaterials,
            PermissionNames.CanRequestMaterials,
            PermissionNames.CanManageMaterials,
            PermissionNames.CanApproveMaterials,
            PermissionNames.CanIssueMaterials);
    }

    private void EnsureMaterialsManageAccess()
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageMaterials);
    }
}

public sealed record UpsertMaterialItemRequest(
    string ItemCode,
    string ItemName,
    string? Category,
    string UnitOfMeasure,
    decimal QuantityOnHand,
    decimal ReorderLevel,
    decimal? UnitCost,
    Guid? BranchId,
    string? Barcode = null,
    Guid? PreferredSupplierId = null,
    decimal? ReorderQuantity = null);

public sealed record ReplenishMaterialRequest(Guid? BranchId, decimal Quantity, decimal? UnitCost, string? Reason, string? ReferenceNumber, string? BatchOrSerial = null);

public sealed record AdjustMaterialRequest(Guid? BranchId, decimal QuantityChange, string? Reason, string? BatchOrSerial = null);

public sealed record MaterialItemResponse(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    string ItemCode,
    string ItemName,
    string? Category,
    string UnitOfMeasure,
    decimal QuantityOnHand,
    decimal ReorderLevel,
    decimal? UnitCost,
    bool IsActive,
    bool IsLowStock,
    DateTime CreatedAt,
    bool IsCriticalStock = false,
    string? Barcode = null,
    Guid? PreferredSupplierId = null,
    string? PreferredSupplierName = null,
    decimal? ReorderQuantity = null);

public sealed record StockMovementResponse(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    Guid MaterialId,
    Guid? WorkOrderId,
    Guid? MaterialRequestId,
    string MovementType,
    decimal Quantity,
    decimal BalanceAfter,
    string? Reason,
    string? ReferenceNumber,
    Guid? CreatedByUserId,
    DateTime CreatedAt,
    decimal? QuantityChange = null,
    string? BatchOrSerial = null);

/// <param name="LowStockParts">Parts at or below their reorder level here (ignoring parts never stocked here with no reorder level).</param>
public static class StockLocationTypes
{
    public const string Branch = "Branch";
    public const string Van = "Van";
    /// <summary>The single location of a tenant that has no branches.</summary>
    public const string Stores = "Stores";
}

/// <summary>One part at one location. LocationId is the branch or van; null for "Main stores".</summary>
public sealed record StockLocationRowResponse(
    Guid MaterialId,
    string ItemCode,
    string ItemName,
    string? Category,
    string UnitOfMeasure,
    string LocationType,
    Guid? LocationId,
    string LocationName,
    decimal QuantityOnHand,
    decimal ReorderLevel,
    decimal? UnitCost,
    bool IsLowStock);

public sealed record MaterialBranchStockResponse(Guid BranchId, string BranchName, decimal QuantityOnHand, decimal ReorderLevel, decimal? UnitCost, bool IsLowStock);

public sealed record MaterialVanStockResponse(Guid VanId, string VanName, decimal QuantityOnHand);

public sealed record MaterialStockLocationsResponse(IReadOnlyList<MaterialBranchStockResponse> Branches, IReadOnlyList<MaterialVanStockResponse> Vans);

public sealed record BranchStockSummaryResponse(Guid BranchId, int PartsStocked, int LowStockParts, decimal StockValue);
