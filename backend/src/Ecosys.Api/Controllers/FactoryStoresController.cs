using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Factory stores and spares: stores and bins, stock levels, receipts, issues and returns to jobs,
/// reservations and kitting, serialised and repairable spares, transfers between stores and
/// factories, cycle counts, valuation, where-used parts lists, requisitions, the vendor catalogue
/// and the chemicals register.
/// </summary>
[ApiController]
[Authorize]
[Route("api/factory/stores")]
public sealed class FactoryStoresController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    ISparesService sparesService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly string[] Finished = [WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    private FactoryActor Actor => new(UserId, UserDisplayName ?? "Unknown");

    /* ── Stores and bins ─────────────────────────────────────────────────── */

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<StoreSummary>>> Stores([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var factories = await FactoriesInScopeAsync(cancellationToken);
        var stores = await dbContext.Stores.Include(x => x.Factory)
            .Where(x => x.TenantId == TenantId && (includeInactive || x.IsActive) && factories.Contains(x.FactoryId))
            .OrderBy(x => x.Factory!.Name).ThenByDescending(x => x.IsMain).ThenBy(x => x.Code).ToListAsync(cancellationToken);
        var ids = stores.Select(x => x.Id).ToList();
        var stock = await dbContext.StoreStocks.Where(x => ids.Contains(x.StoreId))
            .Select(x => new { x.StoreId, x.OnHand, x.Reserved, x.ReorderPoint, x.MinLevel, x.AverageCost, Critical = x.MaterialItem!.IsCriticalSpare })
            .ToListAsync(cancellationToken);
        return Ok(stores.Select(s =>
        {
            var mine = stock.Where(x => x.StoreId == s.Id).ToList();
            return new StoreSummary(s.Id, s.FactoryId, s.Factory?.Name ?? string.Empty, s.Code, s.Name, s.Location, s.IsMain, s.IsActive,
                mine.Count(x => x.OnHand > 0), Math.Round(mine.Sum(x => x.OnHand * x.AverageCost), 2),
                mine.Count(x => x.OnHand - x.Reserved <= x.ReorderPoint && (x.ReorderPoint > 0 || x.MinLevel > 0)),
                mine.Count(x => x.Critical && x.OnHand - x.Reserved <= 0));
        }).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<StoreSummary>> CreateStore([FromBody] SaveStoreRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var store = new Store { TenantId = TenantId };
        await ApplyStoreAsync(store, request, cancellationToken);
        dbContext.Stores.Add(store);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Unwrap(await Stores(true, cancellationToken)).Single(x => x.Id == store.Id));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StoreSummary>> UpdateStore(Guid id, [FromBody] SaveStoreRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var store = await LoadStoreAsync(id, cancellationToken);
        await ApplyStoreAsync(store, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Unwrap(await Stores(true, cancellationToken)).Single(x => x.Id == store.Id));
    }

    [HttpGet("{id:guid}/bins")]
    public async Task<ActionResult<IReadOnlyCollection<StoreBinDto>>> Bins(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await LoadStoreAsync(id, cancellationToken);
        return Ok(await dbContext.StoreBins.Where(x => x.StoreId == id).OrderBy(x => x.Code)
            .Select(x => new StoreBinDto(x.Id, x.Code, x.Description, x.IsActive)).ToListAsync(cancellationToken));
    }

    [HttpPost("{id:guid}/bins")]
    public async Task<ActionResult<StoreBinDto>> AddBin(Guid id, [FromBody] SaveBinRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var store = await LoadStoreAsync(id, cancellationToken);
        var code = Required(request.Code, "Bin code").ToUpperInvariant();
        if (await dbContext.StoreBins.AnyAsync(x => x.StoreId == id && x.Code == code, cancellationToken)) throw new ConflictException($"Bin {code} already exists in {store.Name}.");
        var bin = new StoreBin { TenantId = TenantId, StoreId = id, Code = code, Description = Clean(request.Description) };
        dbContext.StoreBins.Add(bin);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new StoreBinDto(bin.Id, bin.Code, bin.Description, bin.IsActive));
    }

    /* ── Stock ───────────────────────────────────────────────────────────── */

    [HttpGet("{id:guid}/stock")]
    public async Task<ActionResult<IReadOnlyCollection<StoreStockRow>>> Stock(Guid id, [FromQuery] string? search, [FromQuery] string? filter, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var store = await LoadStoreAsync(id, cancellationToken);
        var query = dbContext.StoreStocks.Where(x => x.StoreId == store.Id);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.MaterialItem!.ItemName.ToLower().Contains(term) || x.MaterialItem.ItemCode.ToLower().Contains(term)
                || (x.MaterialItem.Barcode != null && x.MaterialItem.Barcode.ToLower().Contains(term)) || (x.Bin != null && x.Bin.Code.ToLower().Contains(term)));
        }

        query = (filter ?? "all").ToLowerInvariant() switch
        {
            "low" => query.Where(x => x.OnHand - x.Reserved <= x.ReorderPoint && (x.ReorderPoint > 0 || x.MinLevel > 0)),
            "critical" => query.Where(x => x.MaterialItem!.IsCriticalSpare),
            "reserved" => query.Where(x => x.Reserved > 0),
            _ => query
        };
        var rows = await query.Select(x => new
        {
            x.MaterialItemId, x.MaterialItem!.ItemCode, x.MaterialItem.ItemName, x.MaterialItem.UnitOfMeasure, x.MaterialItem.Category,
            x.MaterialItem.IsCriticalSpare, x.MaterialItem.IsSerialised, x.MaterialItem.IsRepairable, x.MaterialItem.IsChemical,
            x.BinId, Bin = x.Bin != null ? x.Bin.Code : null, x.OnHand, x.Reserved, x.MinLevel, x.ReorderPoint, x.MaxLevel, x.AverageCost, x.LastCountedAt
        }).OrderBy(x => x.ItemName).ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new StoreStockRow(
            x.MaterialItemId, x.ItemCode, x.ItemName, x.UnitOfMeasure, x.Category, x.IsCriticalSpare, x.IsSerialised, x.IsRepairable, x.IsChemical,
            x.BinId, x.Bin, x.OnHand, x.Reserved, x.OnHand - x.Reserved, x.MinLevel, x.ReorderPoint, x.MaxLevel, Math.Round(x.AverageCost, 2),
            Math.Round(x.OnHand * x.AverageCost, 2), StockState(x.OnHand - x.Reserved, x.MinLevel, x.ReorderPoint), x.LastCountedAt)).ToList());
    }

    [HttpPut("{id:guid}/stock/{materialItemId:guid}")]
    public async Task<ActionResult<StoreStockRow>> SetLevels(Guid id, Guid materialItemId, [FromBody] StockLevelsRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var store = await LoadStoreAsync(id, cancellationToken);
        await LoadPartAsync(materialItemId, cancellationToken);
        if (request.MinLevel < 0 || request.ReorderPoint < 0 || request.MaxLevel < 0) throw new BusinessRuleException("Levels can't be negative.");
        if (request.ReorderPoint < request.MinLevel) throw new BusinessRuleException("The reorder point should be at or above the minimum, so the order arrives before stock runs short.");
        if (request.MaxLevel is { } max && max < request.ReorderPoint) throw new BusinessRuleException("The maximum must be above the reorder point.");
        if (request.BinId is { } binId && !await dbContext.StoreBins.AnyAsync(x => x.Id == binId && x.StoreId == id, cancellationToken)) throw new BusinessRuleException("Bin was not found in this store.");
        var stock = await sparesService.StockAsync(store, materialItemId, cancellationToken);
        stock.MinLevel = request.MinLevel;
        stock.ReorderPoint = request.ReorderPoint;
        stock.MaxLevel = request.MaxLevel;
        stock.BinId = request.BinId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Unwrap(await Stock(id, null, null, cancellationToken)).Single(x => x.MaterialItemId == materialItemId));
    }

    [HttpPost("{id:guid}/receive")]
    public async Task<ActionResult<StoreStockRow>> Receive(Guid id, [FromBody] ReceiveStockRequest request, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        var store = await LoadStoreAsync(id, cancellationToken);
        var part = await LoadPartAsync(request.MaterialItemId, cancellationToken);
        await sparesService.ReceiveAsync(store, part, request.Quantity * (request.InPurchaseUnits ? part.PurchaseUnitFactor : 1), request.UnitCost is { } c && request.InPurchaseUnits && part.PurchaseUnitFactor > 0 ? c / part.PurchaseUnitFactor : request.UnitCost,
            request.Currency, Clean(request.Reference), request.SerialNumbers, Actor, cancellationToken);
        return Ok(Unwrap(await Stock(id, null, null, cancellationToken)).Single(x => x.MaterialItemId == part.Id));
    }

    [HttpPost("{id:guid}/adjust")]
    public async Task<ActionResult<StoreStockRow>> Adjust(Guid id, [FromBody] AdjustStockRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials);
        var store = await LoadStoreAsync(id, cancellationToken);
        var part = await LoadPartAsync(request.MaterialItemId, cancellationToken);
        await sparesService.AdjustAsync(store, part, request.OnHand, request.Reason ?? string.Empty, Actor, cancellationToken);
        return Ok(Unwrap(await Stock(id, null, null, cancellationToken)).Single(x => x.MaterialItemId == part.Id));
    }

    [HttpGet("movements")]
    public async Task<ActionResult<IReadOnlyCollection<StoreMovementDto>>> Movements([FromQuery] Guid? storeId, [FromQuery] Guid? materialItemId, [FromQuery] Guid? workOrderId, [FromQuery] int? take, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var stores = await StoresInScopeAsync(cancellationToken);
        var query = dbContext.StockMovements.Where(x => x.TenantId == TenantId && x.StoreId != null && stores.Contains(x.StoreId.Value));
        if (storeId.HasValue) query = query.Where(x => x.StoreId == storeId);
        if (materialItemId.HasValue) query = query.Where(x => x.MaterialId == materialItemId);
        if (workOrderId.HasValue) query = query.Where(x => x.WorkOrderId == workOrderId);
        var names = await dbContext.Stores.Where(x => x.TenantId == TenantId).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(take ?? 100, 1, 500))
            .Select(x => new
            {
                x.Id, x.CreatedAt, x.StoreId, x.MaterialId, Part = x.Material!.ItemName, x.Material.ItemCode, x.MovementType, x.QuantityChange, x.Quantity, x.BalanceAfter, x.UnitCost,
                x.ReferenceNumber, x.BatchOrSerial, x.Reason, x.WorkOrderId, By = x.CreatedByUser != null ? x.CreatedByUser.FullName : null
            }).ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new StoreMovementDto(x.Id, x.CreatedAt, x.StoreId!.Value, names.GetValueOrDefault(x.StoreId.Value) ?? string.Empty, x.MaterialId, x.ItemCode, x.Part,
            x.MovementType, x.QuantityChange ?? x.Quantity, x.BalanceAfter, x.UnitCost, x.ReferenceNumber, x.BatchOrSerial, x.Reason, x.WorkOrderId, x.By)).ToList());
    }

    /// <summary>Stock value per store and in total, at moving average cost in the tenant currency.</summary>
    [HttpGet("valuation")]
    public async Task<ActionResult<StockValuation>> Valuation(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials, PermissionNames.CanViewReports);
        var stores = await StoresInScopeAsync(cancellationToken);
        var currency = await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.Currency).SingleAsync(cancellationToken);
        var rows = await dbContext.StoreStocks.Where(x => stores.Contains(x.StoreId) && x.OnHand != 0)
            .Select(x => new { x.StoreId, Store = x.Store!.Name, Factory = x.Store.Factory!.Name, Category = x.MaterialItem!.Category, x.MaterialItem.IsCriticalSpare, Value = x.OnHand * x.AverageCost })
            .ToListAsync(cancellationToken);
        return Ok(new StockValuation(currency, Math.Round(rows.Sum(x => x.Value), 2),
            rows.GroupBy(x => new { x.StoreId, x.Store, x.Factory }).OrderBy(g => g.Key.Factory).ThenBy(g => g.Key.Store)
                .Select(g => new StoreValue(g.Key.StoreId, g.Key.Store, g.Key.Factory, g.Count(), Math.Round(g.Sum(x => x.Value), 2), Math.Round(g.Where(x => x.IsCriticalSpare).Sum(x => x.Value), 2))).ToList(),
            rows.GroupBy(x => x.Category ?? "Uncategorised").OrderByDescending(g => g.Sum(x => x.Value))
                .Select(g => new CategoryValue(g.Key, g.Count(), Math.Round(g.Sum(x => x.Value), 2))).ToList()));
    }

    /* ── Parts ───────────────────────────────────────────────────────────── */

    [HttpGet("parts")]
    public async Task<ActionResult<IReadOnlyCollection<SparePartSummary>>> Parts([FromQuery] string? search, [FromQuery] string? filter, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var query = dbContext.MaterialItems.Where(x => x.TenantId == TenantId && x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.ItemName.ToLower().Contains(term) || x.ItemCode.ToLower().Contains(term) || (x.Barcode != null && x.Barcode.ToLower().Contains(term)));
        }

        query = (filter ?? "all").ToLowerInvariant() switch
        {
            "critical" => query.Where(x => x.IsCriticalSpare),
            "serialised" => query.Where(x => x.IsSerialised),
            "repairable" => query.Where(x => x.IsRepairable),
            "chemical" => query.Where(x => x.IsChemical),
            _ => query
        };
        var parts = await query.OrderBy(x => x.ItemName).Take(500).ToListAsync(cancellationToken);
        var ids = parts.Select(x => x.Id).ToList();
        // Group-wide: every factory's stock is visible so a short factory can borrow from another.
        var stock = await dbContext.StoreStocks.Where(x => ids.Contains(x.MaterialItemId) && x.Store!.IsActive)
            .GroupBy(x => x.MaterialItemId).Select(g => new { g.Key, OnHand = g.Sum(x => x.OnHand), Reserved = g.Sum(x => x.Reserved), Stores = g.Count(x => x.OnHand > 0) })
            .ToDictionaryAsync(x => x.Key, cancellationToken);
        var aClass = await CriticalAssetPartsAsync(ids, cancellationToken);
        return Ok(parts.Select(p => new SparePartSummary(
            p.Id, p.ItemCode, p.ItemName, p.Category, p.UnitOfMeasure, p.UnitCost, p.IsCriticalSpare, aClass.Contains(p.Id), p.IsSerialised, p.IsRepairable, p.IsChemical,
            stock.GetValueOrDefault(p.Id)?.OnHand ?? 0, stock.GetValueOrDefault(p.Id)?.Reserved ?? 0, stock.GetValueOrDefault(p.Id)?.Stores ?? 0)).ToList());
    }

    [HttpPost("parts")]
    public async Task<ActionResult<SparePartDetail>> CreatePart([FromBody] SaveSparePartRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials);
        var part = new MaterialItem { TenantId = TenantId };
        await ApplyPartAsync(part, request, cancellationToken);
        dbContext.MaterialItems.Add(part);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Unwrap(await Part(part.Id, cancellationToken)));
    }

    [HttpPut("parts/{id:guid}")]
    public async Task<ActionResult<SparePartDetail>> UpdatePart(Guid id, [FromBody] SaveSparePartRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials);
        var part = await LoadPartAsync(id, cancellationToken);
        if (part.IsSerialised != request.IsSerialised && await dbContext.StoreStocks.AnyAsync(x => x.MaterialItemId == id && x.OnHand != 0, cancellationToken))
            throw new BusinessRuleException("Serial tracking can only be switched while there is no stock of the part.");
        await ApplyPartAsync(part, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Unwrap(await Part(id, cancellationToken)));
    }

    /// <summary>One part: stock in every store of the group, where it is fitted, serialised units, supplier prices and recent movements.</summary>
    [HttpGet("parts/{id:guid}")]
    public async Task<ActionResult<SparePartDetail>> Part(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var part = await LoadPartAsync(id, cancellationToken);
        var stores = await dbContext.StoreStocks.Where(x => x.MaterialItemId == id && x.Store!.IsActive)
            .OrderBy(x => x.Store!.Factory!.Name).ThenBy(x => x.Store!.Name)
            .Select(x => new PartStoreStock(x.StoreId, x.Store!.Name, x.Store.FactoryId, x.Store.Factory!.Name, x.Bin != null ? x.Bin.Code : null,
                x.OnHand, x.Reserved, x.OnHand - x.Reserved, x.MinLevel, x.ReorderPoint, x.MaxLevel, Math.Round(x.AverageCost, 2)))
            .ToListAsync(cancellationToken);
        var whereUsed = await dbContext.AssetParts.Where(x => x.MaterialItemId == id)
            .OrderBy(x => x.Asset!.AssetCode)
            .Select(x => new WhereUsedRow(x.Id, x.AssetId, x.Asset!.AssetCode, x.Asset.AssetName, x.Asset.Criticality, x.Asset.OrgNode != null ? x.Asset.OrgNode.Name : null, x.QuantityFitted, x.Position, x.Notes))
            .ToListAsync(cancellationToken);
        var units = await UnitsQuery(x => x.MaterialItemId == id).ToListAsync(cancellationToken);
        var prices = await CatalogueQuery(x => x.MaterialItemId == id).ToListAsync(cancellationToken);
        var aClass = whereUsed.Any(x => string.Equals(x.Criticality, "A", StringComparison.OrdinalIgnoreCase));
        return Ok(new SparePartDetail(
            part.Id, part.ItemCode, part.ItemName, part.Category, part.UnitOfMeasure, part.PurchaseUnitOfMeasure, part.PurchaseUnitFactor, part.UnitCost, part.Barcode,
            part.PreferredSupplierId, part.IsCriticalSpare, aClass, part.IsSerialised, part.IsRepairable, part.ExternalId, part.ExternalSystem,
            part.IsChemical, part.ChemicalKind, part.HazardClass, part.FoodGradeClass, part.SdsAttachmentId, part.SdsIssuedOn,
            stores, whereUsed, units, prices));
    }

    /* ── Where used / asset parts lists ─────────────────────────────────── */

    [HttpGet("/api/factory/assets/{assetId:guid}/parts")]
    public async Task<ActionResult<IReadOnlyCollection<AssetPartRow>>> AssetParts(Guid assetId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var asset = await LoadAssetAsync(assetId, cancellationToken);
        var factory = asset.OrgNodeId is { } node ? await FactoryOfAsync(node, cancellationToken) : null;
        var rows = await dbContext.AssetParts.Where(x => x.AssetId == assetId).OrderBy(x => x.MaterialItem!.ItemName)
            .Select(x => new { x.Id, x.MaterialItemId, x.MaterialItem!.ItemCode, x.MaterialItem.ItemName, x.MaterialItem.UnitOfMeasure, x.MaterialItem.IsCriticalSpare, x.QuantityFitted, x.Position, x.Notes })
            .ToListAsync(cancellationToken);
        var ids = rows.Select(x => x.MaterialItemId).ToList();
        var stock = await dbContext.StoreStocks.Where(x => ids.Contains(x.MaterialItemId) && x.Store!.IsActive)
            .Select(x => new { x.MaterialItemId, x.Store!.FactoryId, Free = x.OnHand - x.Reserved }).ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new AssetPartRow(x.Id, x.MaterialItemId, x.ItemCode, x.ItemName, x.UnitOfMeasure, x.IsCriticalSpare || string.Equals(asset.Criticality, "A", StringComparison.OrdinalIgnoreCase),
            x.QuantityFitted, x.Position, x.Notes,
            stock.Where(s => s.MaterialItemId == x.MaterialItemId && s.FactoryId == factory).Sum(s => s.Free),
            stock.Where(s => s.MaterialItemId == x.MaterialItemId).Sum(s => s.Free))).ToList());
    }

    [HttpPost("/api/factory/assets/{assetId:guid}/parts")]
    public async Task<ActionResult<IReadOnlyCollection<AssetPartRow>>> AddAssetPart(Guid assetId, [FromBody] SaveAssetPartRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageAssets, PermissionNames.CanManageMaterials);
        await LoadAssetAsync(assetId, cancellationToken);
        await LoadPartAsync(request.MaterialItemId, cancellationToken);
        if (request.QuantityFitted <= 0) throw new BusinessRuleException("Quantity fitted must be more than zero.");
        var position = Clean(request.Position);
        if (await dbContext.AssetParts.AnyAsync(x => x.AssetId == assetId && x.MaterialItemId == request.MaterialItemId && x.Position == position, cancellationToken))
            throw new ConflictException("That part is already on the asset's parts list at that position.");
        dbContext.AssetParts.Add(new AssetPart { TenantId = TenantId, AssetId = assetId, MaterialItemId = request.MaterialItemId, QuantityFitted = request.QuantityFitted, Position = position, Notes = Clean(request.Notes) });
        await dbContext.SaveChangesAsync(cancellationToken);
        return await AssetParts(assetId, cancellationToken);
    }

    [HttpDelete("/api/factory/assets/{assetId:guid}/parts/{id:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<AssetPartRow>>> RemoveAssetPart(Guid assetId, Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageAssets, PermissionNames.CanManageMaterials);
        await LoadAssetAsync(assetId, cancellationToken);
        var row = await dbContext.AssetParts.SingleOrDefaultAsync(x => x.Id == id && x.AssetId == assetId, cancellationToken) ?? throw new NotFoundException("Parts list line was not found.");
        dbContext.AssetParts.Remove(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await AssetParts(assetId, cancellationToken);
    }

    /* ── Serialised and repairable units ────────────────────────────────── */

    [HttpGet("units")]
    public async Task<ActionResult<IReadOnlyCollection<SpareUnitDto>>> Units([FromQuery] Guid? materialItemId, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var all = string.IsNullOrWhiteSpace(status);
        return Ok(await UnitsQuery(x => (materialItemId == null || x.MaterialItemId == materialItemId) && (all || x.Status == status)).Take(500).ToListAsync(cancellationToken));
    }

    [HttpGet("units/{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyCollection<SpareUnitEventDto>>> UnitHistory(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await LoadUnitAsync(id, cancellationToken);
        var stores = await dbContext.Stores.Where(x => x.TenantId == TenantId).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var rows = await dbContext.SpareUnitEvents.Where(x => x.SpareUnitId == id).OrderByDescending(x => x.OccurredAt)
            .Select(x => new { x.EventType, x.OccurredAt, x.ByName, x.WorkOrderId, x.AssetId, x.StoreId, x.Cost, x.Note }).ToListAsync(cancellationToken);
        var jobIds = rows.Where(x => x.WorkOrderId != null).Select(x => x.WorkOrderId!.Value).ToList();
        var jobs = await dbContext.WorkOrders.Where(x => jobIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.WorkOrderNumber, cancellationToken);
        var assetIds = rows.Where(x => x.AssetId != null).Select(x => x.AssetId!.Value).ToList();
        var assets = await dbContext.Assets.Where(x => assetIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.AssetCode, cancellationToken);
        return Ok(rows.Select(x => new SpareUnitEventDto(x.EventType, x.OccurredAt, x.ByName, x.WorkOrderId is { } w ? jobs.GetValueOrDefault(w) : null,
            x.AssetId is { } a ? assets.GetValueOrDefault(a) : null, x.StoreId is { } s ? stores.GetValueOrDefault(s) : null, x.Cost, x.Note)).ToList());
    }

    [HttpPost("units/{id:guid}/repair")]
    public async Task<ActionResult<SpareUnitDto>> SendForRepair(Guid id, [FromBody] SendForRepairRequest request, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        var unit = await LoadUnitAsync(id, cancellationToken);
        await sparesService.SendForRepairAsync(unit, request.SupplierId, Clean(request.Reference), Utc(request.ExpectedBackAt), Actor, cancellationToken);
        return Ok(await UnitsQuery(x => x.Id == id).SingleAsync(cancellationToken));
    }

    [HttpPost("units/{id:guid}/repair-return")]
    public async Task<ActionResult<SpareUnitDto>> BackFromRepair(Guid id, [FromBody] RepairReturnRequest request, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        var unit = await LoadUnitAsync(id, cancellationToken);
        var store = await LoadStoreAsync(request.StoreId ?? unit.StoreId ?? throw new BusinessRuleException("Choose the store it comes back to."), cancellationToken);
        await sparesService.ReceiveFromRepairAsync(unit, store, request.Cost, request.Currency, request.Note, Actor, cancellationToken);
        return Ok(await UnitsQuery(x => x.Id == id).SingleAsync(cancellationToken));
    }

    [HttpPost("units/{id:guid}/scrap")]
    public async Task<ActionResult<SpareUnitDto>> Scrap(Guid id, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials);
        var unit = await LoadUnitAsync(id, cancellationToken);
        await sparesService.ScrapAsync(unit, request.Reason ?? string.Empty, Actor, cancellationToken);
        return Ok(await UnitsQuery(x => x.Id == id).SingleAsync(cancellationToken));
    }

    /* ── Jobs: reservations, kitting, returns ────────────────────────────── */

    [HttpGet("/api/factory/work-orders/{workOrderId:guid}/spares")]
    public async Task<ActionResult<JobSpares>> JobSparesFor(Guid workOrderId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var job = await LoadJobAsync(workOrderId, cancellationToken);
        return Ok(await JobSparesAsync(job, cancellationToken));
    }

    [HttpPost("/api/factory/work-orders/{workOrderId:guid}/reservations")]
    public async Task<ActionResult<JobSpares>> Reserve(Guid workOrderId, [FromBody] ReserveRequest request, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var job = await LoadOpenJobAsync(workOrderId, cancellationToken);
        var store = await StoreForJobAsync(job, request.StoreId, cancellationToken);
        var part = await LoadPartAsync(request.MaterialItemId, cancellationToken);
        await sparesService.ReserveAsync(store, part, request.Quantity, job, false, Actor, cancellationToken);
        return Ok(await JobSparesAsync(job, cancellationToken));
    }

    /// <summary>Reserves the parts on the job's plan from the job's factory store; what is short is reported, not refused.</summary>
    [HttpPost("/api/factory/work-orders/{workOrderId:guid}/reservations/from-plan")]
    public async Task<ActionResult<JobSpares>> ReserveFromPlan(Guid workOrderId, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var job = await LoadOpenJobAsync(workOrderId, cancellationToken);
        await ReservePlanAsync(job, null, cancellationToken);
        return Ok(await JobSparesAsync(job, cancellationToken));
    }

    /// <summary>Reserves the planned parts for every job in a weekly window, ahead of the window pack.</summary>
    [HttpPost("/api/factory/windows/{windowId:guid}/reserve-spares")]
    public async Task<ActionResult<WindowSparesResult>> ReserveWindow(Guid windowId, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var window = await dbContext.MaintenanceWindows.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == windowId, cancellationToken)
            ?? throw new NotFoundException("The window was not found.");
        if (!(await FactoriesInScopeAsync(cancellationToken)).Contains(window.FactoryId)) throw new NotFoundException("The window was not found.");
        if (!MaintenanceWindowStatuses.IsOpen(window.Status)) throw new BusinessRuleException("The window is closed.");
        var store = await sparesService.DefaultStoreAsync(TenantId, window.FactoryId, cancellationToken)
            ?? throw new BusinessRuleException("This factory has no store yet. Set one up first.");
        var jobIds = await dbContext.MaintenanceWindowJobs.Where(x => x.WindowId == windowId && x.Outcome == null).Select(x => x.WorkOrderId).ToListAsync(cancellationToken);
        var jobs = await dbContext.WorkOrders.Where(x => jobIds.Contains(x.Id) && !Finished.Contains(x.Status)).ToListAsync(cancellationToken);
        var shortages = new List<SpareShortage>();
        var reserved = 0;
        foreach (var job in jobs)
        {
            var (count, shorts) = await ReservePlanAsync(job, store, cancellationToken);
            reserved += count;
            shortages.AddRange(shorts);
        }

        return Ok(new WindowSparesResult(reserved, shortages));
    }

    [HttpPost("reservations/{id:guid}/kit")]
    public async Task<ActionResult<JobSpares>> Kit(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        var reservation = await LoadReservationAsync(id, cancellationToken);
        await sparesService.KitAsync(reservation, Actor, cancellationToken);
        return Ok(await JobSparesAsync(await LoadJobAsync(reservation.WorkOrderId, cancellationToken), cancellationToken));
    }

    [HttpPost("reservations/{id:guid}/release")]
    public async Task<ActionResult<JobSpares>> Release(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanPlan();
        var reservation = await LoadReservationAsync(id, cancellationToken);
        await sparesService.ReleaseAsync(reservation, cancellationToken);
        return Ok(await JobSparesAsync(await LoadJobAsync(reservation.WorkOrderId, cancellationToken), cancellationToken));
    }

    [HttpPost("/api/factory/work-orders/{workOrderId:guid}/parts/{usageId:guid}/return")]
    public async Task<ActionResult<JobSpares>> ReturnPart(Guid workOrderId, Guid usageId, [FromBody] ReturnPartRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanAssignWorkOrders, PermissionNames.CanCompleteWorkOrders, PermissionNames.CanIssueMaterials);
        var job = await LoadJobAsync(workOrderId, cancellationToken);
        var usage = await dbContext.WorkOrderMaterialUsages.SingleOrDefaultAsync(x => x.Id == usageId && x.WorkOrderId == workOrderId, cancellationToken)
            ?? throw new NotFoundException("That part was not found on the job.");
        var store = await StoreForJobAsync(job, request.StoreId, cancellationToken);
        await sparesService.ReturnAsync(usage, store, request.Quantity, request.SerialNumber, request.NeedsRepair, Actor, cancellationToken);
        return Ok(await JobSparesAsync(job, cancellationToken));
    }

    /* ── Transfers ───────────────────────────────────────────────────────── */

    [HttpGet("transfers")]
    public async Task<ActionResult<IReadOnlyCollection<StoreTransferDto>>> Transfers([FromQuery] string? status, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var stores = await StoresInScopeAsync(cancellationToken);
        var query = dbContext.StoreTransfers.Where(x => x.TenantId == TenantId && (stores.Contains(x.FromStoreId) || stores.Contains(x.ToStoreId)));
        if (string.Equals(status, "open", StringComparison.OrdinalIgnoreCase)) query = query.Where(x => x.Status == StoreTransferStatuses.Requested || x.Status == StoreTransferStatuses.Dispatched);
        else if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return Ok(await TransferRows(query).Take(300).ToListAsync(cancellationToken));
    }

    [HttpPost("transfers")]
    public async Task<ActionResult<StoreTransferDto>> RequestTransfer([FromBody] TransferRequest request, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        // The receiving store must be one of mine; the sending store can be anywhere in the group.
        var to = await LoadStoreAsync(request.ToStoreId, cancellationToken);
        var from = await dbContext.Stores.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.FromStoreId, cancellationToken)
            ?? throw new NotFoundException("Sending store was not found.");
        var part = await LoadPartAsync(request.MaterialItemId, cancellationToken);
        var transfer = await sparesService.RequestTransferAsync(from, to, part, request.Quantity, request.Reason, request.WorkOrderId, Actor, cancellationToken);
        return Ok(await TransferRows(dbContext.StoreTransfers.Where(x => x.Id == transfer.Id)).SingleAsync(cancellationToken));
    }

    [HttpPost("transfers/{id:guid}/dispatch")]
    public async Task<ActionResult<StoreTransferDto>> Dispatch(Guid id, [FromBody] DispatchTransferRequest? request, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        var transfer = await LoadTransferAsync(id, cancellationToken);
        await LoadStoreAsync(transfer.FromStoreId, cancellationToken);
        await sparesService.DispatchTransferAsync(transfer, request?.SerialNumbers, Actor, cancellationToken);
        return Ok(await TransferRows(dbContext.StoreTransfers.Where(x => x.Id == id)).SingleAsync(cancellationToken));
    }

    [HttpPost("transfers/{id:guid}/receive")]
    public async Task<ActionResult<StoreTransferDto>> ReceiveTransfer(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        var transfer = await LoadTransferAsync(id, cancellationToken);
        await LoadStoreAsync(transfer.ToStoreId, cancellationToken);
        await sparesService.ReceiveTransferAsync(transfer, Actor, cancellationToken);
        return Ok(await TransferRows(dbContext.StoreTransfers.Where(x => x.Id == id)).SingleAsync(cancellationToken));
    }

    [HttpPost("transfers/{id:guid}/reject")]
    public async Task<ActionResult<StoreTransferDto>> RejectTransfer(Guid id, [FromBody] ReasonRequest request, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        var transfer = await LoadTransferAsync(id, cancellationToken);
        if (transfer.Status != StoreTransferStatuses.Requested) throw new BusinessRuleException("Only a requested transfer can be refused or cancelled.");
        // The sender refuses; the requester cancels.
        var sender = (await StoresInScopeAsync(cancellationToken)).Contains(transfer.FromStoreId);
        transfer.Status = sender && transfer.RequestedByName != Actor.Name ? StoreTransferStatuses.Rejected : StoreTransferStatuses.Cancelled;
        transfer.RejectReason = Clean(request.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await TransferRows(dbContext.StoreTransfers.Where(x => x.Id == id)).SingleAsync(cancellationToken));
    }

    /* ── Cycle counts ────────────────────────────────────────────────────── */

    [HttpGet("counts")]
    public async Task<ActionResult<IReadOnlyCollection<StoreCountSummary>>> Counts([FromQuery] Guid? storeId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var stores = await StoresInScopeAsync(cancellationToken);
        var query = dbContext.StoreCounts.Where(x => x.TenantId == TenantId && stores.Contains(x.StoreId));
        if (storeId.HasValue) query = query.Where(x => x.StoreId == storeId);
        return Ok(await query.OrderByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new StoreCountSummary(x.Id, x.CountNumber, x.StoreId, x.Store!.Name, x.Status, x.CriticalOnly, x.IsBlind, x.Lines.Count, x.Lines.Count(l => l.CountedQuantity != null),
                x.StartedByName, x.CreatedAt, x.PostedAt, x.VarianceValue)).ToListAsync(cancellationToken));
    }

    [HttpPost("counts")]
    public async Task<ActionResult<StoreCountDetail>> OpenCount([FromBody] OpenCountRequest request, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        var store = await LoadStoreAsync(request.StoreId, cancellationToken);
        var count = await sparesService.OpenCountAsync(store, request.BinId, request.CriticalOnly, request.Blind, Actor, cancellationToken);
        return Ok(await CountDetailAsync(count.Id, cancellationToken));
    }

    [HttpGet("counts/{id:guid}")]
    public async Task<ActionResult<StoreCountDetail>> Count(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await LoadCountAsync(id, cancellationToken);
        return Ok(await CountDetailAsync(id, cancellationToken));
    }

    [HttpPut("counts/{id:guid}/lines/{lineId:guid}")]
    public async Task<ActionResult<StoreCountDetail>> CountLine(Guid id, Guid lineId, [FromBody] CountLineRequest request, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        var count = await LoadCountAsync(id, cancellationToken);
        if (count.Status != StockCountStatuses.Open) throw new BusinessRuleException("This count is closed.");
        if (request.CountedQuantity < 0) throw new BusinessRuleException("A count can't be negative.");
        var line = await dbContext.StoreCountLines.SingleOrDefaultAsync(x => x.Id == lineId && x.StoreCountId == id, cancellationToken) ?? throw new NotFoundException("Count line was not found.");
        line.CountedQuantity = request.CountedQuantity;
        line.Notes = Clean(request.Notes);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await CountDetailAsync(id, cancellationToken));
    }

    [HttpPost("counts/{id:guid}/post")]
    public async Task<ActionResult<StoreCountDetail>> PostCount(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials);
        var count = await LoadCountAsync(id, cancellationToken);
        await sparesService.PostCountAsync(count, Actor, cancellationToken);
        return Ok(await CountDetailAsync(id, cancellationToken));
    }

    [HttpPost("counts/{id:guid}/cancel")]
    public async Task<ActionResult<StoreCountDetail>> CancelCount(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        var count = await LoadCountAsync(id, cancellationToken);
        if (count.Status != StockCountStatuses.Open) throw new BusinessRuleException("This count is closed.");
        count.Status = StockCountStatuses.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await CountDetailAsync(id, cancellationToken));
    }

    /* ── Reordering and requisitions ─────────────────────────────────────── */

    /// <summary>Parts at or below their reorder point, with how many to order and the best supplier price.</summary>
    [HttpGet("reorder")]
    public async Task<ActionResult<IReadOnlyCollection<ReorderRow>>> Reorder([FromQuery] Guid? factoryId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var stores = await StoresInScopeAsync(cancellationToken);
        var query = dbContext.StoreStocks.Where(x => stores.Contains(x.StoreId) && x.Store!.IsActive && x.MaterialItem!.IsActive
            && (x.ReorderPoint > 0 || x.MinLevel > 0) && x.OnHand - x.Reserved <= x.ReorderPoint);
        if (factoryId.HasValue) query = query.Where(x => x.Store!.FactoryId == factoryId);
        var rows = await query.Select(x => new
        {
            x.StoreId, Store = x.Store!.Name, x.Store.FactoryId, Factory = x.Store.Factory!.Name, x.MaterialItemId, x.MaterialItem!.ItemCode, x.MaterialItem.ItemName, x.MaterialItem.UnitOfMeasure,
            x.MaterialItem.IsCriticalSpare, x.MaterialItem.ReorderQuantity, x.MaterialItem.PreferredSupplierId, x.OnHand, x.Reserved, x.MinLevel, x.ReorderPoint, x.MaxLevel
        }).ToListAsync(cancellationToken);
        var ids = rows.Select(x => x.MaterialItemId).Distinct().ToList();
        var onOrder = await dbContext.PurchaseRequisitionLines
            .Where(x => x.MaterialItemId != null && ids.Contains(x.MaterialItemId.Value)
                && (x.Requisition!.Status == RequisitionStatuses.Draft || x.Requisition.Status == RequisitionStatuses.Submitted || x.Requisition.Status == RequisitionStatuses.Approved))
            .Select(x => new { MaterialItemId = x.MaterialItemId!.Value, x.Requisition!.StoreId, x.Quantity }).ToListAsync(cancellationToken);
        var poOpen = await dbContext.PurchaseOrderLines
            .Where(x => x.MaterialItemId != null && ids.Contains(x.MaterialItemId.Value) && x.PurchaseOrder!.Status != "Cancelled" && x.PurchaseOrder.Status != "Received" && x.QuantityOrdered > x.QuantityReceived)
            .Select(x => new { MaterialItemId = x.MaterialItemId!.Value, Outstanding = x.QuantityOrdered - x.QuantityReceived }).ToListAsync(cancellationToken);
        var prices = await CatalogueQuery(x => ids.Contains(x.MaterialItemId)).ToListAsync(cancellationToken);
        var stocksElsewhere = await dbContext.StoreStocks.Where(x => ids.Contains(x.MaterialItemId) && x.Store!.IsActive && x.OnHand - x.Reserved > x.ReorderPoint)
            .Select(x => new { x.MaterialItemId, x.StoreId, x.Store!.Name, Spare = x.OnHand - x.Reserved - x.ReorderPoint }).ToListAsync(cancellationToken);
        return Ok(rows.OrderByDescending(x => x.IsCriticalSpare).ThenBy(x => x.OnHand - x.Reserved - x.MinLevel).Select(x =>
        {
            var free = x.OnHand - x.Reserved;
            var target = x.MaxLevel ?? Math.Max(x.ReorderPoint * 2, x.ReorderPoint + 1);
            var pending = onOrder.Where(o => o.MaterialItemId == x.MaterialItemId && o.StoreId == x.StoreId).Sum(o => o.Quantity);
            var suggested = Math.Max(x.ReorderQuantity ?? Math.Ceiling(target - free), 0);
            var best = prices.Where(p => p.MaterialItemId == x.MaterialItemId && (p.ValidUntil == null || p.ValidUntil >= DateTime.UtcNow.Date))
                .OrderByDescending(p => p.IsPreferred || p.SupplierId == x.PreferredSupplierId).ThenBy(p => p.UnitPrice).FirstOrDefault();
            var donor = stocksElsewhere.Where(s => s.MaterialItemId == x.MaterialItemId && s.StoreId != x.StoreId).OrderByDescending(s => s.Spare).FirstOrDefault();
            return new ReorderRow(x.StoreId, x.Store, x.FactoryId, x.Factory, x.MaterialItemId, x.ItemCode, x.ItemName, x.UnitOfMeasure, x.IsCriticalSpare,
                x.OnHand, x.Reserved, free, x.MinLevel, x.ReorderPoint, StockState(free, x.MinLevel, x.ReorderPoint), suggested, pending,
                poOpen.Where(p => p.MaterialItemId == x.MaterialItemId).Sum(p => p.Outstanding),
                best?.SupplierId, best?.SupplierName, best?.UnitPrice, best?.Currency, best?.LeadTimeDays,
                donor?.StoreId, donor?.Name, donor?.Spare);
        }).ToList());
    }

    [HttpGet("requisitions")]
    public async Task<ActionResult<IReadOnlyCollection<RequisitionSummary>>> Requisitions([FromQuery] string? status, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var factories = await FactoriesInScopeAsync(cancellationToken);
        var query = dbContext.PurchaseRequisitions.Where(x => x.TenantId == TenantId && factories.Contains(x.FactoryId));
        if (string.Equals(status, "open", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == RequisitionStatuses.Draft || x.Status == RequisitionStatuses.Submitted || x.Status == RequisitionStatuses.Approved);
        else if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(200)
            .Select(x => new { x.Id, x.RequisitionNumber, x.FactoryId, Factory = x.Factory!.Name, x.StoreId, Store = x.Store != null ? x.Store.Name : null, x.Status, x.Reason, x.NeededBy, x.RequestedByName, x.CreatedAt, x.DecidedByName, x.DecidedAt,
                Lines = x.Lines.Select(l => new { l.Quantity, l.UnitPrice, l.Currency }).ToList() })
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new RequisitionSummary(x.Id, x.RequisitionNumber, x.FactoryId, x.Factory, x.StoreId, x.Store, x.Status, x.Reason, x.NeededBy, x.RequestedByName, x.CreatedAt,
            x.DecidedByName, x.DecidedAt, x.Lines.Count,
            x.Lines.GroupBy(l => l.Currency).Select(g => new MoneyTotal(g.Key, Math.Round(g.Sum(l => l.Quantity * (l.UnitPrice ?? 0)), 2))).ToList())).ToList());
    }

    [HttpGet("requisitions/{id:guid}")]
    public async Task<ActionResult<RequisitionDetail>> Requisition(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanView();
        await LoadRequisitionAsync(id, cancellationToken);
        return Ok(await RequisitionDetailAsync(id, cancellationToken));
    }

    [HttpPost("requisitions")]
    public async Task<ActionResult<RequisitionDetail>> CreateRequisition([FromBody] SaveRequisitionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanRequestMaterials, PermissionNames.CanManageMaterials, PermissionNames.CanAssignWorkOrders);
        var count = await dbContext.PurchaseRequisitions.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken);
        var requisition = new PurchaseRequisition { TenantId = TenantId, RequisitionNumber = $"PR-{count + 1:000000}", RequestedByUserId = UserId, RequestedByName = Actor.Name };
        await ApplyRequisitionAsync(requisition, request, cancellationToken);
        dbContext.PurchaseRequisitions.Add(requisition);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await RequisitionDetailAsync(requisition.Id, cancellationToken));
    }

    [HttpPut("requisitions/{id:guid}")]
    public async Task<ActionResult<RequisitionDetail>> UpdateRequisition(Guid id, [FromBody] SaveRequisitionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanRequestMaterials, PermissionNames.CanManageMaterials, PermissionNames.CanAssignWorkOrders);
        var requisition = await LoadRequisitionAsync(id, cancellationToken);
        if (requisition.Status is not (RequisitionStatuses.Draft or RequisitionStatuses.Submitted)) throw new BusinessRuleException("Only a draft or submitted requisition can be changed.");
        var lines = await dbContext.PurchaseRequisitionLines.Where(x => x.RequisitionId == id).ToListAsync(cancellationToken);
        dbContext.PurchaseRequisitionLines.RemoveRange(lines);
        requisition.Lines.Clear();
        await ApplyRequisitionAsync(requisition, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await RequisitionDetailAsync(id, cancellationToken));
    }

    [HttpPost("requisitions/{id:guid}/{action}")]
    public async Task<ActionResult<RequisitionDetail>> RequisitionAction(Guid id, string action, [FromBody] ReasonRequest? request, CancellationToken cancellationToken)
    {
        var requisition = await LoadRequisitionAsync(id, cancellationToken);
        var now = OccurredAt.UtcNow;
        switch (action.ToLowerInvariant())
        {
            case "submit":
                userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanRequestMaterials, PermissionNames.CanManageMaterials, PermissionNames.CanAssignWorkOrders);
                if (requisition.Status != RequisitionStatuses.Draft) throw new BusinessRuleException("Only a draft can be submitted.");
                requisition.Status = RequisitionStatuses.Submitted;
                requisition.SubmittedAt = now;
                break;
            case "approve":
                userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanApproveMaterials);
                if (requisition.Status != RequisitionStatuses.Submitted) throw new BusinessRuleException("Only a submitted requisition can be approved.");
                if (requisition.RequestedByUserId == UserId && !IsAdmin) throw new BusinessRuleException("Someone other than the requester must approve it.");
                requisition.Status = RequisitionStatuses.Approved;
                requisition.DecidedAt = now;
                requisition.DecidedByName = Actor.Name;
                break;
            case "reject":
                userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanApproveMaterials);
                if (requisition.Status != RequisitionStatuses.Submitted) throw new BusinessRuleException("Only a submitted requisition can be rejected.");
                requisition.Status = RequisitionStatuses.Rejected;
                requisition.RejectReason = Clean(request?.Reason) ?? throw new BusinessRuleException("Say why it is rejected.");
                requisition.DecidedAt = now;
                requisition.DecidedByName = Actor.Name;
                break;
            case "cancel":
                userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanRequestMaterials, PermissionNames.CanManageMaterials, PermissionNames.CanAssignWorkOrders);
                if (requisition.Status is RequisitionStatuses.Ordered or RequisitionStatuses.Cancelled) throw new BusinessRuleException("This requisition is already closed.");
                requisition.Status = RequisitionStatuses.Cancelled;
                break;
            case "order":
                userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials);
                await OrderAsync(requisition, cancellationToken);
                break;
            default:
                throw new BusinessRuleException("Action must be submit, approve, reject, cancel or order.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await RequisitionDetailAsync(id, cancellationToken));
    }

    /// <summary>Receives purchase order lines into a factory store, at the PO price converted to the tenant currency.</summary>
    [HttpPost("/api/factory/purchase-orders/{purchaseOrderId:guid}/receive")]
    public async Task<ActionResult<RequisitionPurchaseOrder>> ReceivePurchaseOrder(Guid purchaseOrderId, [FromBody] ReceivePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        EnsureCanIssue();
        var store = await LoadStoreAsync(request.StoreId, cancellationToken);
        var order = await dbContext.PurchaseOrders.Include(x => x.Lines).Include(x => x.Supplier)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == purchaseOrderId, cancellationToken) ?? throw new NotFoundException("Purchase order was not found.");
        if (order.Status is "Cancelled" or "Received" or "Draft") throw new BusinessRuleException($"Purchase order {order.PoNumber} is {order.Status.ToLowerInvariant()}; it can't be received.");
        if (request.Lines.Count == 0) throw new BusinessRuleException("Choose what arrived.");
        foreach (var receipt in request.Lines)
        {
            var line = order.Lines.SingleOrDefault(x => x.Id == receipt.LineId) ?? throw new BusinessRuleException("Line was not found on the order.");
            var outstanding = line.QuantityOrdered - line.QuantityReceived;
            if (receipt.Quantity <= 0 || receipt.Quantity > outstanding) throw new BusinessRuleException($"{line.ItemName}: receive between 0 and {outstanding:0.##}.");
            if (line.MaterialItemId is { } materialId)
            {
                var part = await LoadPartAsync(materialId, cancellationToken);
                await sparesService.ReceiveAsync(store, part, receipt.Quantity, line.UnitCost, order.Currency, order.PoNumber, receipt.SerialNumbers, Actor, cancellationToken);
            }

            line.QuantityReceived += receipt.Quantity;
            dbContext.PurchaseOrderReceiptEvents.Add(new PurchaseOrderReceiptEvent
            {
                TenantId = TenantId, PurchaseOrderId = order.Id, PurchaseOrderLineId = line.Id, ItemName = line.ItemName, Quantity = receipt.Quantity,
                ReceivedByUserId = UserId, ReceivedByName = Actor.Name
            });
        }

        order.Status = order.Lines.All(x => x.QuantityReceived >= x.QuantityOrdered) ? "Received" : "PartiallyReceived";
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapPo(order));
    }

    /* ── Vendor catalogue ────────────────────────────────────────────────── */

    [HttpGet("catalogue")]
    public async Task<ActionResult<IReadOnlyCollection<CatalogueRow>>> Catalogue([FromQuery] Guid? supplierId, [FromQuery] Guid? materialItemId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLower();
        return Ok(await CatalogueQuery(x => (supplierId == null || x.SupplierId == supplierId) && (materialItemId == null || x.MaterialItemId == materialItemId)
            && (term == null || x.MaterialItem!.ItemName.ToLower().Contains(term) || x.MaterialItem.ItemCode.ToLower().Contains(term) || x.Supplier!.SupplierName.ToLower().Contains(term)
                || (x.VendorPartNumber != null && x.VendorPartNumber.ToLower().Contains(term)))).Take(500).ToListAsync(cancellationToken));
    }

    [HttpPost("catalogue")]
    public async Task<ActionResult<CatalogueRow>> AddCatalogue([FromBody] SaveCatalogueRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials);
        if (await dbContext.VendorCatalogueItems.AnyAsync(x => x.SupplierId == request.SupplierId && x.MaterialItemId == request.MaterialItemId, cancellationToken))
            throw new ConflictException("That supplier already has a price for the part; edit it instead.");
        var item = new VendorCatalogueItem { TenantId = TenantId };
        await ApplyCatalogueAsync(item, request, cancellationToken);
        dbContext.VendorCatalogueItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await CatalogueQuery(x => x.Id == item.Id).SingleAsync(cancellationToken));
    }

    [HttpPut("catalogue/{id:guid}")]
    public async Task<ActionResult<CatalogueRow>> UpdateCatalogue(Guid id, [FromBody] SaveCatalogueRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials);
        var item = await dbContext.VendorCatalogueItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Catalogue line was not found.");
        await ApplyCatalogueAsync(item, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await CatalogueQuery(x => x.Id == item.Id).SingleAsync(cancellationToken));
    }

    [HttpDelete("catalogue/{id:guid}")]
    public async Task<IActionResult> DeleteCatalogue(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials);
        var item = await dbContext.VendorCatalogueItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Catalogue line was not found.");
        dbContext.VendorCatalogueItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /* ── Chemicals register ──────────────────────────────────────────────── */

    [HttpGet("chemicals")]
    public async Task<ActionResult<IReadOnlyCollection<ChemicalRow>>> Chemicals(CancellationToken cancellationToken)
    {
        EnsureCanView();
        var today = DateTime.UtcNow.Date;
        var rows = await dbContext.MaterialItems.Where(x => x.TenantId == TenantId && x.IsActive && x.IsChemical).OrderBy(x => x.ChemicalKind).ThenBy(x => x.ItemName)
            .Select(x => new { x.Id, x.ItemCode, x.ItemName, x.UnitOfMeasure, x.ChemicalKind, x.HazardClass, x.FoodGradeClass, x.SdsAttachmentId, x.SdsIssuedOn,
                OnHand = dbContext.StoreStocks.Where(s => s.MaterialItemId == x.Id).Sum(s => s.OnHand),
                Stores = dbContext.StoreStocks.Where(s => s.MaterialItemId == x.Id && s.OnHand > 0).Select(s => s.Store!.Name).ToList() })
            .ToListAsync(cancellationToken);
        return Ok(rows.Select(x => new ChemicalRow(x.Id, x.ItemCode, x.ItemName, x.UnitOfMeasure, x.ChemicalKind, x.HazardClass, x.FoodGradeClass, x.SdsAttachmentId, x.SdsIssuedOn,
            // A safety data sheet is reviewed at least every 5 years.
            x.SdsAttachmentId is null ? "Missing" : x.SdsIssuedOn is { } issued && issued.Date.AddYears(5) < today ? "Review due" : "Current",
            x.OnHand, x.Stores)).ToList());
    }

    /// <summary>Lubricants issued to jobs, with their food-grade class: the record food safety audits ask for.</summary>
    [HttpGet("chemicals/lubricant-log")]
    public async Task<ActionResult<IReadOnlyCollection<LubricantLogRow>>> LubricantLog([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var start = Utc(from ?? DateTime.UtcNow.AddDays(-90))!.Value;
        var end = Utc(to ?? DateTime.UtcNow)!.Value.AddDays(1);
        var stores = await StoresInScopeAsync(cancellationToken);
        var rows = await dbContext.StockMovements
            .Where(x => x.TenantId == TenantId && x.StoreId != null && stores.Contains(x.StoreId.Value) && x.MovementType == StoreMovementTypes.Issue
                && x.Material!.IsChemical && x.Material.ChemicalKind == ChemicalKinds.Lubricant && x.CreatedAt >= start && x.CreatedAt < end)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new LubricantLogRow(x.CreatedAt, x.Material!.ItemName, x.Material.FoodGradeClass, x.Quantity, x.Material.UnitOfMeasure,
                x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : null, x.WorkOrder != null && x.WorkOrder.Asset != null ? x.WorkOrder.Asset.AssetCode : null,
                x.WorkOrder != null && x.WorkOrder.Asset != null ? x.WorkOrder.Asset.AssetName : null, x.CreatedByUser != null ? x.CreatedByUser.FullName : null))
            .ToListAsync(cancellationToken);
        return Ok(rows);
    }

    /* ── Helpers ─────────────────────────────────────────────────────────── */

    private async Task<(int Reserved, List<SpareShortage> Shortages)> ReservePlanAsync(WorkOrder job, Store? store, CancellationToken cancellationToken)
    {
        if (job.JobPlanId is not { } planId) return (0, []);
        store ??= await StoreForJobAsync(job, null, cancellationToken);
        var resources = await dbContext.JobPlanResources.Where(x => x.JobPlanId == planId && x.MaterialItemId != null && x.Quantity > 0).ToListAsync(cancellationToken);
        var held = await dbContext.StockReservations.Where(x => x.WorkOrderId == job.Id && x.Status != ReservationStatuses.Released)
            .GroupBy(x => x.MaterialItemId).Select(g => new { g.Key, Quantity = g.Sum(x => x.Quantity) }).ToDictionaryAsync(x => x.Key, x => x.Quantity, cancellationToken);
        var used = await dbContext.WorkOrderMaterialUsages.Where(x => x.WorkOrderId == job.Id)
            .GroupBy(x => x.MaterialItemId).Select(g => new { g.Key, Quantity = g.Sum(x => x.QuantityUsed) }).ToDictionaryAsync(x => x.Key, x => x.Quantity, cancellationToken);
        var count = 0;
        var shortages = new List<SpareShortage>();
        foreach (var resource in resources)
        {
            var part = await LoadPartAsync(resource.MaterialItemId!.Value, cancellationToken);
            // Top up only what isn't already reserved (or issued without a reservation).
            var needed = resource.Quantity - Math.Max(held.GetValueOrDefault(part.Id), used.GetValueOrDefault(part.Id));
            if (needed <= 0) continue;
            var result = await sparesService.ReserveAsync(store, part, needed, job, true, Actor, cancellationToken);
            if (result.Reserved > 0) count++;
            if (result.Short > 0) shortages.Add(new SpareShortage(job.Id, job.WorkOrderNumber, part.Id, part.ItemCode, part.ItemName, result.Short, part.UnitOfMeasure, part.IsCriticalSpare));
        }

        return (count, shortages);
    }

    private async Task<JobSpares> JobSparesAsync(WorkOrder job, CancellationToken cancellationToken)
    {
        var reservations = await dbContext.StockReservations.Where(x => x.WorkOrderId == job.Id).OrderBy(x => x.CreatedAt)
            .Select(x => new JobReservation(x.Id, x.StoreId, x.Store!.Name, x.MaterialItemId, x.MaterialItem!.ItemCode, x.MaterialItem.ItemName, x.MaterialItem.UnitOfMeasure,
                x.Quantity, x.IssuedQuantity, x.Status, x.ReservedByName, x.KittedAt, x.KittedByName)).ToListAsync(cancellationToken);
        var used = await dbContext.WorkOrderMaterialUsages.Where(x => x.WorkOrderId == job.Id).OrderBy(x => x.UsedAt)
            .Select(x => new JobPartUsed(x.Id, x.MaterialItemId, x.MaterialItem!.ItemCode, x.MaterialItem.ItemName, x.MaterialItem.UnitOfMeasure, x.MaterialItem.IsSerialised,
                x.QuantityUsed, x.UnitCost, Math.Round(x.QuantityUsed * (x.UnitCost ?? 0), 2), x.Notes, x.UsedAt)).ToListAsync(cancellationToken);
        var planned = job.JobPlanId is { } planId
            ? await dbContext.JobPlanResources.Where(x => x.JobPlanId == planId && x.MaterialItemId != null)
                .Select(x => new JobPlannedPart(x.MaterialItemId!.Value, x.Description, x.Quantity)).ToListAsync(cancellationToken)
            : [];
        var factory = job.OrgNodeId is { } node ? await FactoryOfAsync(node, cancellationToken) : null;
        var defaultStore = factory is { } f ? await sparesService.DefaultStoreAsync(TenantId, f, cancellationToken) : null;
        var serials = await dbContext.SpareUnits.Where(x => x.TenantId == TenantId && x.Status == SpareUnitStatuses.Installed && x.AssetId != null && x.AssetId == job.AssetId)
            .Select(x => new InstalledUnit(x.Id, x.MaterialItemId, x.MaterialItem!.ItemName, x.SerialNumber)).ToListAsync(cancellationToken);
        return new JobSpares(job.Id, factory, defaultStore?.Id, defaultStore?.Name, reservations, used, planned, used.Sum(x => x.Cost), serials);
    }

    private async Task<Store> StoreForJobAsync(WorkOrder job, Guid? storeId, CancellationToken cancellationToken)
    {
        if (storeId is { } id) return await LoadStoreAsync(id, cancellationToken);
        var factory = job.OrgNodeId is { } node ? await FactoryOfAsync(node, cancellationToken) : null;
        return (factory is { } f ? await sparesService.DefaultStoreAsync(TenantId, f, cancellationToken) : null)
            ?? throw new BusinessRuleException("The job's factory has no store. Set one up under Stores first.");
    }

    private async Task OrderAsync(PurchaseRequisition requisition, CancellationToken cancellationToken)
    {
        if (requisition.Status != RequisitionStatuses.Approved) throw new BusinessRuleException("Only an approved requisition can be ordered.");
        var lines = await dbContext.PurchaseRequisitionLines.Where(x => x.RequisitionId == requisition.Id).ToListAsync(cancellationToken);
        if (lines.Any(x => x.SupplierId is null)) throw new BusinessRuleException("Choose a supplier for every line before ordering.");
        var poCount = await dbContext.PurchaseOrders.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken);
        foreach (var group in lines.GroupBy(x => new { SupplierId = x.SupplierId!.Value, x.Currency }))
        {
            var order = new PurchaseOrder
            {
                TenantId = TenantId, PoNumber = $"PO-{++poCount:000000}", SupplierId = group.Key.SupplierId, Status = "Sent", OrderedAt = OccurredAt.UtcNow,
                Currency = group.Key.Currency, ExpectedAt = requisition.NeededBy, Notes = $"From requisition {requisition.RequisitionNumber}"
            };
            foreach (var line in group)
            {
                var price = line.UnitPrice ?? 0;
                order.Lines.Add(new PurchaseOrderLine { TenantId = TenantId, MaterialItemId = line.MaterialItemId, ItemName = line.Description, QuantityOrdered = line.Quantity, UnitCost = price, LineTotal = Math.Round(price * line.Quantity, 2) });
                line.PurchaseOrderId = order.Id;
            }

            order.TotalAmount = order.Lines.Sum(x => x.LineTotal);
            dbContext.PurchaseOrders.Add(order);
        }

        requisition.Status = RequisitionStatuses.Ordered;
    }

    private async Task<RequisitionDetail> RequisitionDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var summary = Unwrap(await Requisitions(null, cancellationToken)).SingleOrDefault(x => x.Id == id)
            ?? (await RequisitionSummaryAsync(id, cancellationToken));
        var requisition = await dbContext.PurchaseRequisitions.SingleAsync(x => x.Id == id, cancellationToken);
        var lines = await dbContext.PurchaseRequisitionLines.Where(x => x.RequisitionId == id).OrderBy(x => x.CreatedAt)
            .Select(x => new RequisitionLineDto(x.Id, x.MaterialItemId, x.MaterialItem != null ? x.MaterialItem.ItemCode : null, x.Description, x.Quantity,
                x.SupplierId, x.Supplier != null ? x.Supplier.SupplierName : null, x.UnitPrice, x.Currency, x.WorkOrderId, x.PurchaseOrderId))
            .ToListAsync(cancellationToken);
        var poIds = lines.Where(x => x.PurchaseOrderId != null).Select(x => x.PurchaseOrderId!.Value).Distinct().ToList();
        var orders = await dbContext.PurchaseOrders.Include(x => x.Lines).Include(x => x.Supplier).Where(x => poIds.Contains(x.Id)).ToListAsync(cancellationToken);
        return new RequisitionDetail(summary, requisition.RejectReason, lines, orders.Select(MapPo).ToList());
    }

    private async Task<RequisitionSummary> RequisitionSummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        var x = await dbContext.PurchaseRequisitions.Include(r => r.Factory).Include(r => r.Store).Include(r => r.Lines).SingleAsync(r => r.Id == id, cancellationToken);
        return new RequisitionSummary(x.Id, x.RequisitionNumber, x.FactoryId, x.Factory?.Name ?? string.Empty, x.StoreId, x.Store?.Name, x.Status, x.Reason, x.NeededBy, x.RequestedByName, x.CreatedAt,
            x.DecidedByName, x.DecidedAt, x.Lines.Count, x.Lines.GroupBy(l => l.Currency).Select(g => new MoneyTotal(g.Key, Math.Round(g.Sum(l => l.Quantity * (l.UnitPrice ?? 0)), 2))).ToList());
    }

    private static RequisitionPurchaseOrder MapPo(PurchaseOrder x) => new(x.Id, x.PoNumber, x.SupplierId, x.Supplier?.SupplierName, x.Status, x.Currency, x.TotalAmount,
        x.Lines.Select(l => new RequisitionPurchaseOrderLine(l.Id, l.MaterialItemId, l.ItemName, l.QuantityOrdered, l.QuantityReceived, l.UnitCost)).ToList());

    private async Task ApplyRequisitionAsync(PurchaseRequisition requisition, SaveRequisitionRequest request, CancellationToken cancellationToken)
    {
        var factories = await FactoriesInScopeAsync(cancellationToken);
        if (!factories.Contains(request.FactoryId)) throw new BusinessRuleException("Choose one of your factories.");
        requisition.FactoryId = request.FactoryId;
        if (request.StoreId is { } storeId)
        {
            var store = await LoadStoreAsync(storeId, cancellationToken);
            if (store.FactoryId != request.FactoryId) throw new BusinessRuleException("The store must be in the requisition's factory.");
        }

        requisition.StoreId = request.StoreId;
        requisition.Reason = Clean(request.Reason);
        requisition.NeededBy = Utc(request.NeededBy);
        if (request.Lines.Count == 0) throw new BusinessRuleException("Add at least one line.");
        var tenantCurrency = await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.Currency).SingleAsync(cancellationToken);
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0) throw new BusinessRuleException("Each line needs a quantity above zero.");
            MaterialItem? part = line.MaterialItemId is { } partId ? await LoadPartAsync(partId, cancellationToken) : null;
            var description = Clean(line.Description) ?? part?.ItemName ?? throw new BusinessRuleException("Each line needs a part or a description.");
            if (line.SupplierId is { } supplierId && !await dbContext.Suppliers.AnyAsync(x => x.TenantId == TenantId && x.Id == supplierId, cancellationToken))
                throw new BusinessRuleException("Supplier was not found.");
            // Price from the vendor catalogue when not given.
            var catalogue = part is not null && line.SupplierId is { } sid
                ? await dbContext.VendorCatalogueItems.FirstOrDefaultAsync(x => x.SupplierId == sid && x.MaterialItemId == part.Id, cancellationToken)
                : null;
            requisition.Lines.Add(new PurchaseRequisitionLine
            {
                TenantId = TenantId, RequisitionId = requisition.Id, MaterialItemId = part?.Id, Description = description, Quantity = line.Quantity,
                SupplierId = line.SupplierId, UnitPrice = line.UnitPrice ?? catalogue?.UnitPrice ?? part?.UnitCost,
                Currency = (line.Currency ?? catalogue?.Currency ?? tenantCurrency).Trim().ToUpperInvariant(), WorkOrderId = line.WorkOrderId
            });
        }
    }

    private async Task ApplyCatalogueAsync(VendorCatalogueItem item, SaveCatalogueRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Suppliers.AnyAsync(x => x.TenantId == TenantId && x.Id == request.SupplierId, cancellationToken)) throw new BusinessRuleException("Supplier was not found.");
        await LoadPartAsync(request.MaterialItemId, cancellationToken);
        if (request.UnitPrice < 0) throw new BusinessRuleException("Price can't be negative.");
        var currency = (request.Currency ?? await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.Currency).SingleAsync(cancellationToken)).Trim().ToUpperInvariant();
        if (currency.Length != 3) throw new BusinessRuleException("Currency is a 3-letter code, e.g. KES or USD.");
        item.SupplierId = request.SupplierId;
        item.MaterialItemId = request.MaterialItemId;
        item.VendorPartNumber = Clean(request.VendorPartNumber);
        item.UnitPrice = request.UnitPrice;
        item.Currency = currency;
        item.LeadTimeDays = request.LeadTimeDays;
        item.MinOrderQuantity = request.MinOrderQuantity;
        item.ValidUntil = Utc(request.ValidUntil);
        item.IsPreferred = request.IsPreferred;
        if (request.IsPreferred)
        {
            foreach (var other in await dbContext.VendorCatalogueItems.Where(x => x.MaterialItemId == request.MaterialItemId && x.Id != item.Id && x.IsPreferred).ToListAsync(cancellationToken))
            {
                other.IsPreferred = false;
            }
        }
    }

    private async Task ApplyPartAsync(MaterialItem part, SaveSparePartRequest request, CancellationToken cancellationToken)
    {
        var code = Required(request.ItemCode, "Part number").ToUpperInvariant();
        if (await dbContext.MaterialItems.AnyAsync(x => x.TenantId == TenantId && x.ItemCode == code && x.Id != part.Id, cancellationToken)) throw new ConflictException($"Part number {code} is already used.");
        part.ItemCode = code;
        part.ItemName = Required(request.ItemName, "Part name");
        part.Category = Clean(request.Category);
        part.UnitOfMeasure = Required(request.UnitOfMeasure, "Unit of measure");
        part.PurchaseUnitOfMeasure = Clean(request.PurchaseUnitOfMeasure);
        part.PurchaseUnitFactor = request.PurchaseUnitFactor is > 0 ? request.PurchaseUnitFactor.Value : 1;
        part.Barcode = Clean(request.Barcode);
        part.UnitCost = request.UnitCost ?? part.UnitCost;
        part.ReorderQuantity = request.ReorderQuantity;
        if (request.PreferredSupplierId is { } supplierId && !await dbContext.Suppliers.AnyAsync(x => x.TenantId == TenantId && x.Id == supplierId, cancellationToken))
            throw new BusinessRuleException("Supplier was not found.");
        part.PreferredSupplierId = request.PreferredSupplierId;
        part.IsCriticalSpare = request.IsCriticalSpare;
        part.IsSerialised = request.IsSerialised;
        part.IsRepairable = request.IsRepairable;
        part.ExternalId = Clean(request.ExternalId);
        part.ExternalSystem = Clean(request.ExternalSystem);
        part.IsChemical = request.IsChemical;
        part.ChemicalKind = request.IsChemical ? ChemicalKinds.Find(request.ChemicalKind) ?? ChemicalKinds.Other : null;
        part.HazardClass = request.IsChemical ? Clean(request.HazardClass) : null;
        part.FoodGradeClass = request.IsChemical && part.ChemicalKind == ChemicalKinds.Lubricant ? FoodGradeClasses.Find(request.FoodGradeClass) : null;
        if (request.SdsAttachmentId is { } sds && !await dbContext.Attachments.AnyAsync(x => x.TenantId == TenantId && x.Id == sds, cancellationToken))
            throw new BusinessRuleException("Safety data sheet file was not found.");
        part.SdsAttachmentId = request.IsChemical ? request.SdsAttachmentId : null;
        part.SdsIssuedOn = request.IsChemical ? Utc(request.SdsIssuedOn) : null;
    }

    private async Task ApplyStoreAsync(Store store, SaveStoreRequest request, CancellationToken cancellationToken)
    {
        var factory = await dbContext.OrgNodes.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.FactoryId && x.Level == OrgNodeLevels.Factory, cancellationToken)
            ?? throw new BusinessRuleException("Choose a factory.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, factory.Id, cancellationToken);
        var code = Required(request.Code, "Store code").ToUpperInvariant();
        if (await dbContext.Stores.AnyAsync(x => x.TenantId == TenantId && x.Code == code && x.Id != store.Id, cancellationToken)) throw new ConflictException($"Store code {code} is already used.");
        if (store.Id != Guid.Empty && store.FactoryId != Guid.Empty && store.FactoryId != factory.Id && await dbContext.StoreStocks.AnyAsync(x => x.StoreId == store.Id && x.OnHand != 0, cancellationToken))
            throw new BusinessRuleException("A store holding stock can't move to another factory; transfer the stock out first.");
        store.FactoryId = factory.Id;
        store.Code = code;
        store.Name = Required(request.Name, "Store name");
        store.Location = Clean(request.Location);
        store.IsActive = request.IsActive ?? true;
        var hasMain = await dbContext.Stores.AnyAsync(x => x.TenantId == TenantId && x.FactoryId == factory.Id && x.IsMain && x.Id != store.Id && x.IsActive, cancellationToken);
        store.IsMain = request.IsMain || !hasMain;
        if (store.IsMain)
        {
            foreach (var other in await dbContext.Stores.Where(x => x.TenantId == TenantId && x.FactoryId == factory.Id && x.IsMain && x.Id != store.Id).ToListAsync(cancellationToken))
            {
                other.IsMain = false;
            }
        }
    }

    private async Task<StoreCountDetail> CountDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var summary = Unwrap(await Counts(null, cancellationToken)).Single(x => x.Id == id);
        var lines = await dbContext.StoreCountLines.Where(x => x.StoreCountId == id).OrderBy(x => x.MaterialItem!.ItemName)
            .Select(x => new { x.Id, x.MaterialItemId, x.MaterialItem!.ItemCode, x.MaterialItem.ItemName, x.MaterialItem.UnitOfMeasure, x.ExpectedQuantity, x.CountedQuantity, x.UnitCost, x.Notes })
            .ToListAsync(cancellationToken);
        var hide = summary.IsBlind && summary.Status == StockCountStatuses.Open;
        return new StoreCountDetail(summary, lines.Select(x => new StoreCountLineDto(x.Id, x.MaterialItemId, x.ItemCode, x.ItemName, x.UnitOfMeasure,
            hide ? null : x.ExpectedQuantity, x.CountedQuantity,
            hide || x.CountedQuantity is null ? null : x.CountedQuantity - x.ExpectedQuantity,
            hide || x.CountedQuantity is null ? null : Math.Round((x.CountedQuantity.Value - x.ExpectedQuantity) * x.UnitCost, 2), x.Notes)).ToList());
    }

    private IQueryable<SpareUnitDto> UnitsQuery(Expression<Func<SpareUnit, bool>>? filter = null) =>
        dbContext.SpareUnits.Where(x => x.TenantId == TenantId).Where(filter ?? (_ => true)).OrderBy(x => x.MaterialItem!.ItemName).ThenBy(x => x.SerialNumber)
            .Select(x => new SpareUnitDto(x.Id, x.MaterialItemId, x.MaterialItem!.ItemCode, x.MaterialItem.ItemName, x.SerialNumber, x.Status,
                x.StoreId, x.Store != null ? x.Store.Name : null, x.AssetId, x.Asset != null ? x.Asset.AssetCode : null, x.Asset != null ? x.Asset.AssetName : null,
                x.RepairSupplierId, x.RepairSupplier != null ? x.RepairSupplier.SupplierName : null, x.RepairReference, x.SentForRepairAt, x.ExpectedBackAt,
                x.RepairCount, x.TotalRepairCost, x.UnitCost));

    private IQueryable<CatalogueRow> CatalogueQuery(Expression<Func<VendorCatalogueItem, bool>>? filter = null) =>
        dbContext.VendorCatalogueItems.Where(x => x.TenantId == TenantId).Where(filter ?? (_ => true)).OrderBy(x => x.MaterialItem!.ItemName).ThenBy(x => x.UnitPrice)
            .Select(x => new CatalogueRow(x.Id, x.SupplierId, x.Supplier!.SupplierName, x.MaterialItemId, x.MaterialItem!.ItemCode, x.MaterialItem.ItemName,
                x.VendorPartNumber, x.UnitPrice, x.Currency, x.LeadTimeDays ?? x.Supplier.LeadTimeDays, x.MinOrderQuantity, x.ValidUntil, x.IsPreferred));

    private IQueryable<StoreTransferDto> TransferRows(IQueryable<StoreTransfer> query) =>
        query.OrderByDescending(x => x.CreatedAt).Select(x => new StoreTransferDto(x.Id, x.TransferNumber, x.FromStoreId, x.FromStore!.Name, x.FromStore.Factory!.Name,
            x.ToStoreId, x.ToStore!.Name, x.ToStore.Factory!.Name, x.MaterialItemId, x.MaterialItem!.ItemCode, x.MaterialItem.ItemName, x.MaterialItem.UnitOfMeasure,
            x.Quantity, x.SerialNumbers, x.Status, x.IsInterFactory, x.Reason, x.RequestedByName, x.CreatedAt, x.DispatchedAt, x.DispatchedByName, x.ReceivedAt, x.ReceivedByName, x.RejectReason));

    /// <summary>Parts fitted to an A-class asset count as critical even when not flagged.</summary>
    private async Task<HashSet<Guid>> CriticalAssetPartsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        (await dbContext.AssetParts.Where(x => ids.Contains(x.MaterialItemId) && x.Asset!.Criticality == "A").Select(x => x.MaterialItemId).Distinct().ToListAsync(cancellationToken)).ToHashSet();

    private static T Unwrap<T>(ActionResult<T> result) => result.Value ?? (T)((ObjectResult)result.Result!).Value!;

    private static string StockState(decimal free, decimal min, decimal reorder) =>
        free <= 0 && (min > 0 || reorder > 0) ? "Out" : free < min ? "BelowMin" : free <= reorder && reorder > 0 ? "Reorder" : free <= 0 ? "None" : "Ok";

    private async Task<HashSet<Guid>> FactoriesInScopeAsync(CancellationToken cancellationToken)
    {
        var nodes = await dbContext.OrgNodes.Where(x => x.TenantId == TenantId).Select(x => new { x.Id, x.ParentId, x.Level }).ToListAsync(cancellationToken);
        var factories = nodes.Where(x => x.Level == OrgNodeLevels.Factory).Select(x => x.Id).ToHashSet();
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (scope.Nodes is null) return factories;
        var parents = nodes.ToDictionary(x => x.Id, x => x.ParentId);
        var result = new HashSet<Guid>();
        foreach (var node in scope.Nodes)
        {
            Guid? current = node;
            for (var depth = 0; current is { } c && depth < 10; depth++)
            {
                if (factories.Contains(c)) { result.Add(c); break; }
                current = parents.GetValueOrDefault(c);
            }
        }

        return result;
    }

    private async Task<Guid?> FactoryOfAsync(Guid orgNodeId, CancellationToken cancellationToken)
    {
        var nodes = await dbContext.OrgNodes.Where(x => x.TenantId == TenantId).Select(x => new { x.Id, x.ParentId, x.Level }).ToDictionaryAsync(x => x.Id, cancellationToken);
        Guid? current = orgNodeId;
        for (var depth = 0; current is { } c && depth < 10 && nodes.TryGetValue(c, out var node); depth++)
        {
            if (node.Level == OrgNodeLevels.Factory) return node.Id;
            current = node.ParentId;
        }

        return null;
    }

    private async Task<List<Guid>> StoresInScopeAsync(CancellationToken cancellationToken)
    {
        var factories = await FactoriesInScopeAsync(cancellationToken);
        return await dbContext.Stores.Where(x => x.TenantId == TenantId && factories.Contains(x.FactoryId)).Select(x => x.Id).ToListAsync(cancellationToken);
    }

    private async Task<Store> LoadStoreAsync(Guid id, CancellationToken cancellationToken)
    {
        var store = await dbContext.Stores.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Store was not found.");
        if (!(await FactoriesInScopeAsync(cancellationToken)).Contains(store.FactoryId)) throw new NotFoundException("Store was not found.");
        return store;
    }

    private async Task<MaterialItem> LoadPartAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.MaterialItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Part was not found.");

    private async Task<SpareUnit> LoadUnitAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.SpareUnits.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Unit was not found.");

    private async Task<StoreTransfer> LoadTransferAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.StoreTransfers.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Transfer was not found.");

    private async Task<StoreCount> LoadCountAsync(Guid id, CancellationToken cancellationToken)
    {
        var count = await dbContext.StoreCounts.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Count was not found.");
        await LoadStoreAsync(count.StoreId, cancellationToken);
        return count;
    }

    private async Task<PurchaseRequisition> LoadRequisitionAsync(Guid id, CancellationToken cancellationToken)
    {
        var requisition = await dbContext.PurchaseRequisitions.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Requisition was not found.");
        if (!(await FactoriesInScopeAsync(cancellationToken)).Contains(requisition.FactoryId)) throw new NotFoundException("Requisition was not found.");
        return requisition;
    }

    private async Task<StockReservation> LoadReservationAsync(Guid id, CancellationToken cancellationToken)
    {
        var reservation = await dbContext.StockReservations.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Reservation was not found.");
        await LoadStoreAsync(reservation.StoreId, cancellationToken);
        return reservation;
    }

    private async Task<Asset> LoadAssetAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await dbContext.Assets.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Asset was not found.");
        if (asset.OrgNodeId is { } node) await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, node, cancellationToken);
        return asset;
    }

    private async Task<WorkOrder> LoadJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.ClientId == null, cancellationToken) ?? throw new NotFoundException("Job was not found.");
        if (job.OrgNodeId is { } node) await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, node, cancellationToken);
        return job;
    }

    private async Task<WorkOrder> LoadOpenJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await LoadJobAsync(id, cancellationToken);
        if (Finished.Contains(job.Status) || job.Status is WorkOrderStatuses.Completed or WorkOrderStatuses.Verified) throw new BusinessRuleException("The job is finished; nothing more can be reserved for it.");
        return job;
    }

    private void EnsureCanView() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewMaterials, PermissionNames.CanViewWorkOrders, PermissionNames.CanManageMaterials);

    private void EnsureCanManage() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials, PermissionNames.CanManageSettings);

    /// <summary>Storekeepers: receiving, issuing, kitting, counting and moving stock.</summary>
    private void EnsureCanIssue() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanIssueMaterials, PermissionNames.CanManageMaterials);

    /// <summary>Planners reserving parts for jobs.</summary>
    private void EnsureCanPlan() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanAssignWorkOrders, PermissionNames.CanIssueMaterials, PermissionNames.CanManageMaterials);

    private static string Required(string? value, string label) => string.IsNullOrWhiteSpace(value) ? throw new BusinessRuleException($"{label} is required.") : value.Trim();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Dates arrive without a kind; PostgreSQL stores UTC.</summary>
    private static DateTime? Utc(DateTime? value) => value is { } v ? DateTime.SpecifyKind(v.Date, DateTimeKind.Utc) : null;
}

