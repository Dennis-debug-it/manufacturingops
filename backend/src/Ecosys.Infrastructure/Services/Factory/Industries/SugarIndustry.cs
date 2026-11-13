using Ecosys.Domain.Entities;
using static Ecosys.Infrastructure.Services.Factory.AssetGroups;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>
/// Cane sugar mills: cane yard, preparation, milling, juice clarification, evaporation, pan boiling,
/// centrifugals, drying and bagging, with bagasse boilers and turbo-alternators. The yearly overhaul is
/// the off-season (inter-crop) shutdown; the weekly stop is the mill cleaning stop.
/// </summary>
internal static class SugarIndustry
{
    private const string WO = CheckFailActions.WorkOrder;
    private const string Window = CheckFailActions.WindowDefect;
    private const string P = FailureCodeKinds.Problem;

    public static readonly IndustryTemplate Template = new()
    {
        Key = IndustryTemplates.SugarKey,
        Name = "Sugar mill",
        Description = "Cane sugar: cane yard, milling, clarification, evaporation, pan boiling, centrifugals and bagging, with bagasse boilers.",
        ProductionUnit = "tonnes cane crushed",
        ProductionUnitShort = "TC",
        Vocabulary = new("off-season shutdown", "Off-season", "Cane haulage fleet",
            "Tractors, trailers and lorries that bring cane in from the outgrowers", "cane"),
        LowSeasonStartMonth = 3,
        LowSeasonEndMonth = 5,
        PeakMonths = "7,8,9,10,11,12",
        Sections =
        [
            new("CANE", "Cane yard and weighbridge", "Cane weighing, unloading cranes and feeder tables"),
            new("PREP", "Cane preparation", "Cane carriers, kickers, knives and shredder"),
            new("MILL", "Milling", "Mill tandem, intermediate carriers and imbibition"),
            new("CLAR", "Juice clarification", "Juice heaters, liming, clarifiers and mud filters"),
            new("EVAP", "Evaporation", "Multiple-effect evaporators"),
            new("PANS", "Pan boiling", "Vacuum pans and crystallisers"),
            new("CENT", "Centrifugals", "Batch and continuous centrifugals"),
            new("DRYB", "Drying and bagging", "Sugar dryers, conveyors and bagging"),
            new("BOIL", "Boilers and power house", "Bagasse boilers, turbo-alternators and feed water"),
            new("UTIL", "Water, effluent and utilities", "Water treatment, effluent, compressors and substations"),
        ],
        AssetClasses =
        [
            new("SUG-CRN", "Cane unloader crane", Lifting, "A", 6, [MeterTypes.RunHours],
            [
                new("Safe working load", "Number", "t"),
                new("Span", "Number", "m"),
                new("Crane type", "Dropdown", Options: "Overhead travelling,Gantry,Derrick"),
            ]),
            new("SUG-CCR", "Cane carrier", Processing, "A", 6, [MeterTypes.RunHours],
            [
                new("Carrier type", "Dropdown", Options: "Main cane carrier,Feeder table,Intermediate carrier"),
                new("Width", "Number", "mm"),
                new("Drive power", "Number", "kW"),
            ]),
            new("SUG-KNF", "Cane knives", Processing, "A", 3, [MeterTypes.RunHours],
            [
                new("Number of knives", "Number"),
                new("Speed", "Number", "rpm"),
                new("Drive power", "Number", "kW"),
            ]),
            new("SUG-SHR", "Shredder", Processing, "A", 3, [MeterTypes.RunHours],
            [
                new("Number of hammers", "Number"),
                new("Speed", "Number", "rpm"),
                new("Drive", "Dropdown", Options: "Steam turbine,Electric motor"),
                new("Drive power", "Number", "kW"),
            ]),
            new("SUG-MIL", "Mill", Processing, "A", 12, [MeterTypes.RunHours, MeterTypes.Production],
            [
                new("Mill number", "Number"),
                new("Roller size", "Text"),
                new("Hydraulic load", "Number", "t"),
                new("Drive", "Dropdown", Options: "Steam turbine,Electric motor,Hydraulic"),
                new("Last roller re-shelling", "Date"),
            ]),
            new("SUG-JHT", "Juice heater", Steam, "B", 12, [],
            [
                new("Heating surface", "Number", "m²"),
                new("Number of passes", "Number"),
                new("Design pressure", "Number", "bar"),
            ]),
            new("SUG-CLR", "Clarifier", Processing, "B", 12, [],
            [
                new("Clarifier type", "Dropdown", Options: "Multi-tray,Short retention,SRI"),
                new("Capacity", "Number", "m³"),
            ]),
            new("SUG-RVF", "Mud filter", Processing, "B", 6, [MeterTypes.RunHours],
            [
                new("Filter type", "Dropdown", Options: "Rotary vacuum,Belt press"),
                new("Filter area", "Number", "m²"),
            ]),
            new("SUG-EVP", "Evaporator body", Steam, "A", 12, [],
            [
                new("Effect", "Number"),
                new("Heating surface", "Number", "m²"),
                new("Design pressure", "Number", "bar"),
            ]),
            new("SUG-PAN", "Vacuum pan", Steam, "A", 12, [],
            [
                new("Pan type", "Dropdown", Options: "Batch,Continuous"),
                new("Capacity", "Number", "t"),
                new("Heating surface", "Number", "m²"),
            ]),
            new("SUG-CRY", "Crystalliser", Processing, "C", 12, [],
            [
                new("Capacity", "Number", "t"),
                new("Cooling", "Dropdown", Options: "Air,Water"),
            ]),
            new("SUG-CEN", "Centrifugal", Processing, "A", 3, [MeterTypes.RunHours, MeterTypes.Production],
            [
                new("Centrifugal type", "Dropdown", Options: "Batch,Continuous"),
                new("Basket diameter", "Number", "mm"),
                new("Motor power", "Number", "kW"),
                new("Massecuite", "Dropdown", Options: "A,B,C"),
            ]),
            new("SUG-DRY", "Sugar dryer", Processing, "B", 6, [MeterTypes.RunHours],
            [
                new("Dryer type", "Dropdown", Options: "Rotary drum,Fluid bed"),
                new("Capacity", "Number", "t/h"),
            ]),
            new("SUG-BAG", "Bagging machine", Processing, "B", 6, [MeterTypes.RunHours, MeterTypes.Production],
            [
                new("Bag size", "Dropdown", "kg", "1,2,25,50"),
                new("Output", "Number", "bags/h"),
            ]),
            IndustryCommon.Conveyor("SUG-CNV", "Belt,Scraper,Screw,Bucket elevator,Grasshopper"),
            IndustryCommon.Boiler("SUG-BLR", "Bagasse,Bagasse and coal,Furnace oil", "Bagasse boiler"),
            new("SUG-TA", "Turbo-alternator", Power, "A", 12, [MeterTypes.RunHours, MeterTypes.Energy],
            [
                new("Rating", "Number", "MW"),
                new("Inlet steam pressure", "Number", "bar"),
                new("Turbine type", "Dropdown", Options: "Back pressure,Condensing,Extraction"),
            ]),
            IndustryCommon.Generator("SUG-GEN", "Standby generator"),
            IndustryCommon.Transformer("SUG-TRF"),
            IndustryCommon.Compressor("SUG-CMP"),
            IndustryCommon.Motor("SUG-MTR"),
            IndustryCommon.Pump("SUG-PMP", "Juice,Syrup,Massecuite,Molasses,Feed water,Injection water"),
            IndustryCommon.Weighbridge("SUG-WB"),
            IndustryCommon.Scale("SUG-SCL"),
            IndustryCommon.Vehicle("SUG-VEH", "Cane haulage vehicle"),
            IndustryCommon.Other("SUG-OTH"),
        ],
        RunningChecks =
        [
            new("Mill shift check", "SUG-MIL", CheckFrequencies.PerShift,
            [
                new("Hydraulic pressure", "reading", WO, "bar", Required: false),
                new("Bearing temperature", "reading", WO, "°C", Max: 70),
                new("No abnormal noise, knocking or choke", "passfail", WO),
                new("Lubrication and cooling water flowing", "passfail", WO),
                new("Imbibition water flowing", "passfail", Window),
                new("Cane crushed this shift", "reading", Unit: "t", Meter: MeterTypes.Production, Required: false),
            ]),
            new("Shredder and knives shift check", "SUG-SHR", CheckFrequencies.PerShift,
            [
                new("No abnormal vibration", "passfail", WO),
                new("Bearing temperature", "reading", WO, "°C", Max: 75),
                new("Drive current", "reading", Unit: "A", Required: false),
                new("Preparation index looks normal", "passfail", Window),
            ]),
            new("Centrifugal shift check", "SUG-CEN", CheckFrequencies.PerShift,
            [
                new("Basket running without vibration", "passfail", WO),
                new("Screens intact, no torn mesh", "passfail", WO),
                new("Brake and interlocks working", "passfail", WO),
                new("Cycle time", "reading", Unit: "min", Required: false),
            ]),
            new("Turbo-alternator shift check", "SUG-TA", CheckFrequencies.PerShift,
            [
                new("Lube oil pressure", "reading", WO, "bar", Min: 1.5m),
                new("Bearing temperature", "reading", WO, "°C", Max: 85),
                new("Load", "reading", Unit: "kW", Required: false),
                new("No steam leaks at glands", "passfail", Window),
            ]),
            IndustryCommon.BoilerShiftLog("SUG-BLR", 18, 45, "Bagasse feed steady and enough in the yard"),
            IndustryCommon.CompressorDaily("SUG-CMP"),
            IndustryCommon.GeneratorDaily("SUG-GEN"),
        ],
        WindowRoutines =
        [
            IndustryCommon.LubricationRound("SUG-CCR", "SUG-MIL", "SUG-SHR", "SUG-KNF", "SUG-CNV"),
            new("Knife and hammer inspection", ["SUG-KNF", "SUG-SHR"], false, 3,
            [
                ("Knife edges and bolts checked", "checkbox", null),
                ("Shredder hammer tips: wear checked", "checkbox", null),
                ("Hammers or knives to replace", "text", null),
            ]),
            new("Mill roller and trash plate check", ["SUG-MIL"], false, 2,
            [
                ("Roller grooves and arcing checked", "checkbox", null),
                ("Trash plate setting", "number", "mm"),
                ("Juice grooves and scrapers clean", "checkbox", null),
            ]),
            new("Heater and evaporator cleaning", ["SUG-JHT", "SUG-EVP"], false, 6,
            [
                ("Tubes cleaned (chemical or mechanical)", "checkbox", null),
                ("Scale thickness after cleaning", "number", "mm"),
                ("Doors and gaskets resealed and pressure tested", "checkbox", null),
            ]),
            new("Centrifugal screen and basket check", ["SUG-CEN"], false, 1.5m,
            [
                ("Screens and backing screens intact", "checkbox", null),
                ("Basket and spindle bearings checked", "checkbox", null),
                ("Ploughs and discharge valves working", "checkbox", null),
            ]),
            IndustryCommon.ElectricalPanels("SUG-TRF", "SUG-TA", "SUG-MTR", "SUG-GEN"),
            IndustryCommon.BoilerWeekly("SUG-BLR"),
        ],
        FailureCodes =
        [
            new(P, "MIL-CHK", "Mill choke", "SUG-MIL"),
            new(P, "MIL-ROL", "Roller grooves worn or arcing lost", "SUG-MIL"),
            new(P, "MIL-BRG", "Mill bearing overheating", "SUG-MIL"),
            new(P, "KNF-BLN", "Cane knives blunt or broken", "SUG-KNF"),
            new(P, "SHR-HAM", "Shredder hammers worn", "SUG-SHR"),
            new(P, "EVP-SCL", "Evaporator scaling", "SUG-EVP"),
            new(P, "PAN-VAC", "Loss of vacuum on pan", "SUG-PAN"),
            new(P, "CEN-SCR", "Centrifugal screen torn", "SUG-CEN"),
            new(P, "TA-TRIP", "Turbo-alternator trip", "SUG-TA"),
            new(P, "BLR-LOW", "Low steam pressure", "SUG-BLR"),
            new(P, "BLR-WTR", "Low water level trip", "SUG-BLR"),
            new(P, "WB-CAL", "Weighbridge reading out of calibration", "SUG-WB"),
        ],
        Compliance =
        [
            .. IndustryCommon.KenyaCore("SUG-BLR", "SUG-CMP", "SUG-WB", "SUG-SCL"),
            new("pressure-vessels", "Pressure vessel examination (evaporators, juice heaters, pans)", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.69 (DOSH)", 24, ["SUG-EVP", "SUG-JHT", "SUG-PAN"]),
            new("cane-cranes", "Cane unloader crane examination", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.63 (DOSH)", 12, ["SUG-CRN"]),
            new("kebs-sugar", "KEBS standardisation mark for sugar", ComplianceKinds.Certificate, "Standards Act (Cap 496)", 12, null, 60),
            new("sugar-licence", "Sugar milling licence (Kenya Sugar Board)", ComplianceKinds.Certificate, "Sugar Act 2024", 12, null, 60),
            IndustryCommon.EffluentLicence(),
        ],
        Zones =
        [
            IndustryCommon.Reception,
            IndustryCommon.CarPark,
            new("Cane yard and weighbridge", AccessZoneLevels.Controlled, false, true, false, "Reflective vest, Safety boots, Hard hat"),
            new("Stores and workshop", AccessZoneLevels.Controlled, false, true, false, "Safety boots"),
            new("Mill house", AccessZoneLevels.Restricted, true, true, false, "Hard hat, Ear protection, Safety boots"),
            new("Boiling house", AccessZoneLevels.Restricted, true, true, false, "Hard hat, Safety boots"),
            new("Sugar store and bagging", AccessZoneLevels.Restricted, true, true, false, "Hairnet, Dust coat, Safety boots"),
            new("Boilers and power house", AccessZoneLevels.Restricted, true, true, false, "Hard hat, Ear protection, Safety boots"),
            new("Molasses tanks and fuel store", AccessZoneLevels.Restricted, true, true, false, "Safety boots"),
            IndustryCommon.Substation,
            new("Effluent treatment plant", AccessZoneLevels.Controlled, false, true, false, "Gumboots"),
        ],
        Checkpoints =
        [
            IndustryCommon.MainGate,
            IndustryCommon.Administration,
            new("Cane yard", "Cane yard and weighbridge", "No unattended cane trucks?\nNo fires in the yard?"),
            new("Weighbridge", "Cane yard and weighbridge", "Weighbridge office locked?"),
            new("Mill house", "Mill house", "No machines left running unattended?"),
            new("Boiling house", "Boiling house", null),
            new("Sugar warehouse", "Sugar store and bagging", "Doors locked and sealed?"),
            new("Molasses tanks", "Molasses tanks and fuel store", "Valves locked, no leaks?"),
            new("Bagasse yard", null, "No smouldering or fire?"),
            new("Boiler and power house", "Boilers and power house", "Boiler attendant present?"),
            IndustryCommon.SubstationPoint,
            IndustryCommon.Perimeter,
        ],
    };
}
