using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record SectionTemplate(string Code, string Name, string Description);

/// <param name="Type">Text, Number, Dropdown or Date.</param>
/// <param name="Options">Dropdown choices, comma-separated.</param>
public sealed record FieldTemplate(string Label, string Type = "Text", string? Unit = null, string? Options = null);

/// <param name="Group">Heading the class is listed under (stored as the category's parent name).</param>
/// <param name="DefaultCriticality">Suggested A/B/C for new assets of this class; the factory can change it.</param>
/// <param name="Meters">Meter types every new asset of this class gets.</param>
public sealed record AssetClassTemplate(
    string Code,
    string Name,
    string Group,
    string? DefaultCriticality,
    int? ServiceIntervalMonths,
    IReadOnlyList<string> Meters,
    IReadOnlyList<FieldTemplate> Fields);

/// <summary>A question on a running check. <paramref name="Meter"/> feeds the reading into the asset's meter of that type.</summary>
public sealed record CheckQuestionTemplate(
    string Prompt,
    string Type,
    string FailAction = CheckFailActions.None,
    string? Unit = null,
    decimal? Min = null,
    decimal? Max = null,
    string? Meter = null,
    bool Required = true);

/// <summary>A running check done every shift or day on the assets of one class.</summary>
public sealed record CheckTemplate(string Name, string AssetClassCode, string Frequency, IReadOnlyList<CheckQuestionTemplate> Questions);

/// <summary>A weekly-window routine on the assets of some classes; walked as one route when <paramref name="Route"/>.</summary>
public sealed record RoutineTemplate(string Name, IReadOnlyList<string> AssetClassCodes, bool Route, decimal Hours, IReadOnlyList<(string Prompt, string Type, string? Unit)> Steps);

/// <summary>A visitor access zone: who may go in and what they need (Visitor Management).</summary>
public sealed record ZoneTemplate(string Name, string Level, bool Escort, bool Induction, bool Permit, string? Ppe);

/// <summary>A patrol checkpoint, the zone it sits in (by name) and its yes/no questions, one per line (Security Patrol).</summary>
public sealed record CheckpointTemplate(string Name, string? Zone, string? Questions);

/// <summary>
/// The words that differ between industries, so screens don't say "low crop" to a sugar mill.
/// </summary>
/// <param name="OverhaulSeason">The yearly long shutdown, e.g. "low-crop shutdown", "off-season shutdown".</param>
/// <param name="LowSeason">The quiet months it happens in, e.g. "Low-crop season", "Off-season".</param>
/// <param name="SupplyFleet">The vehicles that bring the raw material in, e.g. "Leaf collection fleet".</param>
/// <param name="SupplyFleetDescription">One line describing that fleet.</param>
/// <param name="RawMaterial">What comes in to be processed, e.g. "green leaf", "cane", "raw milk".</param>
public sealed record IndustryVocabulary(string OverhaulSeason, string LowSeason, string SupplyFleet, string SupplyFleetDescription, string RawMaterial);

/// <summary>
/// An industry pack: everything about the system that depends on what the factory makes. Sections,
/// asset classes, shift checks, weekly-window routines, failure codes, statutory items, visitor zones,
/// patrol checkpoints, the production unit and the industry's words. Each industry lives in its own file
/// under <c>Industries/</c>; adding one means writing that file and listing it in <see cref="IndustryTemplates.All"/>.
/// All of it is only a starting point: once installed, everything is ordinary data the customer edits.
/// </summary>
public sealed class IndustryTemplate
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string ProductionUnit { get; init; }
    public required string ProductionUnitShort { get; init; }
    public required IndustryVocabulary Vocabulary { get; init; }
    /// <summary>Usual months (1–12) of the quiet season the yearly overhaul happens in.</summary>
    public int? LowSeasonStartMonth { get; init; }
    public int? LowSeasonEndMonth { get; init; }
    /// <summary>Busy months, comma separated, for splitting downtime peak vs low.</summary>
    public string PeakMonths { get; init; } = "";
    public required IReadOnlyList<SectionTemplate> Sections { get; init; }
    public required IReadOnlyList<AssetClassTemplate> AssetClasses { get; init; }
    public IReadOnlyList<CheckTemplate> RunningChecks { get; init; } = [];
    public IReadOnlyList<RoutineTemplate> WindowRoutines { get; init; } = [];
    /// <summary>Industry problems tied to its asset classes; the ISO 14224 codes every industry gets are in <see cref="FailureCodeTemplates.Iso"/>.</summary>
    public IReadOnlyList<FailureCodeTemplate> FailureCodes { get; init; } = [];
    public IReadOnlyList<ComplianceTemplate> Compliance { get; init; } = [];
    public IReadOnlyList<ZoneTemplate> Zones { get; init; } = [];
    public IReadOnlyList<CheckpointTemplate> Checkpoints { get; init; } = [];
}