public sealed record StoreSummary(Guid Id, Guid FactoryId, string FactoryName, string Code, string Name, string? Location, bool IsMain, bool IsActive, int PartsInStock, decimal StockValue, int ToReorder, int CriticalOut);
public sealed record SaveStoreRequest(Guid FactoryId, [StringLength(30)] string Code, [StringLength(150)] string Name, [StringLength(200)] string? Location = null, bool IsMain = false, bool? IsActive = null);
public sealed record StoreBinDto(Guid Id, string Code, string? Description, bool IsActive);
public sealed record SaveBinRequest([StringLength(30)] string Code, [StringLength(200)] string? Description = null);
public sealed record StoreStockRow(Guid MaterialItemId, string ItemCode, string ItemName, string UnitOfMeasure, string? Category, bool IsCriticalSpare, bool IsSerialised, bool IsRepairable, bool IsChemical,
    Guid? BinId, string? Bin, decimal OnHand, decimal Reserved, decimal Available, decimal MinLevel, decimal ReorderPoint, decimal? MaxLevel, decimal AverageCost, decimal Value, string State, DateTime? LastCountedAt);
public sealed record StockLevelsRequest(decimal MinLevel, decimal ReorderPoint, decimal? MaxLevel = null, Guid? BinId = null);
public sealed record ReceiveStockRequest(Guid MaterialItemId, decimal Quantity, decimal? UnitCost = null, [StringLength(3)] string? Currency = null, [StringLength(100)] string? Reference = null,
    IReadOnlyCollection<string>? SerialNumbers = null, bool InPurchaseUnits = false);
