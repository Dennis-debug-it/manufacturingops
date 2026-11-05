using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
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
    IUserAccessService userAccessService,
    IFactorySettingsService factorySettingsService,
    IAssetClassService assetClassService) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>Asset classes available from each industry pack, so the factory can see what adding one would bring.</summary>
    [HttpGet("industry-packs")]
    public ActionResult<IReadOnlyCollection<IndustryAssetPackResponse>> IndustryPacks() =>
        Ok(IndustryTemplates.All
            .Select(pack => new IndustryAssetPackResponse(
                pack.Key,
                pack.Name,
                pack.AssetClasses.Select(x => new IndustryAssetClassResponse(x.Code, x.Name, x.Group, x.DefaultCriticality, x.ServiceIntervalMonths, x.Meters, x.Fields.Select(f => f.Label).ToList())).ToList()))
            .ToList());

    /// <summary>Adds an industry pack's asset classes that the tenant doesn't have yet.</summary>
    [HttpPost("apply-industry-pack")]
    public async Task<ActionResult<InstallAssetClassesResult>> ApplyIndustryPack([FromBody] ApplyIndustryPackRequest request, CancellationToken ct)
    {
        userAccessService.EnsureAdmin();
        var key = request.TemplateKey ?? (await factorySettingsService.GetOrCreateAsync(TenantId, ct)).IndustryTemplate;
        return Ok(await assetClassService.InstallAsync(TenantId, key, ct));
    }

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
            DisplayOrder = request.DisplayOrder,
            DefaultCriticality = NormalizeCriticality(request.DefaultCriticality),
            DefaultMeterTypes = NormalizeMeterTypes(request.DefaultMeterTypes)
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
        cat.DefaultCriticality = NormalizeCriticality(request.DefaultCriticality);
        cat.DefaultMeterTypes = NormalizeMeterTypes(request.DefaultMeterTypes);
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
        c.DefaultCriticality,
        DefaultMeterTypes = (c.DefaultMeterTypes ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries),
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

    /// <summary>A new tenant starts with its industry pack's asset classes (tea by default).</summary>
    private async Task EnsureDefaultCategoriesSeededAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.AssetCategories.AnyAsync(x => x.TenantId == TenantId, cancellationToken))
        {
            return;
        }

        var settings = await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken);
        await assetClassService.InstallAsync(TenantId, settings.IndustryTemplate, cancellationToken);
    }

    private static string? NormalizeCriticality(string? value)
    {
        try
        {
            return AssetCriticality.Normalize(value);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
    }

    private static string? NormalizeMeterTypes(IReadOnlyCollection<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var types = values.Select(x => MeterTypes.Find(x) ?? throw new BusinessRuleException($"Unknown meter type '{x}'.")).Distinct().ToList();
        return string.Join(',', types);
    }
}

public sealed record UpsertAssetCategoryRequest(
    string Name,
    string? ParentCategoryName,
    string? Icon,
    int DisplayOrder,
    string? Code = null,
    string? Description = null,
    int? DefaultServiceIntervalMonths = null,
    string? DefaultCriticality = null,
    IReadOnlyCollection<string>? DefaultMeterTypes = null);

public sealed record ApplyIndustryPackRequest(string? TemplateKey);

public sealed record IndustryAssetClassResponse(string Code, string Name, string Group, string? DefaultCriticality, int? ServiceIntervalMonths, IReadOnlyCollection<string> Meters, IReadOnlyCollection<string> Fields);

public sealed record IndustryAssetPackResponse(string Key, string Name, IReadOnlyCollection<IndustryAssetClassResponse> AssetClasses);

public sealed record UpsertAssetCategoryFieldRequest(
    string FieldLabel,
    string FieldType,
    string? DropdownOptions,
    string? Unit,
    bool IsRequired,
    int DisplayOrder,
    string? HelpText = null,
    bool? IsActive = null);
