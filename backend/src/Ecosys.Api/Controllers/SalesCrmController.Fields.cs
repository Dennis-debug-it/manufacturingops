using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Custom deal fields (Settings › Sales). Everyone with CRM access reads them, to fill them in; admins
/// change them. A field keeps its key for life, so relabelling never touches stored values, imports or
/// stage rules. A field with values cannot change type or be deleted; switch it off instead.
/// </summary>
public sealed partial class SalesCrmController
{
    [HttpGet("fields")]
    public async Task<ActionResult<SalesFieldsResponse>> ListFields(CancellationToken cancellationToken)
    {
        EnsureCanView();
        var fields = await SalesCustomFields.LoadAsync(dbContext, TenantId, cancellationToken);
        var counts = await dbContext.SalesCustomFieldValues.AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .GroupBy(x => x.FieldId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        return Ok(new SalesFieldsResponse(
            fields.Select(x => MapField(x, counts.GetValueOrDefault(x.Id))).ToList(),
            SalesStageRules.BuiltIn
                .Concat(fields.Where(x => x.IsActive).Select(x => new SalesRequirement(SalesStageRules.CustomPrefix + x.Key, x.Label)))
                .ToList()));
    }

    [HttpPost("fields")]
    public async Task<ActionResult<SalesFieldsResponse>> CreateField([FromBody] UpsertSalesCustomFieldRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var fields = await SalesCustomFields.LoadAsync(dbContext, TenantId, cancellationToken);
        var field = new SalesCustomField
        {
            TenantId = TenantId,
            Key = FieldKey(request.Label, fields.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase)),
            SortOrder = request.SortOrder ?? (fields.Count == 0 ? 10 : fields.Max(x => x.SortOrder) + 10),
        };
        await ApplyFieldAsync(field, request, hasValues: false, cancellationToken);
        dbContext.SalesCustomFields.Add(field);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListFields(cancellationToken);
    }

    [HttpPut("fields/{id:guid}")]
    public async Task<ActionResult<SalesFieldsResponse>> UpdateField(Guid id, [FromBody] UpsertSalesCustomFieldRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var field = await dbContext.SalesCustomFields.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Field not found.");
        var hasValues = await dbContext.SalesCustomFieldValues.AnyAsync(x => x.TenantId == TenantId && x.FieldId == id, cancellationToken);
        await ApplyFieldAsync(field, request, hasValues, cancellationToken);
        if (request.SortOrder.HasValue) field.SortOrder = request.SortOrder.Value;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListFields(cancellationToken);
    }

    [HttpDelete("fields/{id:guid}")]
    public async Task<ActionResult<SalesFieldsResponse>> DeleteField(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var field = await dbContext.SalesCustomFields.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Field not found.");
        var used = await dbContext.SalesCustomFieldValues.CountAsync(x => x.TenantId == TenantId && x.FieldId == id, cancellationToken);
        if (used > 0)
            throw new BusinessRuleException($"{used} deal{(used == 1 ? " has" : "s have")} a {field.Label}. Switch the field off instead, so nothing is lost.");

        // Drop it from every stage rule that names it.
        var requirement = SalesStageRules.CustomPrefix + field.Key;
        var stages = await dbContext.SalesPipelineStages
            .Where(x => x.TenantId == TenantId && x.RequiredFields != null && x.RequiredFields.Contains(requirement))
            .ToListAsync(cancellationToken);
        foreach (var stage in stages)
        {
            var kept = SalesStageRules.Parse(stage.RequiredFields).Where(x => !string.Equals(x, requirement, StringComparison.OrdinalIgnoreCase)).ToList();
            stage.RequiredFields = kept.Count == 0 ? null : string.Join(',', kept);
        }
        dbContext.SalesCustomFields.Remove(field);
        await dbContext.SaveChangesAsync(cancellationToken);
        _pipelines = null;
        return await ListFields(cancellationToken);
    }

