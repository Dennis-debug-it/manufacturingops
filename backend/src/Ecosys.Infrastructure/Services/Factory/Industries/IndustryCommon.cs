using Ecosys.Domain.Entities;
using static Ecosys.Infrastructure.Services.Factory.AssetGroups;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>
/// Plant every factory has, whatever it makes: boilers, generators, compressors, motors, pumps,
/// weighbridges, and the Kenyan statutory items that go with them. Industry packs build their own
/// copies with their own class codes (SUG-BLR, DAI-BLR…), so each industry's data stays its own.
/// </summary>
internal static class IndustryCommon
{
    private const string WO = CheckFailActions.WorkOrder;
    private const string Window = CheckFailActions.WindowDefect;

    // ── Asset classes ─────────────────────────────────────────────────────────────────────────

    public static AssetClassTemplate Boiler(string code, string fuels, string name = "Boiler") => new(code, name, Steam, "A", 1, [MeterTypes.RunHours],
    [
        new("Boiler type", "Dropdown", Options: "Fire tube,Water tube,Thermic fluid heater"),
        new("Steam capacity", "Number", "kg/h"),
        new("Design pressure", "Number", "bar"),
        new("Fuel", "Dropdown", Options: fuels),
        new("Registration number", "Text"),
        new("Certificate expiry", "Date"),
    ]);

    public static AssetClassTemplate Generator(string code, string name = "Generator") => new(code, name, Power, "A", 3, [MeterTypes.RunHours],
    [
        new("Capacity", "Number", "kVA"),
        new("Fuel type", "Dropdown", Options: "Diesel,Petrol,Gas"),
        new("Tank capacity", "Number", "L"),
        new("Engine make", "Text"),
    ]);

    public static AssetClassTemplate Transformer(string code) => new(code, "Transformer", Power, "A", 12, [MeterTypes.Energy],
    [
        new("Rating", "Number", "kVA"),
        new("Primary voltage", "Number", "kV"),
        new("Secondary voltage", "Number", "V"),
        new("Cooling", "Dropdown", Options: "ONAN,ONAF,Dry type"),
    ]);

    public static AssetClassTemplate Compressor(string code, string name = "Air compressor") => new(code, name, Power, "B", 3, [MeterTypes.RunHours],
    [
        new("Compressor type", "Dropdown", Options: "Screw,Piston"),
        new("Free air delivery", "Number", "m³/min"),
        new("Working pressure", "Number", "bar"),
        new("Motor power", "Number", "kW"),
    ]);

    public static AssetClassTemplate Motor(string code) => new(code, "Electric motor", Power, "C", 6, [],
    [
        new("Power", "Number", "kW"),
        new("Speed", "Number", "rpm"),
        new("Voltage", "Number", "V"),
        new("Frame size", "Text"),
        new("Drive-end bearing", "Text"),
        new("Non-drive-end bearing", "Text"),
    ]);

    public static AssetClassTemplate Pump(string code, string types = "Centrifugal,Feed water,Submersible,Positive displacement") => new(code, "Pump", Power, "C", 6, [MeterTypes.RunHours],
    [
        new("Pump type", "Dropdown", Options: types),
        new("Flow", "Number", "m³/h"),
        new("Head", "Number", "m"),
        new("Motor power", "Number", "kW"),
    ]);

    public static AssetClassTemplate Conveyor(string code, string types = "Belt,Chain,Screw,Bucket elevator") => new(code, "Conveyor", Processing, "C", 6, [],
    [
        new("Conveyor type", "Dropdown", Options: types),
        new("Length", "Number", "m"),
        new("Motor power", "Number", "kW"),
    ]);

    public static AssetClassTemplate Weighbridge(string code) => new(code, "Weighbridge", Measuring, "B", 12, [],
    [
        new("Capacity", "Number", "t"),
        new("Platform length", "Number", "m"),
        new("Calibration certificate", "Text"),
        new("Calibration due", "Date"),
    ]);

    public static AssetClassTemplate Scale(string code) => new(code, "Scale", Measuring, "C", 12, [],
    [
        new("Capacity", "Number", "kg"),
        new("Resolution", "Number", "g"),
        new("Calibration due", "Date"),
    ]);

    public static AssetClassTemplate Vehicle(string code, string name, string payloadUnit = "t") => new(code, name, Vehicles, "B", 3, [MeterTypes.Odometer],
    [
        new("Registration number", "Text"),
        new("Make and model", "Text"),
        new("Year", "Number"),
        new("Fuel", "Dropdown", Options: "Diesel,Petrol"),
        new("Payload", "Number", payloadUnit),
    ]);

    public static AssetClassTemplate Other(string code) => new(code, "Other Equipment", General, null, null, [], []);

    // ── Running checks ────────────────────────────────────────────────────────────────────────

    public static CheckTemplate BoilerShiftLog(string code, decimal minBar, decimal maxBar, string fuelQuestion) => new("Boiler shift log", code, CheckFrequencies.PerShift,
    [
        new("Steam pressure", "reading", WO, "bar", minBar, maxBar),
        new("Water level gauge glass normal", "passfail", WO),
        new("Blowdown done", "yesno"),
        new("Feed water TDS", "reading", Window, "ppm", Max: 3500, Required: false),
        new("Safety valve lever test", "passfail", WO),
        new(fuelQuestion, "yesno"),
        new("Boiler run hours", "reading", Unit: "h", Meter: MeterTypes.RunHours, Required: false),
    ]);