/// <summary>Headings asset classes are grouped under, shared by every industry.</summary>
public static class AssetGroups
{
    public const string Processing = "Processing";
    public const string Steam = "Steam and heat";
    public const string Power = "Power and utilities";
    public const string Measuring = "Weighing and measuring";
    public const string Refrigeration = "Refrigeration and cold chain";
    public const string Hygiene = "Hygiene and cleaning";
    public const string Lifting = "Lifting";
    public const string Vehicles = "Vehicles";
    public const string General = "General";
}

/// <summary>
/// The industries Ecosys knows (scope §2 design rule 6: no hard-coded tea). A tenant has one industry;
/// a factory can have its own, so a group running a tea factory and a dairy uses one workspace.
/// </summary>
public static class IndustryTemplates
{
    public const string TeaKey = "tea";
    public const string SugarKey = "sugar";
    public const string DairyKey = "dairy";
    public const string GenericKey = "generic";

    public static IndustryTemplate Tea => TeaIndustry.Template;
    public static IndustryTemplate Sugar => SugarIndustry.Template;
    public static IndustryTemplate Dairy => DairyIndustry.Template;
    public static IndustryTemplate Generic => GenericIndustry.Template;

    /// <summary>Every industry, in the order they are offered.</summary>
    public static IReadOnlyList<IndustryTemplate> All => [TeaIndustry.Template, SugarIndustry.Template, DairyIndustry.Template, GenericIndustry.Template];

    /// <summary>Used when a tenant has not chosen (the product launched with tea).</summary>
    public static IndustryTemplate Default => Tea;

    public static IndustryTemplate? Find(string? key) =>
        All.FirstOrDefault(x => string.Equals(x.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>The tenant's industry, from its factory settings.</summary>
    public static async Task<IndustryTemplate> ForTenantAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var key = await dbContext.FactorySettings.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId).Select(x => x.IndustryTemplate).FirstOrDefaultAsync(cancellationToken);
        return Find(key) ?? Default;
    }

    /// <summary>
    /// A factory's industry: its own when it has one (a group with mixed factories), otherwise the
    /// tenant's. A section or sub-section resolves to the factory above it.
    /// </summary>
    public static async Task<IndustryTemplate> ForFactoryAsync(AppDbContext dbContext, Guid tenantId, Guid orgNodeId, CancellationToken cancellationToken = default)
    {
        var nodes = await dbContext.OrgNodes.IgnoreQueryFilters().AsNoTracking().Where(x => x.TenantId == tenantId)
            .Select(x => new { x.Id, x.ParentId, x.Level, x.TemplateKey }).ToDictionaryAsync(x => x.Id, cancellationToken);
        for (Guid? id = orgNodeId; id is { } current && nodes.TryGetValue(current, out var node); id = node.ParentId)
        {
            if (node.Level == OrgNodeLevels.Factory)
            {
                if (Find(node.TemplateKey) is { } own) return own;
                break;
            }
        }

        return await ForTenantAsync(dbContext, tenantId, cancellationToken);
    }

    /// <summary>Every industry the tenant uses: its own and any its factories have.</summary>
    public static async Task<IReadOnlyList<IndustryTemplate>> InUseAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await ForTenantAsync(dbContext, tenantId, cancellationToken);
        var factoryKeys = await dbContext.OrgNodes.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Level == OrgNodeLevels.Factory && x.IsActive && x.TemplateKey != null)
            .Select(x => x.TemplateKey!).Distinct().ToListAsync(cancellationToken);
        return new[] { tenant }.Concat(factoryKeys.Select(Find).OfType<IndustryTemplate>()).DistinctBy(x => x.Key).ToList();
    }
}
