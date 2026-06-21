using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/pm/templates")]
public sealed class PmTemplatesController(
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IPmTemplateService pmTemplateService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TemplateResponse>>> GetAll(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var templates = await pmTemplateService.ListAsync(TenantId, cancellationToken);
        return Ok(templates.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplateResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var template = await pmTemplateService.GetAsync(TenantId, id, cancellationToken);
        return Ok(Map(template));
    }

    [HttpPost]
    public async Task<ActionResult<TemplateResponse>> Create(
        [FromBody] UpsertTemplateRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var template = await pmTemplateService.CreateAsync(TenantId, ToCommand(request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, Map(template));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TemplateResponse>> Update(
        Guid id, [FromBody] UpsertTemplateRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var template = await pmTemplateService.UpdateAsync(TenantId, id, ToCommand(request), cancellationToken);
        return Ok(Map(template));
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<TemplateResponse>> Duplicate(
        Guid id, [FromBody] DuplicateTemplateRequest? request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var template = await pmTemplateService.DuplicateAsync(TenantId, id, request?.Name, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = template.Id }, Map(template));
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<TemplateResponse>> Activate(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        return Ok(Map(await pmTemplateService.SetActiveAsync(TenantId, id, true, cancellationToken)));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<TemplateResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        return Ok(Map(await pmTemplateService.SetActiveAsync(TenantId, id, false, cancellationToken)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        await pmTemplateService.DeleteAsync(TenantId, id, UserId, cancellationToken);
        return NoContent();
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static PmTemplateWriteCommand ToCommand(UpsertTemplateRequest r) =>
        new(r.Name, r.Category, r.Description, r.AssetCategoryId,
            r.Checklist.Select(q => new PmTemplateChecklistItemCommand(
                q.SectionName, q.SectionOrder, q.Question, q.HelperText, q.Type,
                q.Unit, q.MinValue, q.MaxValue, q.DefaultValue,
                q.Required, q.EvidenceRequired, q.TechnicianRemarksAllowed,
                q.RequiresNoteOnFail, q.Order, q.Options)).ToList(),
            r.IsActive, r.Code, r.WorkType, r.DefaultIntervalMonths);

    internal static TemplateResponse Map(PmTemplate t) =>
        new(t.Id,
            t.Name,
            PmTemplateService.NormalizeCategory(t.Category),
            t.Description,
            t.Code,
            t.WorkType,
            t.DefaultIntervalMonths,
            t.AssetCategoryId,
            t.AssetCategory?.Name,
            t.Version,
            t.IsPlatformSeed || t.IsSystemTemplate,
            t.Sections
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new TemplateSectionResponse(x.Id, x.SectionName, x.DisplayOrder))
                .ToList(),
            t.Questions
                .OrderBy(x => x.Section?.DisplayOrder ?? int.MaxValue)
                .ThenBy(x => x.DisplayOrder)
                .Select(x => new TemplateChecklistItemResponse(
                    x.Id, x.SectionId, x.SectionName,
                    x.Section?.DisplayOrder ?? 0,
                    x.Prompt, x.HelperText,
                    PmTemplateService.NormalizeType(x.ResponseType),
                    x.Unit, x.MinValue, x.MaxValue, x.DefaultValue,
                    x.IsRequired, x.EvidenceRequired, x.TechnicianRemarksAllowed,
                    x.RequiresNoteOnFail, x.DisplayOrder, ParseOptions(x.OptionsJson)))
                .ToList(),
            t.IsActive);

    private static IReadOnlyCollection<string> ParseOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed record UpsertTemplateRequest(
    string Name,
    string Category,
    string? Description,
    Guid? AssetCategoryId,
    IReadOnlyCollection<UpsertTemplateChecklistItemRequest> Checklist,
    bool IsActive,
    string? Code = null,
    string WorkType = "PreventiveMaintenance",
    int? DefaultIntervalMonths = null);

public sealed record UpsertTemplateChecklistItemRequest(
    string? SectionName,
    int? SectionOrder,
    string Question,
    string? HelperText,
    string Type,
    string? Unit,
    decimal? MinValue,
    decimal? MaxValue,
    string? DefaultValue,
    bool Required,
    bool EvidenceRequired,
    bool? TechnicianRemarksAllowed,
    bool RequiresNoteOnFail,
    int Order,
    IReadOnlyCollection<string>? Options);

public sealed record DuplicateTemplateRequest(string? Name);

public sealed record TemplateResponse(
    Guid Id,
    string Name,
    string Category,
    string? Description,
    string? Code,
    string WorkType,
    int? DefaultIntervalMonths,
    Guid? AssetCategoryId,
    string? AssetCategoryName,
    int Version,
    bool IsPlatformSeed,
    IReadOnlyCollection<TemplateSectionResponse> Sections,
    IReadOnlyCollection<TemplateChecklistItemResponse> Checklist,
    bool IsActive);

public sealed record TemplateSectionResponse(Guid Id, string Name, int Order);

public sealed record TemplateChecklistItemResponse(
    Guid Id,
    Guid? SectionId,
    string? SectionName,
    int SectionOrder,
    string Question,
    string? HelperText,
    string Type,
    string? Unit,
    decimal? MinValue,
    decimal? MaxValue,
    string? DefaultValue,
    bool Required,
    bool EvidenceRequired,
    bool TechnicianRemarksAllowed,
    bool RequiresNoteOnFail,
    int Order,
    IReadOnlyCollection<string> Options);
