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
[Route("api/purchase-orders")]
public sealed class PurchaseOrdersController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IDocumentNumberingService documentNumberingService,
    IAuditLogService auditLogService,
    IApprovalWorkflowService approvalWorkflowService,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    // ── Purchase orders ──────────────────────────────────────────────────────

    /// <param name="state"><c>open</c> (not yet fully received) or <c>late</c> (sent and past its expected date), as the dashboard counts them.</param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PurchaseOrderListItemResponse>>> ListPurchaseOrders(
        [FromQuery] string? status,
        [FromQuery] string? state,
        [FromServices] ITenantTimeZoneService tenantTimeZoneService,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewMaterials, PermissionNames.CanManageMaterials);

        var query = dbContext.PurchaseOrders
            .Include(x => x.Supplier)
            .Where(x => x.TenantId == TenantId);
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(state))
        {
            var zone = await tenantTimeZoneService.GetTimeZoneAsync(TenantId, cancellationToken);
            query = PurchaseOrderListStates.Apply(query, state, TenantDay.StartUtc(zone, DateTime.UtcNow));
        }

        var orders = await query
            .OrderByDescending(x => x.OrderedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return Ok(orders.Select(MapListItem).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> GetPurchaseOrder(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewMaterials, PermissionNames.CanManageMaterials);
        var order = await LoadOrderAsync(id, cancellationToken);
        return Ok(MapDetail(order));
    }

    [HttpPost]
    public async Task<ActionResult<PurchaseOrderDetailResponse>> CreatePurchaseOrder(
        [FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageMaterials);

        var supplier = await dbContext.Suppliers
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException("Supplier not found.");
        if (request.Lines.Count == 0)
            throw new BusinessRuleException("A purchase order needs at least one line.");

        var order = new PurchaseOrder
        {
            TenantId = TenantId,
            PoNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.PurchaseOrder, cancellationToken),
            SupplierId = supplier.Id,
            ExpectedAt = request.ExpectedAt is null ? null : DateTime.SpecifyKind(request.ExpectedAt.Value, DateTimeKind.Utc),
            Notes = request.Notes?.Trim(),
            ProjectId = await ProjectLinks.ResolveAsync(dbContext, TenantId, request.ProjectId, null, cancellationToken),
        };
        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ItemName))
                throw new BusinessRuleException("Line item name is required.");
            if (line.QuantityOrdered <= 0)
                throw new BusinessRuleException("Line quantity must be greater than zero.");
            order.Lines.Add(new PurchaseOrderLine
            {
                TenantId = TenantId,
                ItemName = line.ItemName.Trim(),
                QuantityOrdered = line.QuantityOrdered,
                UnitCost = line.UnitCost,
                LineTotal = Math.Round(line.QuantityOrdered * line.UnitCost, 2),
            });
        }
        order.TotalAmount = order.Lines.Sum(x => x.LineTotal);

        dbContext.PurchaseOrders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.po.created", nameof(PurchaseOrder), order.Id.ToString(), $"Purchase order {order.PoNumber} created for {supplier.SupplierName}", cancellationToken);

        // Start the approval chain if a matrix is configured; the PO then cannot be
        // sent until fully approved. With no matrix, behavior is unchanged.
        await approvalWorkflowService.StartAsync(
            TenantId, ApprovalDocumentTypes.PurchaseOrder, order.Id, order.PoNumber,
            order.TotalAmount, order.Currency, UserId, cancellationToken);

        order.Supplier = supplier;
        return Ok(MapDetail(order));
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(
        Guid id, [FromBody] PurchaseOrderStatusRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageMaterials);
        var order = await LoadOrderAsync(id, cancellationToken);

        var allowed = new[] { "Sent", "Cancelled" };
        if (!allowed.Contains(request.Status))
            throw new BusinessRuleException("Status can only be set to Sent or Cancelled.");
        if (order.Status is "Received" or "Cancelled")
            throw new BusinessRuleException($"A {order.Status.ToLowerInvariant()} purchase order cannot change status.");

        // A PO under an active approval chain cannot be sent to the supplier until approved.
        if (string.Equals(request.Status, "Sent", StringComparison.OrdinalIgnoreCase))
        {
            var pendingApproval = await approvalWorkflowService.GetActiveForDocumentAsync(
                TenantId, ApprovalDocumentTypes.PurchaseOrder, order.Id, cancellationToken);
            if (pendingApproval is not null)
                throw new BusinessRuleException("This purchase order is awaiting approval and cannot be sent yet.");
        }

        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.po.status", nameof(PurchaseOrder), order.Id.ToString(), $"Purchase order {order.PoNumber} marked {request.Status}", cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> ReceiveLine(
        Guid id, [FromBody] ReceivePurchaseOrderLineRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageMaterials);
        var order = await LoadOrderAsync(id, cancellationToken);

        if (order.Status is "Cancelled")
            throw new BusinessRuleException("Cannot receive against a cancelled purchase order.");

        var line = order.Lines.FirstOrDefault(x => x.Id == request.LineId)
            ?? throw new NotFoundException("Purchase order line not found.");
        if (request.Quantity <= 0)
            throw new BusinessRuleException("Received quantity must be greater than zero.");
        var outstanding = line.QuantityOrdered - line.QuantityReceived;
        if (request.Quantity > outstanding)
            throw new BusinessRuleException($"Only {outstanding:0.###} of {line.ItemName} remain outstanding.");

        line.QuantityReceived += request.Quantity;
        line.UpdatedAt = DateTime.UtcNow;

        var receiverName = await dbContext.Users
            .Where(x => x.Id == UserId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

        // Add via the DbSet (not the tracked parent's collection) so EF reliably marks this
        // client-keyed entity as Added rather than Modified.
        dbContext.PurchaseOrderReceiptEvents.Add(new PurchaseOrderReceiptEvent
        {
            TenantId = TenantId,
            PurchaseOrderId = order.Id,
            PurchaseOrderLineId = line.Id,
            ItemName = line.ItemName,
            Quantity = request.Quantity,
            ReceivedByUserId = UserId,
            ReceivedByName = receiverName,
        });

        order.Status = order.Lines.All(x => x.QuantityReceived >= x.QuantityOrdered)
            ? "Received"
            : "PartiallyReceived";
        order.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.po.received", nameof(PurchaseOrder), order.Id.ToString(), $"Received {request.Quantity:0.###} × {line.ItemName} on {order.PoNumber}", cancellationToken);

        return NoContent();
    }

    // ── Suppliers ────────────────────────────────────────────────────────────

    [HttpGet("/api/suppliers")]
    public async Task<ActionResult<IReadOnlyCollection<SupplierListItemResponse>>> ListSuppliers(CancellationToken cancellationToken)
    {
        // Finance reads suppliers too: a payable is recorded against one.
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewMaterials, PermissionNames.CanManageMaterials, PermissionNames.FinanceView);

        var suppliers = await dbContext.Suppliers
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.SupplierName)
            .ToListAsync(cancellationToken);

        var openCounts = await dbContext.PurchaseOrders
            .Where(x => x.TenantId == TenantId && x.Status != "Received" && x.Status != "Cancelled")
            .GroupBy(x => x.SupplierId)
            .Select(g => new { SupplierId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SupplierId, x => x.Count, cancellationToken);

        var deliveryStats = await ComputeOnTimeStatsAsync(null, cancellationToken);

        return Ok(suppliers.Select(supplier => MapSupplierListItem(
            supplier,
            openCounts.GetValueOrDefault(supplier.Id),
            deliveryStats.GetValueOrDefault(supplier.Id, 100))).ToList());
    }

    [HttpGet("/api/suppliers/{id:guid}")]
    public async Task<ActionResult<SupplierDetailResponse>> GetSupplier(Guid id, CancellationToken cancellationToken)
    {
        // Finance reads suppliers too: a payable is recorded against one.
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewMaterials, PermissionNames.CanManageMaterials, PermissionNames.FinanceView);

        var supplier = await dbContext.Suppliers
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Supplier not found.");

        var orders = await dbContext.PurchaseOrders
            .Include(x => x.Supplier)
            .Include(x => x.Lines)
            .Where(x => x.TenantId == TenantId && x.SupplierId == id)
            .OrderByDescending(x => x.OrderedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var openCount = orders.Count(x => x.Status != "Received" && x.Status != "Cancelled");
        var deliveryStats = await ComputeOnTimeStatsAsync(id, cancellationToken);

        var suppliedParts = orders
            .SelectMany(order => order.Lines
                .Where(line => line.QuantityReceived > 0)
                .Select(line => new { line.ItemName, line.UnitCost, At = line.UpdatedAt ?? order.OrderedAt }))
            .GroupBy(x => x.ItemName)
            .Select(g =>
            {
                var latest = g.OrderByDescending(x => x.At).First();
                return new SupplierPartResponse(g.Key, latest.UnitCost, latest.At);
            })
            .OrderBy(x => x.ItemName)
            .ToList();

        return Ok(new SupplierDetailResponse(
            supplier.Id,
            supplier.SupplierName,
            supplier.ContactPerson,
            supplier.Phone,
            supplier.Email,
            deliveryStats.GetValueOrDefault(id, 100),
            openCount,
            supplier.IsActive ? "Active" : "Inactive",
            supplier.Address,
            supplier.Notes,
            suppliedParts,
            orders.Take(10).Select(MapListItem).ToList()));
    }

    [HttpPost("/api/suppliers")]
    public async Task<ActionResult<SupplierListItemResponse>> CreateSupplier(
        [FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageMaterials);

        if (string.IsNullOrWhiteSpace(request.SupplierName))
            throw new BusinessRuleException("Supplier name is required.");

        var supplier = new Supplier
        {
            TenantId = TenantId,
            SupplierName = request.SupplierName.Trim(),
            ContactPerson = request.ContactPerson?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Address = request.Address?.Trim(),
            Notes = request.Notes?.Trim(),
        };
        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.supplier.created", nameof(Supplier), supplier.Id.ToString(), $"Supplier {supplier.SupplierName} created", cancellationToken);

        return Ok(MapSupplierListItem(supplier, 0, 100));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<PurchaseOrder> LoadOrderAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.PurchaseOrders
            .Include(x => x.Supplier)
            .Include(x => x.Lines)
            .Include(x => x.ReceiptEvents)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Purchase order not found.");

    /// <summary>On-time % = received orders whose last receipt landed on or before the expected date.</summary>
    private async Task<Dictionary<Guid, double>> ComputeOnTimeStatsAsync(Guid? supplierId, CancellationToken cancellationToken)
    {
        var query = dbContext.PurchaseOrders
            .Include(x => x.ReceiptEvents)
            .Where(x => x.TenantId == TenantId && x.Status == "Received" && x.ExpectedAt != null);
        if (supplierId is not null)
            query = query.Where(x => x.SupplierId == supplierId);

        var received = await query.ToListAsync(cancellationToken);
        return received
            .GroupBy(x => x.SupplierId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var onTime = g.Count(order =>
                        order.ReceiptEvents.Count == 0
                        || order.ReceiptEvents.Max(e => e.CreatedAt) <= order.ExpectedAt);
                    return Math.Round(100.0 * onTime / g.Count(), 1);
                });
    }

    private static PurchaseOrderListItemResponse MapListItem(PurchaseOrder order) => new(
        order.Id,
        order.PoNumber,
        order.Supplier?.SupplierName ?? "Unknown",
        order.Status,
        order.OrderedAt,
        order.ExpectedAt,
        order.TotalAmount,
        order.Currency);

    private static PurchaseOrderDetailResponse MapDetail(PurchaseOrder order) => new(
        order.Id,
        order.PoNumber,
        order.Supplier?.SupplierName ?? "Unknown",
        order.Status,
        order.OrderedAt,
        order.ExpectedAt,
        order.TotalAmount,
        order.Currency,
        order.SupplierId,
        order.Notes,
        order.Lines
            .OrderBy(x => x.CreatedAt)
            .Select(x => new PurchaseOrderLineResponse(x.Id, x.ItemName, x.QuantityOrdered, x.QuantityReceived, x.UnitCost, x.LineTotal))
            .ToList(),
        order.ReceiptEvents
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PurchaseOrderReceiptEventResponse(x.CreatedAt, x.ReceivedByName, x.ItemName, x.Quantity))
            .ToList());

    private static SupplierListItemResponse MapSupplierListItem(Supplier supplier, int openOrders, double onTimePercent) => new(
        supplier.Id,
        supplier.SupplierName,
        supplier.ContactPerson,
        supplier.Phone,
        supplier.Email,
        onTimePercent,
        openOrders,
        supplier.IsActive ? "Active" : "Inactive");
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record PurchaseOrderListItemResponse(
    Guid Id, string PoNumber, string SupplierName, string Status,
    DateTime OrderedAt, DateTime? ExpectedAt, decimal TotalAmount, string Currency);

