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
/// Stocktakes: open a count for a branch or a van (the books' balances are snapshotted), record what
/// is on the shelf, then post it to adjust each counted part by counted − expected.
/// </summary>
[ApiController]
[Authorize]
[Route("api/stock-counts")]
public sealed class StockCountsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IStockLedgerService stockLedgerService,
    IDocumentNumberingService documentNumberingService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<StockCountSummaryResponse>>> List([FromQuery] string? status, CancellationToken cancellationToken)
    {
        EnsureViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var query = dbContext.StockCounts.AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .WhereAccessible(scope, x => x.BranchId);
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == status.Trim());

        var counts = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .Select(x => new StockCountSummaryResponse(
                x.Id,
                x.CountNumber,
                x.BranchId,
                x.Branch != null ? x.Branch.Name : null,
                x.Category,
                x.Status,
                x.Lines.Count,
                x.Lines.Count(l => l.CountedQuantity != null),
                x.Lines.Count(l => l.CountedQuantity != null && l.CountedQuantity != l.ExpectedQuantity),
                x.StartedByUser != null ? x.StartedByUser.FullName : null,
                x.CreatedAt,
                x.PostedAt)
            {
                VanId = x.VanId,
                LocationName = x.Van != null ? x.Van.Name : x.Branch != null ? x.Branch.Name : null,
                IsBlind = x.IsBlind,
                RecountLines = x.Lines.Count(l => l.NeedsRecount),
            })
            .ToListAsync(cancellationToken);

        // A blind count's variances are not shown to counters while it is open; the count is too.
        if (!CanSeeExpected())
        {
            counts = counts.Select(x => x.IsBlind && x.Status == StockCountStatuses.Open ? x with { VarianceLines = 0 } : x).ToList();
        }

        return Ok(counts);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockCountDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        EnsureViewAccess();
        return Ok(Map(await LoadAsync(id, cancellationToken)));
    }

    [HttpPost]
    public async Task<ActionResult<StockCountDetailResponse>> Create([FromBody] CreateStockCountRequest request, CancellationToken cancellationToken)
    {
        EnsureCountAccess();
        Guid? branchId;
        Van? van = null;
        if (request.VanId is Guid vanId)
        {
            van = await dbContext.Vans.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == vanId && x.IsActive, cancellationToken)
                ?? throw new BusinessRuleException("Van was not found.");
            if (van.HomeBranchId is Guid home)
                await branchAccessService.EnsureCanAccessBranchAsync(TenantId, home, cancellationToken);
            // A van count is filed under the van's home branch, so branch access applies to it.
            branchId = van.HomeBranchId;
            if (await dbContext.StockCounts.AnyAsync(x => x.TenantId == TenantId && x.VanId == vanId && x.Status == StockCountStatuses.Open, cancellationToken))
                throw new BusinessRuleException($"{van.Name} already has an open stock count. Post or cancel it first.");
        }
        else
        {
            branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId, cancellationToken);
            if (await dbContext.StockCounts.AnyAsync(x => x.TenantId == TenantId && x.VanId == null && x.BranchId == branchId && x.Status == StockCountStatuses.Open, cancellationToken))
                throw new BusinessRuleException("This branch already has an open stock count. Post or cancel it first.");
        }

        var category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        var parts = await dbContext.MaterialItems.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.IsActive && (category == null || x.Category == category))
            .OrderBy(x => x.ItemName)
            .Select(x => new { x.Id, x.QuantityOnHand, x.ReorderLevel, x.UnitCost })
            .ToListAsync(cancellationToken);
        if (parts.Count == 0)
            throw new BusinessRuleException(category is null ? "There are no active parts to count." : $"There are no active parts in {category}.");

        var balances = await BalancesAtAsync(van?.Id, branchId, cancellationToken);
        var count = new StockCount
        {
            TenantId = TenantId,
            CountNumber = await documentNumberingService.GenerateAsync(TenantId, branchId, DocumentTypes.StockCount, cancellationToken),
            BranchId = branchId,
            VanId = van?.Id,
            IsBlind = request.Blind,
            Category = category,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            StartedByUserId = UserId,
        };
        foreach (var part in parts)
        {
            var (quantity, reorderLevel, unitCost) = balances is null
                ? (part.QuantityOnHand, part.ReorderLevel, part.UnitCost)
                : balances.TryGetValue(part.Id, out var held) ? (held.Quantity, held.ReorderLevel, held.UnitCost ?? part.UnitCost) : (0m, 0m, part.UnitCost);
            // "Only parts held here" skips what was never stocked at this location, so a branch or van
            // count is not padded out with the whole catalogue. Add a part found on the shelf with AddLine.
            if (request.OnlyStocked && quantity == 0 && reorderLevel <= 0)
                continue;
            count.Lines.Add(new StockCountLine
            {
                TenantId = TenantId,
                MaterialItemId = part.Id,
                ExpectedQuantity = quantity,
                UnitCost = unitCost,
            });
        }
        if (count.Lines.Count == 0)
            throw new BusinessRuleException("No parts are held at this location. Untick \"only parts held here\" to count the full catalogue.");

        dbContext.StockCounts.Add(count);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.stock_count.opened", nameof(StockCount), count.Id.ToString(),
            $"Opened stock count {count.CountNumber}{(van is null ? string.Empty : $" of van {van.Name}")} ({count.Lines.Count} parts{(count.IsBlind ? ", blind" : string.Empty)}).", cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = count.Id }, Map(await LoadAsync(count.Id, cancellationToken)));
    }

    /// <summary>Adds a part found on the shelf that the count did not list, at its current book balance.</summary>
    [HttpPost("{id:guid}/lines")]
    public async Task<ActionResult<StockCountDetailResponse>> AddLine(Guid id, [FromBody] AddStockCountLineRequest request, CancellationToken cancellationToken)
    {
        EnsureCountAccess();
        var count = await LoadAsync(id, cancellationToken);
        EnsureOpen(count);
        if (count.Lines.Any(x => x.MaterialItemId == request.MaterialItemId))
            throw new BusinessRuleException("That part is already on this count.");
        var part = await dbContext.MaterialItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.MaterialItemId && x.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Part was not found.");

        var balances = await BalancesAtAsync(count.VanId, count.BranchId, cancellationToken);
        var (quantity, unitCost) = balances is null
            ? (part.QuantityOnHand, part.UnitCost)
            : balances.TryGetValue(part.Id, out var held) ? (held.Quantity, held.UnitCost ?? part.UnitCost) : (0m, part.UnitCost);
        var line = new StockCountLine
        {
            TenantId = TenantId,
            StockCountId = count.Id,
            MaterialItemId = part.Id,
            ExpectedQuantity = quantity,
            UnitCost = unitCost,
        };
        if (request.CountedQuantity is { } counted)
        {
            if (counted < 0) throw new BusinessRuleException("A count cannot be negative.");
            line.CountedQuantity = counted;
            line.CountedByUserId = UserId;
            line.CountedAt = DateTime.UtcNow;
        }
        dbContext.StockCountLines.Add(line);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(await LoadAsync(count.Id, cancellationToken)));
    }

    /// <summary>
    /// Records counted quantities; a null quantity clears a line back to uncounted. A line can be
    /// marked for a recount, and changing its count clears the mark unless it is set again.
    /// </summary>
    [HttpPut("{id:guid}/lines")]
    public async Task<ActionResult<StockCountDetailResponse>> SaveLines(Guid id, [FromBody] SaveStockCountLinesRequest request, CancellationToken cancellationToken)
    {
        EnsureCountAccess();
        var count = await LoadAsync(id, cancellationToken);
        EnsureOpen(count);

        foreach (var entry in request.Lines)
        {
            var line = count.Lines.SingleOrDefault(x => x.Id == entry.LineId)
                ?? throw new BusinessRuleException("A line does not belong to this stock count.");
            if (entry.CountedQuantity is < 0)
                throw new BusinessRuleException($"The count for {line.MaterialItem?.ItemName ?? "a part"} cannot be negative.");

            if (line.CountedQuantity != entry.CountedQuantity)
            {
                line.CountedQuantity = entry.CountedQuantity;
                line.CountedByUserId = entry.CountedQuantity is null ? null : UserId;
                line.CountedAt = entry.CountedQuantity is null ? null : DateTime.UtcNow;
                line.NeedsRecount = false;
            }
            if (entry.NeedsRecount is bool recount)
                line.NeedsRecount = recount;
            line.Notes = string.IsNullOrWhiteSpace(entry.Notes) ? null : entry.Notes.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(count));
    }

    /// <summary>
    /// Books every counted variance as an adjustment against the snapshot (counted − expected), in one
    /// transaction. Uncounted lines are left untouched. Lines marked for recount block posting, and a
    /// count whose variances are worth more than Settings → Inventory "Require Approval Above Value"
    /// can only be posted by someone who approves materials.
    /// </summary>
    [HttpPost("{id:guid}/post")]
    public async Task<ActionResult<StockCountDetailResponse>> Post(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials, PermissionNames.CanApproveMaterials);
        var approvalLimit = (await InventoryModuleSettings.LoadAsync(dbContext, TenantId, cancellationToken)).RequireApprovalAboveValue;
        StockCount count = null!;
        var adjusted = 0;
        await stockLedgerService.ExecuteAsync(async ct =>
        {
            count = await LoadAsync(id, ct);
            EnsureOpen(count);
            if (count.Lines.All(x => x.CountedQuantity is null))
                throw new BusinessRuleException("Nothing has been counted yet.");
            var recount = count.Lines.Count(x => x.NeedsRecount);
            if (recount > 0)
                throw new BusinessRuleException($"{recount} line{(recount == 1 ? " is" : "s are")} marked for recount. Recount or clear {(recount == 1 ? "it" : "them")} before posting.");

            var grossVariance = count.Lines
                .Where(x => x.CountedQuantity is not null)
                .Sum(x => Math.Abs((x.CountedQuantity!.Value - x.ExpectedQuantity) * (x.UnitCost ?? 0)));
            if (approvalLimit > 0 && grossVariance > approvalLimit && !IsAdmin && !HasPermission(PermissionNames.CanApproveMaterials))
                throw new ForbiddenException($"The variances on this count are worth {grossVariance:N2}, above the {approvalLimit:N2} approval limit. Someone who approves materials must post it.");

            foreach (var line in count.Lines.Where(x => x.CountedQuantity is not null))
            {
                var delta = line.CountedQuantity!.Value - line.ExpectedQuantity;
                if (delta == 0)
                    continue;
                var material = await dbContext.MaterialItems.SingleAsync(x => x.TenantId == TenantId && x.Id == line.MaterialItemId, ct);
                var reason = line.Notes is null ? $"Stock count {count.CountNumber}" : $"Stock count {count.CountNumber}: {line.Notes}";
                if (count.VanId is Guid vanId)
                    await stockLedgerService.RecordVanAdjustmentAsync(material, vanId, delta, UserId, reason, count.CountNumber, ct);
                else
                    await stockLedgerService.RecordAdjustmentAsync(material, count.BranchId, delta, UserId, reason, count.CountNumber, cancellationToken: ct);
                adjusted++;
            }

            count.Status = StockCountStatuses.Posted;
            count.PostedByUserId = UserId;
            count.PostedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "inventory.stock_count.posted", nameof(StockCount), count.Id.ToString(),
            $"Posted stock count {count.CountNumber}: {adjusted} part(s) adjusted.", cancellationToken);
        return Ok(Map(await LoadAsync(count.Id, cancellationToken)));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<StockCountDetailResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        EnsureCountAccess();
        var count = await LoadAsync(id, cancellationToken);
        EnsureOpen(count);
        count.Status = StockCountStatuses.Cancelled;
        count.CancelledAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.stock_count.cancelled", nameof(StockCount), count.Id.ToString(),
            $"Cancelled stock count {count.CountNumber}.", cancellationToken);
        return Ok(Map(count));
    }

    /// <summary>
    /// Book balances at the counted location, by part: the van's, or the branch's. Null for a workspace
    /// without branches, where each part's own total is the balance.
    /// </summary>
    private async Task<Dictionary<Guid, (decimal Quantity, decimal ReorderLevel, decimal? UnitCost)>?> BalancesAtAsync(Guid? vanId, Guid? branchId, CancellationToken cancellationToken)
    {
        if (vanId is Guid van)
        {
            return (await dbContext.VanMaterialStocks.AsNoTracking()
                    .Where(x => x.TenantId == TenantId && x.VanId == van)
                    .Select(x => new { x.MaterialId, x.QuantityOnHand, x.ReorderLevel, x.UnitCost })
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => x.MaterialId, x => (x.QuantityOnHand, x.ReorderLevel, x.UnitCost));
        }

        if (branchId is Guid branch)
        {
            return (await dbContext.BranchMaterialStocks.AsNoTracking()
                    .Where(x => x.TenantId == TenantId && x.BranchId == branch)
                    .Select(x => new { x.MaterialId, x.QuantityOnHand, x.ReorderLevel, x.UnitCost })
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => x.MaterialId, x => (x.QuantityOnHand, x.ReorderLevel, x.UnitCost));
        }

        return null;
    }

    private async Task<StockCount> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        return await dbContext.StockCounts
            .Include(x => x.Branch)
            .Include(x => x.Van)
            .Include(x => x.StartedByUser)
            .Include(x => x.PostedByUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.MaterialItem)
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Stock count was not found.");
    }

    private static void EnsureOpen(StockCount count)
    {
        if (count.Status != StockCountStatuses.Open)
            throw new BusinessRuleException($"Stock count {count.CountNumber} is {count.Status.ToLowerInvariant()} and can no longer change.");
    }

    private void EnsureViewAccess() =>
        userAccessService.EnsureAdminOrAnyPermission(
            PermissionNames.CanViewMaterials, PermissionNames.CanManageMaterials, PermissionNames.CanApproveMaterials, PermissionNames.CanIssueMaterials);

    private void EnsureCountAccess() =>
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials, PermissionNames.CanIssueMaterials);

    /// <summary>Who sees book quantities on an open blind count: the people who post counts.</summary>
    private bool CanSeeExpected() =>
        IsAdmin || HasPermission(PermissionNames.CanManageMaterials) || HasPermission(PermissionNames.CanApproveMaterials);

    private StockCountDetailResponse Map(StockCount count)
    {
        var hideExpected = count.IsBlind && count.Status == StockCountStatuses.Open && !CanSeeExpected();
        var lines = count.Lines
            .OrderBy(x => x.MaterialItem?.ItemName)
            .Select(x =>
            {
                decimal? variance = x.CountedQuantity is { } counted ? counted - x.ExpectedQuantity : null;
                return new StockCountLineResponse(
                    x.Id,
                    x.MaterialItemId,
                    x.MaterialItem?.ItemCode,
                    x.MaterialItem?.ItemName,
                    x.MaterialItem?.UnitOfMeasure,
                    x.MaterialItem?.Category,
                    hideExpected ? null : x.ExpectedQuantity,
                    x.CountedQuantity,
                    hideExpected ? null : variance,
                    x.UnitCost,
                    hideExpected ? null : variance is { } v ? Math.Round(v * (x.UnitCost ?? 0), 2) : null,
                    x.Notes)
                {
                    Barcode = x.MaterialItem?.Barcode,
                    NeedsRecount = x.NeedsRecount,
                };
            })
            .ToList();

        return new StockCountDetailResponse(
            count.Id,
            count.CountNumber,
            count.BranchId,
            count.Branch?.Name,
            count.Category,
            count.Status,
            count.Notes,
            count.StartedByUser?.FullName,
            count.CreatedAt,
            count.PostedByUser?.FullName,
            count.PostedAt,
            lines,
            lines.Sum(x => x.VarianceValue ?? 0))
        {
            VanId = count.VanId,
            LocationName = count.Van?.Name ?? count.Branch?.Name,
            IsBlind = count.IsBlind,
            ExpectedHidden = hideExpected,
            GrossVarianceValue = hideExpected ? 0 : lines.Sum(x => Math.Abs(x.VarianceValue ?? 0)),
        };
    }
}

