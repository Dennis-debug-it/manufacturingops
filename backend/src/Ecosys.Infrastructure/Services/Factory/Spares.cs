using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public static class StoreMovementTypes
{
    public const string Receipt = "Receipt";
    public const string Issue = "Issue";
    public const string Return = "Return";
    public const string TransferOut = "TransferOut";
    public const string TransferIn = "TransferIn";
    public const string Adjustment = "Adjustment";
    public const string Count = "Count";
    public const string RepairReturn = "RepairReturn";
    public const string Scrap = "Scrap";
}

public sealed record ReserveResult(StockReservation? Reservation, decimal Reserved, decimal Short);

public interface ISparesService
{
    Task<StoreStock> StockAsync(Store store, Guid materialItemId, CancellationToken cancellationToken = default);

    /// <summary>The store a factory issues from by default: its main store, or its only one.</summary>
    Task<Store?> DefaultStoreAsync(Guid tenantId, Guid factoryId, CancellationToken cancellationToken = default);

    /// <summary>Goods in at a unit cost in <paramref name="currency"/>; the store's average cost is re-weighted in the tenant currency.</summary>
    Task<StoreStock> ReceiveAsync(Store store, MaterialItem material, decimal quantity, decimal? unitCost, string? currency, string? reference, IReadOnlyCollection<string>? serials, FactoryActor actor, CancellationToken cancellationToken = default);

    /// <summary>Issues a part to a job, using its reservation first. Returns the usage recorded on the job.</summary>
    Task<WorkOrderMaterialUsage> IssueAsync(Store store, MaterialItem material, decimal quantity, WorkOrder workOrder, string? serialNumber, string? notes, FactoryActor actor, CancellationToken cancellationToken = default);

    /// <summary>Returns unused parts from a job to a store; a serialised unit comes back serviceable or for repair.</summary>
    Task ReturnAsync(WorkOrderMaterialUsage usage, Store store, decimal quantity, string? serialNumber, bool needsRepair, FactoryActor actor, CancellationToken cancellationToken = default);

    Task AdjustAsync(Store store, MaterialItem material, decimal newOnHand, string reason, FactoryActor actor, CancellationToken cancellationToken = default);

    Task<ReserveResult> ReserveAsync(Store store, MaterialItem material, decimal quantity, WorkOrder workOrder, bool allowPartial, FactoryActor actor, CancellationToken cancellationToken = default);

    Task KitAsync(StockReservation reservation, FactoryActor actor, CancellationToken cancellationToken = default);

    Task ReleaseAsync(StockReservation reservation, CancellationToken cancellationToken = default);

    /// <summary>Releases what is still held for a job that is finished or cancelled.</summary>
    Task ReleaseForJobAsync(Guid workOrderId, CancellationToken cancellationToken = default);

    Task<StoreTransfer> RequestTransferAsync(Store from, Store to, MaterialItem material, decimal quantity, string? reason, Guid? workOrderId, FactoryActor actor, CancellationToken cancellationToken = default);
    Task DispatchTransferAsync(StoreTransfer transfer, IReadOnlyCollection<string>? serials, FactoryActor actor, CancellationToken cancellationToken = default);
    Task ReceiveTransferAsync(StoreTransfer transfer, FactoryActor actor, CancellationToken cancellationToken = default);

    Task<StoreCount> OpenCountAsync(Store store, Guid? binId, bool criticalOnly, bool blind, FactoryActor actor, CancellationToken cancellationToken = default);
    Task PostCountAsync(StoreCount count, FactoryActor actor, CancellationToken cancellationToken = default);

    Task SendForRepairAsync(SpareUnit unit, Guid supplierId, string? reference, DateTime? expectedBack, FactoryActor actor, CancellationToken cancellationToken = default);
    Task ReceiveFromRepairAsync(SpareUnit unit, Store store, decimal? cost, string? currency, string? note, FactoryActor actor, CancellationToken cancellationToken = default);
    Task ScrapAsync(SpareUnit unit, string reason, FactoryActor actor, CancellationToken cancellationToken = default);

    /// <summary>Converts an amount to the tenant currency at the latest rate.</summary>
    Task<decimal> ToTenantCurrencyAsync(Guid tenantId, decimal amount, string? currency, CancellationToken cancellationToken = default);
}

internal sealed class SparesService(AppDbContext dbContext, INotificationDispatcher? notificationDispatcher = null) : ISparesService
{
    public const string CriticalLowKey = "spares.critical-low";