public sealed record AdjustStockRequest(Guid MaterialItemId, decimal OnHand, [StringLength(500)] string? Reason);
public sealed record StoreMovementDto(Guid Id, DateTime At, Guid StoreId, string StoreName, Guid MaterialItemId, string ItemCode, string ItemName, string Type, decimal Change, decimal BalanceAfter, decimal? UnitCost,
    string? Reference, string? Serials, string? Reason, Guid? WorkOrderId, string? By);
public sealed record StockValuation(string Currency, decimal Total, IReadOnlyCollection<StoreValue> Stores, IReadOnlyCollection<CategoryValue> Categories);
public sealed record StoreValue(Guid StoreId, string StoreName, string FactoryName, int Lines, decimal Value, decimal CriticalValue);
public sealed record CategoryValue(string Category, int Lines, decimal Value);
public sealed record SparePartSummary(Guid Id, string ItemCode, string ItemName, string? Category, string UnitOfMeasure, decimal? UnitCost, bool IsCriticalSpare, bool OnCriticalAsset,
    bool IsSerialised, bool IsRepairable, bool IsChemical, decimal GroupOnHand, decimal GroupReserved, int StoresHolding);
public sealed record SaveSparePartRequest(
    [StringLength(100)] string ItemCode, [StringLength(200)] string ItemName, [StringLength(50)] string UnitOfMeasure,
    [StringLength(100)] string? Category = null, [StringLength(50)] string? PurchaseUnitOfMeasure = null, decimal? PurchaseUnitFactor = null,
    [StringLength(100)] string? Barcode = null, decimal? UnitCost = null, decimal? ReorderQuantity = null, Guid? PreferredSupplierId = null,
    bool IsCriticalSpare = false, bool IsSerialised = false, bool IsRepairable = false, [StringLength(100)] string? ExternalId = null, [StringLength(50)] string? ExternalSystem = null,
    bool IsChemical = false, [StringLength(50)] string? ChemicalKind = null, [StringLength(200)] string? HazardClass = null, [StringLength(10)] string? FoodGradeClass = null,
    Guid? SdsAttachmentId = null, DateTime? SdsIssuedOn = null);
