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
[Route("api/work-orders/{workOrderId:guid}")]
public sealed class WorkOrderBillingController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    // ── Billable Items ───────────────────────────────────────────────────────

    [HttpGet("billable-items")]
    public async Task<ActionResult<IReadOnlyCollection<BillableItemResponse>>> ListBillableItems(
        Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceViewBillableWork);
        await EnsureWorkOrderAccessAsync(workOrderId, cancellationToken);
        var items = await dbContext.WorkOrderBillableItems
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(MapItem).ToList());
    }

    [HttpPost("billable-items")]
    public async Task<ActionResult<BillableItemResponse>> AddBillableItem(
        Guid workOrderId, [FromBody] UpsertBillableItemRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        await EnsureWorkOrderAccessAsync(workOrderId, cancellationToken);

        var lineTotal = request.Quantity * request.UnitRate;
        var taxAmount = lineTotal * (request.TaxRate / 100m);
        var item = new WorkOrderBillableItem
        {
            TenantId = TenantId,
            WorkOrderId = workOrderId,
            ItemType = NormalizeItemType(request.ItemType),
            Description = request.Description.Trim(),
            Quantity = request.Quantity,
            Unit = request.Unit.Trim(),
            UnitRate = request.UnitRate,
            RateCardId = request.RateCardId,
            LineTotal = lineTotal,
            TaxRate = request.TaxRate,
            TaxAmount = taxAmount,
            GrandTotal = lineTotal + taxAmount,
            IsManualEntry = true,
            Notes = request.Notes?.Trim(),
        };
        dbContext.WorkOrderBillableItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(ListBillableItems), new { workOrderId }, MapItem(item));
    }

    [HttpPut("billable-items/{itemId:guid}")]
    public async Task<ActionResult<BillableItemResponse>> UpdateBillableItem(
        Guid workOrderId, Guid itemId, [FromBody] UpsertBillableItemRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var item = await dbContext.WorkOrderBillableItems
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.Id == itemId, cancellationToken)
            ?? throw new NotFoundException("Billable item was not found.");

        var lineTotal = request.Quantity * request.UnitRate;
        var taxAmount = lineTotal * (request.TaxRate / 100m);
        item.ItemType = NormalizeItemType(request.ItemType);
        item.Description = request.Description.Trim();
        item.Quantity = request.Quantity;
        item.Unit = request.Unit.Trim();
        item.UnitRate = request.UnitRate;
        item.RateCardId = request.RateCardId;
        item.LineTotal = lineTotal;
        item.TaxRate = request.TaxRate;
        item.TaxAmount = taxAmount;
        item.GrandTotal = lineTotal + taxAmount;
        item.Notes = request.Notes?.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapItem(item));
    }

    [HttpDelete("billable-items/{itemId:guid}")]
    public async Task<IActionResult> DeleteBillableItem(
        Guid workOrderId, Guid itemId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var item = await dbContext.WorkOrderBillableItems
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.Id == itemId, cancellationToken)
            ?? throw new NotFoundException("Billable item was not found.");
        dbContext.WorkOrderBillableItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("billable-items/auto-populate")]
    public async Task<ActionResult<IReadOnlyCollection<BillableItemResponse>>> AutoPopulateBillableItems(
        Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var workOrder = await EnsureWorkOrderAccessAsync(workOrderId, cancellationToken);

        // Load existing items to detect already-tracked sources and avoid double-billing.
        var existingItems = await dbContext.WorkOrderBillableItems
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .ToListAsync(cancellationToken);

        var calloutAlreadyPresent = existingItems.Any(x =>
            string.Equals(x.ItemType, "Callout", StringComparison.OrdinalIgnoreCase));

        var alreadyTrackedMaterialIds = existingItems
            .Where(x => x.SourceMaterialUsageId.HasValue)
            .Select(x => x.SourceMaterialUsageId!.Value)
            .ToHashSet();

        var alreadyTrackedTimeLogIds = existingItems
            .Where(x => x.SourceTimeLogId.HasValue)
            .Select(x => x.SourceTimeLogId!.Value)
            .ToHashSet();

        // Load tenant default tax rate
        var defaultTaxRate = await GetDefaultTaxRateAsync(cancellationToken);

        var generated = new List<WorkOrderBillableItem>();

        // 1. Material usages → Material items (skip any already tracked by a billable item)
        var materialUsages = await dbContext.WorkOrderMaterialUsages
            .Include(x => x.MaterialItem)
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.Chargeable)
            .ToListAsync(cancellationToken);

        foreach (var usage in materialUsages)
        {
            if (alreadyTrackedMaterialIds.Contains(usage.Id))
                continue;

            var unitRate = usage.UnitCost ?? usage.MaterialItem?.UnitCost ?? 0;
            var lineTotal = usage.QuantityUsed * unitRate;
            var taxAmount = lineTotal * (defaultTaxRate / 100m);
            generated.Add(new WorkOrderBillableItem
            {
                TenantId = TenantId,
                WorkOrderId = workOrderId,
                ItemType = "Material",
                Description = usage.MaterialItem?.ItemName ?? "Material",
                Quantity = usage.QuantityUsed,
                Unit = usage.MaterialItem?.UnitOfMeasure ?? "unit",
                UnitRate = unitRate,
                LineTotal = lineTotal,
                TaxRate = defaultTaxRate,
                TaxAmount = taxAmount,
                GrandTotal = lineTotal + taxAmount,
                IsManualEntry = false,
                Notes = usage.Notes,
                SourceMaterialUsageId = usage.Id,
            });
        }

        // 2. Time logs → Labour items (skip already-tracked and already-billed logs)
        var timeLogs = await dbContext.WorkOrderTimeLogs
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && !x.IsBilled)
            .ToListAsync(cancellationToken);
        timeLogs = timeLogs.Where(x => !alreadyTrackedTimeLogIds.Contains(x.Id)).ToList();

        if (timeLogs.Count > 0)
        {
            // Resolve labour rate card
            var labourCard = await dbContext.RateCards
                .Where(x => x.TenantId == TenantId && x.IsActive && x.Category == "Labour" && x.TimeType == "Standard")
                .OrderBy(x => x.IsDefault ? 0 : 1)
                .ThenBy(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);

            var labourRate = labourCard?.UnitRate ?? 0m;
            var labourTaxRate = labourCard?.TaxRate ?? defaultTaxRate;

            foreach (var log in timeLogs)
            {
                var hours = Math.Round(log.DurationMinutes / 60m, 2);
                var lineTotal = hours * labourRate;
                var taxAmount = lineTotal * (labourTaxRate / 100m);
                generated.Add(new WorkOrderBillableItem
                {
                    TenantId = TenantId,
                    WorkOrderId = workOrderId,
                    ItemType = "Labour",
                    Description = string.IsNullOrWhiteSpace(log.TechnicianName)
                        ? "Labour — technician time"
                        : $"Labour — {log.TechnicianName}",
                    Quantity = hours,
                    Unit = "hour",
                    UnitRate = labourRate,
                    RateCardId = labourCard?.Id,
                    LineTotal = lineTotal,
                    TaxRate = labourTaxRate,
                    TaxAmount = taxAmount,
                    GrandTotal = lineTotal + taxAmount,
                    IsManualEntry = false,
                    Notes = log.Notes,
                    SourceTimeLogId = log.Id,
                });
            }
        }

        // 3. Callout fee — add once if not already present
        if (!calloutAlreadyPresent)
        {
            var calloutCard = await dbContext.RateCards
                .Where(x => x.TenantId == TenantId && x.IsActive && x.Category == "Callout")
                .OrderBy(x => x.IsDefault ? 0 : 1)
                .ThenBy(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);

            if (calloutCard is not null)
            {
                var calloutTaxRate = calloutCard.TaxRate ?? defaultTaxRate;
                var taxAmount = calloutCard.UnitRate * (calloutTaxRate / 100m);
                generated.Add(new WorkOrderBillableItem
                {
                    TenantId = TenantId,
                    WorkOrderId = workOrderId,
                    ItemType = "Callout",
                    Description = calloutCard.Name,
                    Quantity = 1,
                    Unit = "visit",
                    UnitRate = calloutCard.UnitRate,
                    RateCardId = calloutCard.Id,
                    LineTotal = calloutCard.UnitRate,
                    TaxRate = calloutTaxRate,
                    TaxAmount = taxAmount,
                    GrandTotal = calloutCard.UnitRate + taxAmount,
                    IsManualEntry = false,
                });
            }
        }

        // Return for user review — do NOT save
        return Ok(generated.Select(MapItem).ToList());
    }

    // ── Time Logs ────────────────────────────────────────────────────────────

    [HttpGet("time-logs")]
    public async Task<ActionResult<IReadOnlyCollection<TimeLogResponse>>> ListTimeLogs(
        Guid workOrderId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceViewBillableWork);
        await EnsureWorkOrderAccessAsync(workOrderId, cancellationToken);
        var logs = await dbContext.WorkOrderTimeLogs
            .Where(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId)
            .OrderBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);
        return Ok(logs.Select(MapLog).ToList());
    }

    [HttpPost("time-logs")]
    public async Task<ActionResult<TimeLogResponse>> AddTimeLog(
        Guid workOrderId, [FromBody] UpsertTimeLogRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        await EnsureWorkOrderAccessAsync(workOrderId, cancellationToken);

        var startedAt = DateTime.SpecifyKind(request.StartedAt, DateTimeKind.Utc);
        var endedAt = request.EndedAt.HasValue ? DateTime.SpecifyKind(request.EndedAt.Value, DateTimeKind.Utc) : (DateTime?)null;
        var durationMinutes = endedAt.HasValue
            ? (int)(endedAt.Value - startedAt).TotalMinutes
            : request.DurationMinutes;

        string? techName = null;
        if (request.TechnicianId.HasValue)
        {
            var tech = await dbContext.Technicians
                .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.TechnicianId.Value, cancellationToken);
            techName = tech?.FullName;
        }

        var log = new WorkOrderTimeLog
        {
            TenantId = TenantId,
            WorkOrderId = workOrderId,
            TechnicianId = request.TechnicianId,
            TechnicianName = techName ?? request.TechnicianName,
            StartedAt = startedAt,
            EndedAt = endedAt,
            DurationMinutes = Math.Max(0, durationMinutes),
            Notes = request.Notes?.Trim(),
        };
        dbContext.WorkOrderTimeLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(ListTimeLogs), new { workOrderId }, MapLog(log));
    }

    [HttpPut("time-logs/{logId:guid}")]
    public async Task<ActionResult<TimeLogResponse>> UpdateTimeLog(
        Guid workOrderId, Guid logId, [FromBody] UpsertTimeLogRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var log = await dbContext.WorkOrderTimeLogs
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.Id == logId, cancellationToken)
            ?? throw new NotFoundException("Time log was not found.");

        var startedAt = DateTime.SpecifyKind(request.StartedAt, DateTimeKind.Utc);
        var endedAt = request.EndedAt.HasValue ? DateTime.SpecifyKind(request.EndedAt.Value, DateTimeKind.Utc) : (DateTime?)null;
        var durationMinutes = endedAt.HasValue
            ? (int)(endedAt.Value - startedAt).TotalMinutes
            : request.DurationMinutes;

        log.TechnicianId = request.TechnicianId;
        log.TechnicianName = request.TechnicianName;
        log.StartedAt = startedAt;
        log.EndedAt = endedAt;
        log.DurationMinutes = Math.Max(0, durationMinutes);
        log.Notes = request.Notes?.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapLog(log));
    }

    [HttpDelete("time-logs/{logId:guid}")]
    public async Task<IActionResult> DeleteTimeLog(
        Guid workOrderId, Guid logId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var log = await dbContext.WorkOrderTimeLogs
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.WorkOrderId == workOrderId && x.Id == logId, cancellationToken)
            ?? throw new NotFoundException("Time log was not found.");
        dbContext.WorkOrderTimeLogs.Remove(log);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Billing Status ───────────────────────────────────────────────────────

    [HttpPut("billing-status")]
    public async Task<IActionResult> UpdateBillingStatus(
        Guid workOrderId, [FromBody] UpdateBillingStatusRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageBillableWork);
        var workOrder = await EnsureWorkOrderAccessAsync(workOrderId, cancellationToken);
        workOrder.BillingStatus = NormalizeBillingStatus(request.BillingStatus);
        workOrder.IsBillable = request.IsBillable;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<WorkOrder> EnsureWorkOrderAccessAsync(Guid workOrderId, CancellationToken cancellationToken)
    {
        var workOrder = await dbContext.WorkOrders
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");
        return workOrder;
    }

    // Default 16% Kenya VAT — future: read from tenant settings table
    private static Task<decimal> GetDefaultTaxRateAsync(CancellationToken _) =>
        Task.FromResult(16m);

    private static BillableItemResponse MapItem(WorkOrderBillableItem item) =>
        new(item.Id, item.WorkOrderId, item.ItemType, item.Description, item.Quantity, item.Unit,
            item.UnitRate, item.RateCardId, item.LineTotal, item.TaxRate, item.TaxAmount,
            item.GrandTotal, item.IsManualEntry, item.IsBilled, item.Notes, item.CreatedAt);

    private static TimeLogResponse MapLog(WorkOrderTimeLog log) =>
        new(log.Id, log.WorkOrderId, log.TechnicianId, log.TechnicianName, log.StartedAt,
            log.EndedAt, log.DurationMinutes, log.Notes, log.IsBilled, log.CreatedAt);

    private static string NormalizeItemType(string value) => value.Trim() switch
    {
        "Labour" or "Material" or "Callout" or "Service" or "Transport" or "Other" => value.Trim(),
        _ => throw new BusinessRuleException("ItemType must be Labour, Material, Callout, Service, Transport, or Other.")
    };

    private static string NormalizeBillingStatus(string value) => value.Trim() switch
    {
        "Unbilled" or "PartiallyBilled" or "Billed" or "NotBillable" => value.Trim(),
        _ => throw new BusinessRuleException("BillingStatus must be Unbilled, PartiallyBilled, Billed, or NotBillable.")
    };
}

public sealed record UpsertBillableItemRequest(
    string ItemType,
    string Description,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    Guid? RateCardId,
    decimal TaxRate,
    string? Notes);

public sealed record BillableItemResponse(
    Guid Id,
    Guid WorkOrderId,
    string ItemType,
    string Description,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    Guid? RateCardId,
    decimal LineTotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal GrandTotal,
    bool IsManualEntry,
    bool IsBilled,
    string? Notes,
    DateTime CreatedAt);

public sealed record UpsertTimeLogRequest(
    Guid? TechnicianId,
    string? TechnicianName,
    DateTime StartedAt,
    DateTime? EndedAt,
    int DurationMinutes,
    string? Notes);

public sealed record TimeLogResponse(
    Guid Id,
    Guid WorkOrderId,
    Guid? TechnicianId,
    string? TechnicianName,
    DateTime StartedAt,
    DateTime? EndedAt,
    int DurationMinutes,
    string? Notes,
    bool IsBilled,
    DateTime CreatedAt);

public sealed record UpdateBillingStatusRequest(
    string BillingStatus,
    bool IsBillable);