    public async Task<StoreStock> StockAsync(Store store, Guid materialItemId, CancellationToken cancellationToken = default)
    {
        var stock = dbContext.StoreStocks.Local.FirstOrDefault(x => x.StoreId == store.Id && x.MaterialItemId == materialItemId)
            ?? await dbContext.StoreStocks.SingleOrDefaultAsync(x => x.StoreId == store.Id && x.MaterialItemId == materialItemId, cancellationToken);
        if (stock is null)
        {
            stock = new StoreStock { TenantId = store.TenantId, StoreId = store.Id, MaterialItemId = materialItemId };
            dbContext.StoreStocks.Add(stock);
        }

        return stock;
    }

    public async Task<Store?> DefaultStoreAsync(Guid tenantId, Guid factoryId, CancellationToken cancellationToken = default) =>
        await dbContext.Stores.Where(x => x.TenantId == tenantId && x.FactoryId == factoryId && x.IsActive)
            .OrderByDescending(x => x.IsMain).ThenBy(x => x.Code).FirstOrDefaultAsync(cancellationToken);

    public async Task<StoreStock> ReceiveAsync(Store store, MaterialItem material, decimal quantity, decimal? unitCost, string? currency, string? reference, IReadOnlyCollection<string>? serials, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        EnsureActive(store);
        if (quantity <= 0) throw new BusinessRuleException("Quantity must be more than zero.");
        var serialList = Serials(serials);
        if (material.IsSerialised)
        {
            if (quantity != decimal.Truncate(quantity)) throw new BusinessRuleException($"{material.ItemName} is serialised: receive whole units.");
            if (serialList.Count != (int)quantity) throw new BusinessRuleException($"Give a serial number for each of the {quantity:0} {material.ItemName} received.");
        }

        var stock = await StockAsync(store, material.Id, cancellationToken);
        var cost = unitCost is { } c ? await ToTenantCurrencyAsync(store.TenantId, c, currency, cancellationToken) : stock.AverageCost > 0 ? stock.AverageCost : material.UnitCost ?? 0;
        // Moving average: (old value + new value) / new quantity.
        var held = Math.Max(stock.OnHand, 0);
        stock.AverageCost = held + quantity > 0 ? Math.Round((held * stock.AverageCost + quantity * cost) / (held + quantity), 4) : cost;
        material.UnitCost = Math.Round(cost, 2);

        if (material.IsSerialised)
        {
            var existing = await dbContext.SpareUnits.Where(x => x.TenantId == store.TenantId && x.MaterialItemId == material.Id && serialList.Contains(x.SerialNumber))
                .Select(x => x.SerialNumber).ToListAsync(cancellationToken);
            if (existing.Count > 0) throw new ConflictException($"Serial number {existing[0]} is already registered for {material.ItemName}.");
            foreach (var serial in serialList)
            {
                var unit = new SpareUnit { TenantId = store.TenantId, MaterialItemId = material.Id, SerialNumber = serial, StoreId = store.Id, UnitCost = Math.Round(cost, 2) };
                dbContext.SpareUnits.Add(unit);
                AddEvent(unit, "Received", actor, storeId: store.Id, note: reference);
            }
        }

        Move(store, material, stock, quantity, StoreMovementTypes.Receipt, cost, reference, actor, batch: serialList.Count > 0 ? string.Join(", ", serialList) : null);
        ClearLowIfRestocked(stock);
        await dbContext.SaveChangesAsync(cancellationToken);
        return stock;
    }

