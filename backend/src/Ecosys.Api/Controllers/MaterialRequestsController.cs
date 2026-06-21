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
[Route("api/material-requests")]
public sealed class MaterialRequestsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IStockLedgerService stockLedgerService,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IDocumentNumberingService documentNumberingService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<MaterialRequestResponse>>> GetAll([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);

        var requests = await Query(scope)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(requests.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaterialRequestResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var request = await Query(scope).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material request was not found.");

        return Ok(Map(request));
    }

    [HttpPost]
    public async Task<ActionResult<MaterialRequestResponse>> Create([FromBody] CreateMaterialRequestRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();

        if (request.Lines.Count == 0)
        {
            throw new BusinessRuleException("At least one material line is required.");
        }

        var workOrderScope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);
        var workOrder = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.Id == request.WorkOrderId)
            .WhereAccessible(workOrderScope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Work order was not found for this tenant.");

        var materialRequest = new MaterialRequest
        {
            TenantId = TenantId,
            BranchId = workOrder.BranchId,
            WorkOrderId = request.WorkOrderId,
            RequestedByUserId = UserId,
            RequestNumber = await documentNumberingService.GenerateAsync(TenantId, workOrder.BranchId, DocumentTypes.MaterialRequest, cancellationToken),
            Status = "Pending",
            Lines = new List<MaterialRequestLine>()
        };

        foreach (var line in request.Lines)
        {
            var materialItem = await dbContext.MaterialItems.SingleOrDefaultAsync(
                x => x.TenantId == TenantId && x.Id == line.MaterialItemId,
                cancellationToken) ?? throw new BusinessRuleException("Material item was not found for this tenant.");

            if (line.QuantityRequested <= 0)
            {
                throw new BusinessRuleException("Quantity requested must be greater than zero.");
            }

            materialRequest.Lines.Add(new MaterialRequestLine
            {
                MaterialItemId = materialItem.Id,
                QuantityRequested = line.QuantityRequested
            });
        }

        dbContext.MaterialRequests.Add(materialRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, materialRequest.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == materialRequest.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = materialRequest.Id }, Map(persisted));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<MaterialRequestResponse>> Approve(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanApproveMaterials);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var materialRequest = await dbContext.MaterialRequests
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Material request was not found.");

        materialRequest.Status = "Approved";
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Material request approved",
            nameof(MaterialRequest),
            materialRequest.Id.ToString(),
            $"Approved material request '{materialRequest.RequestNumber}'.",
            cancellationToken);

        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, materialRequest.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == materialRequest.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/issue")]
    public async Task<ActionResult<MaterialRequestResponse>> Issue(Guid id, [FromBody] IssueMaterialRequestRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanIssueMaterials);
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var materialRequest = await dbContext.MaterialRequests
            .Include(x => x.WorkOrder)
            .Include(x => x.Lines)
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Material request was not found.");

        foreach (var issueLine in request.Lines)
        {
            var line = materialRequest.Lines.SingleOrDefault(x => x.Id == issueLine.MaterialRequestLineId)
                ?? throw new BusinessRuleException("Material request line was not found.");

            var item = await dbContext.MaterialItems.SingleAsync(x => x.TenantId == TenantId && x.Id == line.MaterialItemId, cancellationToken);
            var quantityToIssue = issueLine.QuantityIssued <= 0 ? line.QuantityRequested - line.QuantityIssued : issueLine.QuantityIssued;

            if (quantityToIssue <= 0)
            {
                continue;
            }

            line.QuantityIssued += quantityToIssue;
            await stockLedgerService.RecordIssueAsync(
                item,
                materialRequest.BranchId,
                quantityToIssue,
                UserId,
                $"Issued against material request {materialRequest.RequestNumber}",
                materialRequest.RequestNumber,
                materialRequest.WorkOrderId,
                materialRequest.Id,
                false,
                cancellationToken);
        }

        materialRequest.Status = "Issued";
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Material request issued",
            nameof(MaterialRequest),
            materialRequest.Id.ToString(),
            $"Issued stock for material request '{materialRequest.RequestNumber}'.",
            cancellationToken);

        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, materialRequest.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == materialRequest.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<MaterialRequestResponse>> Close(Guid id, [FromBody] CloseMaterialRequestRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var materialRequest = await dbContext.MaterialRequests
            .Include(x => x.WorkOrder)
            .Include(x => x.Lines)
            .Where(x => x.TenantId == TenantId && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Material request was not found.");

        foreach (var closeLine in request.Lines)
        {
            var line = materialRequest.Lines.SingleOrDefault(x => x.Id == closeLine.MaterialRequestLineId)
                ?? throw new BusinessRuleException("Material request line was not found.");

            if (closeLine.QuantityUsed < 0 || closeLine.QuantityReturned < 0)
            {
                throw new BusinessRuleException("Used and returned quantities cannot be negative.");
            }

            if (closeLine.QuantityUsed + closeLine.QuantityReturned > line.QuantityIssued)
            {
                throw new BusinessRuleException("Used plus returned quantity cannot exceed issued quantity.");
            }

            line.QuantityUsed = closeLine.QuantityUsed;
            line.QuantityReturned = closeLine.QuantityReturned;

            if (closeLine.QuantityReturned > 0)
            {
                var item = await dbContext.MaterialItems.SingleAsync(x => x.TenantId == TenantId && x.Id == line.MaterialItemId, cancellationToken);
                await stockLedgerService.RecordReturnAsync(
                    item,
                    materialRequest.BranchId,
                    closeLine.QuantityReturned,
                    UserId,
                    $"Returned from material request {materialRequest.RequestNumber}",
                    materialRequest.RequestNumber,
                    materialRequest.WorkOrderId,
                    materialRequest.Id,
                    cancellationToken);
            }
        }

        materialRequest.Status = "Closed";
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Material request closed",
            nameof(MaterialRequest),
            materialRequest.Id.ToString(),
            $"Closed material request '{materialRequest.RequestNumber}'.",
            cancellationToken);

        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, materialRequest.BranchId, cancellationToken))
            .SingleAsync(x => x.Id == materialRequest.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    private IQueryable<MaterialRequest> Query(BranchQueryScope scope) =>
        dbContext.MaterialRequests
            .Include(x => x.Branch)
            .Include(x => x.WorkOrder)
            .Include(x => x.RequestedByUser)
            .Include(x => x.Lines)
                .ThenInclude(x => x.MaterialItem)
            .Where(x => x.TenantId == TenantId)
            .WhereAccessible(scope, x => x.BranchId);

    private static MaterialRequestResponse Map(MaterialRequest request) =>
        new(
            request.Id,
            request.RequestNumber,
            request.BranchId,
            request.Branch?.Name,
            request.WorkOrderId,
            request.WorkOrder?.WorkOrderNumber,
            request.RequestedByUserId,
            request.RequestedByUser?.FullName,
            request.Status,
            request.CreatedAt,
            request.Lines.Select(line => new MaterialRequestLineResponse(
                line.Id,
                line.MaterialItemId,
                line.MaterialItem?.ItemCode,
                line.MaterialItem?.ItemName,
                line.QuantityRequested,
                line.QuantityIssued,
                line.QuantityUsed,
                line.QuantityReturned)).ToList());
}