public sealed record PartStoreStock(Guid StoreId, string StoreName, Guid FactoryId, string FactoryName, string? Bin, decimal OnHand, decimal Reserved, decimal Available, decimal MinLevel, decimal ReorderPoint, decimal? MaxLevel, decimal AverageCost);
public sealed record WhereUsedRow(Guid Id, Guid AssetId, string AssetCode, string AssetName, string? Criticality, string? Location, decimal QuantityFitted, string? Position, string? Notes);
public sealed record SparePartDetail(
    Guid Id, string ItemCode, string ItemName, string? Category, string UnitOfMeasure, string? PurchaseUnitOfMeasure, decimal PurchaseUnitFactor, decimal? UnitCost, string? Barcode,
    Guid? PreferredSupplierId, bool IsCriticalSpare, bool OnCriticalAsset, bool IsSerialised, bool IsRepairable, string? ExternalId, string? ExternalSystem,
    bool IsChemical, string? ChemicalKind, string? HazardClass, string? FoodGradeClass, Guid? SdsAttachmentId, DateTime? SdsIssuedOn,
    IReadOnlyCollection<PartStoreStock> Stores, IReadOnlyCollection<WhereUsedRow> WhereUsed, IReadOnlyCollection<SpareUnitDto> Units, IReadOnlyCollection<CatalogueRow> Prices);