    public async Task<WorkOrderMaterialUsage> IssueAsync(Store store, MaterialItem material, decimal quantity, WorkOrder workOrder, string? serialNumber, string? notes, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        EnsureActive(store);
        if (quantity <= 0) throw new BusinessRuleException("Quantity must be more than zero.");
        var stock = await StockAsync(store, material.Id, cancellationToken);
        var reservations = await dbContext.StockReservations
            .Where(x => x.WorkOrderId == workOrder.Id && x.StoreId == store.Id && x.MaterialItemId == material.Id
                && (x.Status == ReservationStatuses.Reserved || x.Status == ReservationStatuses.Kitted))
            .OrderByDescending(x => x.Status == ReservationStatuses.Kitted).ThenBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        var heldForThisJob = reservations.Sum(x => x.Quantity - x.IssuedQuantity);
        var free = stock.OnHand - stock.Reserved;
        if (quantity > free + heldForThisJob)
        {
            var available = Math.Max(free + heldForThisJob, 0);
            throw new BusinessRuleException($"Only {available:0.##} {material.UnitOfMeasure} of {material.ItemName} can be issued from {store.Name}"
                + (stock.Reserved - heldForThisJob > 0 ? $" ({stock.Reserved - heldForThisJob:0.##} more is reserved for other jobs)." : "."));
        }

        SpareUnit? unit = null;
        if (material.IsSerialised)
        {
            if (quantity != 1) throw new BusinessRuleException($"{material.ItemName} is serialised: issue one unit at a time, by serial number.");
            if (string.IsNullOrWhiteSpace(serialNumber)) throw new BusinessRuleException($"Choose which {material.ItemName} (serial number) is being fitted.");
            unit = await dbContext.SpareUnits.SingleOrDefaultAsync(x => x.TenantId == store.TenantId && x.MaterialItemId == material.Id && x.SerialNumber == serialNumber.Trim(), cancellationToken)
                ?? throw new BusinessRuleException($"No {material.ItemName} with serial number {serialNumber.Trim()}.");
            if (unit.Status != SpareUnitStatuses.InStore || unit.StoreId != store.Id)
                throw new BusinessRuleException($"{material.ItemName} {unit.SerialNumber} is not on the shelf in {store.Name}.");
            unit.Status = SpareUnitStatuses.Installed;
            unit.AssetId = workOrder.AssetId;
            AddEvent(unit, "Installed", actor, workOrder.Id, workOrder.AssetId, store.Id);
        }

        // Use the job's own reservation first; what it held stops being reserved.
        var fromReservation = 0m;
        foreach (var reservation in reservations)
        {
            if (fromReservation >= quantity) break;
            var take = Math.Min(reservation.Quantity - reservation.IssuedQuantity, quantity - fromReservation);
            reservation.IssuedQuantity += take;
            fromReservation += take;
            if (reservation.IssuedQuantity >= reservation.Quantity) reservation.Status = ReservationStatuses.Issued;
        }

        stock.Reserved = Math.Max(stock.Reserved - fromReservation, 0);
        var cost = stock.AverageCost > 0 ? stock.AverageCost : material.UnitCost ?? 0;
        Move(store, material, stock, -quantity, StoreMovementTypes.Issue, cost, workOrder.WorkOrderNumber, actor, workOrder.Id, unit?.SerialNumber);
        var usage = new WorkOrderMaterialUsage
        {
            TenantId = store.TenantId, WorkOrderId = workOrder.Id, MaterialItemId = material.Id, AssetId = workOrder.AssetId,
            QuantityUsed = quantity, UnitCost = Math.Round(cost, 2),
            Notes = Join($"From {store.Name}" + (unit is null ? string.Empty : $", serial {unit.SerialNumber}"), notes),
            UsedByUserId = actor.UserId ?? Guid.Empty, UsedAt = OccurredAt.UtcNow
        };
        dbContext.WorkOrderMaterialUsages.Add(usage);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CheckLowAsync(store, material, stock, cancellationToken);
        return usage;
    }