    private async Task ApplyFieldAsync(SalesCustomField field, UpsertSalesCustomFieldRequest request, bool hasValues, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            throw new BusinessRuleException("Give the field a label.");
        var type = SalesCustomFieldTypes.All.FirstOrDefault(x => string.Equals(x, request.FieldType?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException($"Field type is one of: {string.Join(", ", SalesCustomFieldTypes.All)}.");
        if (hasValues && type != field.FieldType)
            throw new BusinessRuleException("Deals already have values for this field, so its type cannot change. Add a new field instead.");
        var options = (request.Options ?? []).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (type == SalesCustomFieldTypes.Dropdown && options.Count == 0)
            throw new BusinessRuleException("A dropdown needs at least one choice.");
        if (options.Any(x => x.Contains('\n') || x.Length > 100))
            throw new BusinessRuleException("Keep each choice to one line of up to 100 characters.");
        if (request.PipelineId.HasValue && (await PipelinesAsync(cancellationToken)).All(x => x.Id != request.PipelineId))
            throw new NotFoundException("Pipeline not found.");

        field.Label = request.Label.Trim();
        field.FieldType = type;
        field.Options = type == SalesCustomFieldTypes.Dropdown ? string.Join('\n', options) : null;
        field.HelpText = string.IsNullOrWhiteSpace(request.HelpText) ? null : request.HelpText.Trim();
        field.PipelineId = request.PipelineId;
        field.IsActive = request.IsActive;
    }

    /// <summary>A field key from its label ("Building size (m²)" → "building_size_m"), unique in the workspace.</summary>
    private static string FieldKey(string label, HashSet<string> taken)
    {
        var words = new string((label ?? string.Empty).ToLowerInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var key = string.Join('_', words);
        if (key.Length == 0) key = "field";
        if (key.Length > 50) key = key[..50];
        var candidate = key;
        for (var i = 2; taken.Contains(candidate); i++)
            candidate = $"{key}_{i}";
        return candidate;
    }

    /// <summary>
    /// Deals whose <paramref name="fieldKey"/> field matches: any value when <paramref name="fieldValue"/> is empty,
    /// text containing it for text fields, else the exact value.
    /// </summary>
    private async Task<IQueryable<SalesOpportunity>> ApplyFieldFilterAsync(IQueryable<SalesOpportunity> query, string? fieldKey, string? fieldValue, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
            return query;
        var field = (await SalesCustomFields.LoadAsync(dbContext, TenantId, cancellationToken))
            .FirstOrDefault(x => string.Equals(x.Key, fieldKey.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException($"Unknown deal field '{fieldKey}'.");
        var fieldId = field.Id;
        if (string.IsNullOrWhiteSpace(fieldValue))
            return query.Where(x => x.CustomFieldValues.Any(v => v.FieldId == fieldId));
        if (field.FieldType == SalesCustomFieldTypes.Text)
        {
            var text = fieldValue.Trim().ToLower();
            return query.Where(x => x.CustomFieldValues.Any(v => v.FieldId == fieldId && v.Value.ToLower().Contains(text)));
        }
        var value = SalesCustomFields.Normalize(field, fieldValue);
        return value is null
            ? query.Where(x => !x.CustomFieldValues.Any(v => v.FieldId == fieldId))
            : query.Where(x => x.CustomFieldValues.Any(v => v.FieldId == fieldId && v.Value == value));
    }

    private static SalesCustomFieldResponse MapField(SalesCustomField x, int valueCount) =>
        new(x.Id, x.Key, x.Label, x.FieldType, SalesCustomFields.OptionsOf(x), x.HelpText, x.PipelineId, x.IsActive, x.SortOrder, valueCount);

    /// <summary>The deal's values for the fields that show in its pipeline (switched-off fields with a value still show).</summary>
    private async Task<IReadOnlyList<SalesDealFieldValue>> DealFieldValuesAsync(Guid opportunityId, Guid pipelineId, CancellationToken cancellationToken)
    {
        var fields = await SalesCustomFields.LoadAsync(dbContext, TenantId, cancellationToken);
        var values = await dbContext.SalesCustomFieldValues.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.OpportunityId == opportunityId)
            .ToDictionaryAsync(x => x.FieldId, x => x.Value, cancellationToken);
        return fields
            .Where(f => SalesCustomFields.AppliesTo(f, pipelineId) || values.ContainsKey(f.Id))
            .Select(f => new SalesDealFieldValue(f.Id, f.Key, f.Label, f.FieldType, values.GetValueOrDefault(f.Id)))
            .ToList();
    }
}

public sealed record SalesCustomFieldResponse(
    Guid Id, string Key, string Label, string FieldType, IReadOnlyList<string> Options, string? HelpText,
    Guid? PipelineId, bool IsActive, int SortOrder, int ValueCount);

/// <summary>The fields, plus everything a stage can require (built-in checks and active fields, as "cf:key").</summary>
public sealed record SalesFieldsResponse(IReadOnlyList<SalesCustomFieldResponse> Fields, IReadOnlyList<SalesRequirement> Requirements);

public sealed record UpsertSalesCustomFieldRequest(
    [StringLength(100)] string Label,
    [StringLength(20)] string FieldType,
    IReadOnlyList<string>? Options,
    [StringLength(300)] string? HelpText,
    Guid? PipelineId,
    bool IsActive = true,
    int? SortOrder = null);

public sealed record SalesDealFieldValue(Guid FieldId, string Key, string Label, string FieldType, string? Value);
