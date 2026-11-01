using System.Globalization;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>The workspace's custom deal fields: loading, value checks and saving a deal's values.</summary>
public static class SalesCustomFields
{
    public static async Task<IReadOnlyList<SalesCustomField>> LoadAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.SalesCustomFields.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    public static IReadOnlyList<string> OptionsOf(SalesCustomField field) =>
        (field.Options ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Whether the field shows on deals in this pipeline.</summary>
    public static bool AppliesTo(SalesCustomField field, Guid pipelineId) =>
        field.IsActive && (field.PipelineId is null || field.PipelineId == pipelineId);

    /// <summary>The stored form of a value (dates as yyyy-MM-dd, numbers invariant, "Yes" for a ticked box); null clears it.</summary>
    public static string? Normalize(SalesCustomField field, string? raw)
    {
        var value = raw?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        switch (field.FieldType)
        {
            case SalesCustomFieldTypes.Number:
                if (!decimal.TryParse(value.Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    throw new BusinessRuleException($"{field.Label} must be a number.");
                return number.ToString(CultureInfo.InvariantCulture);
            case SalesCustomFieldTypes.Date:
                if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date))
                    throw new BusinessRuleException($"{field.Label} must be a date (e.g. 2026-11-30).");
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            case SalesCustomFieldTypes.Checkbox:
                return value.ToLowerInvariant() switch
                {
                    "true" or "yes" or "y" or "1" => "Yes",
                    "false" or "no" or "n" or "0" => null,
                    _ => throw new BusinessRuleException($"{field.Label} is yes or no."),
                };
            case SalesCustomFieldTypes.Dropdown:
                var option = OptionsOf(field).FirstOrDefault(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))
                    ?? throw new BusinessRuleException($"{field.Label} must be one of: {string.Join(", ", OptionsOf(field))}.");
                return option;
            default:
                return value.Length > 1000 ? value[..1000] : value;
        }
    }

    /// <summary>
    /// Sets the deal's values for the fields named (by key). Fields not named keep their value; an
    /// empty value clears it. Unknown keys are refused so a typo does not silently lose data.
    /// </summary>
    public static async Task ApplyAsync(
        AppDbContext dbContext, Guid tenantId, SalesOpportunity opportunity, IReadOnlyDictionary<string, string?> values,
        IReadOnlyList<SalesCustomField> fields, CancellationToken cancellationToken)
    {
        if (values.Count == 0) return;
        var existing = await dbContext.SalesCustomFieldValues
            .Where(x => x.TenantId == tenantId && x.OpportunityId == opportunity.Id)
            .ToListAsync(cancellationToken);
        foreach (var (key, raw) in values)
        {
            var field = fields.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException($"Unknown deal field '{key}'.");
            if (!field.IsActive) continue;
            var value = Normalize(field, raw);
            var row = existing.FirstOrDefault(x => x.FieldId == field.Id);
            if (value is null)
            {
                if (row is not null) dbContext.SalesCustomFieldValues.Remove(row);
            }
            else if (row is null)
            {
                row = new SalesCustomFieldValue { TenantId = tenantId, OpportunityId = opportunity.Id, FieldId = field.Id, Value = value };
                dbContext.SalesCustomFieldValues.Add(row);
                existing.Add(row);
            }
            else
            {
                row.Value = value;
            }
        }
    }
}

public sealed record SalesRequirement(string Key, string Label);

/// <summary>What a deal has, for checking stage requirements without going back to the database.</summary>
public sealed record SalesDealFacts(
    IReadOnlyList<SalesCustomField> Fields,
    IReadOnlyDictionary<Guid, string> Values,
    bool HasContactPerson,
    bool HasQuotation);

/// <summary>
/// Stage rules: things a deal needs before it can enter a stage. A stage's requirements also apply to
/// every later stage (Won included), so skipping a stage does not skip its rules; Lost has none.
/// Only moves people make are checked; a client accepting a quote or a contract renewing always moves the deal.
/// </summary>
public static class SalesStageRules
{
    public const string CustomPrefix = "cf:";

    public static readonly IReadOnlyList<SalesRequirement> BuiltIn =
    [
        new("value", "Estimated value"),
        new("closeDate", "Expected close date"),
        new("contact", "Contact person"),
        new("serviceType", "Type of work"),
        new("site", "Site"),
        new("description", "Scope / notes"),
        new("quote", "A quotation"),
    ];

