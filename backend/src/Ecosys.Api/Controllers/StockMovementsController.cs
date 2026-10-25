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
    IBranchAccessService branchAccessService) : TenantAwareControllerBase(tenantContext)
{
    /// <param name="branchId">A branch: its own movements plus those of vans based there.</param>
    /// <param name="type">A movement type, e.g. Receipt, Issue, Adjustment, TransferOut, VanConsume.</param>
    /// <param name="search">Matches part name or code, reference or reason.</param>
    [HttpGet]
    public async Task<ActionResult<PagedResult<StockLedgerEntryResponse>>> List(
        [FromQuery] Guid? materialId,
        [FromQuery] Guid? branchId,
        [FromQuery] Guid? vanId,
        [FromQuery] string? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        EnsureViewAccess();
        var query = await FilterAsync(materialId, branchId, vanId, type, from, to, search, cancellationToken);
        var result = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(Projection)
            .ToPagedResultAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    /// <summary>Filters shared with the CSV export in the inventory reports.</summary>
    internal async Task<IQueryable<StockMovement>> FilterAsync(
        Guid? materialId, Guid? branchId, Guid? vanId, string? type, DateTime? from, DateTime? to, string? search,
        CancellationToken cancellationToken)
    {
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var query = dbContext.StockMovements.AsNoTracking().Where(x => x.TenantId == TenantId);
        query = ApplyScope(query, scope);

        if (materialId.HasValue)
            query = query.Where(x => x.MaterialId == materialId);
        if (vanId.HasValue)
            query = query.Where(x => x.VanId == vanId);
        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "All", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = type.Trim();
            query = query.Where(x => x.MovementType == normalized);
        }
        if (from.HasValue)
        {
            var start = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt >= start);
        }
        if (to.HasValue)
        {
            if (from.HasValue && to < from)
                throw new BusinessRuleException("The end date must be on or after the start date.");
            // A date with no time means the whole of that day.
            var end = DateTime.SpecifyKind(to.Value.TimeOfDay == TimeSpan.Zero ? to.Value.AddDays(1) : to.Value, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.Material!.ItemName.ToLower().Contains(term)
                || x.Material.ItemCode.ToLower().Contains(term)
                || (x.ReferenceNumber != null && x.ReferenceNumber.ToLower().Contains(term))
                || (x.Reason != null && x.Reason.ToLower().Contains(term)));
        }

        return query;
    }

    /// <summary>
    /// Branch access covers a branch's own movements and those of the vans based there; branchless
    /// movements (a workspace without branches) are visible whenever branches are not configured.
    /// </summary>
    private static IQueryable<StockMovement> ApplyScope(IQueryable<StockMovement> query, BranchQueryScope scope)
    {
        if (!scope.HasConfiguredBranches)
            return query;
        if (scope.RequestedBranchId is { } requested)
            return query.Where(x => x.BranchId == requested || (x.VanId != null && x.Van!.HomeBranchId == requested));

        var ids = scope.AccessibleBranchIds.ToList();
        return query.Where(x =>
            (x.BranchId != null && ids.Contains(x.BranchId.Value))
            || (x.VanId != null && x.Van!.HomeBranchId != null && ids.Contains(x.Van.HomeBranchId.Value)));
    }

    internal static readonly System.Linq.Expressions.Expression<Func<StockMovement, StockLedgerEntryResponse>> Projection = x =>
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
            x.VanId,
            x.Van != null ? x.Van.Name : null,
            x.WorkOrderId,
            x.WorkOrder != null ? x.WorkOrder.WorkOrderNumber : null,
            x.ReferenceNumber,
            x.Reason,
            x.CreatedByUser != null ? x.CreatedByUser.FullName : null);

    private void EnsureViewAccess() =>
        userAccessService.EnsureAdminOrAnyPermission(
            PermissionNames.CanViewMaterials,
            PermissionNames.CanManageMaterials,
            PermissionNames.CanApproveMaterials,
            PermissionNames.CanIssueMaterials);
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
    Guid? VanId,
    string? VanName,
    Guid? WorkOrderId,
    string? WorkOrderNumber,
    string? ReferenceNumber,
    string? Reason,
    string? CreatedByName);
