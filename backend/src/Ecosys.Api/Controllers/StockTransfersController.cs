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
[Route("api/stock-transfers")]
public sealed class StockTransfersController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IStockLedgerService stockLedgerService,
    IDocumentNumberingService documentNumberingService) : TenantAwareControllerBase(tenantContext)
{
    [HttpPost]
    public async Task<ActionResult<StockTransferResponse>> Create([FromBody] CreateStockTransferRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        Validate(request);
        await EnsureTransferAccessAsync(request.FromBranchId, request.ToBranchId, cancellationToken);

        var material = await dbContext.MaterialItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == request.MaterialId, cancellationToken)
            ?? throw new BusinessRuleException("Material item was not found for this tenant.");

        var transfer = new StockTransfer
        {
            TenantId = TenantId,
            FromBranchId = request.FromBranchId,
            ToBranchId = request.ToBranchId,
            MaterialId = request.MaterialId,
            Quantity = request.Quantity,
            Status = "Submitted",
            RequestedByUserId = UserId,
            Reason = Normalize(request.Reason),
            ReferenceNumber = await documentNumberingService.GenerateAsync(TenantId, request.FromBranchId, DocumentTypes.StockTransfer, cancellationToken)
        };

        dbContext.StockTransfers.Add(transfer);
        await dbContext.SaveChangesAsync(cancellationToken);

        var persisted = await Query()
            .SingleAsync(x => x.Id == transfer.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = transfer.Id }, Map(persisted));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<StockTransferResponse>>> GetAll(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var transfers = await ApplyTransferAccess(Query())
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(transfers.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockTransferResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var transfer = await ApplyTransferAccess(Query())
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Stock transfer was not found.");

        return Ok(Map(transfer));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<StockTransferResponse>> Approve(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanIssueMaterials);

        var transfer = await ApplyTransferAccess(dbContext.StockTransfers)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Stock transfer was not found.");

        if (transfer.Status is "Cancelled" or "Completed")
        {
            throw new BusinessRuleException("Only open transfers can be approved.");
        }

        transfer.Status = "Approved";
        transfer.ApprovedByUserId = UserId;
        transfer.ApprovedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var persisted = await Query().SingleAsync(x => x.Id == transfer.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<StockTransferResponse>> Complete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanIssueMaterials);

        var transfer = await ApplyTransferAccess(dbContext.StockTransfers)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Stock transfer was not found.");

        if (transfer.Status != "Approved")
        {
            throw new BusinessRuleException("Only approved transfers can be completed.");
        }

        var material = await dbContext.MaterialItems.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == transfer.MaterialId, cancellationToken)
            ?? throw new BusinessRuleException("Material item was not found for this tenant.");

        await stockLedgerService.RecordTransferOutAsync(material, transfer.FromBranchId, transfer.Quantity, UserId, transfer.Reason, transfer.ReferenceNumber, cancellationToken);
        await stockLedgerService.RecordTransferInAsync(material, transfer.ToBranchId, transfer.Quantity, UserId, transfer.Reason, transfer.ReferenceNumber, cancellationToken);

        transfer.Status = "Completed";
        transfer.CompletedByUserId = UserId;
        transfer.CompletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var persisted = await Query().SingleAsync(x => x.Id == transfer.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<StockTransferResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();

        var transfer = await ApplyTransferAccess(dbContext.StockTransfers)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Stock transfer was not found.");

        if (transfer.Status == "Completed")
        {
            throw new BusinessRuleException("Completed transfers cannot be cancelled.");
        }

        transfer.Status = "Cancelled";
        await dbContext.SaveChangesAsync(cancellationToken);

        var persisted = await Query().SingleAsync(x => x.Id == transfer.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    private IQueryable<StockTransfer> Query() =>
        dbContext.StockTransfers
            .Include(x => x.FromBranch)
            .Include(x => x.ToBranch)
            .Include(x => x.Material)
            .Where(x => x.TenantId == TenantId);

    private IQueryable<StockTransfer> ApplyTransferAccess(IQueryable<StockTransfer> query)
    {
        if (IsAdmin)
        {
            return query.Where(x => x.TenantId == TenantId);
        }

        var user = dbContext.Users
            .Include(x => x.BranchAssignments)
            .Single(x => x.TenantId == TenantId && x.Id == UserId);

        if (user.HasAllBranchAccess)
        {
            return query.Where(x => x.TenantId == TenantId);
        }

        var branchIds = user.BranchAssignments.Select(x => x.BranchId).ToList();
        return query.Where(x => x.TenantId == TenantId && branchIds.Contains(x.FromBranchId) && branchIds.Contains(x.ToBranchId));
    }

    private async Task EnsureTransferAccessAsync(Guid fromBranchId, Guid toBranchId, CancellationToken cancellationToken)
    {
        if (fromBranchId == toBranchId)
        {
            throw new BusinessRuleException("Cannot transfer stock to the same branch.");
        }

        await branchAccessService.EnsureCanAccessBranchAsync(TenantId, fromBranchId, cancellationToken);
        await branchAccessService.EnsureCanAccessBranchAsync(TenantId, toBranchId, cancellationToken);
    }

    private static void Validate(CreateStockTransferRequest request)
    {
        if (request.FromBranchId == request.ToBranchId)
        {
            throw new BusinessRuleException("Cannot transfer stock to the same branch.");
        }

        if (request.Quantity <= 0)
        {
            throw new BusinessRuleException("Quantity must be greater than zero.");
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StockTransferResponse Map(StockTransfer transfer) =>
        new(
            transfer.Id,
            transfer.FromBranchId,
            transfer.FromBranch?.Name,
            transfer.ToBranchId,
            transfer.ToBranch?.Name,
            transfer.MaterialId,
            transfer.Material?.ItemName,
            transfer.Quantity,
            transfer.Status,
            transfer.RequestedByUserId,
            transfer.ApprovedByUserId,
            transfer.CompletedByUserId,
            transfer.Reason,
            transfer.ReferenceNumber,
            transfer.CreatedAt,
            transfer.ApprovedAt,
            transfer.CompletedAt);
}

public sealed record CreateStockTransferRequest(Guid FromBranchId, Guid ToBranchId, Guid MaterialId, decimal Quantity, string? Reason);

public sealed record StockTransferResponse(
    Guid Id,
    Guid FromBranchId,
    string? FromBranchName,
    Guid ToBranchId,
    string? ToBranchName,
    Guid MaterialId,
    string? MaterialName,
    decimal Quantity,
    string Status,
    Guid RequestedByUserId,
    Guid? ApprovedByUserId,
    Guid? CompletedByUserId,
    string? Reason,
    string? ReferenceNumber,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    DateTime? CompletedAt);
