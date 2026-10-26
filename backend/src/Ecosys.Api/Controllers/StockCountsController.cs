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
/// Stocktakes: open a count for a branch (the books' balances are snapshotted), record what is on
/// the shelf, then post it to adjust each counted part by counted − expected.
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
                x.PostedAt))
            .ToListAsync(cancellationToken);
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
        var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId, cancellationToken);
        if (await dbContext.StockCounts.AnyAsync(x => x.TenantId == TenantId && x.BranchId == branchId && x.Status == StockCountStatuses.Open, cancellationToken))
            throw new BusinessRuleException("This branch already has an open stock count. Post or cancel it first.");

        var category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        var parts = await dbContext.MaterialItems.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.IsActive && (category == null || x.Category == category))
            .OrderBy(x => x.ItemName)
            .Select(x => new { x.Id, x.QuantityOnHand, x.UnitCost })
            .ToListAsync(cancellationToken);
        if (parts.Count == 0)
            throw new BusinessRuleException(category is null ? "There are no active parts to count." : $"There are no active parts in {category}.");

        var branchStock = branchId is null
            ? []
            : await dbContext.BranchMaterialStocks.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.BranchId == branchId)
                .ToDictionaryAsync(x => x.MaterialId, cancellationToken);

        var count = new StockCount
        {
            TenantId = TenantId,
            CountNumber = await documentNumberingService.GenerateAsync(TenantId, branchId, DocumentTypes.StockCount, cancellationToken),
            BranchId = branchId,
            Category = category,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            StartedByUserId = UserId,
        };
        foreach (var part in parts)
        {
            var stock = branchStock.GetValueOrDefault(part.Id);
            count.Lines.Add(new StockCountLine
            {
                TenantId = TenantId,
                MaterialItemId = part.Id,
                ExpectedQuantity = branchId is null ? part.QuantityOnHand : stock?.QuantityOnHand ?? 0,
                UnitCost = stock?.UnitCost ?? part.UnitCost,
            });
        }

        dbContext.StockCounts.Add(count);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.stock_count.opened", nameof(StockCount), count.Id.ToString(),
            $"Opened stock count {count.CountNumber} ({count.Lines.Count} parts).", cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = count.Id }, Map(await LoadAsync(count.Id, cancellationToken)));
    }

    /// <summary>Records counted quantities; a null quantity clears a line back to uncounted.</summary>
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
            }
            line.Notes = string.IsNullOrWhiteSpace(entry.Notes) ? null : entry.Notes.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(count));
    }

    /// <summary>
    /// Books every counted variance as an adjustment against the snapshot (counted − expected), in one
    /// transaction. Uncounted lines are left untouched.
    /// </summary>
    [HttpPost("{id:guid}/post")]
    public async Task<ActionResult<StockCountDetailResponse>> Post(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageMaterials, PermissionNames.CanApproveMaterials);
        StockCount count = null!;
        var adjusted = 0;
        await stockLedgerService.ExecuteAsync(async ct =>
        {
            count = await LoadAsync(id, ct);
            EnsureOpen(count);
            if (count.Lines.All(x => x.CountedQuantity is null))
                throw new BusinessRuleException("Nothing has been counted yet.");

            foreach (var line in count.Lines.Where(x => x.CountedQuantity is not null))
            {
                var delta = line.CountedQuantity!.Value - line.ExpectedQuantity;
                if (delta == 0)
                    continue;
                var material = await dbContext.MaterialItems.SingleAsync(x => x.TenantId == TenantId && x.Id == line.MaterialItemId, ct);
                var reason = line.Notes is null ? $"Stock count {count.CountNumber}" : $"Stock count {count.CountNumber}: {line.Notes}";
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

    private async Task<StockCount> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        return await dbContext.StockCounts
            .Include(x => x.Branch)
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

    private static StockCountDetailResponse Map(StockCount count)
    {
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
                    x.ExpectedQuantity,
                    x.CountedQuantity,
                    variance,
                    x.UnitCost,
                    variance is { } v ? Math.Round(v * (x.UnitCost ?? 0), 2) : null,
                    x.Notes);
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
            lines.Sum(x => x.VarianceValue ?? 0));
    }
}

public sealed record CreateStockCountRequest(Guid? BranchId, string? Category = null, string? Notes = null);

public sealed record SaveStockCountLinesRequest(IReadOnlyList<SaveStockCountLineRequest> Lines);

public sealed record SaveStockCountLineRequest(Guid LineId, decimal? CountedQuantity, string? Notes = null);

public sealed record StockCountSummaryResponse(
    Guid Id, string CountNumber, Guid? BranchId, string? BranchName, string? Category, string Status,
    int LineCount, int CountedLines, int VarianceLines, string? StartedByName, DateTime CreatedAt, DateTime? PostedAt);

public sealed record StockCountLineResponse(
    Guid Id, Guid MaterialItemId, string? ItemCode, string? ItemName, string? UnitOfMeasure, string? Category,
    decimal ExpectedQuantity, decimal? CountedQuantity, decimal? Variance, decimal? UnitCost, decimal? VarianceValue, string? Notes);

/// <param name="NetVarianceValue">Counted variances valued at the cost snapshotted when the count opened.</param>
public sealed record StockCountDetailResponse(
    Guid Id, string CountNumber, Guid? BranchId, string? BranchName, string? Category, string Status, string? Notes,
    string? StartedByName, DateTime CreatedAt, string? PostedByName, DateTime? PostedAt,
    IReadOnlyList<StockCountLineResponse> Lines, decimal NetVarianceValue);