public sealed record AssetPartRow(Guid Id, Guid MaterialItemId, string ItemCode, string ItemName, string UnitOfMeasure, bool IsCritical, decimal QuantityFitted, string? Position, string? Notes,
    decimal AvailableInFactory, decimal AvailableInGroup);
public sealed record SaveAssetPartRequest(Guid MaterialItemId, decimal QuantityFitted = 1, [StringLength(200)] string? Position = null, [StringLength(500)] string? Notes = null);
public sealed record SpareUnitDto(Guid Id, Guid MaterialItemId, string ItemCode, string ItemName, string SerialNumber, string Status, Guid? StoreId, string? StoreName,
    Guid? AssetId, string? AssetCode, string? AssetName, Guid? RepairSupplierId, string? RepairSupplierName, string? RepairReference, DateTime? SentForRepairAt, DateTime? ExpectedBackAt,
    int RepairCount, decimal TotalRepairCost, decimal? UnitCost);
public sealed record SpareUnitEventDto(string Type, DateTime At, string? By, string? WorkOrderNumber, string? AssetCode, string? StoreName, decimal? Cost, string? Note);
public sealed record SendForRepairRequest(Guid SupplierId, [StringLength(100)] string? Reference = null, DateTime? ExpectedBackAt = null);
public sealed record RepairReturnRequest(Guid? StoreId = null, decimal? Cost = null, [StringLength(3)] string? Currency = null, [StringLength(1000)] string? Note = null);
public sealed record ReasonRequest([StringLength(1000)] string? Reason);
public sealed record ReserveRequest(Guid MaterialItemId, decimal Quantity, Guid? StoreId = null);
public sealed record ReturnPartRequest(decimal Quantity, Guid? StoreId = null, [StringLength(100)] string? SerialNumber = null, bool NeedsRepair = false);
public sealed record JobReservation(Guid Id, Guid StoreId, string StoreName, Guid MaterialItemId, string ItemCode, string ItemName, string UnitOfMeasure, decimal Quantity, decimal IssuedQuantity,
    string Status, string? ReservedByName, DateTime? KittedAt, string? KittedByName);
