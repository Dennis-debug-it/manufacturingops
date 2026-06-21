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
[Route("api/sla-definitions")]
public sealed class SlaDefinitionsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SlaDefinitionResponse>>> GetAll(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var definitions = await dbContext.SlaDefinitions
            .Include(x => x.Rules)
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.PlanName)
            .ToListAsync(cancellationToken);

        return Ok(definitions.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SlaDefinitionResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var definition = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("SLA definition was not found.");

        return Ok(Map(definition));
    }

    [HttpPost]
    public async Task<ActionResult<SlaDefinitionResponse>> Create([FromBody] UpsertSlaDefinitionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        Validate(request);
        await EnsureUniquePlanNameAsync(request.PlanName, null, cancellationToken);

        var definition = new SlaDefinition
        {
            TenantId = TenantId,
            PlanName = request.PlanName.Trim(),
            Description = Normalize(request.Description),
            IsActive = request.IsActive,
            Rules = request.Rules.Select(rule => new SlaRule
            {
                TenantId = TenantId,
                Priority = NormalizePriority(rule.Priority),
                ResponseTargetHours = rule.ResponseTargetHours,
                ResolutionTargetHours = rule.ResolutionTargetHours,
                BusinessHoursOnly = rule.BusinessHoursOnly
            }).ToList()
        };

        dbContext.SlaDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "SLA definition created",
            nameof(SlaDefinition),
            definition.Id.ToString(),
            $"Created SLA plan '{definition.PlanName}'.",
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = definition.Id }, Map(definition));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SlaDefinitionResponse>> Update(Guid id, [FromBody] UpsertSlaDefinitionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        Validate(request);

        var definition = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("SLA definition was not found.");

        await EnsureUniquePlanNameAsync(request.PlanName, definition.Id, cancellationToken);

        definition.PlanName = request.PlanName.Trim();
        definition.Description = Normalize(request.Description);
        definition.IsActive = request.IsActive;

        dbContext.SlaRules.RemoveRange(definition.Rules);
        definition.Rules = request.Rules.Select(rule => new SlaRule
        {
            TenantId = TenantId,
            SlaDefinitionId = definition.Id,
            Priority = NormalizePriority(rule.Priority),
            ResponseTargetHours = rule.ResponseTargetHours,
            ResolutionTargetHours = rule.ResolutionTargetHours,
            BusinessHoursOnly = rule.BusinessHoursOnly
        }).ToList();

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "SLA definition updated",
            nameof(SlaDefinition),
            definition.Id.ToString(),
            $"Updated SLA plan '{definition.PlanName}'.",
            cancellationToken);

        return Ok(Map(definition));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var definition = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("SLA definition was not found.");

        var isInUse = await dbContext.Clients.AnyAsync(
            x => x.TenantId == TenantId && x.SlaDefinitionId == id,
            cancellationToken);

        if (isInUse)
        {
            throw new BusinessRuleException("This SLA definition is assigned to one or more clients.");
        }

        dbContext.SlaDefinitions.Remove(definition);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "SLA definition deleted",
            nameof(SlaDefinition),
            definition.Id.ToString(),
            $"Deleted SLA plan '{definition.PlanName}'.",
            cancellationToken);

        return NoContent();
    }

    private Task<SlaDefinition?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.SlaDefinitions
            .Include(x => x.Rules)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);

    private async Task EnsureUniquePlanNameAsync(string planName, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedPlanName = planName.Trim().ToLowerInvariant();
        var exists = await dbContext.SlaDefinitions.AnyAsync(
            x => x.TenantId == TenantId
                && x.Id != currentId
                && x.PlanName.ToLower() == normalizedPlanName,
            cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException("SLA plan name already exists for this tenant.");
        }
    }

    private static void Validate(UpsertSlaDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlanName))
        {
            throw new BusinessRuleException("Plan name is required.");
        }

        if (request.Rules is null || request.Rules.Count == 0)
        {
            throw new BusinessRuleException("At least one SLA rule is required.");
        }

        var priorities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in request.Rules)
        {
            var priority = NormalizePriority(rule.Priority);
            if (!priorities.Add(priority))
            {
                throw new BusinessRuleException("Each priority can only appear once in an SLA plan.");
            }

            if (rule.ResponseTargetHours < 0 || rule.ResolutionTargetHours < 0)
            {
                throw new BusinessRuleException("SLA target hours must be zero or greater.");
            }
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizePriority(string? value)
    {
        var normalized = value?.Trim();
        return normalized switch
        {
            "Critical" or "High" or "Medium" or "Low" => normalized,
            _ => throw new BusinessRuleException("Priority must be Critical, High, Medium, or Low.")
        };
    }

    private static int PriorityOrder(string priority) => priority switch
    {
        "Critical" => 0,
        "High" => 1,
        "Medium" => 2,
        "Low" => 3,
        _ => 10
    };

    private static SlaDefinitionResponse Map(SlaDefinition definition) =>
        new(
            definition.Id,
            definition.PlanName,
            definition.Description,
            definition.IsActive,
            definition.Rules
                .OrderBy(x => PriorityOrder(x.Priority))
                .Select(x => new SlaRuleResponse(
                    x.Id,
                    x.Priority,
                    x.ResponseTargetHours,
                    x.ResolutionTargetHours,
                    x.BusinessHoursOnly))
                .ToList(),
            definition.CreatedAt,
            definition.UpdatedAt);
}

public sealed record UpsertSlaDefinitionRequest(
    string PlanName,
    string? Description,
    bool IsActive,
    IReadOnlyCollection<UpsertSlaRuleRequest> Rules);

public sealed record UpsertSlaRuleRequest(
    string Priority,
    decimal ResponseTargetHours,
    decimal ResolutionTargetHours,
    bool BusinessHoursOnly);

public sealed record SlaDefinitionResponse(
    Guid Id,
    string PlanName,
    string? Description,
    bool IsActive,
    IReadOnlyCollection<SlaRuleResponse> Rules,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record SlaRuleResponse(
    Guid Id,
    string Priority,
    decimal ResponseTargetHours,
    decimal ResolutionTargetHours,
    bool BusinessHoursOnly);
