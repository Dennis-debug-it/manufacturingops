using Ecosys.Domain.Entities;
using static Ecosys.Infrastructure.Services.Factory.AssetGroups;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>
/// Dairy processors: milk reception and cooling, separation, pasteurisation and UHT, cultured products,
/// butter and ghee, milk powder, packaging and cold stores, with CIP, boilers and ammonia refrigeration.
/// Food safety drives the checks: holding temperatures, cold chain and cleaning.
/// </summary>
internal static class DairyIndustry
{
    private const string WO = CheckFailActions.WorkOrder;
    private const string Window = CheckFailActions.WindowDefect;
    private const string P = FailureCodeKinds.Problem;

    public static readonly IndustryTemplate Template = new()
    {
        Key = IndustryTemplates.DairyKey,
        Name = "Dairy",
        Description = "Milk processing: reception, pasteurisation and UHT, yoghurt and mala, butter, powder and packaging, with cold chain and CIP.",
        ProductionUnit = "litres of milk processed",
        ProductionUnitShort = "L milk",
        Vocabulary = new("annual shutdown", "Low-intake season", "Milk collection fleet",
            "Tankers and pickups that collect milk from cooling centres and farmers", "raw milk"),
        LowSeasonStartMonth = 1,
        LowSeasonEndMonth = 3,
        PeakMonths = "4,5,6,10,11,12",
        Sections =
        [
            new("RECV", "Milk reception", "Tanker bay, testing, cooling and raw milk silos"),
            new("PROC", "Processing", "Separation, standardisation, pasteurisation and homogenising"),
            new("UHT", "UHT and long life", "UHT plant and aseptic filling"),
            new("CULT", "Cultured products", "Yoghurt, mala and cheese"),
            new("BUTR", "Butter and ghee", "Churning and ghee"),
            new("PWDR", "Milk powder", "Evaporator and spray dryer"),
            new("PACK", "Packaging", "Pouch, bottle and carton filling"),
            new("COLD", "Cold stores and dispatch", "Cold rooms, crates and loading"),
            new("CIP", "CIP and hygiene", "Cleaning-in-place stations"),
            new("UTIL", "Boilers, refrigeration and utilities", "Boilers, ammonia plant, compressors and effluent"),
        ],
        AssetClasses =
        [
            new("DAI-SIL", "Milk silo or tank", Processing, "B", 12, [],
            [
                new("Capacity", "Number", "L"),
                new("Insulated", "Dropdown", Options: "Yes,No"),
                new("Agitator power", "Number", "kW"),
            ]),
            new("DAI-PHE", "Plate cooler", Refrigeration, "B", 12, [],
            [
                new("Capacity", "Number", "L/h"),
                new("Cooling medium", "Dropdown", Options: "Ice water,Glycol,Ammonia"),
            ]),
            new("DAI-SEP", "Separator", Processing, "A", 6, [MeterTypes.RunHours],
            [
                new("Separator type", "Dropdown", Options: "Cream separator,Clarifier,Bactofuge"),
                new("Capacity", "Number", "L/h"),
                new("Motor power", "Number", "kW"),
            ]),
            new("DAI-PAS", "Pasteuriser (HTST)", Processing, "A", 6, [MeterTypes.RunHours, MeterTypes.Production],
            [
                new("Capacity", "Number", "L/h"),
                new("Holding temperature", "Number", "°C"),
                new("Holding time", "Number", "s"),
                new("Plate pack sections", "Number"),
            ]),
            new("DAI-HOM", "Homogeniser", Processing, "A", 3, [MeterTypes.RunHours],
            [
                new("Capacity", "Number", "L/h"),
                new("Pressure", "Number", "bar"),
                new("Number of pistons", "Number"),
            ]),
            new("DAI-UHT", "UHT plant", Processing, "A", 3, [MeterTypes.RunHours, MeterTypes.Production],
            [
                new("Heating type", "Dropdown", Options: "Indirect plate,Indirect tubular,Direct steam injection"),
                new("Capacity", "Number", "L/h"),
            ]),
            new("DAI-FRM", "Fermentation or process tank", Processing, "C", 12, [],
            [
                new("Capacity", "Number", "L"),
                new("Jacketed", "Dropdown", Options: "Yes,No"),
            ]),
            new("DAI-CHN", "Butter churn", Processing, "B", 6, [MeterTypes.RunHours],
            [
                new("Churn type", "Dropdown", Options: "Batch,Continuous"),
                new("Capacity", "Number", "kg/h"),
            ]),
            new("DAI-EVP", "Evaporator", Steam, "A", 12, [MeterTypes.RunHours],
            [
                new("Effects", "Number"),
                new("Water evaporation", "Number", "kg/h"),
            ]),
            new("DAI-SPD", "Spray dryer", Processing, "A", 6, [MeterTypes.RunHours, MeterTypes.Production],
            [
                new("Powder output", "Number", "kg/h"),
                new("Inlet air temperature", "Number", "°C"),
            ]),
            new("DAI-FIL", "Filling machine", Processing, "A", 3, [MeterTypes.RunHours, MeterTypes.Production],
            [
                new("Pack type", "Dropdown", Options: "Pouch,Bottle,Cup,Carton,Aseptic brick"),
                new("Output", "Number", "packs/h"),
            ]),
            new("DAI-CIP", "CIP station", Hygiene, "B", 6, [],
            [
                new("Tanks", "Number"),
                new("Circuits", "Number"),
            ]),
            new("DAI-REF", "Refrigeration compressor", Refrigeration, "A", 3, [MeterTypes.RunHours],
            [
                new("Refrigerant", "Dropdown", Options: "Ammonia,R404A,R134a,Glycol chiller"),
                new("Cooling capacity", "Number", "kW"),
                new("Compressor type", "Dropdown", Options: "Screw,Reciprocating"),
            ]),
            new("DAI-CLD", "Cold room", Refrigeration, "B", 6, [MeterTypes.Temperature],
            [
                new("Volume", "Number", "m³"),
                new("Set temperature", "Number", "°C"),
            ]),
            IndustryCommon.Boiler("DAI-BLR", "Furnace oil,Diesel,LPG,Briquettes,Firewood"),
            IndustryCommon.Generator("DAI-GEN"),
            IndustryCommon.Transformer("DAI-TRF"),
            IndustryCommon.Compressor("DAI-CMP"),
            IndustryCommon.Motor("DAI-MTR"),
            IndustryCommon.Pump("DAI-PMP", "Centrifugal (sanitary),Positive displacement,Feed water,CIP return"),
            new("DAI-FLM", "Flow meter", Measuring, "B", 12, [],
            [
                new("Meter type", "Dropdown", Options: "Electromagnetic,Mass (Coriolis),Turbine"),
                new("Range", "Text"),
                new("Calibration due", "Date"),
            ]),
            IndustryCommon.Weighbridge("DAI-WB"),
            IndustryCommon.Scale("DAI-SCL"),
            IndustryCommon.Vehicle("DAI-VEH", "Milk tanker", "L"),
            IndustryCommon.Other("DAI-OTH"),
        ],
        RunningChecks =
        [
            new("Pasteuriser shift check", "DAI-PAS", CheckFrequencies.PerShift,
            [
                new("Holding temperature", "reading", WO, "°C", Min: 72, Meter: MeterTypes.Temperature),
                new("Flow diversion valve tested and diverting below limit", "passfail", WO),
                new("Temperature chart recorder working", "passfail", WO),
                new("Regeneration section: no leaks or pressure reversal", "passfail", WO),
                new("Milk pasteurised this shift", "reading", Unit: "L", Meter: MeterTypes.Production, Required: false),
            ]),
            new("Milk cooler check", "DAI-PHE", CheckFrequencies.PerShift,
            [
                new("Milk temperature after cooling", "reading", WO, "°C", Max: 4),
                new("Ice water or glycol temperature", "reading", Window, "°C", Max: 2, Required: false),
                new("No leaks at plate pack", "passfail", WO),
            ]),
            new("Cold room daily check", "DAI-CLD", CheckFrequencies.Daily,
            [
                new("Room temperature", "reading", WO, "°C", Max: 5, Meter: MeterTypes.Temperature),
                new("Door seals and curtains intact", "passfail", Window),
                new("Evaporator coils free of heavy ice", "passfail", Window),
            ]),
            new("Ammonia plant daily check", "DAI-REF", CheckFrequencies.Daily,
            [
                new("No ammonia smell in the plant room", "passfail", WO),
                new("Suction pressure", "reading", Unit: "bar", Required: false),
                new("Discharge pressure", "reading", WO, "bar", Max: 15),
                new("Oil level correct", "passfail", Window),
                new("Gas detector and alarm panel normal", "passfail", WO),
            ]),
            new("CIP check", "DAI-CIP", CheckFrequencies.PerShift,
            [
                new("Caustic concentration", "reading", WO, "%", 1, 2),
                new("Caustic temperature", "reading", WO, "°C", Min: 70),
                new("Acid concentration", "reading", Window, "%", 0.5m, 1.5m, Required: false),
                new("Return flow and spray balls working", "passfail", WO),
            ]),
            IndustryCommon.BoilerShiftLog("DAI-BLR", 6, 10, "Fuel enough for the next shift"),
            IndustryCommon.GeneratorDaily("DAI-GEN"),
        ],
        WindowRoutines =
        [
            new("Plate heat exchanger inspection", ["DAI-PAS", "DAI-PHE"], false, 3,
            [
                ("Gaskets checked for hardening and leaks", "checkbox", null),
                ("Plates dye-tested or pressure-tested for pinholes", "checkbox", null),
                ("Gaskets or plates to replace", "text", null),
            ]),
            new("Homogeniser valve and seal check", ["DAI-HOM"], false, 2,
            [
                ("Homogenising valves and seats inspected", "checkbox", null),
                ("Piston seals checked; no product leaks", "checkbox", null),
                ("Crankcase oil level", "checkbox", null),
            ]),
            new("Filling machine weekly service", ["DAI-FIL"], false, 2,
            [
                ("Sealing jaws cleaned and temperature checked", "checkbox", null),
                ("Dosing accuracy checked", "number", "ml"),
                ("Guards and interlocks working", "checkbox", null),
            ]),
            new("Refrigeration weekly checks", ["DAI-REF", "DAI-CLD"], true, 2,
            [
                ("Condenser and evaporator coils clean", "checkbox", null),
                ("Oil drained from separators and pots", "checkbox", null),
                ("Gas detectors tested", "checkbox", null),
            ]),
            IndustryCommon.ElectricalPanels("DAI-TRF", "DAI-GEN", "DAI-MTR"),
            IndustryCommon.BoilerWeekly("DAI-BLR"),
        ],
        FailureCodes =
        [
            new(P, "PAS-DIV", "Flow diverted: holding temperature below limit", "DAI-PAS"),
            new(P, "PHE-LEAK", "Plate heat exchanger leak or gasket failure", "DAI-PAS"),
            new(P, "HOM-VLV", "Homogeniser valve or seat worn", "DAI-HOM"),
            new(P, "SEP-VIB", "Separator vibration", "DAI-SEP"),
            new(P, "FIL-SEAL", "Filler not sealing packs", "DAI-FIL"),
            new(P, "REF-NH3", "Ammonia leak", "DAI-REF"),
            new(P, "CLD-TEMP", "Cold room above temperature", "DAI-CLD"),
            new(P, "CIP-CONC", "CIP solution out of strength", "DAI-CIP"),
            new(P, "BLR-LOW", "Low steam pressure", "DAI-BLR"),
        ],
        Compliance =
        [
            .. IndustryCommon.KenyaCore("DAI-BLR", "DAI-CMP", "DAI-WB", "DAI-SCL"),
            new("ammonia-vessels", "Ammonia refrigeration pressure vessels examination", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.69 (DOSH)", 24, ["DAI-REF"]),
            new("flow-meters", "Milk flow meter calibration", ComplianceKinds.Calibration, "Weights and Measures Act (Cap 513)", 12, ["DAI-FLM"]),
            new("kdb-licence", "Dairy processing licence (Kenya Dairy Board)", ComplianceKinds.Certificate, "Dairy Industry Act (Cap 336)", 12, null, 60),
            new("food-safety-audit", "Food safety management audit (ISO 22000 / FSSC 22000)", ComplianceKinds.Certificate, "Customer and KEBS requirement", 12, null, 60),
            new("food-handlers", "Food handlers' medical certificates", ComplianceKinds.Certificate, "Public Health Act (Cap 242)", 6, null, 30),
            IndustryCommon.EffluentLicence(),
        ],
        Zones =
        [
            IndustryCommon.Reception,
            IndustryCommon.CarPark,
            new("Milk reception bay", AccessZoneLevels.Controlled, false, true, false, "Hairnet, White coat, Gumboots"),
            new("Laboratory", AccessZoneLevels.Controlled, true, false, false, "White coat"),
            new("Processing hall", AccessZoneLevels.Restricted, true, true, false, "Hairnet, White coat, Gumboots, Mouth mask"),
            new("Packaging hall", AccessZoneLevels.Restricted, true, true, false, "Hairnet, White coat, Gumboots, Mouth mask"),
            new("Cold stores and dispatch", AccessZoneLevels.Restricted, true, true, false, "Warm jacket, Gumboots"),
            new("Ammonia plant room", AccessZoneLevels.Restricted, true, true, true, "Safety boots"),
            new("Boiler house", AccessZoneLevels.Restricted, true, true, false, "Safety boots, Ear protection"),
            IndustryCommon.Substation,
            new("Effluent treatment plant", AccessZoneLevels.Controlled, false, true, false, "Gumboots"),
        ],
        Checkpoints =
        [
            IndustryCommon.MainGate,
            IndustryCommon.Administration,
            new("Milk reception bay", "Milk reception bay", "Tanker bay clear?\nSilo valves closed?"),
            new("Processing hall", "Processing hall", "No machines left running unattended?"),
            new("Cold store", "Cold stores and dispatch", "Doors closed?\nTemperature display normal?"),
            new("Finished goods dispatch", "Cold stores and dispatch", "Loading bay doors locked?"),
            new("Ammonia plant room", "Ammonia plant room", "No ammonia smell?\nAlarm panel normal?"),
            new("Boiler house", "Boiler house", "Boiler attendant present?"),
            new("Effluent plant", "Effluent treatment plant", null),
            IndustryCommon.SubstationPoint,
            IndustryCommon.Perimeter,
        ],
    };
}