public sealed record JobPartUsed(Guid Id, Guid MaterialItemId, string ItemCode, string ItemName, string UnitOfMeasure, bool IsSerialised, decimal Quantity, decimal? UnitCost, decimal Cost, string? Notes, DateTime UsedAt);
public sealed record JobPlannedPart(Guid MaterialItemId, string Description, decimal Quantity);
public sealed record InstalledUnit(Guid Id, Guid MaterialItemId, string ItemName, string SerialNumber);
public sealed record JobSpares(Guid WorkOrderId, Guid? FactoryId, Guid? DefaultStoreId, string? DefaultStoreName, IReadOnlyCollection<JobReservation> Reservations,
    IReadOnlyCollection<JobPartUsed> Used, IReadOnlyCollection<JobPlannedPart> Planned, decimal PartsCost, IReadOnlyCollection<InstalledUnit> InstalledOnAsset);
public sealed record SpareShortage(Guid WorkOrderId, string WorkOrderNumber, Guid MaterialItemId, string ItemCode, string ItemName, decimal Short, string UnitOfMeasure, bool IsCritical);
public sealed record WindowSparesResult(int LinesReserved, IReadOnlyCollection<SpareShortage> Shortages);
public sealed record StoreTransferDto(Guid Id, string TransferNumber, Guid FromStoreId, string FromStoreName, string FromFactoryName, Guid ToStoreId, string ToStoreName, string ToFactoryName,
    Guid MaterialItemId, string ItemCode, string ItemName, string UnitOfMeasure, decimal Quantity, string? SerialNumbers, string Status, bool IsInterFactory, string? Reason,
    string? RequestedByName, DateTime RequestedAt, DateTime? DispatchedAt, string? DispatchedByName, DateTime? ReceivedAt, string? ReceivedByName, string? RejectReason);
