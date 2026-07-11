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
[Route("api/vans")]
public sealed class VansController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IStockLedgerService stockLedgerService,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    // ── Van registry ─────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<VanResponse>>> GetAll([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewMaterials);

        var query = dbContext.Vans
            .Include(x => x.Technician)
            .Include(x => x.HomeBranch)
            .Where(x => x.TenantId == TenantId);

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var vans = await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var vanIds = vans.Select(x => x.Id).ToList();

        var itemCounts = await dbContext.VanMaterialStocks
            .Where(x => x.TenantId == TenantId && vanIds.Contains(x.VanId) && x.QuantityOnHand > 0)
            .GroupBy(x => x.VanId)
            .Select(g => new { VanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.VanId, x => x.Count, cancellationToken);

        return Ok(vans.Select(x => Map(x, itemCounts.TryGetValue(x.Id, out var count) ? count : 0)).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VanResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewMaterials);
        var van = await LoadVanAsync(id, cancellationToken);
        var itemCount = await dbContext.VanMaterialStocks.CountAsync(x => x.TenantId == TenantId && x.VanId == id && x.QuantityOnHand > 0, cancellationToken);
        return Ok(Map(van, itemCount));
    }

    [HttpPost]
    public async Task<ActionResult<VanResponse>> Create([FromBody] UpsertVanRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageMaterials);
        Validate(request);
        await ValidateReferencesAsync(request, cancellationToken);

        var van = new Van
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            RegistrationNumber = Normalize(request.RegistrationNumber),
            TechnicianId = request.TechnicianId,
            HomeBranchId = request.HomeBranchId,
            IsActive = true,
        };

        dbContext.Vans.Add(van);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "van.created", nameof(Van), van.Id.ToString(), $"Van '{van.Name}' created.", cancellationToken);

        var persisted = await LoadVanAsync(van.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = van.Id }, Map(persisted, 0));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<VanResponse>> Update(Guid id, [FromBody] UpsertVanRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageMaterials);
        Validate(request);
        await ValidateReferencesAsync(request, cancellationToken);

        var van = await LoadVanAsync(id, cancellationToken);
        van.Name = request.Name.Trim();
        van.RegistrationNumber = Normalize(request.RegistrationNumber);
        van.TechnicianId = request.TechnicianId;
        van.HomeBranchId = request.HomeBranchId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "van.updated", nameof(Van), van.Id.ToString(), $"Van '{van.Name}' updated.", cancellationToken);

        var itemCount = await dbContext.VanMaterialStocks.CountAsync(x => x.TenantId == TenantId && x.VanId == id && x.QuantityOnHand > 0, cancellationToken);
        return Ok(Map(await LoadVanAsync(id, cancellationToken), itemCount));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageMaterials);
        var van = await LoadVanAsync(id, cancellationToken);

        var hasStock = await dbContext.VanMaterialStocks.AnyAsync(x => x.TenantId == TenantId && x.VanId == id && x.QuantityOnHand > 0, cancellationToken);
        if (hasStock)
        {
            throw new BusinessRuleException("Return all stock from this van before deactivating it.");
        }

        van.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "van.deactivated", nameof(Van), van.Id.ToString(), $"Van '{van.Name}' deactivated.", cancellationToken);
        return NoContent();
    }

    // ── Van stock ────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/stock")]
    public async Task<ActionResult<IReadOnlyCollection<VanStockLineResponse>>> GetStock(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewMaterials);
        _ = await LoadVanAsync(id, cancellationToken);

        var lines = await dbContext.VanMaterialStocks
            .Include(x => x.Material)
            .Where(x => x.TenantId == TenantId && x.VanId == id)
            .OrderBy(x => x.Material!.ItemName)
            .Select(x => new VanStockLineResponse(
                x.MaterialId,
                x.Material!.ItemName,
                x.Material.ItemCode,
                x.Material.UnitOfMeasure,
                x.QuantityOnHand,
                x.ReorderLevel,
                x.UnitCost))
            .ToListAsync(cancellationToken);

        return Ok(lines);
    }

    [HttpPost("{id:guid}/load")]
    public async Task<IActionResult> Load(Guid id, [FromBody] VanStockMoveRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanIssueMaterials);
        var van = await LoadVanAsync(id, cancellationToken);
        var material = await LoadMaterialAsync(request.MaterialId, cancellationToken);

        if (!request.BranchId.HasValue)
        {
            throw new BusinessRuleException("A source branch is required to load a van.");
        }
        await EnsureBranchAsync(request.BranchId.Value, cancellationToken);

        await stockLedgerService.RecordVanLoadAsync(material, request.BranchId.Value, van.Id, request.Quantity, UserId, Normalize(request.Reason), cancellationToken: cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "van.stock.loaded", nameof(Van), van.Id.ToString(), $"Loaded {request.Quantity} x '{material.ItemName}' onto van '{van.Name}'.", cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, [FromBody] VanStockMoveRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanIssueMaterials);
        var van = await LoadVanAsync(id, cancellationToken);
        var material = await LoadMaterialAsync(request.MaterialId, cancellationToken);

        if (!request.BranchId.HasValue)
        {
            throw new BusinessRuleException("A destination branch is required to return van stock.");
        }
        await EnsureBranchAsync(request.BranchId.Value, cancellationToken);

        await stockLedgerService.RecordVanReturnAsync(material, van.Id, request.BranchId.Value, request.Quantity, UserId, Normalize(request.Reason), cancellationToken: cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "van.stock.returned", nameof(Van), van.Id.ToString(), $"Returned {request.Quantity} x '{material.ItemName}' from van '{van.Name}'.", cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/consume")]
    public async Task<IActionResult> Consume(Guid id, [FromBody] VanStockConsumeRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var van = await LoadVanAsync(id, cancellationToken);
        var material = await LoadMaterialAsync(request.MaterialId, cancellationToken);

        if (request.WorkOrderId.HasValue)
        {
            var workOrderExists = await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId && x.Id == request.WorkOrderId.Value, cancellationToken);
            if (!workOrderExists)
            {
                throw new BusinessRuleException("Work order was not found for this tenant.");
            }
        }

        await stockLedgerService.RecordVanConsumeAsync(material, van.Id, request.Quantity, UserId, Normalize(request.Reason), workOrderId: request.WorkOrderId, cancellationToken: cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "van.stock.consumed", nameof(Van), van.Id.ToString(), $"Consumed {request.Quantity} x '{material.ItemName}' from van '{van.Name}'.", cancellationToken);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Van> LoadVanAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Vans
            .Include(x => x.Technician)
            .Include(x => x.HomeBranch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Van was not found.");

    private async Task<MaterialItem> LoadMaterialAsync(Guid materialId, CancellationToken cancellationToken) =>
        await dbContext.MaterialItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == materialId, cancellationToken)
        ?? throw new BusinessRuleException("Material item was not found for this tenant.");

    private async Task EnsureBranchAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Branches.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == branchId, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Branch was not found for this tenant.");
        }
    }

    private async Task ValidateReferencesAsync(UpsertVanRequest request, CancellationToken cancellationToken)
    {
        if (request.TechnicianId.HasValue)
        {
            var exists = await dbContext.Technicians.AnyAsync(x => x.TenantId == TenantId && x.Id == request.TechnicianId.Value, cancellationToken);
            if (!exists)
            {
                throw new BusinessRuleException("Assigned technician was not found for this tenant.");
            }
        }

        if (request.HomeBranchId.HasValue)
        {
            await EnsureBranchAsync(request.HomeBranchId.Value, cancellationToken);
        }
    }

    private static void Validate(UpsertVanRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Van name is required.");
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static VanResponse Map(Van van, int itemCount) =>
        new(
            van.Id,
            van.Name,
            van.RegistrationNumber,
            van.TechnicianId,
            van.Technician?.FullName,
            van.HomeBranchId,
            van.HomeBranch?.Name,
            van.IsActive,
            itemCount,
            van.CreatedAt);
}

public sealed record UpsertVanRequest(string Name, string? RegistrationNumber, Guid? TechnicianId, Guid? HomeBranchId);

public sealed record VanResponse(
    Guid Id,
    string Name,
    string? RegistrationNumber,
    Guid? TechnicianId,
    string? TechnicianName,
    Guid? HomeBranchId,
    string? HomeBranchName,
    bool IsActive,
    int DistinctItemCount,
    DateTime CreatedAt);

public sealed record VanStockLineResponse(
    Guid MaterialId,
    string MaterialName,
    string MaterialCode,
    string UnitOfMeasure,
    decimal QuantityOnHand,
    decimal ReorderLevel,
    decimal? UnitCost);

public sealed record VanStockMoveRequest(Guid MaterialId, Guid? BranchId, decimal Quantity, string? Reason);
public sealed record VanStockConsumeRequest(Guid MaterialId, decimal Quantity, Guid? WorkOrderId, string? Reason);
