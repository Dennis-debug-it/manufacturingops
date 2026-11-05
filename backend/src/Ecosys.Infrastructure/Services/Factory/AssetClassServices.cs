using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record InstallAssetClassesResult(int Created, int Skipped);

public interface IAssetClassService
{
    /// <summary>
    /// Adds an industry pack's asset classes (with their fields, default criticality and meters)
    /// to the tenant. Classes whose name already exists are left alone, so it is safe to run
    /// again after the factory has edited them.
    /// </summary>
    Task<InstallAssetClassesResult> InstallAsync(Guid tenantId, string templateKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies an asset class's defaults to a new asset: its suggested criticality when none was
    /// chosen, and its standard meters. The caller saves.
    /// </summary>
    Task ApplyDefaultsAsync(Asset asset, CancellationToken cancellationToken = default);
}

internal sealed class AssetClassService(AppDbContext dbContext) : IAssetClassService
{
    public async Task<InstallAssetClassesResult> InstallAsync(Guid tenantId, string templateKey, CancellationToken cancellationToken = default)
    {
        var template = IndustryTemplates.Find(templateKey) ?? throw new BusinessRuleException("Unknown industry template.");
        var existing = (await dbContext.AssetCategories
                .Where(x => x.TenantId == tenantId)
                .Select(x => x.Name)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var order = existing.Count;

        var created = 0;
        foreach (var item in template.AssetClasses.Where(x => !existing.Contains(x.Name)))
        {
            var category = new AssetCategory
            {
                TenantId = tenantId,
                Name = item.Name,
                Code = item.Code,
                ParentCategoryName = item.Group,
                Description = $"{template.Name} template",
                Icon = "tool",
                IsDefault = true,
                IsActive = true,
                DisplayOrder = order++,
                DefaultServiceIntervalMonths = item.ServiceIntervalMonths,
                DefaultCriticality = item.DefaultCriticality,
                DefaultMeterTypes = item.Meters.Count == 0 ? null : string.Join(',', item.Meters)
            };

            foreach (var (field, index) in item.Fields.Select((field, index) => (field, index)))
            {
                category.Fields.Add(new AssetCategoryField
                {
                    FieldName = FieldName(field.Label),
                    FieldLabel = field.Label,
                    FieldType = field.Type,
                    Unit = field.Unit,
                    DropdownOptions = field.Options,
                    IsActive = true,
                    DisplayOrder = index
                });
            }

            dbContext.AssetCategories.Add(category);
            created++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new InstallAssetClassesResult(created, template.AssetClasses.Count - created);
    }

    public async Task ApplyDefaultsAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        if (!asset.AssetCategoryId.HasValue)
        {
            return;
        }

        var category = asset.AssetCategory
            ?? await dbContext.AssetCategories.SingleOrDefaultAsync(x => x.Id == asset.AssetCategoryId, cancellationToken);
        if (category is null)
        {
            return;
        }

        asset.Criticality ??= category.DefaultCriticality;

        var types = (category.DefaultMeterTypes ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(MeterTypes.Find)
            .Where(x => x is not null)
            .Cast<string>()
            .Distinct()
            .ToList();
        if (types.Count == 0)
        {
            return;
        }

        var productionUnit = await dbContext.FactorySettings
            .Where(x => x.TenantId == asset.TenantId)
            .Select(x => x.ProductionUnit)
            .SingleOrDefaultAsync(cancellationToken) ?? IndustryTemplates.Tea.ProductionUnit;
        var hasPrimary = await dbContext.Meters.AnyAsync(x => x.AssetId == asset.Id && x.IsPrimary, cancellationToken);

        foreach (var type in types)
        {
            var cumulative = MeterTypes.IsCumulativeByDefault(type);
            // The first counter becomes the main meter that meter-based PM reads.
            var primary = !hasPrimary && cumulative;
            hasPrimary |= primary;
            dbContext.Meters.Add(new Meter
            {
                TenantId = asset.TenantId,
                AssetId = asset.Id,
                Name = MeterName(type),
                MeterType = type,
                Unit = MeterTypes.DefaultUnit(type, productionUnit),
                IsCumulative = cumulative,
                IsPrimary = primary
            });
        }
    }

    private static string FieldName(string label) =>
        new string(label.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');

    private static string MeterName(string type) => type switch
    {
        MeterTypes.RunHours => "Run hours",
        MeterTypes.Production => "Production",
        MeterTypes.Odometer => "Odometer",
        MeterTypes.Energy => "Energy",
        _ => type
    };
}