    public static CheckTemplate CompressorDaily(string code) => new("Compressor daily check", code, CheckFrequencies.Daily,
    [
        new("Discharge pressure", "reading", WO, "bar", 6, 8),
        new("Oil level correct", "passfail", Window),
        new("Condensate drained", "yesno"),
        new("Compressor run hours", "reading", Unit: "h", Meter: MeterTypes.RunHours, Required: false),
    ]);

    public static CheckTemplate GeneratorDaily(string code) => new("Generator daily check", code, CheckFrequencies.Daily,
    [
        new("Fuel level", "reading", WO, "%", Min: 25),
        new("Battery voltage", "reading", WO, "V", 24, 28.5m),
        new("Coolant level correct", "passfail", WO),
        new("Engine oil level correct", "passfail", WO),
        new("Generator run hours", "reading", Unit: "h", Meter: MeterTypes.RunHours, Required: false),
    ]);

    // ── Weekly window routines ────────────────────────────────────────────────────────────────

    public static RoutineTemplate ElectricalPanels(params string[] codes) => new("Electrical panel inspection", codes, true, 2,
    [
        ("Panels dust-free and doors closing", "checkbox", null),
        ("No hot spots or burnt terminals", "checkbox", null),
        ("Earthing and covers in place", "checkbox", null),
    ]);

    public static RoutineTemplate BoilerWeekly(string code) => new("Boiler weekly checks", [code], false, 3,
    [
        ("Water level gauges blown down and clear", "checkbox", null),
        ("Safety valves tested (lift)", "checkbox", null),
        ("Fire tubes and grate cleaned", "checkbox", null),
        ("Working pressure on restart", "number", "bar"),
    ]);

    public static RoutineTemplate LubricationRound(params string[] codes) => new("Weekly lubrication round", codes, true, 3,
    [
        ("Grease bearings to the lubrication chart", "checkbox", null),
        ("Gearbox oil levels topped up", "checkbox", null),
        ("Chains oiled; no dry or seized links", "checkbox", null),
        ("Note any leaking seals", "text", null),
    ]);

    // ── Kenyan statutory items ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The items every Kenyan factory tracks under OSHA 2007, the Weights and Measures Act and the fire
    /// rules, tied to the pack's own boiler, compressor, weighbridge and scale classes (null to leave out).
    /// </summary>
    public static IReadOnlyList<ComplianceTemplate> KenyaCore(string? boiler, string? compressor, string? weighbridge, string? scale) =>
    [
        .. boiler is null ? Array.Empty<ComplianceTemplate>() :
        [
            new("boiler-exam", "Boiler examination by an approved person", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.67 (DOSH)", 14, [boiler]),
            new("boiler-hydro", "Boiler hydraulic test", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.67 (DOSH)", 24, [boiler]),
        ],
        .. compressor is null ? Array.Empty<ComplianceTemplate>() :
            [new("air-receiver", "Air receiver (pressure vessel) examination", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.69–70 (DOSH)", 24, [compressor])],
        new("lifting-appliances", "Lifting appliances examination (hoists, chain blocks, cranes)", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.63 (DOSH)", 12, null),
        new("lifting-tackle", "Chains, ropes and lifting tackle examination", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.64 (DOSH)", 6, null),
        .. weighbridge is null ? Array.Empty<ComplianceTemplate>() :
            [new("weighbridge", "Weighbridge verification", ComplianceKinds.Calibration, "Weights and Measures Act (Cap 513)", 12, [weighbridge])],
        .. scale is null ? Array.Empty<ComplianceTemplate>() :
            [new("scales", "Scale verification and calibration", ComplianceKinds.Calibration, "Weights and Measures Act (Cap 513)", 12, [scale])],
        new("fire-extinguishers", "Fire extinguishers service", ComplianceKinds.FireSafety, "Fire Risk Reduction Rules 2007", 12, null),
        new("fire-audit", "Fire safety audit", ComplianceKinds.FireSafety, "Fire Risk Reduction Rules 2007", 12, null),
        new("osh-audit", "Occupational safety and health audit", ComplianceKinds.Certificate, "OSHA 2007 s.11", 12, null),
        new("workplace-registration", "Workplace registration certificate", ComplianceKinds.Certificate, "OSHA 2007 s.44", 12, null, null),
        new("medical-exam", "Medical examination of exposed workers", ComplianceKinds.Certificate, "Medical Examination Rules 2005", 12, null, null),
    ];

    public static ComplianceTemplate EffluentLicence() =>
        new("effluent-licence", "Effluent discharge licence", ComplianceKinds.Certificate, "EMCA (Water Quality) Regulations 2006 (NEMA)", 12, null, 60);

    // ── Zones and checkpoints every site has ──────────────────────────────────────────────────

    public static ZoneTemplate Reception => new("Reception and offices", AccessZoneLevels.Public, false, false, false, null);
    public static ZoneTemplate CarPark => new("Car park", AccessZoneLevels.Public, false, false, false, null);
    public static ZoneTemplate Substation => new("Electrical substation", AccessZoneLevels.Restricted, true, true, true, "Safety boots, Insulated gloves");
    public static CheckpointTemplate MainGate => new("Main entrance", null, "Gate locked?\nGuard house manned?");
    public static CheckpointTemplate Administration => new("Administration block", "Reception and offices", "Doors and windows locked?");
    public static CheckpointTemplate SubstationPoint => new("Electrical substation", "Electrical substation", "Fence and gate intact?");
    public static CheckpointTemplate Perimeter => new("Perimeter fence", null, "Fence intact, no breaches?\nSecurity lights working?");
}
