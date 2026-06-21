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
[Route("api/asset-categories")]
[Route("api/asset-templates")]
[Authorize]
public sealed class AssetCategoriesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly (string Name, string[] Fields)[] DefaultDefinitions =
    [
        ("Generator", ["Fuel Type", "Capacity KVA", "Tank Capacity", "Phase", "Runtime Hours", "Engine Make", "Alternator Make"]),
        ("UPS", []),
        ("HVAC", []),
        ("Solar", []),
        ("Fire Panel", []),
        ("Pump", []),
        ("Transformer", []),
        ("Switchgear", []),
        ("Other Equipment", []),
    ];

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        await EnsureDefaultCategoriesSeededAsync(ct);

        var categories = await dbContext.AssetCategories
            .Where(x => x.TenantId == TenantId && (includeInactive || x.IsActive))
            .Include(x => x.Fields)
            .OrderBy(x => x.ParentCategoryName)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

        return Ok(categories.Select(MapCategory));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        await EnsureDefaultCategoriesSeededAsync(ct);

        var cat = await dbContext.AssetCategories
            .Include(x => x.Fields)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct)
            ?? throw new NotFoundException("Asset category not found.");

        return Ok(MapCategory(cat));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertAssetCategoryRequest request, CancellationToken ct)
    {
        userAccessService.EnsureAdmin();

        var cat = new AssetCategory
        {
            TenantId = TenantId,
            Name = NormalizeRequired(request.Name, "Template name"),
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ParentCategoryName = request.ParentCategoryName?.Trim(),
            Icon = request.Icon ?? "tool",
            IsDefault = false,
            IsActive = true,
            IsSystemTemplate = false,
            DefaultServiceIntervalMonths = request.DefaultServiceIntervalMonths,
            DisplayOrder = request.DisplayOrder
        };
        dbContext.AssetCategories.Add(cat);
        await dbContext.SaveChangesAsync(ct);
        return Ok(MapCategory(cat));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertAssetCategoryRequest request, CancellationToken ct)
    {
        userAccessService.EnsureAdmin();

        var cat = await dbContext.AssetCategories
            .Include(x => x.Fields)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct)
            ?? throw new NotFoundException("Asset category not found.");

        cat.Name = NormalizeRequired(request.Name, "Template name");
        cat.Code = string.IsNullOrWhiteSpace(request.Code) ? cat.Code : request.Code.Trim();
        cat.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        cat.ParentCategoryName = request.ParentCategoryName?.Trim();
        cat.Icon = request.Icon ?? cat.Icon;
        cat.DefaultServiceIntervalMonths = request.DefaultServiceIntervalMonths;
        cat.DisplayOrder = request.DisplayOrder;
        await dbContext.SaveChangesAsync(ct);
        return Ok(MapCategory(cat));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        userAccessService.EnsureAdmin();

        var cat = await dbContext.AssetCategories
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct)
            ?? throw new NotFoundException("Asset category not found.");

        var hasAssets = await dbContext.Assets.AnyAsync(
            x => x.TenantId == TenantId && x.AssetCategoryId == id,
            ct);
        if (hasAssets)
            throw new BusinessRuleException("Cannot delete a category that has assets assigned to it.");

        cat.IsActive = false;
        await dbContext.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/fields")]
    public async Task<IActionResult> ListFields(Guid id, CancellationToken ct)
    {
        await EnsureDefaultCategoriesSeededAsync(ct);

        var cat = await dbContext.AssetCategories
            .Include(x => x.Fields)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct)
            ?? throw new NotFoundException("Asset category not found.");

        return Ok(cat.Fields.OrderBy(f => f.DisplayOrder).Select(MapField));
    }

    [HttpPost("{id:guid}/fields")]
    public async Task<IActionResult> AddField(Guid id, [FromBody] UpsertAssetCategoryFieldRequest request, CancellationToken ct)
    {
        userAccessService.EnsureAdmin();

        var cat = await dbContext.AssetCategories
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct)
            ?? throw new NotFoundException("Asset category not found.");

        var fieldName = BuildFieldName(request.FieldLabel);
        await EnsureUniqueFieldNameAsync(id, fieldName, null, ct);

        var field = new AssetCategoryField
        {
            AssetCategoryId = cat.Id,
            FieldName = fieldName,
            FieldLabel = NormalizeRequired(request.FieldLabel, "Field label"),
            FieldType = request.FieldType,
            DropdownOptions = request.DropdownOptions,
            Unit = request.Unit,
            HelpText = request.HelpText,
            IsRequired = request.IsRequired,
            IsActive = request.IsActive ?? true,
            DisplayOrder = request.DisplayOrder
        };
        dbContext.AssetCategoryFields.Add(field);
        await dbContext.SaveChangesAsync(ct);
        return Ok(MapField(field));
    }

    [HttpPut("{id:guid}/fields/{fieldId:guid}")]
    public async Task<IActionResult> UpdateField(Guid id, Guid fieldId, [FromBody] UpsertAssetCategoryFieldRequest request, CancellationToken ct)
    {
        userAccessService.EnsureAdmin();

        var fieldName = BuildFieldName(request.FieldLabel);
        await EnsureUniqueFieldNameAsync(id, fieldName, fieldId, ct);

        var field = await dbContext.AssetCategoryFields
            .Include(x => x.AssetCategory)
            .SingleOrDefaultAsync(
                x => x.Id == fieldId
                    && x.AssetCategoryId == id
                    && x.AssetCategory != null
                    && x.AssetCategory.TenantId == TenantId,
                ct)
            ?? throw new NotFoundException("Field not found.");

        field.FieldName = fieldName;
        field.FieldLabel = NormalizeRequired(request.FieldLabel, "Field label");
        field.FieldType = request.FieldType;
        field.DropdownOptions = request.DropdownOptions;
        field.Unit = request.Unit;
        field.HelpText = request.HelpText;
        field.IsRequired = request.IsRequired;
        field.IsActive = request.IsActive ?? field.IsActive;
        field.DisplayOrder = request.DisplayOrder;
        await dbContext.SaveChangesAsync(ct);
        return Ok(MapField(field));
    }

    [HttpDelete("{id:guid}/fields/{fieldId:guid}")]
    public async Task<IActionResult> DeleteField(Guid id, Guid fieldId, CancellationToken ct)
    {
        userAccessService.EnsureAdmin();

        var field = await dbContext.AssetCategoryFields
            .Include(x => x.AssetCategory)
            .SingleOrDefaultAsync(
                x => x.Id == fieldId
                    && x.AssetCategoryId == id
                    && x.AssetCategory != null
                    && x.AssetCategory.TenantId == TenantId,
                ct)
            ?? throw new NotFoundException("Field not found.");

        dbContext.AssetCategoryFields.Remove(field);
        await dbContext.SaveChangesAsync(ct);
        return NoContent();
    }

    private static object MapCategory(AssetCategory c) => new
    {
        c.Id,
        c.TenantId,
        c.Name,
        c.Code,
        c.Description,
        c.ContractorType,
        c.ParentCategoryName,
        c.Icon,
        c.IsDefault,
        c.IsActive,
        c.IsSystemTemplate,
        c.DefaultServiceIntervalMonths,
        c.DisplayOrder,
        c.CreatedAt,
        c.UpdatedAt,
        Fields = c.Fields.OrderBy(f => f.DisplayOrder).Select(MapField)
    };

    private static object MapField(AssetCategoryField f) => new
    {
        f.Id,
        f.AssetCategoryId,
        f.FieldName,
        f.FieldLabel,
        f.FieldType,
        f.DropdownOptions,
        f.Unit,
        f.HelpText,
        f.IsRequired,
        f.IsActive,
        f.DisplayOrder,
        f.CreatedAt,
        f.UpdatedAt
    };

    private static string NormalizeRequired(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException($"{label} is required.");
        }

        return value.Trim();
    }

    private static string BuildFieldName(string? label)
    {
        var normalizedLabel = NormalizeRequired(label, "Field label");
        var chars = normalizedLabel
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray();
        var fieldName = string.Join(string.Empty, chars)
            .Trim('_');

        while (fieldName.Contains("__", StringComparison.Ordinal))
        {
            fieldName = fieldName.Replace("__", "_", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new BusinessRuleException("Field label must contain at least one letter or number.");
        }

        return fieldName;
    }

    private async Task EnsureUniqueFieldNameAsync(Guid categoryId, string fieldName, Guid? fieldId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.AssetCategoryFields.AnyAsync(
            x => x.AssetCategoryId == categoryId
                && x.FieldName == fieldName
                && (!fieldId.HasValue || x.Id != fieldId.Value),
            cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException("Field labels must be unique within an asset template.");
        }
    }

    private async Task EnsureDefaultCategoriesSeededAsync(CancellationToken cancellationToken)
    {
        var hasAny = await dbContext.AssetCategories.AnyAsync(x => x.TenantId == TenantId, cancellationToken);
        if (hasAny)
        {
            return;
        }

        var categories = new List<AssetCategory>();
        foreach (var (index, definition) in DefaultDefinitions.Select((item, index) => (index, item)))
        {
            var category = new AssetCategory
            {
                TenantId = TenantId,
                Name = definition.Name,
                Icon = "tool",
                IsDefault = true,
                IsActive = true,
                DisplayOrder = index
            };

            for (var fieldIndex = 0; fieldIndex < definition.Fields.Length; fieldIndex++)
            {
                var label = definition.Fields[fieldIndex];
                category.Fields.Add(new AssetCategoryField
                {
                    FieldName = label.ToLowerInvariant().Replace(' ', '_'),
                    FieldLabel = label,
                    FieldType = "Text",
                    IsRequired = false,
                    IsActive = true,
                    DisplayOrder = fieldIndex
                });
            }

            categories.Add(category);
        }

        dbContext.AssetCategories.AddRange(categories);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed record UpsertAssetCategoryRequest(
    string Name,
    string? ParentCategoryName,
    string? Icon,
    int DisplayOrder,
    string? Code = null,
    string? Description = null,
    int? DefaultServiceIntervalMonths = null);

public sealed record UpsertAssetCategoryFieldRequest(
    string FieldLabel,
    string FieldType,
    string? DropdownOptions,
    string? Unit,
    bool IsRequired,
    int DisplayOrder,
    string? HelpText = null,
    bool? IsActive = null);
