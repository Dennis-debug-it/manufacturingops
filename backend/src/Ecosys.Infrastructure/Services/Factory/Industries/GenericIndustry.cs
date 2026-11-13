using Ecosys.Domain.Entities;
using static Ecosys.Infrastructure.Services.Factory.AssetGroups;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>
/// Any other factory (cement, flour, plastics, beverages…) until it has its own pack: the plant every
/// factory has, a production line, and the Kenyan statutory core. GEN-… codes.
/// </summary>
internal static class GenericIndustry
{
    public static readonly IndustryTemplate Template = new()
    {
        Key = IndustryTemplates.GenericKey,
        Name = "General manufacturing",
        Description = "Any other factory: production lines, utilities, workshop and stores, to tailor to your plant.",
        ProductionUnit = "units produced",
        ProductionUnitShort = "units",
        Vocabulary = new("annual shutdown", "Shutdown season", "Fleet", "Vehicles that bring raw materials in and take products out", "raw materials"),
        Sections =
        [
            new("PROD", "Production", "Production lines"),
            new("UTIL", "Utilities", "Power, compressed air, water and steam"),
            new("WKSP", "Workshop", "Maintenance workshop and tools"),
            new("STOR", "Stores", "Spares and consumables stores"),
        ],
        AssetClasses =
        [
            new("GEN-MTR", "Electric motor", Power, "C", 6, [], [new("Power", "Number", "kW"), new("Speed", "Number", "rpm")]),
            new("GEN-PMP", "Pump", Power, "C", 6, [MeterTypes.RunHours], [new("Flow", "Number", "m³/h"), new("Head", "Number", "m")]),
            new("GEN-CMP", "Compressor", Power, "B", 3, [MeterTypes.RunHours], [new("Working pressure", "Number", "bar")]),
            new("GEN-GEN", "Generator", Power, "A", 3, [MeterTypes.RunHours], [new("Capacity", "Number", "kVA")]),
            new("GEN-CNV", "Conveyor", Processing, "C", 6, [], [new("Length", "Number", "m")]),
            new("GEN-LIN", "Production machine", Processing, "B", 6, [MeterTypes.RunHours, MeterTypes.Production],
            [
                new("Machine type", "Text"),
                new("Output", "Number", "units/h"),
                new("Motor power", "Number", "kW"),
            ]),
            IndustryCommon.Boiler("GEN-BLR", "Furnace oil,Diesel,LPG,Coal,Biomass,Electric"),
            IndustryCommon.Transformer("GEN-TRF"),
            IndustryCommon.Weighbridge("GEN-WB"),
            IndustryCommon.Scale("GEN-SCL"),
            IndustryCommon.Vehicle("GEN-VEH", "Vehicle"),
            new("GEN-OTH", "Other Equipment", General, null, null, [], []),
        ],
        RunningChecks =
        [
            IndustryCommon.BoilerShiftLog("GEN-BLR", 6, 10, "Fuel enough for the next shift"),
            IndustryCommon.CompressorDaily("GEN-CMP"),
            IndustryCommon.GeneratorDaily("GEN-GEN"),
        ],
        WindowRoutines =
        [
            IndustryCommon.LubricationRound("GEN-LIN", "GEN-CNV", "GEN-MTR", "GEN-PMP"),
            IndustryCommon.ElectricalPanels("GEN-TRF", "GEN-GEN", "GEN-MTR"),
            IndustryCommon.BoilerWeekly("GEN-BLR"),
        ],
        Compliance = [.. IndustryCommon.KenyaCore("GEN-BLR", "GEN-CMP", "GEN-WB", "GEN-SCL"), IndustryCommon.EffluentLicence()],
        Zones =
        [
            IndustryCommon.Reception,
            IndustryCommon.CarPark,
            new("Stores and workshop", AccessZoneLevels.Controlled, false, true, false, "Safety boots"),
            new("Production floor", AccessZoneLevels.Restricted, true, true, false, "Safety boots, Ear protection"),
            new("Finished goods warehouse", AccessZoneLevels.Controlled, false, true, false, "Safety boots, Reflective vest"),
            new("Utilities and boiler house", AccessZoneLevels.Restricted, true, true, false, "Safety boots, Ear protection"),
            IndustryCommon.Substation,
        ],
        Checkpoints =
        [
            IndustryCommon.MainGate,
            IndustryCommon.Administration,
            new("Production floor", "Production floor", "No machines left running unattended?"),
            new("Finished goods warehouse", "Finished goods warehouse", "Doors locked and sealed?"),
            new("Stores", "Stores and workshop", "Stores locked?"),
            new("Utilities and boiler house", "Utilities and boiler house", null),
            IndustryCommon.SubstationPoint,
            IndustryCommon.Perimeter,
        ],
    };
}