    public static IReadOnlyList<string> Parse(string? raw) =>
        (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Checks and joins requirement keys for storage; unknown ones are refused.</summary>
    public static string? Format(IEnumerable<string>? keys, IReadOnlyList<SalesCustomField> fields)
    {
        var list = (keys ?? []).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var key in list)
        {
            var known = BuiltIn.Any(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
                || (key.StartsWith(CustomPrefix, StringComparison.OrdinalIgnoreCase)
                    && fields.Any(f => string.Equals(f.Key, key[CustomPrefix.Length..], StringComparison.OrdinalIgnoreCase)));
            if (!known) throw new BusinessRuleException($"Unknown stage requirement '{key}'.");
        }
        return list.Count == 0 ? null : string.Join(',', list);
    }

    /// <summary>Requirement keys for entering <paramref name="targetKey"/>: its own and every earlier stage's.</summary>
    public static IReadOnlyList<string> RequiredFor(SalesPipelineView pipeline, string targetKey)
    {
        if (string.Equals(targetKey, SalesPipelineCatalog.LostKey, StringComparison.OrdinalIgnoreCase)) return [];
        var rank = pipeline.Rank(targetKey);
        if (rank < 0) return [];
        return pipeline.Stages.Take(rank + 1)
            .Where(x => x.Key != SalesPipelineCatalog.LostKey)
            .SelectMany(x => x.Required)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static async Task<SalesDealFacts> LoadFactsAsync(AppDbContext dbContext, Guid tenantId, Guid opportunityId, CancellationToken cancellationToken)
    {
        var fields = await SalesCustomFields.LoadAsync(dbContext, tenantId, cancellationToken);
        // Values not yet saved count too, so a deal can be filled in and moved in one request.
        var saved = await dbContext.SalesCustomFieldValues.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.OpportunityId == opportunityId)
            .ToDictionaryAsync(x => x.FieldId, x => x.Value, cancellationToken);
        foreach (var entry in dbContext.ChangeTracker.Entries<SalesCustomFieldValue>().Where(x => x.Entity.OpportunityId == opportunityId))
        {
            if (entry.State == EntityState.Deleted) saved.Remove(entry.Entity.FieldId);
            else saved[entry.Entity.FieldId] = entry.Entity.Value;
        }
        var hasContact = await dbContext.SalesOpportunityContacts.AnyAsync(x => x.TenantId == tenantId && x.OpportunityId == opportunityId, cancellationToken);
        var hasQuote = await dbContext.TenantQuotations.AnyAsync(x => x.TenantId == tenantId && x.SalesOpportunityId == opportunityId, cancellationToken);
        return new SalesDealFacts(fields, saved, hasContact, hasQuote);
    }

    /// <summary>Labels of the requirements this deal does not meet. Switched-off fields, and fields for another pipeline, are not asked for.</summary>
    public static IReadOnlyList<string> Missing(SalesOpportunity deal, Guid pipelineId, IReadOnlyList<string> required, SalesDealFacts facts)
    {
        var missing = new List<string>();
        foreach (var key in required)
        {
            if (key.StartsWith(CustomPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var field = facts.Fields.FirstOrDefault(f => string.Equals(f.Key, key[CustomPrefix.Length..], StringComparison.OrdinalIgnoreCase));
                if (field is not null && SalesCustomFields.AppliesTo(field, pipelineId) && !facts.Values.ContainsKey(field.Id))
                    missing.Add(field.Label);
                continue;
            }
            var met = key.ToLowerInvariant() switch
            {
                "value" => deal.EstimatedValue > 0,
                "closedate" => deal.ExpectedCloseDate.HasValue,
                "contact" => facts.HasContactPerson || !string.IsNullOrWhiteSpace(deal.ContactName),
                "servicetype" => !string.IsNullOrWhiteSpace(deal.ServiceType),
                "site" => deal.SiteId.HasValue,
                "description" => !string.IsNullOrWhiteSpace(deal.Description),
                "quote" => facts.HasQuotation,
                _ => true,
            };
            if (!met)
                missing.Add(BuiltIn.First(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Label);
        }
        return missing;
    }

    /// <summary>Refuses the move when the deal is missing something the stage asks for.</summary>
    public static void EnsureCanEnter(SalesOpportunity deal, SalesPipelineView pipeline, SalesPipelineStageView target, SalesDealFacts facts)
    {
        var missing = Missing(deal, pipeline.Id, RequiredFor(pipeline, target.Key), facts);
        if (missing.Count > 0)
            throw new BusinessRuleException($"Before moving to {target.Name}, fill in: {string.Join(", ", missing)}.");
    }
}