    public async Task ReturnAsync(WorkOrderMaterialUsage usage, Store store, decimal quantity, string? serialNumber, bool needsRepair, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        EnsureActive(store);
        if (quantity <= 0 || quantity > usage.QuantityUsed) throw new BusinessRuleException($"Return between 0 and {usage.QuantityUsed:0.##}.");
        var material = await dbContext.MaterialItems.SingleAsync(x => x.Id == usage.MaterialItemId, cancellationToken);
        var workOrder = await dbContext.WorkOrders.SingleAsync(x => x.Id == usage.WorkOrderId, cancellationToken);
        var stock = await StockAsync(store, material.Id, cancellationToken);
        usage.QuantityUsed -= quantity;

        if (material.IsSerialised)
        {
            if (string.IsNullOrWhiteSpace(serialNumber)) throw new BusinessRuleException("Say which serial number is coming back.");
            var unit = await dbContext.SpareUnits.SingleOrDefaultAsync(x => x.TenantId == store.TenantId && x.MaterialItemId == material.Id && x.SerialNumber == serialNumber.Trim(), cancellationToken)
                ?? throw new BusinessRuleException($"No {material.ItemName} with serial number {serialNumber.Trim()}.");
            unit.StoreId = store.Id;
            unit.AssetId = null;
            if (needsRepair)
            {
                // Comes off the machine for repair: not usable stock until it is back.
                unit.Status = SpareUnitStatuses.AwaitingRepair;
                AddEvent(unit, "Removed for repair", actor, workOrder.Id, workOrder.AssetId, store.Id);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            unit.Status = SpareUnitStatuses.InStore;
            AddEvent(unit, "Returned", actor, workOrder.Id, workOrder.AssetId, store.Id);
        }
        else if (needsRepair)
        {
            throw new BusinessRuleException($"{material.ItemName} is not tracked by serial number; return it serviceable or scrap it.");
        }

        Move(store, material, stock, quantity, StoreMovementTypes.Return, usage.UnitCost ?? stock.AverageCost, workOrder.WorkOrderNumber, actor, workOrder.Id, serialNumber?.Trim());
        await dbContext.SaveChangesAsync(cancellationToken);
        ClearLowIfRestocked(stock);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AdjustAsync(Store store, MaterialItem material, decimal newOnHand, string reason, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Give a reason for the adjustment.");
        if (newOnHand < 0) throw new BusinessRuleException("Stock can't be negative.");
        if (material.IsSerialised) throw new BusinessRuleException($"{material.ItemName} is serialised: receive, scrap or count units instead of adjusting the total.");
        var stock = await StockAsync(store, material.Id, cancellationToken);
        if (newOnHand < stock.Reserved) throw new BusinessRuleException($"{stock.Reserved:0.##} is reserved for jobs; release it first.");
        var change = newOnHand - stock.OnHand;
        if (change == 0) return;
        Move(store, material, stock, change, StoreMovementTypes.Adjustment, stock.AverageCost, null, actor, reason: reason.Trim());
        await dbContext.SaveChangesAsync(cancellationToken);
        if (change < 0) await CheckLowAsync(store, material, stock, cancellationToken);
        else { ClearLowIfRestocked(stock); await dbContext.SaveChangesAsync(cancellationToken); }
    }

    public async Task<ReserveResult> ReserveAsync(Store store, MaterialItem material, decimal quantity, WorkOrder workOrder, bool allowPartial, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        EnsureActive(store);
        if (quantity <= 0) throw new BusinessRuleException("Quantity must be more than zero.");
        var stock = await StockAsync(store, material.Id, cancellationToken);
        var free = Math.Max(stock.OnHand - stock.Reserved, 0);
        var take = Math.Min(free, quantity);
        if (take < quantity && !allowPartial)
        {
            throw new BusinessRuleException($"Only {free:0.##} {material.UnitOfMeasure} of {material.ItemName} is free in {store.Name}.");
        }

        if (take <= 0)
        {
            return new ReserveResult(null, 0, quantity);
        }

        var reservation = new StockReservation
        {
            TenantId = store.TenantId, StoreId = store.Id, MaterialItemId = material.Id, WorkOrderId = workOrder.Id,
            Quantity = take, ReservedByName = actor.Name
        };
        dbContext.StockReservations.Add(reservation);
        stock.Reserved += take;
        await dbContext.SaveChangesAsync(cancellationToken);
        // Reserving can take the free stock below the reorder point even though nothing left the shelf.
        await CheckLowAsync(store, material, stock, cancellationToken);
        return new ReserveResult(reservation, take, quantity - take);
    }

    public async Task KitAsync(StockReservation reservation, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (reservation.Status != ReservationStatuses.Reserved) throw new BusinessRuleException("Only a reserved line can be kitted.");
        reservation.Status = ReservationStatuses.Kitted;
        reservation.KittedAt = OccurredAt.UtcNow;
        reservation.KittedByName = actor.Name;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(StockReservation reservation, CancellationToken cancellationToken = default)
    {
        if (!ReservationStatuses.IsOpen(reservation.Status)) return;
        var store = await dbContext.Stores.SingleAsync(x => x.Id == reservation.StoreId, cancellationToken);
        var stock = await StockAsync(store, reservation.MaterialItemId, cancellationToken);
        stock.Reserved = Math.Max(stock.Reserved - (reservation.Quantity - reservation.IssuedQuantity), 0);
        reservation.Status = reservation.IssuedQuantity > 0 ? ReservationStatuses.Issued : ReservationStatuses.Released;
        ClearLowIfRestocked(stock);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseForJobAsync(Guid workOrderId, CancellationToken cancellationToken = default)
    {
        var open = await dbContext.StockReservations
            .Where(x => x.WorkOrderId == workOrderId && (x.Status == ReservationStatuses.Reserved || x.Status == ReservationStatuses.Kitted)).ToListAsync(cancellationToken);
        foreach (var reservation in open)
        {
            await ReleaseAsync(reservation, cancellationToken);
        }
    }

    public async Task<StoreTransfer> RequestTransferAsync(Store from, Store to, MaterialItem material, decimal quantity, string? reason, Guid? workOrderId, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (from.Id == to.Id) throw new BusinessRuleException("Choose two different stores.");
        EnsureActive(from);
        EnsureActive(to);
        if (quantity <= 0) throw new BusinessRuleException("Quantity must be more than zero.");
        if (material.IsSerialised && quantity != decimal.Truncate(quantity)) throw new BusinessRuleException($"{material.ItemName} is serialised: move whole units.");
        var count = await dbContext.StoreTransfers.IgnoreQueryFilters().CountAsync(x => x.TenantId == from.TenantId, cancellationToken);
        var transfer = new StoreTransfer
        {
            TenantId = from.TenantId, TransferNumber = $"TRF-{count + 1:000000}", FromStoreId = from.Id, ToStoreId = to.Id, MaterialItemId = material.Id,
            Quantity = quantity, IsInterFactory = from.FactoryId != to.FactoryId, Reason = Clean(reason), WorkOrderId = workOrderId, RequestedByName = actor.Name
        };
        dbContext.StoreTransfers.Add(transfer);
        await dbContext.SaveChangesAsync(cancellationToken);
        return transfer;
    }

    public async Task DispatchTransferAsync(StoreTransfer transfer, IReadOnlyCollection<string>? serials, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (transfer.Status != StoreTransferStatuses.Requested) throw new BusinessRuleException("Only a requested transfer can be dispatched.");
        var from = await dbContext.Stores.SingleAsync(x => x.Id == transfer.FromStoreId, cancellationToken);
        var material = await dbContext.MaterialItems.SingleAsync(x => x.Id == transfer.MaterialItemId, cancellationToken);
        var stock = await StockAsync(from, material.Id, cancellationToken);
        var free = stock.OnHand - stock.Reserved;
        if (transfer.Quantity > free) throw new BusinessRuleException($"Only {Math.Max(free, 0):0.##} {material.UnitOfMeasure} of {material.ItemName} is free in {from.Name}.");

        var serialList = Serials(serials);
        if (material.IsSerialised)
        {
            if (serialList.Count != (int)transfer.Quantity) throw new BusinessRuleException($"Choose the {transfer.Quantity:0} serial number(s) being sent.");
            var units = await dbContext.SpareUnits.Where(x => x.TenantId == transfer.TenantId && x.MaterialItemId == material.Id && serialList.Contains(x.SerialNumber)).ToListAsync(cancellationToken);
            if (units.Count != serialList.Count || units.Any(u => u.Status != SpareUnitStatuses.InStore || u.StoreId != from.Id))
                throw new BusinessRuleException($"Every unit sent must be on the shelf in {from.Name}.");
            foreach (var unit in units)
            {
                unit.StoreId = null; // in transit
                AddEvent(unit, "Dispatched", actor, storeId: from.Id, note: transfer.TransferNumber);
            }

            transfer.SerialNumbers = string.Join(", ", serialList);
        }

        transfer.UnitCost = stock.AverageCost;
        transfer.Status = StoreTransferStatuses.Dispatched;
        transfer.DispatchedAt = OccurredAt.UtcNow;
        transfer.DispatchedByName = actor.Name;
        Move(from, material, stock, -transfer.Quantity, StoreMovementTypes.TransferOut, stock.AverageCost, transfer.TransferNumber, actor, transfer.WorkOrderId, transfer.SerialNumbers);
        await dbContext.SaveChangesAsync(cancellationToken);
        await CheckLowAsync(from, material, stock, cancellationToken);
    }

    public async Task ReceiveTransferAsync(StoreTransfer transfer, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (transfer.Status != StoreTransferStatuses.Dispatched) throw new BusinessRuleException("Only a dispatched transfer can be received.");
        var to = await dbContext.Stores.SingleAsync(x => x.Id == transfer.ToStoreId, cancellationToken);
        var material = await dbContext.MaterialItems.SingleAsync(x => x.Id == transfer.MaterialItemId, cancellationToken);
        var stock = await StockAsync(to, material.Id, cancellationToken);
        var cost = transfer.UnitCost ?? stock.AverageCost;
        var held = Math.Max(stock.OnHand, 0);
        stock.AverageCost = held + transfer.Quantity > 0 ? Math.Round((held * stock.AverageCost + transfer.Quantity * cost) / (held + transfer.Quantity), 4) : cost;

        if (material.IsSerialised && transfer.SerialNumbers is { } list)
        {
            var serialList = Serials(list.Split(','));
            foreach (var unit in await dbContext.SpareUnits.Where(x => x.TenantId == transfer.TenantId && x.MaterialItemId == material.Id && serialList.Contains(x.SerialNumber)).ToListAsync(cancellationToken))
            {
                unit.StoreId = to.Id;
                AddEvent(unit, "Received by transfer", actor, storeId: to.Id, note: transfer.TransferNumber);
            }
        }

        transfer.Status = StoreTransferStatuses.Received;
        transfer.ReceivedAt = OccurredAt.UtcNow;
        transfer.ReceivedByName = actor.Name;
        Move(to, material, stock, transfer.Quantity, StoreMovementTypes.TransferIn, cost, transfer.TransferNumber, actor, transfer.WorkOrderId, transfer.SerialNumbers);
        ClearLowIfRestocked(stock);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StoreCount> OpenCountAsync(Store store, Guid? binId, bool criticalOnly, bool blind, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        EnsureActive(store);
        if (await dbContext.StoreCounts.AnyAsync(x => x.StoreId == store.Id && x.Status == StockCountStatuses.Open, cancellationToken))
            throw new ConflictException($"{store.Name} already has a count open. Post or cancel it first.");
        var stocks = dbContext.StoreStocks.Include(x => x.MaterialItem).Where(x => x.StoreId == store.Id && x.MaterialItem!.IsActive);
        if (binId.HasValue) stocks = stocks.Where(x => x.BinId == binId);
        if (criticalOnly) stocks = stocks.Where(x => x.MaterialItem!.IsCriticalSpare);
        var lines = await stocks.ToListAsync(cancellationToken);
        if (lines.Count == 0) throw new BusinessRuleException("Nothing to count: no parts are stocked there.");

        var number = await dbContext.StoreCounts.IgnoreQueryFilters().CountAsync(x => x.TenantId == store.TenantId, cancellationToken);
        var count = new StoreCount
        {
            TenantId = store.TenantId, CountNumber = $"CNT-{number + 1:000000}", StoreId = store.Id, BinId = binId,
            CriticalOnly = criticalOnly, IsBlind = blind, StartedByName = actor.Name
        };
        foreach (var stock in lines)
        {
            count.Lines.Add(new StoreCountLine { TenantId = store.TenantId, MaterialItemId = stock.MaterialItemId, ExpectedQuantity = stock.OnHand, UnitCost = stock.AverageCost });
        }

        dbContext.StoreCounts.Add(count);
        await dbContext.SaveChangesAsync(cancellationToken);
        return count;
    }

    public async Task PostCountAsync(StoreCount count, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (count.Status != StockCountStatuses.Open) throw new BusinessRuleException("This count is already closed.");
        var store = await dbContext.Stores.SingleAsync(x => x.Id == count.StoreId, cancellationToken);
        var lines = await dbContext.StoreCountLines.Include(x => x.MaterialItem).Where(x => x.StoreCountId == count.Id).ToListAsync(cancellationToken);
        if (lines.All(x => x.CountedQuantity is null)) throw new BusinessRuleException("Nothing has been counted yet.");
        var variance = 0m;
        var now = OccurredAt.UtcNow;
        foreach (var line in lines.Where(x => x.CountedQuantity is not null))
        {
            var stock = await StockAsync(store, line.MaterialItemId, cancellationToken);
            stock.LastCountedAt = now;
            // Adjust by counted − expected so issues made while the count was open are kept.
            var change = line.CountedQuantity!.Value - line.ExpectedQuantity;
            if (change == 0) continue;
            if (stock.OnHand + change < 0) throw new BusinessRuleException($"{line.MaterialItem!.ItemName}: the count would take stock below zero; recount it.");
            variance += change * line.UnitCost;
            Move(store, line.MaterialItem!, stock, change, StoreMovementTypes.Count, line.UnitCost, count.CountNumber, actor, reason: line.Notes);
        }

        count.Status = StockCountStatuses.Posted;
        count.PostedAt = now;
        count.PostedByName = actor.Name;
        count.VarianceValue = Math.Round(variance, 2);
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var line in lines.Where(x => x.CountedQuantity < x.ExpectedQuantity))
        {
            await CheckLowAsync(store, line.MaterialItem!, await StockAsync(store, line.MaterialItemId, cancellationToken), cancellationToken);
        }
    }

    public async Task SendForRepairAsync(SpareUnit unit, Guid supplierId, string? reference, DateTime? expectedBack, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (unit.Status is not (SpareUnitStatuses.AwaitingRepair or SpareUnitStatuses.InStore))
            throw new BusinessRuleException("Only a unit in the store or waiting for repair can go out for repair.");
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(x => x.TenantId == unit.TenantId && x.Id == supplierId, cancellationToken)
            ?? throw new BusinessRuleException("Repairer was not found.");
        var material = await dbContext.MaterialItems.SingleAsync(x => x.Id == unit.MaterialItemId, cancellationToken);
        if (!material.IsRepairable) throw new BusinessRuleException($"{material.ItemName} is not marked repairable.");
        if (unit.Status == SpareUnitStatuses.InStore && unit.StoreId is { } storeId)
        {
            // A serviceable unit leaving the shelf (e.g. for reconditioning) leaves stock too.
            var store = await dbContext.Stores.SingleAsync(x => x.Id == storeId, cancellationToken);
            var stock = await StockAsync(store, material.Id, cancellationToken);
            if (stock.OnHand - stock.Reserved < 1) throw new BusinessRuleException("That unit is reserved for a job.");
            Move(store, material, stock, -1, StoreMovementTypes.TransferOut, stock.AverageCost, reference, actor, batch: unit.SerialNumber, reason: $"Sent for repair to {supplier.SupplierName}");
        }

        unit.Status = SpareUnitStatuses.AtRepair;
        unit.RepairSupplierId = supplier.Id;
        unit.RepairReference = Clean(reference);
        unit.SentForRepairAt = OccurredAt.UtcNow;
        unit.ExpectedBackAt = expectedBack;
        AddEvent(unit, "Sent for repair", actor, storeId: unit.StoreId, note: $"{supplier.SupplierName}{(reference is null ? "" : $" ({reference})")}");
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReceiveFromRepairAsync(SpareUnit unit, Store store, decimal? cost, string? currency, string? note, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (unit.Status != SpareUnitStatuses.AtRepair) throw new BusinessRuleException("This unit is not out for repair.");
        EnsureActive(store);
        var material = await dbContext.MaterialItems.SingleAsync(x => x.Id == unit.MaterialItemId, cancellationToken);
        var repairCost = cost is { } c ? await ToTenantCurrencyAsync(unit.TenantId, c, currency, cancellationToken) : 0;
        unit.Status = SpareUnitStatuses.InStore;
        unit.StoreId = store.Id;
        unit.RepairCount++;
        unit.TotalRepairCost += Math.Round(repairCost, 2);
        AddEvent(unit, "Back from repair", actor, storeId: store.Id, cost: repairCost, note: Clean(note));
        var stock = await StockAsync(store, material.Id, cancellationToken);
        // A repaired unit comes back at its repair cost; the average carries both new and reconditioned units.
        var held = Math.Max(stock.OnHand, 0);
        stock.AverageCost = Math.Round((held * stock.AverageCost + repairCost) / (held + 1), 4);
        Move(store, material, stock, 1, StoreMovementTypes.RepairReturn, repairCost, unit.RepairReference, actor, batch: unit.SerialNumber);
        ClearLowIfRestocked(stock);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ScrapAsync(SpareUnit unit, string reason, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BusinessRuleException("Say why it is scrapped.");
        if (unit.Status is SpareUnitStatuses.Scrapped or SpareUnitStatuses.Installed) throw new BusinessRuleException("Remove the unit from the machine before scrapping it.");
        if (unit.Status == SpareUnitStatuses.InStore && unit.StoreId is { } storeId)
        {
            var store = await dbContext.Stores.SingleAsync(x => x.Id == storeId, cancellationToken);
            var material = await dbContext.MaterialItems.SingleAsync(x => x.Id == unit.MaterialItemId, cancellationToken);
            var stock = await StockAsync(store, material.Id, cancellationToken);
            Move(store, material, stock, -1, StoreMovementTypes.Scrap, stock.AverageCost, null, actor, batch: unit.SerialNumber, reason: reason.Trim());
            await dbContext.SaveChangesAsync(cancellationToken);
            await CheckLowAsync(store, material, stock, cancellationToken);
        }

        unit.Status = SpareUnitStatuses.Scrapped;
        AddEvent(unit, "Scrapped", actor, storeId: unit.StoreId, note: reason.Trim());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<decimal> ToTenantCurrencyAsync(Guid tenantId, decimal amount, string? currency, CancellationToken cancellationToken = default)
    {
        var tenantCurrency = await dbContext.Tenants.Where(x => x.Id == tenantId).Select(x => x.Currency).SingleOrDefaultAsync(cancellationToken) ?? "KES";
        var from = string.IsNullOrWhiteSpace(currency) ? tenantCurrency : currency.Trim().ToUpperInvariant();
        if (string.Equals(from, tenantCurrency, StringComparison.OrdinalIgnoreCase)) return amount;
        var rates = dbContext.ExchangeRates.Where(x => x.TenantId == tenantId && x.EffectiveFrom <= DateTime.UtcNow);
        var direct = await rates.Where(x => x.BaseCurrency == from && x.QuoteCurrency == tenantCurrency).OrderByDescending(x => x.EffectiveFrom).Select(x => (decimal?)x.Rate).FirstOrDefaultAsync(cancellationToken);
        if (direct is { } d) return Math.Round(amount * d, 4);
        var inverse = await rates.Where(x => x.BaseCurrency == tenantCurrency && x.QuoteCurrency == from).OrderByDescending(x => x.EffectiveFrom).Select(x => (decimal?)x.Rate).FirstOrDefaultAsync(cancellationToken);
        if (inverse is { } i && i != 0) return Math.Round(amount / i, 4);
        throw new BusinessRuleException($"There is no {from} to {tenantCurrency} exchange rate. Add one under settings first.");
    }

    /// <summary>Tells stores and the factory's supervisors when a critical spare falls to its reorder point or runs out (once per dip).</summary>
    private async Task CheckLowAsync(Store store, MaterialItem material, StoreStock stock, CancellationToken cancellationToken)
    {
        var free = stock.OnHand - stock.Reserved;
        if (!material.IsCriticalSpare || stock.LowAlertSentAt is not null) return;
        if (free > stock.ReorderPoint && free > stock.MinLevel && free > 0) return;
        stock.LowAlertSentAt = OccurredAt.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (notificationDispatcher is null) return;

        var recipients = new HashSet<Guid>(await FactoryRecipients.SupervisorsAsync(dbContext, store.TenantId, store.FactoryId, cancellationToken));
        var storeRoles = await dbContext.RolePermissions.AsNoTracking()
            .Where(x => x.TenantId == store.TenantId && (x.CanManageMaterials || x.CanIssueMaterials)).Select(x => x.Role).ToListAsync(cancellationToken);
        foreach (var id in await dbContext.Users.AsNoTracking().Where(x => x.TenantId == store.TenantId && x.IsActive && storeRoles.Contains(x.Role)).Select(x => x.Id).ToListAsync(cancellationToken))
        {
            recipients.Add(id);
        }

        if (recipients.Count == 0) return;
        var outOfStock = free <= 0;
        var state = outOfStock ? "out of stock" : $"down to {free:0.##} {material.UnitOfMeasure}";
        await notificationDispatcher.NotifyAsync(new NotificationRequest(
            store.TenantId, CriticalLowKey, recipients.ToList(),
            $"Critical spare {state}: {material.ItemName}",
            $"{material.ItemName} ({material.ItemCode}) in {store.Name} is {state} (reorder at {stock.ReorderPoint:0.##}, minimum {stock.MinLevel:0.##}).",
            "/maintenance/stores?tab=reorder", nameof(StoreStock), stock.Id,
            new Dictionary<string, string?>
            {
                ["PartName"] = material.ItemName, ["PartCode"] = material.ItemCode, ["Store"] = store.Name, ["State"] = state,
                ["ReorderPoint"] = stock.ReorderPoint.ToString("0.##"), ["MinLevel"] = stock.MinLevel.ToString("0.##")
            },
            outOfStock ? NotificationSeverities.Critical : NotificationSeverities.Warning), cancellationToken);
    }

    private static void ClearLowIfRestocked(StoreStock stock)
    {
        if (stock.LowAlertSentAt is not null && stock.OnHand - stock.Reserved > stock.ReorderPoint && stock.OnHand - stock.Reserved > stock.MinLevel)
        {
            stock.LowAlertSentAt = null;
        }
    }

    private void Move(Store store, MaterialItem material, StoreStock stock, decimal change, string type, decimal unitCost, string? reference, FactoryActor actor,
        Guid? workOrderId = null, string? batch = null, string? reason = null)
    {
        stock.OnHand += change;
        material.QuantityOnHand += change;
        dbContext.StockMovements.Add(new StockMovement
        {
            TenantId = store.TenantId, StoreId = store.Id, MaterialId = material.Id, WorkOrderId = workOrderId,
            MovementType = type, Quantity = Math.Abs(change), QuantityChange = change, BalanceAfter = stock.OnHand,
            UnitCost = Math.Round(unitCost, 4), ReferenceNumber = reference, BatchOrSerial = batch, Reason = reason,
            CreatedByUserId = actor.UserId
        });
    }

    private void AddEvent(SpareUnit unit, string type, FactoryActor actor, Guid? workOrderId = null, Guid? assetId = null, Guid? storeId = null, decimal? cost = null, string? note = null)
    {
        var item = new SpareUnitEvent
        {
            TenantId = unit.TenantId, SpareUnitId = unit.Id, EventType = type, OccurredAt = OccurredAt.UtcNow, ByName = actor.Name,
            WorkOrderId = workOrderId, AssetId = assetId, StoreId = storeId, Cost = cost, Note = note
        };
        dbContext.SpareUnitEvents.Add(item);
    }

    private static void EnsureActive(Store store)
    {
        if (!store.IsActive) throw new BusinessRuleException($"{store.Name} is closed.");
    }

    private static List<string> Serials(IEnumerable<string>? serials) =>
        (serials ?? []).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Join(string first, string? second) => string.IsNullOrWhiteSpace(second) ? first : $"{first}. {second.Trim()}";
}
