using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>Lead assignment rules (Settings › Sales). Everyone with CRM access can read them; admins change them.</summary>
public sealed partial class SalesCrmController
{
    [HttpGet("assignment-rules")]
    public async Task<ActionResult<IReadOnlyList<SalesAssignmentRuleResponse>>> ListAssignmentRules(CancellationToken cancellationToken)
    {
        EnsureCanView();
        var rules = await dbContext.SalesAssignmentRules.AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var ownerIds = rules.SelectMany(x => SalesAssignmentRules.ParseOwners(x.OwnerUserIds)).Distinct().ToList();
        var names = await dbContext.Users.AsNoTracking().Where(x => x.TenantId == TenantId && ownerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        return Ok(rules.Select(rule => new SalesAssignmentRuleResponse(
            rule.Id, rule.Name, rule.SortOrder, rule.IsActive, rule.Source, rule.ServiceType, rule.LocationContains, rule.Method,
            SalesAssignmentRules.ParseOwners(rule.OwnerUserIds).Select(id => new SalesOwnerRef(id, names.GetValueOrDefault(id, "Former user"))).ToList())).ToList());
    }

    [HttpPost("assignment-rules")]
    public async Task<ActionResult<IReadOnlyList<SalesAssignmentRuleResponse>>> CreateAssignmentRule([FromBody] UpsertSalesAssignmentRuleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var rule = new SalesAssignmentRule
        {
            TenantId = TenantId,
            SortOrder = (await dbContext.SalesAssignmentRules.Where(x => x.TenantId == TenantId).MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0) + 10,
        };
        await ApplyRuleAsync(rule, request, cancellationToken);
        dbContext.SalesAssignmentRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListAssignmentRules(cancellationToken);
    }

    [HttpPut("assignment-rules/{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<SalesAssignmentRuleResponse>>> UpdateAssignmentRule(Guid id, [FromBody] UpsertSalesAssignmentRuleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var rule = await dbContext.SalesAssignmentRules.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Rule not found.");
        await ApplyRuleAsync(rule, request, cancellationToken);
        if (request.SortOrder.HasValue) rule.SortOrder = request.SortOrder.Value;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListAssignmentRules(cancellationToken);
    }

    [HttpDelete("assignment-rules/{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<SalesAssignmentRuleResponse>>> DeleteAssignmentRule(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var rule = await dbContext.SalesAssignmentRules.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Rule not found.");
        dbContext.SalesAssignmentRules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListAssignmentRules(cancellationToken);
    }

    private async Task ApplyRuleAsync(SalesAssignmentRule rule, UpsertSalesAssignmentRuleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Give the rule a name.");
        var owners = (request.OwnerUserIds ?? []).Distinct().ToList();
        if (owners.Count == 0)
            throw new BusinessRuleException("Pick at least one person for the rule to assign to.");
        var eligible = await SalesCrmAccess.ListEligibleOwnersAsync(dbContext, rolePermissionService, TenantId, cancellationToken);
        if (owners.Any(id => eligible.All(x => x.Id != id)))
            throw new BusinessRuleException("Everyone in a rule must be an active user who can manage the sales pipeline.");

        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        rule.Name = request.Name.Trim();
        rule.IsActive = request.IsActive;
        rule.Source = Clean(request.Source);
        rule.ServiceType = Clean(request.ServiceType);
        rule.LocationContains = Clean(request.LocationContains);
        rule.Method = string.Equals(request.Method, SalesAssignmentRules.RoundRobin, StringComparison.OrdinalIgnoreCase) ? SalesAssignmentRules.RoundRobin : SalesAssignmentRules.Fixed;
        var joined = string.Join(',', owners);
        if (rule.OwnerUserIds != joined) rule.LastAssignedIndex = -1;
        rule.OwnerUserIds = joined;
    }
}

public sealed record SalesOwnerRef(Guid Id, string FullName);

public sealed record SalesAssignmentRuleResponse(
    Guid Id, string Name, int SortOrder, bool IsActive, string? Source, string? ServiceType, string? LocationContains, string Method,
    IReadOnlyList<SalesOwnerRef> Owners);

public sealed record UpsertSalesAssignmentRuleRequest(
    [StringLength(120)] string Name,
    bool IsActive,
    [StringLength(60)] string? Source,
    [StringLength(50)] string? ServiceType,
    [StringLength(120)] string? LocationContains,
    [StringLength(20)] string? Method,
    IReadOnlyList<Guid>? OwnerUserIds,
    int? SortOrder = null);