/// <param name="VanId">Count a van instead of a branch; BranchId is ignored then.</param>
/// <param name="OnlyStocked">List only parts held at the location (or with a reorder level there).</param>
/// <param name="Blind">Hide book quantities from counters until the count is posted.</param>
public sealed record CreateStockCountRequest(Guid? BranchId, string? Category = null, string? Notes = null, Guid? VanId = null, bool OnlyStocked = false, bool Blind = false);

public sealed record AddStockCountLineRequest(Guid MaterialItemId, decimal? CountedQuantity = null);

public sealed record SaveStockCountLinesRequest(IReadOnlyList<SaveStockCountLineRequest> Lines);

/// <param name="NeedsRecount">Sets or clears the recount mark; null leaves it (a changed count clears it).</param>
public sealed record SaveStockCountLineRequest(Guid LineId, decimal? CountedQuantity, string? Notes = null, bool? NeedsRecount = null);

public sealed record StockCountSummaryResponse(
    Guid Id, string CountNumber, Guid? BranchId, string? BranchName, string? Category, string Status,
    int LineCount, int CountedLines, int VarianceLines, string? StartedByName, DateTime CreatedAt, DateTime? PostedAt)
{
    public Guid? VanId { get; init; }
    /// <summary>The van's name for a van count, else the branch's.</summary>
    public string? LocationName { get; init; }
    public bool IsBlind { get; init; }
    public int RecountLines { get; init; }
}

