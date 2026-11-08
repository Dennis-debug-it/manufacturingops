using Ecosys.Api.Common;
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
/// The tenant-wide stock ledger: every receipt, issue, return, adjustment, transfer and van move,
/// newest first. Each part's own history stays on <c>/api/materials/{id}/movements</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/stock-movements")]
public sealed class StockMovementsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    ITenantTimeZoneService tenantTimeZoneService) : TenantAwareControllerBase(tenantContext)
{
    /// <param name="branchId">A branch: its own movements plus those of vans based there.</param>
    /// <param name="from">First day included, in the workspace's time zone.</param>
    /// <param name="to">Last day included, in the workspace's time zone.</param>
    /// <param name="type">A movement type, e.g. Receipt, Issue, Adjustment, TransferOut.</param>
    /// <param name="search">Matches part name or code, reference, reason or batch/serial number.</param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<StockLedgerEntryResponse>>> List(
        [FromQuery] Guid? materialId,
        [FromQuery] Guid? branchId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        EnsureViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var zone = await tenantTimeZoneService.GetTimeZoneAsync(TenantId, cancellationToken);
        var query = StockLedgerQuery.Apply(dbContext.StockMovements.AsNoTracking().Where(x => x.TenantId == TenantId), scope, zone,
            new StockLedgerFilter(materialId, type, from, to, search));
        var result = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(Projection)
            .ToPagedResultAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    public static readonly System.Linq.Expressions.Expression<Func<StockMovement, StockLedgerEntryResponse>> Projection = x =>
        new StockLedgerEntryResponse(
            x.Id,
            x.CreatedAt,
            x.MovementType,
            x.MaterialId,
            x.Material != null ? x.Material.ItemCode : null,
            x.Material != null ? x.Material.ItemName : null,
            x.Material != null ? x.Material.UnitOfMeasure : null,
            x.Quantity,
            x.QuantityChange,
            x.BalanceAfter,
            x.BranchId,
            x.Branch != null ? x.Branch.Name : null,
            x.WorkOrderId,
            x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : null,
            x.ReferenceNumber,
            x.Reason,
            x.CreatedByUser != null ? x.CreatedByUser.FullName : null,
            x.BatchOrSerial);

    private void EnsureViewAccess() =>
        userAccessService.EnsureAdminOrAnyPermission(
            PermissionNames.CanViewMaterials,
            PermissionNames.CanManageMaterials,
            PermissionNames.CanApproveMaterials,
            PermissionNames.CanIssueMaterials);
}

public sealed record StockLedgerFilter(Guid? MaterialId, string? Type, DateTime? From, DateTime? To, string? Search);

/// <summary>The stock ledger's filters, shared by the movement feed and the inventory reports.</summary>
public static class StockLedgerQuery
{
    public static IQueryable<StockMovement> Apply(IQueryable<StockMovement> query, BranchQueryScope scope, TimeZoneInfo zone, StockLedgerFilter filter)
    {
        query = ApplyScope(query, scope);
        if (filter.MaterialId.HasValue)
            query = query.Where(x => x.MaterialId == filter.MaterialId);
        if (!string.IsNullOrWhiteSpace(filter.Type) && !string.Equals(filter.Type, "All", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = filter.Type.Trim();
            query = query.Where(x => x.MovementType == normalized);
        }

        var (start, end) = DayRange(filter.From, filter.To, zone);
        if (start.HasValue)
            query = query.Where(x => x.CreatedAt >= start);
        if (end.HasValue)
            query = query.Where(x => x.CreatedAt < end);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(x =>
                x.Material!.ItemName.ToLower().Contains(term)
                || x.Material.ItemCode.ToLower().Contains(term)
                || (x.ReferenceNumber != null && x.ReferenceNumber.ToLower().Contains(term))
                || (x.Reason != null && x.Reason.ToLower().Contains(term))
                || (x.BatchOrSerial != null && x.BatchOrSerial.ToLower().Contains(term)));
        }

        return query;
    }

    /// <summary>
    /// Whole days in the workspace's time zone, as UTC bounds: from the start of <paramref name="from"/>
    /// to the start of the day after <paramref name="to"/>.
    /// </summary>
    public static (DateTime? Start, DateTime? End) DayRange(DateTime? from, DateTime? to, TimeZoneInfo zone)
    {
        if (from.HasValue && to.HasValue && to.Value.Date < from.Value.Date)
            throw new BusinessRuleException("The end date must be on or after the start date.");
        DateTime? start = from is { } f ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(f.Date, DateTimeKind.Unspecified), zone) : null;
        DateTime? end = to is { } t ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(t.Date.AddDays(1), DateTimeKind.Unspecified), zone) : null;
        return (start, end);
    }

    /// <summary>
    /// Branch access covers a branch's own movements; branchless
    /// movements (a workspace without branches) are visible whenever branches are not configured.
    /// </summary>
    private static IQueryable<StockMovement> ApplyScope(IQueryable<StockMovement> query, BranchQueryScope scope)
    {
        if (!scope.HasConfiguredBranches)
            return query;
        if (scope.RequestedBranchId is { } requested)
            return query.Where(x => x.BranchId == requested);

        var ids = scope.AccessibleBranchIds.ToList();
        return query.Where(x => x.BranchId != null && ids.Contains(x.BranchId.Value));
    }
}

/// <param name="QuantityChange">Signed change to the location's balance; null on older adjustments of unknown direction.</param>
public sealed record StockLedgerEntryResponse(
    Guid Id,
    DateTime CreatedAt,
    string MovementType,
    Guid MaterialId,
    string? ItemCode,
    string? ItemName,
    string? UnitOfMeasure,
    decimal Quantity,
    decimal? QuantityChange,
    decimal BalanceAfter,
    Guid? BranchId,
    string? BranchName,
    Guid? WorkOrderId,
    string? WorkOrderNumber,
    string? ReferenceNumber,
    string? Reason,
    string? CreatedByName,
    string? BatchOrSerial = null);
