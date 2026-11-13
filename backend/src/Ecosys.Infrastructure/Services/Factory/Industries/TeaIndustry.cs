using Ecosys.Domain.Entities;
using static Ecosys.Infrastructure.Services.Factory.AssetGroups;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>
/// Tea factories (orthodox and CTC black tea), as Ecosys launched with. Codes are TEA-…; they are the
/// codes existing tea workspaces hold, so they must not change.
/// </summary>
internal static class TeaIndustry
{
    private const string WO = CheckFailActions.WorkOrder;
    private const string Window = CheckFailActions.WindowDefect;
    private const string P = FailureCodeKinds.Problem;

    public static readonly IndustryTemplate Template = new()
    {
        Key = IndustryTemplates.TeaKey,
        Name = "Tea factory",
        Description = "Black tea (CTC or orthodox): leaf reception, withering, rolling, fermentation, drying, sorting and packing.",
        ProductionUnit = "kg made tea",
        ProductionUnitShort = "kg MT",
        Vocabulary = new("low-crop shutdown", "Low-crop season", "Leaf collection fleet",
            "Lorries, pickups and tractors that bring green leaf in", "green leaf"),
        LowSeasonStartMonth = 7,
        LowSeasonEndMonth = 9,
        PeakMonths = "3,4,5,10,11,12",
        Sections =
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
        AssetClasses =
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
            ],
        RunningChecks =
        [
            new("Withering fan shift check", "TEA-WFN", CheckFrequencies.PerShift,
            [
                new("Fan running without abnormal noise", "passfail", WO),
                new("No abnormal vibration", "passfail", WO),
                new("Belt tension and guard in place", "passfail", Window),
                new("Motor current", "reading", Unit: "A", Required: false),
            ]),
            new("CTC machine shift check", "TEA-CTC", CheckFrequencies.PerShift,
            [
                new("Roller gap set correctly", "passfail", Window),
                new("No abnormal noise or knocking", "passfail", WO),
                new("Bearing temperature", "reading", WO, "°C", Max: 80),
                new("Lubrication done", "yesno"),
                new("Leaf processed this shift", "reading", Unit: "kg", Meter: MeterTypes.Production, Required: false),
            ]),
            new("Fluid bed dryer shift check", "TEA-FBD", CheckFrequencies.PerShift,
            [
                new("Inlet air temperature", "reading", WO, "°C", 100, 140, MeterTypes.Temperature),
                new("Exhaust temperature", "reading", Unit: "°C", Required: false),
                new("Bed fluidising evenly", "passfail", WO),
                new("Fire detection and spark arrestor working", "passfail", WO),
                new("Dryer run hours", "reading", Unit: "h", Meter: MeterTypes.RunHours, Required: false),
            ]),
            new("Boiler shift log", "TEA-BLR", CheckFrequencies.PerShift,
            [
                new("Steam pressure", "reading", WO, "bar", 6, 10),
                new("Water level gauge glass normal", "passfail", WO),
                new("Blowdown done", "yesno"),
                new("Feed water TDS", "reading", Window, "ppm", Max: 3500, Required: false),
                new("Safety valve lever test", "passfail", WO),
                new("Fuel (firewood) stock enough for next shift", "yesno"),
                new("Boiler run hours", "reading", Unit: "h", Meter: MeterTypes.RunHours, Required: false),
            ]),
            new("Compressor daily check", "TEA-CMP", CheckFrequencies.Daily,
            [
                new("Discharge pressure", "reading", WO, "bar", 6, 8),
                new("Oil level correct", "passfail", Window),
                new("Condensate drained", "yesno"),
                new("Compressor run hours", "reading", Unit: "h", Meter: MeterTypes.RunHours, Required: false),
            ]),
            new("Generator daily check", "TEA-GEN", CheckFrequencies.Daily,
            [
                new("Fuel level", "reading", WO, "%", Min: 25),
                new("Battery voltage", "reading", WO, "V", 24, 28.5m),
                new("Coolant level correct", "passfail", WO),
                new("Engine oil level correct", "passfail", WO),
                new("Generator run hours", "reading", Unit: "h", Meter: MeterTypes.RunHours, Required: false),
            ]),
        ],
        WindowRoutines =
        [
            new("Weekly lubrication round", ["TEA-CTC", "TEA-RV", "TEA-CFU", "TEA-CNV", "TEA-WFN", "TEA-SRT"], true, 3,
            [
                ("Grease bearings to the lubrication chart", "checkbox", null),
                ("Gearbox oil levels topped up", "checkbox", null),
                ("Chains oiled; no dry or seized links", "checkbox", null),
                ("Note any leaking seals", "text", null),
            ]),
            new("Belt and chain tension check", ["TEA-CNV", "TEA-WFN", "TEA-CTC", "TEA-RV"], true, 2,
            [
                ("Belts: tension and wear checked, guards refitted", "checkbox", null),
                ("Chains: tension and sprocket wear checked", "checkbox", null),
                ("Belts or chains to replace", "text", null),
            ]),
            new("CTC segment inspection", ["TEA-CTC"], false, 2,
            [
                ("Segment teeth: wear and chipping checked", "checkbox", null),
                ("Roller gap", "number", "mm"),
                ("Segments to re-cut or replace", "text", null),
            ]),
            new("Dryer and fluid bed cleaning", ["TEA-FBD"], false, 4,
            [
                ("Perforated bed and chambers cleaned", "checkbox", null),
                ("Cyclones and ducts cleared of fluff", "checkbox", null),
                ("Heat exchanger tubes inspected", "checkbox", null),
                ("Fan impellers clean and balanced", "checkbox", null),
            ]),
            new("Withering fan check", ["TEA-WFN"], true, 1.5m,
            [
                ("Blades and hub tight, no cracks", "checkbox", null),
                ("Motor mounting and cable glands secure", "checkbox", null),
                ("Motor current on trial run", "number", "A"),
            ]),
            new("Electrical panel inspection", ["TEA-TRF", "TEA-GEN", "TEA-MTR"], true, 2,
            [
                ("Panels dust-free and doors closing", "checkbox", null),
                ("No hot spots or burnt terminals", "checkbox", null),
                ("Earthing and covers in place", "checkbox", null),
            ]),
            new("Boiler weekly checks", ["TEA-BLR"], false, 3,
            [
                ("Water level gauges blown down and clear", "checkbox", null),
                ("Safety valves tested (lift)", "checkbox", null),
                ("Fire tubes and grate cleaned", "checkbox", null),
                ("Working pressure on restart", "number", "bar"),
            ]),
        ],
        FailureCodes =
        [
            new(P, "CTC-TTH", "Roller teeth blunt or worn", "TEA-CTC"),
            new(P, "CTC-GAP", "Roller gap out of setting", "TEA-CTC"),
            new(P, "RV-CHK", "Rotorvane choking", "TEA-RV"),
            new(P, "FBD-FIRE", "Fire in dryer chamber", "TEA-FBD"),
            new(P, "FBD-BED", "Uneven or choked fluid bed", "TEA-FBD"),
            new(P, "WTR-MESH", "Trough mesh torn", "TEA-WTR"),
            new(P, "WF-IMP", "Fan impeller damaged or unbalanced", "TEA-WFN"),
            new(P, "BLR-LOW", "Low steam pressure", "TEA-BLR"),
            new(P, "BLR-WTR", "Low water level trip", "TEA-BLR"),
            new(P, "WB-CAL", "Weighbridge reading out of calibration", "TEA-WB"),
        ],
        Compliance =
        [
            new("boiler-exam", "Boiler examination by an approved person", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.67 (DOSH)", 14, ["TEA-BLR"]),
            new("boiler-hydro", "Boiler hydraulic test", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.67 (DOSH)", 24, ["TEA-BLR"]),
            new("air-receiver", "Air receiver (pressure vessel) examination", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.69–70 (DOSH)", 24, ["TEA-CMP"]),
            new("lifting-appliances", "Lifting appliances examination (hoists, chain blocks, cranes)", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.63 (DOSH)", 12, null),
            new("lifting-tackle", "Chains, ropes and lifting tackle examination", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.64 (DOSH)", 6, null),
            new("weighbridge", "Weighbridge verification", ComplianceKinds.Calibration, "Weights and Measures Act (Cap 513)", 12, ["TEA-WB"]),
            new("scales", "Scale verification and calibration", ComplianceKinds.Calibration, "Weights and Measures Act (Cap 513)", 12, ["TEA-SCL"]),
            new("metal-detector", "Metal detector calibration and test", ComplianceKinds.Calibration, "ISO 22000 / customer requirement", 6, null),
            new("fire-extinguishers", "Fire extinguishers service", ComplianceKinds.FireSafety, "Fire Risk Reduction Rules 2007", 12, null),
            new("fire-audit", "Fire safety audit", ComplianceKinds.FireSafety, "Fire Risk Reduction Rules 2007", 12, null),
            new("osh-audit", "Occupational safety and health audit", ComplianceKinds.Certificate, "OSHA 2007 s.11", 12, null),
            new("workplace-registration", "Workplace registration certificate", ComplianceKinds.Certificate, "OSHA 2007 s.44", 12, null, null),
            new("medical-exam", "Medical examination of exposed workers", ComplianceKinds.Certificate, "Medical Examination Rules 2005", 12, null, null),
        ],
        Zones =
        [
            new("Reception and offices", AccessZoneLevels.Public, false, false, false, null),
            new("Car park", AccessZoneLevels.Public, false, false, false, null),
            new("Weighbridge", AccessZoneLevels.Controlled, false, true, false, "Reflective vest"),
            new("Green leaf receiving", AccessZoneLevels.Controlled, false, true, false, "Reflective vest, Safety boots"),
            new("Stores and workshop", AccessZoneLevels.Controlled, false, true, false, "Safety boots"),
            new("Processing hall", AccessZoneLevels.Restricted, true, true, false, "Hairnet, Dust coat, Safety boots"),
            new("Packing and finished goods", AccessZoneLevels.Restricted, true, true, false, "Hairnet, Dust coat"),
            new("Boiler house", AccessZoneLevels.Restricted, true, true, false, "Safety boots, Ear protection"),
            new("Fuel and firewood store", AccessZoneLevels.Restricted, true, true, false, "Safety boots"),
            new("Electrical substation", AccessZoneLevels.Restricted, true, true, true, "Safety boots, Insulated gloves"),
        ],
        Checkpoints =
        [
            new("Main entrance", null, "Gate locked?\nGuard house manned?"),
            new("Administration block", "Reception and offices", "Doors and windows locked?"),
            new("Weighbridge", "Weighbridge", "Weighbridge office locked?"),
            new("Green leaf receiving", "Green leaf receiving", null),
            new("Processing hall", "Processing hall", "No machines left running unattended?"),
            new("Boiler house", "Boiler house", "Boiler attendant present?"),
            new("Generator room", null, "Door locked?"),
            new("Fuel and firewood store", "Fuel and firewood store", "Padlock intact?\nNo fuel leaks?"),
            new("Finished goods warehouse", "Packing and finished goods", "Doors locked and sealed?"),
            new("Electrical substation", "Electrical substation", "Fence and gate intact?"),
            new("Water treatment plant", null, null),
            new("Perimeter fence", null, "Fence intact, no breaches?\nSecurity lights working?"),
        ],
    };
}
