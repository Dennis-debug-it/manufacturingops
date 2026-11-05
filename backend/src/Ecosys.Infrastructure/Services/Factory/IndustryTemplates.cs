using Ecosys.Domain.Entities;

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

public sealed record IndustryTemplate(
    string Key,
    string Name,
    string ProductionUnit,
    string ProductionUnitShort,
    IReadOnlyList<SectionTemplate> Sections,
    IReadOnlyList<AssetClassTemplate> AssetClasses);

/// <summary>
/// Industry template packs (scope §2 design rule 6: no hard-coded tea). Tea ships first; other
/// industries are added here as data. Sections are only defaults: once applied, a factory's
/// sections are ordinary org nodes the customer can rename, reorder or remove.
/// </summary>
public static class IndustryTemplates
{
    private const string Processing = "Processing";
    private const string Steam = "Steam and heat";
    private const string Power = "Power and utilities";
    private const string Measuring = "Weighing and measuring";
    private const string Vehicles = "Vehicles";
    private const string General = "General";

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
        ],
        [
            new("TEA-WTR", "Withering trough", Processing, "C", 12, [],
            [
                new("Trough length", "Number", "m"),
                new("Trough width", "Number", "m"),
                new("Leaf capacity", "Number", "kg"),
                new("Number of fans", "Number"),
                new("Heat source", "Dropdown", Options: "None,Steam,Hot air,Firewood furnace"),
            ]),
            new("TEA-WFN", "Withering fan", Processing, "B", 6, [MeterTypes.RunHours],
            [
                new("Fan diameter", "Number", "mm"),
                new("Motor power", "Number", "kW"),
                new("Airflow", "Number", "m³/h"),
                new("Drive", "Dropdown", Options: "Direct,Belt"),
            ]),
            new("TEA-RV", "Rotorvane", Processing, "A", 3, [MeterTypes.RunHours, MeterTypes.Production],
            [
                new("Size", "Dropdown", "inch", "8,15"),
                new("Throughput", "Number", "kg/h"),
                new("Motor power", "Number", "kW"),
            ]),
            new("TEA-CTC", "CTC machine", Processing, "A", 1, [MeterTypes.RunHours, MeterTypes.Production],
            [
                new("Number of roller pairs", "Number"),
                new("Roller length", "Number", "mm"),
                new("Teeth per inch", "Number", "TPI"),
                new("Speed ratio", "Text"),
                new("Motor power", "Number", "kW"),
                new("Last re-cut date", "Date"),
            ]),
            new("TEA-CFU", "Continuous fermenting unit", Processing, "B", 3, [MeterTypes.RunHours],
            [
                new("Bed length", "Number", "m"),
                new("Bed width", "Number", "m"),
                new("Throughput", "Number", "kg/h"),
                new("Fan power", "Number", "kW"),
            ]),
            new("TEA-FBD", "Fluid bed dryer", Processing, "A", 3, [MeterTypes.RunHours, MeterTypes.Production],
            [
                new("Throughput", "Number", "kg/h"),
                new("Heat source", "Dropdown", Options: "Steam,Hot air furnace,Thermic fluid"),
                new("Inlet temperature", "Number", "°C"),
                new("Fan power", "Number", "kW"),
            ]),
            new("TEA-SRT", "Sorting and grading machine", Processing, "B", 6, [MeterTypes.RunHours],
            [
                new("Machine type", "Dropdown", Options: "Fibre extractor,Vibro sorter,Colour sorter,Grader,Winnower"),
                new("Throughput", "Number", "kg/h"),
                new("Motor power", "Number", "kW"),
            ]),
            new("TEA-PCK", "Packing machine", Processing, "B", 6, [MeterTypes.RunHours],
            [
                new("Packing type", "Dropdown", Options: "Bulk bagging,Chest packing,Tea bag,Sachet"),
                new("Output", "Number", "packs/min"),
                new("Motor power", "Number", "kW"),
            ]),
            new("TEA-CNV", "Conveyor", Processing, "C", 6, [],
            [
                new("Conveyor type", "Dropdown", Options: "Belt,Chain,Screw,Bucket elevator"),
                new("Length", "Number", "m"),
                new("Motor power", "Number", "kW"),
            ]),
            new("TEA-BLR", "Boiler", Steam, "A", 1, [MeterTypes.RunHours],
            [
                new("Boiler type", "Dropdown", Options: "Fire tube,Water tube,Thermic fluid heater"),
                new("Steam capacity", "Number", "kg/h"),
                new("Design pressure", "Number", "bar"),
                new("Fuel", "Dropdown", Options: "Firewood,Briquettes,Furnace oil,Coal"),
                new("Registration number", "Text"),
                new("Certificate expiry", "Date"),
            ]),
            new("TEA-GEN", "Generator", Power, "A", 3, [MeterTypes.RunHours],
            [
                new("Capacity", "Number", "kVA"),
                new("Fuel type", "Dropdown", Options: "Diesel,Petrol,Gas"),
                new("Tank capacity", "Number", "L"),
                new("Phase", "Dropdown", Options: "Single,Three"),
                new("Engine make", "Text"),
                new("Alternator make", "Text"),
            ]),
            new("TEA-TRF", "Transformer", Power, "A", 12, [MeterTypes.Energy],
            [
                new("Rating", "Number", "kVA"),
                new("Primary voltage", "Number", "kV"),
                new("Secondary voltage", "Number", "V"),
                new("Cooling", "Dropdown", Options: "ONAN,ONAF,Dry type"),
            ]),
            new("TEA-HYD", "Small hydro plant", Power, "A", 6, [MeterTypes.RunHours, MeterTypes.Energy],
            [
                new("Installed capacity", "Number", "kW"),
                new("Head", "Number", "m"),
                new("Turbine type", "Dropdown", Options: "Pelton,Turgo,Francis,Crossflow"),
                new("Generator rating", "Number", "kVA"),
            ]),
            new("TEA-CMP", "Compressor", Power, "B", 3, [MeterTypes.RunHours],
            [
                new("Compressor type", "Dropdown", Options: "Screw,Piston"),
                new("Free air delivery", "Number", "m³/min"),
                new("Working pressure", "Number", "bar"),
                new("Motor power", "Number", "kW"),
            ]),
            new("TEA-MTR", "Electric motor", Power, "C", 6, [],
            [
                new("Power", "Number", "kW"),
                new("Speed", "Number", "rpm"),
                new("Voltage", "Number", "V"),
                new("Frame size", "Text"),
                new("Drive-end bearing", "Text"),
                new("Non-drive-end bearing", "Text"),
            ]),
            new("TEA-PMP", "Pump", Power, "C", 6, [MeterTypes.RunHours],
            [
                new("Pump type", "Dropdown", Options: "Centrifugal,Feed water,Submersible,Positive displacement"),
                new("Flow", "Number", "m³/h"),
                new("Head", "Number", "m"),
                new("Motor power", "Number", "kW"),
            ]),
            new("TEA-WB", "Weighbridge", Measuring, "B", 12, [],
            [
                new("Capacity", "Number", "t"),
                new("Platform length", "Number", "m"),
                new("Calibration certificate", "Text"),
                new("Calibration due", "Date"),
            ]),
            new("TEA-SCL", "Scale", Measuring, "C", 12, [],
            [
                new("Capacity", "Number", "kg"),
                new("Resolution", "Number", "g"),
                new("Calibration due", "Date"),
            ]),
            new("TEA-VEH", "Vehicle", Vehicles, "B", 3, [MeterTypes.Odometer],
            [
                new("Registration number", "Text"),
                new("Make and model", "Text"),
                new("Year", "Number"),
                new("Fuel", "Dropdown", Options: "Diesel,Petrol"),
                new("Payload", "Number", "t"),
            ]),
            new("TEA-OTH", "Other Equipment", General, null, null, [], []),
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
        ],
        [
            new("GEN-MTR", "Electric motor", Power, "C", 6, [], [new("Power", "Number", "kW"), new("Speed", "Number", "rpm")]),
            new("GEN-PMP", "Pump", Power, "C", 6, [MeterTypes.RunHours], [new("Flow", "Number", "m³/h"), new("Head", "Number", "m")]),
            new("GEN-CMP", "Compressor", Power, "B", 3, [MeterTypes.RunHours], [new("Working pressure", "Number", "bar")]),
            new("GEN-GEN", "Generator", Power, "A", 3, [MeterTypes.RunHours], [new("Capacity", "Number", "kVA")]),
            new("GEN-CNV", "Conveyor", Processing, "C", 6, [], [new("Length", "Number", "m")]),
            new("GEN-OTH", "Other Equipment", General, null, null, [], []),
        ]);

    public static readonly IReadOnlyList<IndustryTemplate> All = [Tea, Generic];

    public static IndustryTemplate? Find(string? key) =>
        All.FirstOrDefault(x => string.Equals(x.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));
}
