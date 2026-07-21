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
[Route("api/settings/escalation-rules")]
public sealed class EscalationRulesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EscalationRuleResponse>>> GetAll(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var rules = await dbContext.EscalationRules
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(rules.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EscalationRuleResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var rule = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("Escalation rule was not found.");

        return Ok(Map(rule));
    }

    [HttpPost]
    public async Task<ActionResult<EscalationRuleResponse>> Create([FromBody] UpsertEscalationRuleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        await ValidateAsync(request, cancellationToken);

        var rule = new EscalationRule
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            IsActive = request.IsActive,
            TriggerCondition = request.TriggerCondition,
            TriggerValue = request.TriggerValue,
            Action = request.Action,
            ActionValue = string.IsNullOrWhiteSpace(request.ActionValue) ? null : request.ActionValue.Trim(),
            CooldownMins = request.CooldownMins,
        };

        dbContext.EscalationRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "Escalation rule created", nameof(EscalationRule), rule.Id.ToString(), $"Created escalation rule '{rule.Name}'.", cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = rule.Id }, Map(rule));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EscalationRuleResponse>> Update(Guid id, [FromBody] UpsertEscalationRuleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        await ValidateAsync(request, cancellationToken);

        var rule = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("Escalation rule was not found.");

        rule.Name = request.Name.Trim();
        rule.IsActive = request.IsActive;
        rule.TriggerCondition = request.TriggerCondition;
        rule.TriggerValue = request.TriggerValue;
        rule.Action = request.Action;
        rule.ActionValue = string.IsNullOrWhiteSpace(request.ActionValue) ? null : request.ActionValue.Trim();
        rule.CooldownMins = request.CooldownMins;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "Escalation rule updated", nameof(EscalationRule), rule.Id.ToString(), $"Updated escalation rule '{rule.Name}'.", cancellationToken);

        return Ok(Map(rule));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var rule = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("Escalation rule was not found.");

        dbContext.EscalationRules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "Escalation rule deleted", nameof(EscalationRule), rule.Id.ToString(), $"Deleted escalation rule '{rule.Name}'.", cancellationToken);

        return NoContent();
    }

    private Task<EscalationRule?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.EscalationRules.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);

    private async Task ValidateAsync(UpsertEscalationRuleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Rule name is required.");
        }

        if (!EscalationConditions.All.Contains(request.TriggerCondition))
        {
            throw new BusinessRuleException("Invalid trigger condition.");
        }

        if (request.TriggerValue <= 0)
        {
            throw new BusinessRuleException("Trigger value must be greater than zero.");
        }

        if (!EscalationActions.All.Contains(request.Action))
        {
            throw new BusinessRuleException("Invalid action.");
        }

        if (request.CooldownMins < 0)
        {
            throw new BusinessRuleException("Cooldown minutes must be zero or greater.");
        }

        if (string.Equals(request.Action, EscalationActions.ReassignToUserId, StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(request.ActionValue, out var userId) ||
                !await dbContext.Users.AnyAsync(x => x.TenantId == TenantId && x.Id == userId, cancellationToken))
            {
                throw new BusinessRuleException("A valid user must be selected for the reassign action.");
            }
        }

        if (string.Equals(request.Action, EscalationActions.ChangePriority, StringComparison.OrdinalIgnoreCase)
            && !WorkOrderPriorities.All.Contains(request.ActionValue ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("A valid priority must be selected for the change priority action.");
        }
    }

    private static EscalationRuleResponse Map(EscalationRule rule) =>
        new(
            rule.Id,
            rule.Name,
            rule.IsActive,
            rule.TriggerCondition,
            rule.TriggerValue,
            rule.Action,
            rule.ActionValue,
            rule.CooldownMins,
            rule.CreatedAt,
            rule.UpdatedAt);
}

public sealed record UpsertEscalationRuleRequest(
    string Name,
    bool IsActive,
    string TriggerCondition,
    int TriggerValue,
    string Action,
    string? ActionValue,
    int CooldownMins);

public sealed record EscalationRuleResponse(
    Guid Id,
    string Name,
    bool IsActive,
    string TriggerCondition,
    int TriggerValue,
    string Action,
    string? ActionValue,
    int CooldownMins,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