public sealed record PurchaseOrderLineResponse(
    Guid Id, string ItemName, decimal QuantityOrdered, decimal QuantityReceived, decimal UnitCost, decimal LineTotal);

public sealed record PurchaseOrderReceiptEventResponse(DateTime ReceivedAt, string ReceivedBy, string ItemName, decimal Quantity);

public sealed record PurchaseOrderDetailResponse(
    Guid Id, string PoNumber, string SupplierName, string Status,
    DateTime OrderedAt, DateTime? ExpectedAt, decimal TotalAmount, string Currency,
    Guid SupplierId, string? Notes,
    IReadOnlyList<PurchaseOrderLineResponse> Lines,
    IReadOnlyList<PurchaseOrderReceiptEventResponse> ReceivingHistory);

public sealed record CreatePurchaseOrderLineRequest(string ItemName, decimal QuantityOrdered, decimal UnitCost);

public sealed record CreatePurchaseOrderRequest(
    Guid SupplierId, DateTime? ExpectedAt, IReadOnlyList<CreatePurchaseOrderLineRequest> Lines, string? Notes = null, Guid? ProjectId = null);

public sealed record PurchaseOrderStatusRequest(string Status);

public sealed record ReceivePurchaseOrderLineRequest(Guid LineId, decimal Quantity);

public sealed record SupplierListItemResponse(
    Guid Id, string SupplierName, string? ContactPerson, string? Phone, string? Email,
    double OnTimeDeliveryPercent, int OpenPurchaseOrders, string Status);

public sealed record SupplierPartResponse(string ItemName, decimal LastUnitCost, DateTime LastSuppliedAt);

public sealed record SupplierDetailResponse(
    Guid Id, string SupplierName, string? ContactPerson, string? Phone, string? Email,
    double OnTimeDeliveryPercent, int OpenPurchaseOrders, string Status,
    string? Address, string? Notes,
    IReadOnlyList<SupplierPartResponse> SuppliedParts,
    IReadOnlyList<PurchaseOrderListItemResponse> RecentPurchaseOrders);

public sealed record CreateSupplierRequest(
    string SupplierName, string? ContactPerson = null, string? Phone = null,
    string? Email = null, string? Address = null, string? Notes = null);