public sealed record TransferRequest(Guid FromStoreId, Guid ToStoreId, Guid MaterialItemId, decimal Quantity, [StringLength(500)] string? Reason = null, Guid? WorkOrderId = null);
public sealed record DispatchTransferRequest(IReadOnlyCollection<string>? SerialNumbers);
public sealed record StoreCountSummary(Guid Id, string CountNumber, Guid StoreId, string StoreName, string Status, bool CriticalOnly, bool IsBlind, int Lines, int Counted, string? StartedByName,
    DateTime StartedAt, DateTime? PostedAt, decimal? VarianceValue);
public sealed record StoreCountLineDto(Guid Id, Guid MaterialItemId, string ItemCode, string ItemName, string UnitOfMeasure, decimal? Expected, decimal? Counted, decimal? Variance, decimal? VarianceValue, string? Notes);
public sealed record StoreCountDetail(StoreCountSummary Count, IReadOnlyCollection<StoreCountLineDto> Lines);
public sealed record OpenCountRequest(Guid StoreId, Guid? BinId = null, bool CriticalOnly = false, bool Blind = true);
public sealed record CountLineRequest(decimal CountedQuantity, [StringLength(500)] string? Notes = null);
public sealed record ReorderRow(Guid StoreId, string StoreName, Guid FactoryId, string FactoryName, Guid MaterialItemId, string ItemCode, string ItemName, string UnitOfMeasure, bool IsCriticalSpare,
    decimal OnHand, decimal Reserved, decimal Available, decimal MinLevel, decimal ReorderPoint, string State, decimal SuggestedQuantity, decimal OnRequisition, decimal OnOrder,
    Guid? SupplierId, string? SupplierName, decimal? UnitPrice, string? Currency, int? LeadTimeDays, Guid? DonorStoreId, string? DonorStoreName, decimal? DonorSpare);
