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
            .Include(x => x.BranchStocks)
                .ThenInclude(x => x.Branch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material item was not found.");

        return Ok(Map(item, scope, await SettingsAsync(cancellationToken)));
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyCollection<MaterialItemResponse>>> GetLowStock([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        EnsureMaterialsViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var items = await LoadVisibleMaterialResponsesAsync(scope, "active", null, cancellationToken);
        return Ok(items.Where(x => x.IsLowStock).ToList());
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

        var responseScope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, Map(item, responseScope, await SettingsAsync(cancellationToken)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MaterialItemResponse>> Update(Guid id, [FromBody] UpsertMaterialItemRequest request, CancellationToken cancellationToken)
    {
        EnsureMaterialsManageAccess();
        Validate(request);
        var item = await dbContext.MaterialItems
            .Include(x => x.BranchStocks)
                .ThenInclude(x => x.Branch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material item was not found.");

        await EnsureUniqueCodeAsync(request.ItemCode, item.Id, cancellationToken);

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

        var item = await dbContext.MaterialItems
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
            await stockLedgerService.RecordReceiptAsync(item, branchId, request.Quantity, UserId, request.Reason, request.ReferenceNumber, cancellationToken: ct);
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
            "Material replenished",
            nameof(MaterialItem),
            item.Id.ToString(),
            $"Replenished '{item.ItemName}' by {request.Quantity} {item.UnitOfMeasure}.",
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

        var item = await dbContext.MaterialItems
            .Include(x => x.BranchStocks)
                .ThenInclude(x => x.Branch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material item was not found.");

        var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId, cancellationToken);
        await stockLedgerService.ExecuteAsync(async ct =>
        {
            await stockLedgerService.RecordAdjustmentAsync(item, branchId, request.QuantityChange, UserId, request.Reason, cancellationToken: ct);
            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

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
                x.QuantityChange))
            .ToListAsync(cancellationToken);

        return Ok(movements);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        EnsureMaterialsManageAccess();

        var item = await dbContext.MaterialItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material item was not found.");

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
            isLowStock = quantityOnHand <= reorderLevel;
            isCritical = settings.IsCritical(quantityOnHand, reorderLevel);
        }
        else if (scope.RequestedBranchId.HasValue)
        {
            var stock = item.BranchStocks.SingleOrDefault(x => x.BranchId == scope.RequestedBranchId.Value);
            quantityOnHand = stock?.QuantityOnHand ?? 0;
            reorderLevel = stock?.ReorderLevel ?? item.ReorderLevel;
            unitCost = stock?.UnitCost ?? item.UnitCost;
            isLowStock = quantityOnHand <= reorderLevel;
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
                isLowStock = quantityOnHand <= reorderLevel;
                isCritical = settings.IsCritical(quantityOnHand, reorderLevel);
            }
            else
            {
                quantityOnHand = visibleStocks.Sum(x => x.QuantityOnHand);
                reorderLevel = visibleStocks.Sum(x => x.ReorderLevel);
                unitCost = item.UnitCost;
                isLowStock = visibleStocks.Any(x => x.QuantityOnHand <= x.ReorderLevel);
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
            isCritical);
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
    Guid? BranchId);

public sealed record ReplenishMaterialRequest(Guid? BranchId, decimal Quantity, decimal? UnitCost, string? Reason, string? ReferenceNumber);

public sealed record AdjustMaterialRequest(Guid? BranchId, decimal QuantityChange, string? Reason);

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
    bool IsCriticalStock = false);

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
    decimal? QuantityChange = null);

/// <param name="LowStockParts">Parts at or below their reorder level here (ignoring parts never stocked here with no reorder level).</param>
public sealed record BranchStockSummaryResponse(Guid BranchId, int PartsStocked, int LowStockParts, decimal StockValue);