/// <param name="ExpectedQuantity">Null while a blind count is open, for counters.</param>
public sealed record StockCountLineResponse(
    Guid Id, Guid MaterialItemId, string? ItemCode, string? ItemName, string? UnitOfMeasure, string? Category,
    decimal? ExpectedQuantity, decimal? CountedQuantity, decimal? Variance, decimal? UnitCost, decimal? VarianceValue, string? Notes)
{
    public string? Barcode { get; init; }
    public bool NeedsRecount { get; init; }
}

/// <param name="NetVarianceValue">Counted variances valued at the cost snapshotted when the count opened.</param>
public sealed record StockCountDetailResponse(
    Guid Id, string CountNumber, Guid? BranchId, string? BranchName, string? Category, string Status, string? Notes,
    string? StartedByName, DateTime CreatedAt, string? PostedByName, DateTime? PostedAt,
    IReadOnlyList<StockCountLineResponse> Lines, decimal NetVarianceValue)
{
    public Guid? VanId { get; init; }
    public string? LocationName { get; init; }
    public bool IsBlind { get; init; }
    /// <summary>True when this viewer is not shown the book quantities (an open blind count).</summary>
    public bool ExpectedHidden { get; init; }
    /// <summary>Total value of variances in either direction; what the approval limit is checked against.</summary>
    public decimal GrossVarianceValue { get; init; }
}
