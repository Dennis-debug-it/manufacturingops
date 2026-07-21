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
[Route("api/settings/sla-policies")]
public sealed class SlaPoliciesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly (string Priority, decimal ResponseHours, decimal ResolutionHours)[] DefaultSeed =
    [
        ("Critical", 4m, 8m),
        ("High", 8m, 24m),
        ("Medium", 24m, 72m),
        ("Low", 72m, 168m)
    ];

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SlaPolicyResponse>>> GetAll(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var definition = await EnsureDefaultDefinitionAsync(cancellationToken);
        return Ok(definition.Rules.OrderBy(x => PriorityOrder(x.Priority)).Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SlaPolicyResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var rule = await LoadRuleAsync(id, cancellationToken)
            ?? throw new NotFoundException("SLA policy was not found.");

        return Ok(Map(rule));
    }

    [HttpPost]
    public async Task<ActionResult<SlaPolicyResponse>> Create([FromBody] UpsertSlaPolicyRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        Validate(request);

        var definition = await EnsureDefaultDefinitionAsync(cancellationToken);
        var priority = NormalizePriority(request.Priority);

        if (definition.Rules.Any(x => string.Equals(x.Priority, priority, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessRuleException($"A policy for priority '{priority}' already exists.");
        }

        var rule = new SlaRule
        {
            TenantId = TenantId,
            SlaDefinitionId = definition.Id,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            Priority = priority,
            ResponseTargetHours = request.ResponseTargetMinutes / 60m,
            ResolutionTargetHours = request.ResolutionTargetMinutes / 60m,
            BusinessHoursOnly = request.BusinessHoursOnly,
            BusinessHoursStart = ParseTimeOfDay(request.BusinessHoursStart),
            BusinessHoursEnd = ParseTimeOfDay(request.BusinessHoursEnd),
            Escalations = MapEscalations(request.Escalations, TenantId)
        };

        dbContext.SlaRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId, UserId, "SLA policy created", nameof(SlaRule), rule.Id.ToString(),
            $"Created SLA policy for priority '{priority}'.", cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = rule.Id }, Map(rule));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SlaPolicyResponse>> Update(Guid id, [FromBody] UpsertSlaPolicyRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        Validate(request);

        var rule = await LoadRuleAsync(id, cancellationToken)
            ?? throw new NotFoundException("SLA policy was not found.");

        var priority = NormalizePriority(request.Priority);
        var duplicateExists = await dbContext.SlaRules.AnyAsync(
            x => x.TenantId == TenantId && x.SlaDefinitionId == rule.SlaDefinitionId && x.Id != id
                && x.Priority.ToLower() == priority.ToLower(),
            cancellationToken);

        if (duplicateExists)
        {
            throw new BusinessRuleException($"A policy for priority '{priority}' already exists.");
        }

        rule.Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        rule.Priority = priority;
        rule.ResponseTargetHours = request.ResponseTargetMinutes / 60m;
        rule.ResolutionTargetHours = request.ResolutionTargetMinutes / 60m;
        rule.BusinessHoursOnly = request.BusinessHoursOnly;
        rule.BusinessHoursStart = ParseTimeOfDay(request.BusinessHoursStart);
        rule.BusinessHoursEnd = ParseTimeOfDay(request.BusinessHoursEnd);

        dbContext.SlaEscalations.RemoveRange(rule.Escalations);
        rule.Escalations = MapEscalations(request.Escalations, TenantId);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId, UserId, "SLA policy updated", nameof(SlaRule), rule.Id.ToString(),
            $"Updated SLA policy for priority '{priority}'.", cancellationToken);

        return Ok(Map(rule));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var rule = await LoadRuleAsync(id, cancellationToken)
            ?? throw new NotFoundException("SLA policy was not found.");

        dbContext.SlaRules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId, UserId, "SLA policy deleted", nameof(SlaRule), rule.Id.ToString(),
            $"Deleted SLA policy for priority '{rule.Priority}'.", cancellationToken);

        return NoContent();
    }

    private async Task<SlaDefinition> EnsureDefaultDefinitionAsync(CancellationToken cancellationToken)
    {
        var definition = await dbContext.SlaDefinitions
            .Include(x => x.Rules)
            .ThenInclude(x => x.Escalations)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsDefault, cancellationToken);

        if (definition is not null)
        {
            return definition;
        }

        definition = new SlaDefinition
        {
            TenantId = TenantId,
            PlanName = "Default",
            Description = "Default tenant-wide SLA policy, seeded automatically.",
            IsActive = true,
            IsDefault = true,
            Rules = DefaultSeed.Select(seed => new SlaRule
            {
                TenantId = TenantId,
                Name = $"{seed.Priority} priority",
                Priority = seed.Priority,
                ResponseTargetHours = seed.ResponseHours,
                ResolutionTargetHours = seed.ResolutionHours,
                BusinessHoursOnly = false
            }).ToList()
        };

        dbContext.SlaDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken);
        return definition;
    }

    private Task<SlaRule?> LoadRuleAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.SlaRules
            .Include(x => x.Escalations)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);

    private static List<SlaEscalation> MapEscalations(IReadOnlyCollection<UpsertSlaEscalationRequest>? escalations, Guid tenantId) =>
        (escalations ?? []).Select(x => new SlaEscalation
        {
            TenantId = tenantId,
            TriggerAtPercent = Math.Clamp(x.TriggerAtPercent, 1, 100),
            NotifyRoles = string.IsNullOrWhiteSpace(x.NotifyRoles) ? null : x.NotifyRoles.Trim(),
            EscalateToUserId = x.EscalateToUserId
        }).ToList();

    private static void Validate(UpsertSlaPolicyRequest request)
    {
        if (request.ResponseTargetMinutes <= 0 || request.ResolutionTargetMinutes <= 0)
        {
            throw new BusinessRuleException("SLA targets must be greater than zero minutes.");
        }

        if (request.ResponseTargetMinutes > request.ResolutionTargetMinutes)
        {
            throw new BusinessRuleException("Response target cannot exceed the resolution target.");
        }
    }

    private static string NormalizePriority(string? value)
    {
        var normalized = value?.Trim();
        return normalized switch
        {
            "Critical" or "High" or "Medium" or "Low" => normalized,
            _ => throw new BusinessRuleException("Priority must be Critical, High, Medium, or Low.")
        };
    }

    private static TimeSpan? ParseTimeOfDay(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TimeSpan.TryParse(value, out var parsed)
            ? parsed
            : throw new BusinessRuleException("Business hours must be in HH:mm format.");
    }

    private static int PriorityOrder(string priority) => priority switch
    {
        "Critical" => 0,
        "High" => 1,
        "Medium" => 2,
        "Low" => 3,
        _ => 10
    };

    private static SlaPolicyResponse Map(SlaRule rule) => new(
        rule.Id,
        rule.SlaDefinitionId,
        rule.Name,
        rule.Priority,
        (int)Math.Round(rule.ResponseTargetHours * 60m),
        (int)Math.Round(rule.ResolutionTargetHours * 60m),
        rule.BusinessHoursOnly,
        rule.BusinessHoursStart?.ToString(@"hh\:mm"),
        rule.BusinessHoursEnd?.ToString(@"hh\:mm"),
        rule.Escalations
            .OrderBy(x => x.TriggerAtPercent)
            .Select(x => new SlaEscalationResponse(x.Id, x.TriggerAtPercent, x.NotifyRoles, x.EscalateToUserId))
            .ToList());
}

public sealed record UpsertSlaEscalationRequest(int TriggerAtPercent, string? NotifyRoles, Guid? EscalateToUserId);

public sealed record UpsertSlaPolicyRequest(
    string? Name,
    string Priority,
    int ResponseTargetMinutes,
    int ResolutionTargetMinutes,
    bool BusinessHoursOnly,
    string? BusinessHoursStart,
    string? BusinessHoursEnd,
    IReadOnlyCollection<UpsertSlaEscalationRequest>? Escalations);

public sealed record SlaEscalationResponse(Guid Id, int TriggerAtPercent, string? NotifyRoles, Guid? EscalateToUserId);

public sealed record SlaPolicyResponse(
    Guid Id,
    Guid SlaDefinitionId,
    string? Name,
    string Priority,
    int ResponseTargetMinutes,
    int ResolutionTargetMinutes,
    bool BusinessHoursOnly,
    string? BusinessHoursStart,
    string? BusinessHoursEnd,
    IReadOnlyCollection<SlaEscalationResponse> Escalations);
