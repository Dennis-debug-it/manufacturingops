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

/// <summary>Tenant configuration for Projects: feature toggles, trades, types, stages and templates.</summary>
[ApiController]
[Authorize]
[Route("api/project-settings")]
public sealed class ProjectSettingsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IProjectAccessService projectAccessService,
    IProjectSettingsService projectSettingsService,
    IProjectSetupService projectSetupService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly JsonSerializerOptions TemplateJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<ActionResult<ProjectSettingsModel>> Get(CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsView);
        return Ok(await projectSettingsService.GetAsync(TenantId, cancellationToken));
    }

    [HttpPut]
    public async Task<ActionResult<ProjectSettingsModel>> Save([FromBody] ProjectSettingsModel request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsSettings);
        var saved = await projectSettingsService.SaveAsync(TenantId, request, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.settings.updated", nameof(TenantModuleSetting), "projects",
            "Project settings updated.", cancellationToken);
        return Ok(saved);
    }

    // ── Types ──────────────────────────────────────────────────────────────────

    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectTypeResponse>>> ListTypes(CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsView);
        await projectSetupService.EnsureDefaultsAsync(TenantId, cancellationToken);
        var types = await dbContext.ProjectTypes
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new ProjectTypeResponse(x.Id, x.Name, x.Description, x.DefaultTemplateId, x.IsActive, x.SortOrder))
            .ToListAsync(cancellationToken);
        return Ok(types);
    }

    [HttpPost("types")]
    public async Task<ActionResult<ProjectTypeResponse>> CreateType([FromBody] UpsertProjectTypeRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsSettings);
        var name = await ValidateTypeAsync(request, null, cancellationToken);
        var type = new ProjectType
        {
            TenantId = TenantId, Name = name, Description = request.Description?.Trim(), DefaultTemplateId = request.DefaultTemplateId,
            IsActive = request.IsActive ?? true, SortOrder = request.SortOrder ?? await NextTypeOrderAsync(cancellationToken),
        };
        dbContext.ProjectTypes.Add(type);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ProjectTypeResponse(type.Id, type.Name, type.Description, type.DefaultTemplateId, type.IsActive, type.SortOrder));
    }

    [HttpPut("types/{id:guid}")]
    public async Task<ActionResult<ProjectTypeResponse>> UpdateType(Guid id, [FromBody] UpsertProjectTypeRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsSettings);
        var type = await dbContext.ProjectTypes.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Project type not found.");
        type.Name = await ValidateTypeAsync(request, id, cancellationToken);
        type.Description = request.Description?.Trim();
        type.DefaultTemplateId = request.DefaultTemplateId;
        type.SortOrder = request.SortOrder ?? type.SortOrder;
        if (request.IsActive is false && type.IsActive
            && !await dbContext.ProjectTypes.AnyAsync(x => x.TenantId == TenantId && x.Id != id && x.IsActive, cancellationToken))
            throw new BusinessRuleException("Keep at least one active project type.");
        type.IsActive = request.IsActive ?? type.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ProjectTypeResponse(type.Id, type.Name, type.Description, type.DefaultTemplateId, type.IsActive, type.SortOrder));
    }

    // ── Stages ─────────────────────────────────────────────────────────────────

    [HttpGet("stages")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectStageResponse>>> ListStages(CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsView);
        await projectSetupService.EnsureDefaultsAsync(TenantId, cancellationToken);
        var stages = await dbContext.ProjectStages
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new ProjectStageResponse(x.Id, x.Name, x.Category, x.SortOrder, x.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(stages);
    }

    [HttpPost("stages")]
    public async Task<ActionResult<ProjectStageResponse>> CreateStage([FromBody] UpsertProjectStageRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsSettings);
        var (name, category) = await ValidateStageAsync(request, null, cancellationToken);
        var nextOrder = (await dbContext.ProjectStages.Where(x => x.TenantId == TenantId).MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0) + 10;
        var stage = new ProjectStage { TenantId = TenantId, Name = name, Category = category, SortOrder = request.SortOrder ?? nextOrder, IsActive = request.IsActive ?? true };
        dbContext.ProjectStages.Add(stage);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ProjectStageResponse(stage.Id, stage.Name, stage.Category, stage.SortOrder, stage.IsActive));
    }

    [HttpPut("stages/{id:guid}")]
    public async Task<ActionResult<ProjectStageResponse>> UpdateStage(Guid id, [FromBody] UpsertProjectStageRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsSettings);
        var stage = await dbContext.ProjectStages.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Stage not found.");
        var (name, category) = await ValidateStageAsync(request, id, cancellationToken);
        var inUse = await dbContext.Projects.AnyAsync(x => x.TenantId == TenantId && x.StageId == id, cancellationToken);
        if (inUse && category != stage.Category)
            throw new BusinessRuleException("This stage is used by projects, so its category can't change. Create a new stage instead.");
        if (request.IsActive is false && stage.IsActive && stage.Category == ProjectStageCategories.Planning
            && !await dbContext.ProjectStages.AnyAsync(x => x.TenantId == TenantId && x.Id != id && x.IsActive && x.Category == ProjectStageCategories.Planning, cancellationToken))
            throw new BusinessRuleException("Keep at least one active Planning stage so new projects have a starting stage.");
        stage.Name = name;
        stage.Category = category;
        stage.SortOrder = request.SortOrder ?? stage.SortOrder;
        stage.IsActive = request.IsActive ?? stage.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ProjectStageResponse(stage.Id, stage.Name, stage.Category, stage.SortOrder, stage.IsActive));
    }

    // ── Templates ──────────────────────────────────────────────────────────────

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectTemplateResponse>>> ListTemplates(CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsView);
        var templates = await dbContext.ProjectTemplates
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(templates.Select(MapTemplate).ToList());
    }

    [HttpPost("templates")]
    public async Task<ActionResult<ProjectTemplateResponse>> CreateTemplate([FromBody] UpsertProjectTemplateRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsSettings);
        var name = await ValidateTemplateAsync(request, null, cancellationToken);
        var template = new ProjectTemplate
        {
            TenantId = TenantId, Name = name, IsActive = request.IsActive ?? true,
            ItemsJson = JsonSerializer.Serialize(NormalizeItems(request.Items), TemplateJsonOptions),
        };
        dbContext.ProjectTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapTemplate(template));
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<ActionResult<ProjectTemplateResponse>> UpdateTemplate(Guid id, [FromBody] UpsertProjectTemplateRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsSettings);
        var template = await dbContext.ProjectTemplates.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Template not found.");
        template.Name = await ValidateTemplateAsync(request, id, cancellationToken);
        template.IsActive = request.IsActive ?? template.IsActive;
        template.ItemsJson = JsonSerializer.Serialize(NormalizeItems(request.Items), TemplateJsonOptions);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapTemplate(template));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<int> NextTypeOrderAsync(CancellationToken cancellationToken) =>
        (await dbContext.ProjectTypes.Where(x => x.TenantId == TenantId).MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0) + 10;

    private async Task<string> ValidateTypeAsync(UpsertProjectTypeRequest request, Guid? existingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Type name is required.");
        var name = request.Name.Trim();
        var lowered = name.ToLower();
        if (await dbContext.ProjectTypes.AnyAsync(x => x.TenantId == TenantId && x.Id != existingId && x.Name.ToLower() == lowered, cancellationToken))
            throw new BusinessRuleException($"A project type named '{name}' already exists.");
        if (request.DefaultTemplateId.HasValue && !await dbContext.ProjectTemplates.AnyAsync(x => x.TenantId == TenantId && x.Id == request.DefaultTemplateId, cancellationToken))
            throw new BusinessRuleException("Select a valid default template.");
        return name;
    }

    private async Task<(string Name, string Category)> ValidateStageAsync(UpsertProjectStageRequest request, Guid? existingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Stage name is required.");
        var category = ProjectStageCategories.All.FirstOrDefault(x => string.Equals(x, request.Category?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException($"Category must be one of: {string.Join(", ", ProjectStageCategories.All)}.");
        var name = request.Name.Trim();
        var lowered = name.ToLower();
        if (await dbContext.ProjectStages.AnyAsync(x => x.TenantId == TenantId && x.Id != existingId && x.Name.ToLower() == lowered, cancellationToken))
            throw new BusinessRuleException($"A stage named '{name}' already exists.");
        return (name, category);
    }

    private async Task<string> ValidateTemplateAsync(UpsertProjectTemplateRequest request, Guid? existingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Template name is required.");
        var name = request.Name.Trim();
        var lowered = name.ToLower();
        if (await dbContext.ProjectTemplates.AnyAsync(x => x.TenantId == TenantId && x.Id != existingId && x.Name.ToLower() == lowered, cancellationToken))
            throw new BusinessRuleException($"A template named '{name}' already exists.");
        return name;
    }

    private static List<ProjectTemplateItem> NormalizeItems(IReadOnlyList<ProjectTemplateItem>? items)
    {
        var normalized = (items ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new ProjectTemplateItem(
                x.Name.Trim(),
                x.Weight < 0 ? throw new BusinessRuleException("Milestone weights cannot be negative.") : x.Weight,
                x.OffsetDays,
                (x.Tasks ?? []).Where(t => !string.IsNullOrWhiteSpace(t.Title)).Select(t => new ProjectTemplateTaskItem(t.Title.Trim(), t.OffsetDays)).ToList()))
            .ToList();
        return normalized;
    }

    private static ProjectTemplateResponse MapTemplate(ProjectTemplate x)
    {
        List<ProjectTemplateItem> items;
        try
        {
            items = JsonSerializer.Deserialize<List<ProjectTemplateItem>>(x.ItemsJson, TemplateJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            items = [];
        }
        return new ProjectTemplateResponse(x.Id, x.Name, x.IsActive, items);
    }
}

public sealed record ProjectTypeResponse(Guid Id, string Name, string? Description, Guid? DefaultTemplateId, bool IsActive, int SortOrder);

public sealed record UpsertProjectTypeRequest(string Name, string? Description, Guid? DefaultTemplateId, bool? IsActive, int? SortOrder);

public sealed record ProjectStageResponse(Guid Id, string Name, string Category, int SortOrder, bool IsActive);

public sealed record UpsertProjectStageRequest(string Name, string Category, int? SortOrder, bool? IsActive);

public sealed record ProjectTemplateResponse(Guid Id, string Name, bool IsActive, IReadOnlyList<ProjectTemplateItem> Items);

public sealed record UpsertProjectTemplateRequest(string Name, IReadOnlyList<ProjectTemplateItem>? Items, bool? IsActive);