public sealed record CreateMaterialRequestRequest(Guid WorkOrderId, IReadOnlyCollection<CreateMaterialRequestLineRequest> Lines);

public sealed record CreateMaterialRequestLineRequest(Guid MaterialItemId, decimal QuantityRequested);

public sealed record IssueMaterialRequestRequest(IReadOnlyCollection<IssueMaterialRequestLineRequest> Lines);

public sealed record IssueMaterialRequestLineRequest(Guid MaterialRequestLineId, decimal QuantityIssued);

public sealed record CloseMaterialRequestRequest(IReadOnlyCollection<CloseMaterialRequestLineRequest> Lines);

public sealed record CloseMaterialRequestLineRequest(Guid MaterialRequestLineId, decimal QuantityUsed, decimal QuantityReturned);

public sealed record MaterialRequestResponse(
    Guid Id,
    string RequestNumber,
    Guid? BranchId,
    string? BranchName,
    Guid WorkOrderId,
    string? WorkOrderNumber,
    Guid RequestedByUserId,
    string? RequestedByName,
    string Status,
    DateTime CreatedAt,
    IReadOnlyCollection<MaterialRequestLineResponse> Lines);

public sealed record MaterialRequestLineResponse(
    Guid Id,
    Guid MaterialItemId,
    string? ItemCode,
    string? ItemName,
    decimal QuantityRequested,
    decimal QuantityIssued,
    decimal QuantityUsed,
    decimal QuantityReturned);
