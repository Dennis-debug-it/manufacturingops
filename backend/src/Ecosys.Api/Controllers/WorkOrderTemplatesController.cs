using System.Text.Json;
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
[Route("api/workorder-templates")]
public sealed class WorkOrderTemplatesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<WorkOrderTemplateResponse>>> GetAll(
        [FromQuery] string? jobType,
        [FromQuery] string? search,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCreateWorkOrders);

        var query = QueryTemplates();

        if (!string.IsNullOrWhiteSpace(jobType))
        {
            var normalizedJobType = WorkOrderTypes.Normalize(jobType);
            query = query.Where(x => x.JobType == normalizedJobType);
        }

        if (activeOnly == true)
        {
            query = query.Where(x => x.IsActive);
        }

        var templates = await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            templates = templates
                .Where(x => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (x.Description ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return Ok(templates.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkOrderTemplateResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanCreateWorkOrders);
        var template = await QueryTemplates().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order template was not found.");
        return Ok(Map(template));
    }

    [HttpPost]
    public async Task<ActionResult<WorkOrderTemplateResponse>> Create(
        [FromBody] UpsertWorkOrderTemplateRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        await ValidateAsync(request, null, cancellationToken);

        var template = new WorkOrderTemplate
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            JobType = WorkOrderTypes.Normalize(request.JobType),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            EstimatedDurationMins = request.EstimatedDurationMins,
            DefaultPriority = WorkOrderPriorities.Normalize(request.DefaultPriority),
            RequiredSkillsJson = SerializeSkills(request.RequiredSkills),
            ChecklistTemplateId = request.ChecklistTemplateId,
            DefaultMaterialsJson = SerializeMaterials(request.DefaultMaterials),
            IsActive = request.IsActive,
        };

        dbContext.WorkOrderTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "Work order template created", nameof(WorkOrderTemplate), template.Id.ToString(), $"Created work order template '{template.Name}'.", cancellationToken);

        var persisted = await QueryTemplates().SingleAsync(x => x.Id == template.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, Map(persisted));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkOrderTemplateResponse>> Update(
        Guid id, [FromBody] UpsertWorkOrderTemplateRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var template = await dbContext.WorkOrderTemplates.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order template was not found.");

        await ValidateAsync(request, id, cancellationToken);

        template.Name = request.Name.Trim();
        template.JobType = WorkOrderTypes.Normalize(request.JobType);
        template.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        template.EstimatedDurationMins = request.EstimatedDurationMins;
        template.DefaultPriority = WorkOrderPriorities.Normalize(request.DefaultPriority);
        template.RequiredSkillsJson = SerializeSkills(request.RequiredSkills);
        template.ChecklistTemplateId = request.ChecklistTemplateId;
        template.DefaultMaterialsJson = SerializeMaterials(request.DefaultMaterials);
        template.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        var persisted = await QueryTemplates().SingleAsync(x => x.Id == template.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var template = await dbContext.WorkOrderTemplates.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Work order template was not found.");

        var inUse = await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId && x.WorkOrderTemplateId == id, cancellationToken);
        if (inUse)
        {
            template.IsActive = false;
            await dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        dbContext.WorkOrderTemplates.Remove(template);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<WorkOrderTemplate> QueryTemplates() =>
        dbContext.WorkOrderTemplates
            .Include(x => x.ChecklistTemplate)
            .Where(x => x.TenantId == TenantId);

    private async Task ValidateAsync(UpsertWorkOrderTemplateRequest request, Guid? excludingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Template name is required.");
        }

        _ = WorkOrderTypes.Normalize(request.JobType);
        _ = WorkOrderPriorities.Normalize(request.DefaultPriority);

        var nameTaken = await dbContext.WorkOrderTemplates
            .AnyAsync(x => x.TenantId == TenantId && x.Id != (excludingId ?? Guid.Empty) && x.Name.ToLower() == request.Name.Trim().ToLower(), cancellationToken);
        if (nameTaken)
        {
            throw new BusinessRuleException("A work order template with this name already exists.");
        }

        if (request.ChecklistTemplateId.HasValue)
        {
            var checklistExists = await dbContext.PmTemplates
                .AnyAsync(x => x.TenantId == TenantId && x.Id == request.ChecklistTemplateId.Value, cancellationToken);
            if (!checklistExists)
            {
                throw new BusinessRuleException("Checklist template was not found for this tenant.");
            }
        }

        if (request.DefaultMaterials is { Count: > 0 })
        {
            var materialIds = request.DefaultMaterials.Select(x => x.MaterialId).Distinct().ToList();
            var foundCount = await dbContext.MaterialItems
                .CountAsync(x => x.TenantId == TenantId && materialIds.Contains(x.Id), cancellationToken);
            if (foundCount != materialIds.Count)
            {
                throw new BusinessRuleException("One or more default materials were not found for this tenant.");
            }
        }
    }

    private static string? SerializeSkills(IReadOnlyCollection<string>? skills) =>
        skills is { Count: > 0 } ? JsonSerializer.Serialize(skills.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList()) : null;

    private static string? SerializeMaterials(IReadOnlyCollection<WorkOrderTemplateMaterialRequest>? materials) =>
        materials is { Count: > 0 } ? JsonSerializer.Serialize(materials.Select(m => new { m.MaterialId, m.Quantity }).ToList()) : null;

    private static IReadOnlyCollection<string> ParseSkills(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private WorkOrderTemplateResponse Map(WorkOrderTemplate template)
    {
        var materials = ParseMaterialRefs(template.DefaultMaterialsJson);
        var materialIds = materials.Select(m => m.MaterialId).ToList();
        var materialNames = materialIds.Count == 0
            ? new Dictionary<Guid, string>()
            : dbContext.MaterialItems
                .Where(x => x.TenantId == TenantId && materialIds.Contains(x.Id))
                .ToDictionary(x => x.Id, x => x.ItemName);

        return new WorkOrderTemplateResponse(
            template.Id,
            template.Name,
            template.JobType,
            template.Description,
            template.EstimatedDurationMins,
            template.DefaultPriority,
            ParseSkills(template.RequiredSkillsJson),
            template.ChecklistTemplateId,
            template.ChecklistTemplate?.Name,
            materials.Select(m => new WorkOrderTemplateMaterialResponse(m.MaterialId, materialNames.GetValueOrDefault(m.MaterialId), m.Quantity)).ToList(),
            template.IsActive,
            template.CreatedAt);
    }

    private static IReadOnlyCollection<(Guid MaterialId, decimal Quantity)> ParseMaterialRefs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var raw = JsonSerializer.Deserialize<List<MaterialRefDto>>(json) ?? [];
            return raw.Select(x => (x.MaterialId, x.Quantity)).ToList();
        }
        catch { return []; }
    }

    private sealed record MaterialRefDto(Guid MaterialId, decimal Quantity);
}

public sealed record WorkOrderTemplateMaterialRequest(Guid MaterialId, decimal Quantity);

public sealed record UpsertWorkOrderTemplateRequest(
    string Name,
    string JobType,
    string? Description,
    int? EstimatedDurationMins,
    string DefaultPriority,
    IReadOnlyCollection<string>? RequiredSkills,
    Guid? ChecklistTemplateId,
    IReadOnlyCollection<WorkOrderTemplateMaterialRequest>? DefaultMaterials,
    bool IsActive = true);

public sealed record WorkOrderTemplateMaterialResponse(Guid MaterialId, string? MaterialName, decimal Quantity);

public sealed record WorkOrderTemplateResponse(
    Guid Id,
    string Name,
    string JobType,
    string? Description,
    int? EstimatedDurationMins,
    string DefaultPriority,
    IReadOnlyCollection<string> RequiredSkills,
    Guid? ChecklistTemplateId,
    string? ChecklistTemplateName,
    IReadOnlyCollection<WorkOrderTemplateMaterialResponse> DefaultMaterials,
    bool IsActive,
    DateTime CreatedAt);
