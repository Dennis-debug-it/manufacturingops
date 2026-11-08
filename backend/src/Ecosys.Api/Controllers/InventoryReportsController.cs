using System.Text;
using Ecosys.Api.Common;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Inventory reports computed on the server from the stock tables and the ledger, scoped to the
/// branches the user can see. Each takes <c>format=csv</c> to download the same rows.
/// </summary>
[ApiController]
[Authorize]
[Route("api/inventory-reports")]
public sealed class InventoryReportsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    ITenantTimeZoneService tenantTimeZoneService) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>Stock on hand by location (branches and vans) at each holding's cost.</summary>
    [HttpGet("valuation")]
    public async Task<IActionResult> Valuation([FromQuery] Guid? branchId, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        EnsureViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var rows = new List<StockValuationRowResponse>();

        if (!scope.HasConfiguredBranches)
        {
            // No branches: the workspace holds one pool of stock on the part itself.
            rows.AddRange(await dbContext.MaterialItems.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.IsActive && x.QuantityOnHand != 0)
                .Select(x => new StockValuationRowResponse("Stock", "Workspace", x.Id, x.ItemCode, x.ItemName, x.Category, x.UnitOfMeasure,
                    x.QuantityOnHand, x.UnitCost, Math.Round(x.QuantityOnHand * (x.UnitCost ?? 0), 2)))
                .ToListAsync(cancellationToken));
        }
        else
        {
            rows.AddRange(await dbContext.BranchMaterialStocks.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.Material!.IsActive && x.QuantityOnHand != 0)
                .WhereAccessible(scope, x => (Guid?)x.BranchId)
                .Select(x => new StockValuationRowResponse(x.Branch!.Name, "Branch", x.MaterialId, x.Material!.ItemCode, x.Material.ItemName,
                    x.Material.Category, x.Material.UnitOfMeasure, x.QuantityOnHand, x.UnitCost ?? x.Material.UnitCost,
                    Math.Round(x.QuantityOnHand * (x.UnitCost ?? x.Material.UnitCost ?? 0), 2)))
                .ToListAsync(cancellationToken));
        }

        rows = rows.OrderBy(x => x.Location).ThenBy(x => x.ItemName).ToList();
        if (IsCsv(format))
        {
            return CsvFile("stock-valuation.csv", "Location,Type,Code,Part,Category,Unit,Quantity,Unit Cost,Value",
                rows.Select(x => new object?[] { x.Location, x.LocationType, x.ItemCode, x.ItemName, x.Category, x.UnitOfMeasure, x.Quantity, x.UnitCost, x.Value }));
        }

        var byLocation = rows
            .GroupBy(x => (x.Location, x.LocationType))
            .Select(g => new StockValuationLocationResponse(g.Key.Location, g.Key.LocationType, g.Count(), g.Sum(x => x.Value)))
            .ToList();
        return Ok(new StockValuationResponse(rows, byLocation, rows.Sum(x => x.Value)));
    }

    /// <summary>
    /// Parts consumed in a period: issued to jobs, requests and issues plus used off vans, less unused
    /// parts returned. Transfers and receipts are moves, not use, and are left out.
    /// </summary>
    [HttpGet("usage")]
    public async Task<IActionResult> Usage(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? branchId, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        EnsureViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var zone = await tenantTimeZoneService.GetTimeZoneAsync(TenantId, cancellationToken);
        var movements = StockLedgerQuery.Apply(
            dbContext.StockMovements.AsNoTracking().Where(x => x.TenantId == TenantId && (x.MovementType == "Issue" || x.MovementType == "Return")),
            scope, zone, new StockLedgerFilter(null, null, from, to, null));

        var grouped = await movements
            .GroupBy(x => new { x.MaterialId, x.Material!.ItemCode, x.Material.ItemName, x.Material.Category, x.Material.UnitOfMeasure, x.Material.UnitCost })
            .Select(g => new
            {
                g.Key.MaterialId,
                g.Key.ItemCode,
                g.Key.ItemName,
                g.Key.Category,
                g.Key.UnitOfMeasure,
                g.Key.UnitCost,
                Used = g.Where(x => x.MovementType != "Return").Sum(x => x.Quantity),
                Returned = g.Where(x => x.MovementType == "Return").Sum(x => x.Quantity),
                Jobs = g.Where(x => x.WorkOrderId != null).Select(x => x.WorkOrderId).Distinct().Count(),
            })
            .ToListAsync(cancellationToken);

        var rows = grouped
            .Select(x => new StockUsageRowResponse(
                x.MaterialId, x.ItemCode, x.ItemName, x.Category, x.UnitOfMeasure,
                x.Used, x.Returned, x.Used - x.Returned, x.Jobs, x.UnitCost,
                Math.Round((x.Used - x.Returned) * (x.UnitCost ?? 0), 2)))
            .Where(x => x.QuantityUsed != 0 || x.QuantityReturned != 0)
            .OrderByDescending(x => x.NetValue)
            .ThenBy(x => x.ItemName)
            .ToList();

        if (IsCsv(format))
        {
            return CsvFile($"stock-usage{FileSuffix(from, to)}.csv", "Code,Part,Category,Unit,Used,Returned,Net Used,Jobs,Unit Cost,Net Value",
                rows.Select(x => new object?[] { x.ItemCode, x.ItemName, x.Category, x.UnitOfMeasure, x.QuantityUsed, x.QuantityReturned, x.NetQuantity, x.JobCount, x.UnitCost, x.NetValue }));
        }

        return Ok(new StockUsageResponse(rows, rows.Sum(x => x.NetValue)));
    }

    /// <summary>The stock ledger, filtered as on the Movements page, as a CSV download.</summary>
    [HttpGet("movements")]
    public async Task<IActionResult> Movements(
        [FromQuery] Guid? materialId, [FromQuery] Guid? branchId, [FromQuery] string? type,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        EnsureViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var zone = await tenantTimeZoneService.GetTimeZoneAsync(TenantId, cancellationToken);
        var rows = await StockLedgerQuery.Apply(dbContext.StockMovements.AsNoTracking().Where(x => x.TenantId == TenantId), scope, zone,
                new StockLedgerFilter(materialId, type, from, to, search))
            .OrderByDescending(x => x.CreatedAt)
            .Take(50_000)
            .Select(StockMovementsController.Projection)
            .ToListAsync(cancellationToken);

        return CsvFile($"stock-movements{FileSuffix(from, to)}.csv",
            "When,Type,Code,Part,Change,Unit,Balance After,Branch,Work Order,Reference,Reason,Batch / Serial,By",
            rows.Select(x => new object?[]
            {
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc), zone).ToString("yyyy-MM-dd HH:mm"),
                x.MovementType, x.ItemCode, x.ItemName, x.QuantityChange?.ToString() ?? $"±{x.Quantity}", x.UnitOfMeasure, x.BalanceAfter,
                x.BranchName, x.WorkOrderNumber, x.ReferenceNumber, x.Reason, x.BatchOrSerial, x.CreatedByName,
            }));
    }

    private static bool IsCsv(string? format) => string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);

    private static string FileSuffix(DateTime? from, DateTime? to) =>
        (from, to) switch
        {
            ({ } f, { } t) => $"-{f:yyyyMMdd}-{t:yyyyMMdd}",
            ({ } f, null) => $"-from-{f:yyyyMMdd}",
            (null, { } t) => $"-to-{t:yyyyMMdd}",
            _ => string.Empty,
        };

    private FileContentResult CsvFile(string fileName, string header, IEnumerable<object?[]> rows)
    {
        var csv = new StringBuilder();
        csv.AppendLine(header);
        foreach (var row in rows)
            csv.AppendLine(Csv.Row(row));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    private void EnsureViewAccess() =>
        userAccessService.EnsureAdminOrAnyPermission(
            PermissionNames.CanViewMaterials, PermissionNames.CanManageMaterials, PermissionNames.CanApproveMaterials, PermissionNames.CanIssueMaterials);
}

public sealed record StockValuationRowResponse(
    string Location, string LocationType, Guid MaterialItemId, string ItemCode, string ItemName, string? Category, string UnitOfMeasure,
    decimal Quantity, decimal? UnitCost, decimal Value);

public sealed record StockValuationLocationResponse(string Location, string LocationType, int Parts, decimal Value);

public sealed record StockValuationResponse(
    IReadOnlyList<StockValuationRowResponse> Rows, IReadOnlyList<StockValuationLocationResponse> Locations, decimal TotalValue);

/// <param name="NetValue">Net quantity at the part's current cost.</param>
public sealed record StockUsageRowResponse(
    Guid MaterialItemId, string ItemCode, string ItemName, string? Category, string UnitOfMeasure,
    decimal QuantityUsed, decimal QuantityReturned, decimal NetQuantity, int JobCount, decimal? UnitCost, decimal NetValue);

public sealed record StockUsageResponse(IReadOnlyList<StockUsageRowResponse> Rows, decimal TotalValue);
