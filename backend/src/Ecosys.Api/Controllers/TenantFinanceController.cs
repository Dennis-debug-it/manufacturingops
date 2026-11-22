using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/finance")]
public sealed class TenantFinanceController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("overview")]
    public async Task<ActionResult<TenantFinanceOverviewResponse>> GetOverview(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceView);

        var approvalRules = await dbContext.FinanceApprovalRules.Where(x => x.TenantId == TenantId && x.IsActive).ToListAsync(cancellationToken);
        var materialIssues = await dbContext.MaterialIssues.Where(x => x.TenantId == TenantId).ToListAsync(cancellationToken);

        return Ok(new TenantFinanceOverviewResponse(
            approvalRules.Count,
            materialIssues.Count(x => string.Equals(x.IssueStatus, "pending_approval", StringComparison.OrdinalIgnoreCase))));
    }

    [HttpGet("summary")]
    public Task<ActionResult<TenantFinanceOverviewResponse>> GetSummary(CancellationToken cancellationToken) =>
        GetOverview(cancellationToken);

    // ── Approval Rules ───────────────────────────────────────────────────────

    [HttpGet("approvals")]
    public async Task<ActionResult<IReadOnlyCollection<FinanceApprovalRuleResponse>>> ListApprovalRules(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceViewApprovals);
        var rules = await dbContext.FinanceApprovalRules
            .Include(x => x.RequiredApproverUser)
            .Include(x => x.RequiredAssignmentGroup)
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.ApprovalArea)
            .ThenBy(x => x.Sequence)
            .ToListAsync(cancellationToken);
        return Ok(rules.Select(MapRule).ToList());
    }

    [HttpGet("approval-rules")]
    public Task<ActionResult<IReadOnlyCollection<FinanceApprovalRuleResponse>>> ListApprovalRulesAlias(CancellationToken cancellationToken) =>
        ListApprovalRules(cancellationToken);

    [HttpPost("approvals")]
    public async Task<ActionResult<FinanceApprovalRuleResponse>> CreateApprovalRule([FromBody] UpsertFinanceApprovalRuleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageApprovals);
        var entity = new FinanceApprovalRule
        {
            TenantId = TenantId,
            ApprovalArea = NormalizeApprovalArea(request.ApprovalArea),
            ThresholdAmount = request.ThresholdAmount,
            Currency = NormalizeCurrency(request.Currency),
            RequiredRole = request.RequiredRole.Trim(),
            RequiredApproverUserId = request.RequiredApproverUserId,
            RequiredAssignmentGroupId = request.RequiredAssignmentGroupId,
            Sequence = request.Sequence,
            IsActive = request.IsActive
        };
        dbContext.FinanceApprovalRules.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(entity).Reference(x => x.RequiredApproverUser).LoadAsync(cancellationToken);
        await dbContext.Entry(entity).Reference(x => x.RequiredAssignmentGroup).LoadAsync(cancellationToken);
        return CreatedAtAction(nameof(ListApprovalRules), new { id = entity.Id }, MapRule(entity));
    }

    [HttpPost("approval-rules")]
    public Task<ActionResult<FinanceApprovalRuleResponse>> CreateApprovalRuleAlias([FromBody] UpsertFinanceApprovalRuleRequest request, CancellationToken cancellationToken) =>
        CreateApprovalRule(request, cancellationToken);

    [HttpPut("approvals/{id:guid}")]
    public async Task<ActionResult<FinanceApprovalRuleResponse>> UpdateApprovalRule(Guid id, [FromBody] UpsertFinanceApprovalRuleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.FinanceManageApprovals);
        var entity = await dbContext.FinanceApprovalRules.Include(x => x.RequiredApproverUser).Include(x => x.RequiredAssignmentGroup)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Approval rule was not found.");
        entity.ApprovalArea = NormalizeApprovalArea(request.ApprovalArea);
        entity.ThresholdAmount = request.ThresholdAmount;
        entity.Currency = NormalizeCurrency(request.Currency);
        entity.RequiredRole = request.RequiredRole.Trim();
        entity.RequiredApproverUserId = request.RequiredApproverUserId;
        entity.RequiredAssignmentGroupId = request.RequiredAssignmentGroupId;
        entity.Sequence = request.Sequence;
        entity.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapRule(entity));
    }

    [HttpPut("approval-rules/{id:guid}")]
    public Task<ActionResult<FinanceApprovalRuleResponse>> UpdateApprovalRuleAlias(Guid id, [FromBody] UpsertFinanceApprovalRuleRequest request, CancellationToken cancellationToken) =>
        UpdateApprovalRule(id, request, cancellationToken);

    // ── Private helpers ──────────────────────────────────────────────────────

    private static FinanceApprovalRuleResponse MapRule(FinanceApprovalRule rule) =>
        new(rule.Id, rule.TenantId, rule.ApprovalArea, rule.ThresholdAmount, rule.Currency, rule.RequiredRole, rule.RequiredApproverUserId, rule.RequiredApproverUser?.FullName, rule.RequiredAssignmentGroupId, rule.RequiredAssignmentGroup?.Name, rule.Sequence, rule.IsActive, rule.CreatedAt);

    private static string NormalizeApprovalArea(string value) => value.Trim().ToLowerInvariant() switch
    {
        "material_issue" => value.Trim().ToLowerInvariant(),
        _ => throw new BusinessRuleException("Invalid approval area.")
    };

    private static string NormalizeCurrency(string value) => string.IsNullOrWhiteSpace(value) ? "KES" : value.Trim().ToUpperInvariant();

}

public sealed record TenantFinanceOverviewResponse(
    int ApprovalRulesCount,
    int PendingMaterialIssueApprovalsCount);

public sealed record UpsertFinanceApprovalRuleRequest(
    string ApprovalArea,
    decimal ThresholdAmount,
    string Currency,
    string RequiredRole,
    Guid? RequiredApproverUserId,
    Guid? RequiredAssignmentGroupId,
    int Sequence,
    bool IsActive);

public sealed record FinanceApprovalRuleResponse(
    Guid Id,
    Guid TenantId,
    string ApprovalArea,
    decimal ThresholdAmount,
    string Currency,
    string RequiredRole,
    Guid? RequiredApproverUserId,
    string? RequiredApproverUserName,
    Guid? RequiredAssignmentGroupId,
    string? RequiredAssignmentGroupName,
    int Sequence,
    bool IsActive,
    DateTime CreatedAt);
