namespace Ecosys.Infrastructure.Services.Factory;

public sealed record SectionTemplate(string Code, string Name, string Description);

public sealed record IndustryTemplate(
    string Key,
    string Name,
    string ProductionUnit,
    string ProductionUnitShort,
    IReadOnlyList<SectionTemplate> Sections);

/// <summary>
/// Industry template packs (scope §2 design rule 6: no hard-coded tea). Tea ships first; other
/// industries are added here as data. Sections are only defaults: once applied, a factory's
/// sections are ordinary org nodes the customer can rename, reorder or remove.
/// </summary>
public static class IndustryTemplates
{
    public const string TeaKey = "tea";
    public const string GenericKey = "generic";

    public static readonly IndustryTemplate Tea = new(
        TeaKey,
        "Tea factory",
        "kg made tea",
        "kg MT",
        [
            new("LEAF", "Leaf reception", "Weighbridge, leaf sheds and green leaf handling"),
            new("WITH", "Withering", "Withering troughs and fans"),
            new("ROLL", "Rolling (CTC)", "Rotorvanes and CTC machines"),
            new("FERM", "Fermentation", "Continuous fermenting units"),
            new("DRY", "Drying", "Fluid bed dryers and heat exchangers"),
            new("SORT", "Sorting and grading", "Fibre extractors, sorters and grading machines"),
            new("PACK", "Packing and dispatch", "Packing machines, stores and dispatch"),
            new("BOIL", "Boiler house", "Boilers, feed water and fuel handling"),
            new("UTIL", "Power and utilities", "Generators, transformers, compressors and small hydro"),
        ]);

    public static readonly IndustryTemplate Generic = new(
        GenericKey,
        "General manufacturing",
        "units produced",
        "units",
        [
            new("PROD", "Production", "Production lines"),
            new("UTIL", "Utilities", "Power, compressed air, water and steam"),
            new("WKSP", "Workshop", "Maintenance workshop and tools"),
            new("STOR", "Stores", "Spares and consumables stores"),
        ]);

    public static readonly IReadOnlyList<IndustryTemplate> All = [Tea, Generic];

    public static IndustryTemplate? Find(string? key) =>
        All.FirstOrDefault(x => string.Equals(x.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));
}