public sealed record MoneyTotal(string Currency, decimal Amount);
public sealed record RequisitionSummary(Guid Id, string RequisitionNumber, Guid FactoryId, string FactoryName, Guid? StoreId, string? StoreName, string Status, string? Reason, DateTime? NeededBy,
    string? RequestedByName, DateTime CreatedAt, string? DecidedByName, DateTime? DecidedAt, int Lines, IReadOnlyCollection<MoneyTotal> Totals);
public sealed record RequisitionLineDto(Guid Id, Guid? MaterialItemId, string? ItemCode, string Description, decimal Quantity, Guid? SupplierId, string? SupplierName, decimal? UnitPrice, string Currency, Guid? WorkOrderId, Guid? PurchaseOrderId);
public sealed record RequisitionPurchaseOrderLine(Guid Id, Guid? MaterialItemId, string ItemName, decimal QuantityOrdered, decimal QuantityReceived, decimal UnitCost);
public sealed record RequisitionPurchaseOrder(Guid Id, string PoNumber, Guid SupplierId, string? SupplierName, string Status, string Currency, decimal Total, IReadOnlyCollection<RequisitionPurchaseOrderLine> Lines);
public sealed record RequisitionDetail(RequisitionSummary Requisition, string? RejectReason, IReadOnlyCollection<RequisitionLineDto> Lines, IReadOnlyCollection<RequisitionPurchaseOrder> PurchaseOrders);
public sealed record SaveRequisitionLine(Guid? MaterialItemId, decimal Quantity, [StringLength(300)] string? Description = null, Guid? SupplierId = null, decimal? UnitPrice = null, [StringLength(3)] string? Currency = null, Guid? WorkOrderId = null);
public sealed record SaveRequisitionRequest(Guid FactoryId, IReadOnlyCollection<SaveRequisitionLine> Lines, Guid? StoreId = null, [StringLength(1000)] string? Reason = null, DateTime? NeededBy = null);
public sealed record ReceivePurchaseOrderLine(Guid LineId, decimal Quantity, IReadOnlyCollection<string>? SerialNumbers = null);
public sealed record ReceivePurchaseOrderRequest(Guid StoreId, IReadOnlyCollection<ReceivePurchaseOrderLine> Lines);
public sealed record CatalogueRow(Guid Id, Guid SupplierId, string SupplierName, Guid MaterialItemId, string PartCode, string PartName, string? VendorPartNumber, decimal UnitPrice, string Currency,
    int? LeadTimeDays, decimal? MinOrderQuantity, DateTime? ValidUntil, bool IsPreferred);
public sealed record SaveCatalogueRequest(Guid SupplierId, Guid MaterialItemId, decimal UnitPrice, [StringLength(3)] string? Currency = null, [StringLength(100)] string? VendorPartNumber = null,
    int? LeadTimeDays = null, decimal? MinOrderQuantity = null, DateTime? ValidUntil = null, bool IsPreferred = false);
public sealed record ChemicalRow(Guid Id, string ItemCode, string ItemName, string UnitOfMeasure, string? Kind, string? HazardClass, string? FoodGradeClass, Guid? SdsAttachmentId, DateTime? SdsIssuedOn,
    string SdsStatus, decimal OnHand, IReadOnlyCollection<string> Stores);
public sealed record LubricantLogRow(DateTime At, string Lubricant, string? FoodGradeClass, decimal Quantity, string UnitOfMeasure, string? WorkOrderNumber, string? AssetCode, string? AssetName, string? By);
