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
[Route("api/material-issues")]
public sealed class MaterialIssuesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService,
    IDocumentNumberingService documentNumberingService,
    IStockLedgerService stockLedgerService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<MaterialIssueResponse>>> List([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        EnsureMaterialsViewAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);
        var issues = await Query(scope).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return Ok(issues.Select(Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<MaterialIssueResponse>> Create([FromBody] CreateMaterialIssueRequest request, CancellationToken cancellationToken)
    {
        EnsureMaterialsRequestAccess();
        if (request.Lines.Count == 0)
        {
            throw new BusinessRuleException("At least one issue line is required.");
        }

        Guid? workOrderBranchId = null;
        if (request.WorkOrderId.HasValue)
        {
            workOrderBranchId = await dbContext.WorkOrders.Where(x => x.TenantId == TenantId && x.Id == request.WorkOrderId.Value).Select(x => x.BranchId).SingleOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleException("Work order was not found for this tenant.");
        }

        var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId ?? workOrderBranchId, cancellationToken);
        var issue = new MaterialIssue
        {
            TenantId = TenantId,
            BranchId = branchId,
            WorkOrderId = request.WorkOrderId,
            AssetId = request.AssetId,
            ClientId = request.ClientId,
            SiteId = request.SiteId,
            SystemId = request.SystemId,
            IssuedToUserId = request.IssuedToUserId,
            IssueNumber = await documentNumberingService.GenerateAsync(TenantId, branchId, "MaterialIssue", cancellationToken),
            IssueStatus = "draft",
            Notes = Normalize(request.Notes),
            RequestedByUserId = UserId
        };

        var materialIds = request.Lines.Select(x => x.MaterialItemId).Distinct().ToList();
        var materials = await dbContext.MaterialItems.Where(x => x.TenantId == TenantId && materialIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        foreach (var line in request.Lines)
        {
            if (line.QuantityRequested <= 0)
            {
                throw new BusinessRuleException("Quantity requested must be greater than zero.");
            }

            if (!materials.TryGetValue(line.MaterialItemId, out var material))
            {
                throw new BusinessRuleException("Material item was not found for this tenant.");
            }

            issue.Lines.Add(new MaterialIssueLine
            {
                TenantId = TenantId,
                MaterialItemId = material.Id,
                QuantityRequested = line.QuantityRequested,
                QuantityIssued = line.QuantityIssued ?? 0,
                Unit = material.UnitOfMeasure,
                UnitCost = material.UnitCost,
                Notes = Normalize(line.Notes)
            });
        }

        issue.TotalEstimatedCost = issue.Lines.Sum(x => x.QuantityRequested * (x.UnitCost ?? 0));
        dbContext.MaterialIssues.Add(issue);
        await dbContext.SaveChangesAsync(cancellationToken);

        var approvalRule = await FindApplicableApprovalRuleAsync(issue.TotalEstimatedCost, cancellationToken);
        if (approvalRule is not null)
        {
            issue.IssueStatus = "pending_approval";
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (IsAdmin || HasPermission(PermissionNames.CanIssueMaterials))
        {
            await IssueInternalAsync(issue, true, cancellationToken);
        }

        await auditLogService.LogAsync(TenantId, UserId, "material_issue.created", nameof(MaterialIssue), issue.Id.ToString(), $"Created material issue '{issue.IssueNumber}'.", cancellationToken);
        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, issue.BranchId, cancellationToken)).SingleAsync(x => x.Id == issue.Id, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = issue.Id }, Map(persisted));
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<MaterialIssueResponse>> Approve(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanApproveMaterials);
        var issue = await dbContext.MaterialIssues.Include(x => x.Lines).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material issue was not found.");
        issue.IssueStatus = "approved";
        issue.ApprovedByUserId = UserId;
        issue.ApprovedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "material_issue.approved", nameof(MaterialIssue), issue.Id.ToString(), $"Approved material issue '{issue.IssueNumber}'.", cancellationToken);
        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, issue.BranchId, cancellationToken)).SingleAsync(x => x.Id == issue.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<MaterialIssueResponse>> Reject(Guid id, [FromBody] RejectMaterialIssueRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanApproveMaterials);
        var issue = await dbContext.MaterialIssues.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material issue was not found.");
        issue.IssueStatus = "rejected";
        issue.RejectedByUserId = UserId;
        issue.RejectedAt = DateTime.UtcNow;
        issue.RejectionReason = Normalize(request.Reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "material_issue.rejected", nameof(MaterialIssue), issue.Id.ToString(), $"Rejected material issue '{issue.IssueNumber}'.", cancellationToken);
        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, issue.BranchId, cancellationToken)).SingleAsync(x => x.Id == issue.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/issue")]
    public async Task<ActionResult<MaterialIssueResponse>> Issue(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanIssueMaterials);
        var issue = await dbContext.MaterialIssues.Include(x => x.Lines).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material issue was not found.");
        await IssueInternalAsync(issue, false, cancellationToken);
        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, issue.BranchId, cancellationToken)).SingleAsync(x => x.Id == issue.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/return")]
    public async Task<ActionResult<MaterialIssueResponse>> Return(Guid id, [FromBody] ReturnMaterialIssueRequest request, CancellationToken cancellationToken)
    {
        EnsureMaterialsRequestAccess();
        var issue = await dbContext.MaterialIssues.Include(x => x.Lines).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material issue was not found.");
        if (!string.Equals(issue.IssueStatus, "issued", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Only issued material can be returned.");
        }

        await stockLedgerService.ExecuteAsync(async ct =>
        {
            foreach (var line in request.Lines)
            {
                var issueLine = issue.Lines.SingleOrDefault(x => x.Id == line.MaterialIssueLineId)
                    ?? throw new BusinessRuleException("Material issue line was not found.");
                if (line.QuantityReturned <= 0 || issueLine.QuantityReturned + line.QuantityReturned > issueLine.QuantityIssued)
                {
                    throw new BusinessRuleException("Returned quantity must be positive and cannot exceed the issued quantity.");
                }
                issueLine.QuantityReturned += line.QuantityReturned;
                var material = await dbContext.MaterialItems.SingleAsync(x => x.TenantId == TenantId && x.Id == issueLine.MaterialItemId, ct);
                await stockLedgerService.RecordReturnAsync(material, issue.BranchId, line.QuantityReturned, UserId, $"Returned from material issue {issue.IssueNumber}", issue.IssueNumber, issue.WorkOrderId, null, ct);
            }

            issue.IssueStatus = "returned";
            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "material_issue.returned", nameof(MaterialIssue), issue.Id.ToString(), $"Returned material issue '{issue.IssueNumber}'.", cancellationToken);
        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, issue.BranchId, cancellationToken)).SingleAsync(x => x.Id == issue.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<MaterialIssueResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        EnsureMaterialsRequestAccess();
        var issue = await dbContext.MaterialIssues.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Material issue was not found.");
        if (issue.IssueStatus is not "draft" and not "pending_approval")
        {
            throw new BusinessRuleException("Only draft or pending approval issues can be cancelled.");
        }

        issue.IssueStatus = "cancelled";
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "material_issue.cancelled", nameof(MaterialIssue), issue.Id.ToString(), $"Cancelled material issue '{issue.IssueNumber}'.", cancellationToken);
        var persisted = await Query(await branchAccessService.GetQueryScopeAsync(TenantId, issue.BranchId, cancellationToken)).SingleAsync(x => x.Id == issue.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    private async Task IssueInternalAsync(MaterialIssue issue, bool autoIssue, CancellationToken cancellationToken)
    {
        if (!autoIssue && issue.IssueStatus is not "approved" and not "draft")
        {
            throw new BusinessRuleException("Only approved or draft issues can be issued.");
        }

        var allowNegativeStock = IsAdmin || HasPermission(PermissionNames.MaterialsOverrideStock);
        await stockLedgerService.ExecuteAsync(async ct =>
        {
            var materialIds = issue.Lines.Select(x => x.MaterialItemId).Distinct().ToList();
            var materials = await dbContext.MaterialItems.Where(x => x.TenantId == TenantId && materialIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            foreach (var line in issue.Lines)
            {
                if (!materials.TryGetValue(line.MaterialItemId, out var material))
                {
                    throw new BusinessRuleException("Material item was not found for this tenant.");
                }

                var quantityToIssue = line.QuantityIssued > 0 ? line.QuantityIssued : line.QuantityRequested;
                if (quantityToIssue <= 0)
                {
                    continue;
                }

                await stockLedgerService.RecordIssueAsync(material, issue.BranchId, quantityToIssue, UserId, $"Issued via material issue {issue.IssueNumber}", issue.IssueNumber, issue.WorkOrderId, null, allowNegativeStock, ct);
                line.QuantityIssued = quantityToIssue;
                line.UnitCost ??= material.UnitCost;

                if (issue.WorkOrderId.HasValue)
                {
                    dbContext.WorkOrderMaterialUsages.Add(new WorkOrderMaterialUsage
                    {
                        TenantId = TenantId,
                        WorkOrderId = issue.WorkOrderId.Value,
                        MaterialItemId = material.Id,
                        AssetId = issue.AssetId,
                        QuantityUsed = quantityToIssue,
                        UnitCost = line.UnitCost,
                        Chargeable = true,
                        Notes = issue.Notes,
                        UsedByUserId = UserId,
                        UsedAt = DateTime.UtcNow
                    });
                }
            }

            issue.IssueStatus = "issued";
            issue.IssuedByUserId = UserId;
            issue.IssuedAt = DateTime.UtcNow;
            issue.TotalIssuedCost = issue.Lines.Sum(x => x.QuantityIssued * (x.UnitCost ?? 0));
            await dbContext.SaveChangesAsync(ct);
        }, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, autoIssue ? "material_issue.issued_auto" : "material_issue.issued", nameof(MaterialIssue), issue.Id.ToString(), $"Issued material issue '{issue.IssueNumber}'.", cancellationToken);
    }

    private async Task<FinanceApprovalRule?> FindApplicableApprovalRuleAsync(decimal amount, CancellationToken cancellationToken) =>
        await dbContext.FinanceApprovalRules
            .Where(x => x.TenantId == TenantId && x.IsActive && x.ApprovalArea == "material_issue" && amount >= x.ThresholdAmount)
            .OrderBy(x => x.Sequence)
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<MaterialIssue> Query(BranchQueryScope scope) =>
        dbContext.MaterialIssues
            .Include(x => x.Branch)
            .Include(x => x.WorkOrder)
            .Include(x => x.Asset)
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.System)
            .Include(x => x.RequestedByUser)
            .Include(x => x.IssuedByUser)
            .Include(x => x.ApprovedByUser)
            .Include(x => x.IssuedToUser)
            .Include(x => x.Lines).ThenInclude(x => x.MaterialItem)
            .Where(x => x.TenantId == TenantId)
            .WhereAccessible(scope, x => x.BranchId);

    private static MaterialIssueResponse Map(MaterialIssue issue) =>
        new(
            issue.Id,
            issue.IssueNumber,
            issue.BranchId,
            issue.Branch?.Name,
            issue.WorkOrderId,
            issue.WorkOrder?.WorkOrderNumber,
            issue.AssetId,
            issue.Asset?.AssetName,
            issue.ClientId,
            issue.Client?.ClientName,
            issue.SiteId,
            issue.Site?.SiteName,
            issue.SystemId,
            issue.System?.SystemName,
            issue.IssuedToUserId,
            issue.IssuedToUser?.FullName,
            issue.RequestedByUserId,
            issue.RequestedByUser?.FullName,
            issue.IssuedByUserId,
            issue.IssuedByUser?.FullName,
            issue.ApprovedByUserId,
            issue.ApprovedByUser?.FullName,
            issue.IssueStatus,
            issue.Notes,
            issue.IssuedAt,
            issue.ApprovedAt,
            issue.RejectedAt,
            issue.RejectionReason,
            issue.TotalEstimatedCost,
            issue.TotalIssuedCost,
            issue.CreatedAt,
            issue.Lines.Select(line => new MaterialIssueLineResponse(line.Id, line.MaterialItemId, line.MaterialItem?.ItemName, line.MaterialItem?.ItemCode, line.QuantityRequested, line.QuantityIssued, line.QuantityReturned, line.Unit, line.UnitCost, line.Notes)).ToList());

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureMaterialsViewAccess()
    {
        userAccessService.EnsureAdminOrAnyPermission(
            PermissionNames.CanViewMaterials,
            PermissionNames.CanRequestMaterials,
            PermissionNames.CanManageMaterials,
            PermissionNames.CanApproveMaterials,
            PermissionNames.CanIssueMaterials);
    }

    private void EnsureMaterialsRequestAccess()
    {
        userAccessService.EnsureAdminOrAnyPermission(
            PermissionNames.CanRequestMaterials,
            PermissionNames.CanManageMaterials,
            PermissionNames.CanIssueMaterials);
    }
}

public sealed record CreateMaterialIssueRequest(
    Guid? BranchId,
    Guid? WorkOrderId,
    Guid? AssetId,
    Guid? ClientId,
    Guid? SiteId,
    Guid? SystemId,
    Guid? IssuedToUserId,
    string? Notes,
    IReadOnlyCollection<CreateMaterialIssueLineRequest> Lines);

public sealed record CreateMaterialIssueLineRequest(Guid MaterialItemId, decimal QuantityRequested, decimal? QuantityIssued, string? Notes);
public sealed record RejectMaterialIssueRequest(string? Reason);
public sealed record ReturnMaterialIssueRequest(IReadOnlyCollection<ReturnMaterialIssueLineRequest> Lines);
public sealed record ReturnMaterialIssueLineRequest(Guid MaterialIssueLineId, decimal QuantityReturned);

public sealed record MaterialIssueResponse(
    Guid Id,
    string IssueNumber,
    Guid? BranchId,
    string? BranchName,
    Guid? WorkOrderId,
    string? WorkOrderNumber,
    Guid? AssetId,
    string? AssetName,
    Guid? ClientId,
    string? ClientName,
    Guid? SiteId,
    string? SiteName,
    Guid? SystemId,
    string? SystemName,
    Guid? IssuedToUserId,
    string? IssuedToUserName,
    Guid RequestedByUserId,
    string? RequestedByUserName,
    Guid? IssuedByUserId,
    string? IssuedByUserName,
    Guid? ApprovedByUserId,
    string? ApprovedByUserName,
    string IssueStatus,
    string? Notes,
    DateTime? IssuedAt,
    DateTime? ApprovedAt,
    DateTime? RejectedAt,
    string? RejectionReason,
    decimal TotalEstimatedCost,
    decimal TotalIssuedCost,
    DateTime CreatedAt,
    IReadOnlyCollection<MaterialIssueLineResponse> Lines);

public sealed record MaterialIssueLineResponse(
    Guid Id,
    Guid MaterialItemId,
    string? MaterialItemName,
    string? ItemCode,
    decimal QuantityRequested,
    decimal QuantityIssued,
    decimal QuantityReturned,
    string Unit,
    decimal? UnitCost,
    string? Notes);
