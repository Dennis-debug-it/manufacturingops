using Ecosys.Infrastructure.Services;

namespace Ecosys.Infrastructure.Reports;

/// <summary>
/// Plexus Energy's "Solar Power Installation Report": the checklist technicians fill in on site and
/// the keys its answers feed into the report (PDF and the client's own Word template).
///
/// Answers are matched to report fields by question text, which the work order keeps as a snapshot
/// when the checklist is attached. Renaming a question in the template editor therefore blanks that
/// field in future reports; re-installing the template restores the original questions.
/// </summary>
public static class SolarInstallationTemplate
{
    /// <summary>PmTemplate.Code that switches a work order's service report to this layout.</summary>
    public const string Code = "PLEXUS-SOLAR-INSTALL";
    public const string LayoutKey = "solar-installation";
    public const string Name = "Solar PV Installation & Commissioning";
    public const string Category = "Solar PV";

    /// <summary>
    /// Built for Plexus Energy only: it installs and renders only in a workspace whose name or company
    /// name contains "Plexus Energy". Checked on the server, both when installing and when a report is made.
    /// </summary>
    public static bool IsForTenant(string? name, string? companyName) =>
        ContainsPlexusEnergy(name) || ContainsPlexusEnergy(companyName);

    private static bool ContainsPlexusEnergy(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Contains("plexus energy", StringComparison.OrdinalIgnoreCase);

    public sealed record Field(
        string Key,
        string Section,
        string Prompt,
        string Type,
        bool Required = false,
        string? Unit = null,
        string? Helper = null,
        IReadOnlyCollection<string>? Options = null,
        decimal? Min = null,
        decimal? Max = null,
        bool NoteOnFail = false);

    public sealed record PhotoSlot(string Key, string Caption);

    private static readonly string[] Status = ["Installed & tested", "Installed – issue noted", "Not applicable"];
    private static readonly string[] PassFail = ["Pass", "Fail", "N/A"];

    private const string SProject = "1. Project & System";
    private const string SModules = "2. PV Modules & Mounting";
    private const string SStrings = "3. PV Strings";
    private const string SInverter = "4. Inverters";
    private const string SBattery = "5. Battery Bank";
    private const string SProtection = "6. Cabling & Protection";
    private const string STests = "7. Testing & Commissioning";
    private const string SMonitoring = "8. Monitoring";
    private const string SClose = "9. Observations & Handover";

    public static readonly IReadOnlyList<PhotoSlot> PhotoSlots =
    [
        new("photo_system_setup", "System Set Up"),
        new("photo_inverter_parameters", "Inverter Parameters"),
        new("photo_panels", "PV Panels"),
        new("photo_dc_combiner", "DC Combiner Board"),
        new("photo_ac_combiner", "AC Combiner Board"),
        new("photo_earthing", "Earthing"),
        new("photo_earthing_results", "Earthing Test Results"),
        new("photo_monitoring_platform", "Monitoring Platform"),
    ];

    /// <summary>The ten commissioning tests in report order: key prefix, test name, acceptance criterion, value unit.</summary>
    public static readonly IReadOnlyList<(string Key, string Test, string Criterion, string? Unit)> Tests =
    [
        ("t_polarity", "Polarity check", "Correct polarity on all strings", null),
        ("t_voc", "String open-circuit voltage (Voc)", "Within ±5% of expected value", null),
        ("t_isc", "String short-circuit / operating current", "Consistent between strings; within expected range", null),
        ("t_insulation", "Insulation resistance (DC)", "≥ 1 MΩ", "MΩ"),
        ("t_vdrop", "DC voltage drop", "Within design limit", "%"),
        ("t_earth", "Earth resistance", "Within design limit", "Ω"),
        ("t_ac_output", "AC output parameters", "230 V AC ±10%, 50 Hz", null),
        ("t_transfer", "Changeover / grid transfer", "Seamless transfer between sources", null),
        ("t_bms", "BMS operation", "Charge parameters follow BMS settings", null),
        ("t_monitoring", "Monitoring", "Online; values match on-site readings", null),
    ];

    public const int StringCount = 4;

    // Declared after the lists it is built from: static fields initialise in source order.
    public static readonly IReadOnlyList<Field> Fields = BuildFields();

    private static List<Field> BuildFields()
    {
        var f = new List<Field>
        {
            new("system_type", SProject, "System type", "dropdown", true, Options: ["Hybrid", "Off-grid", "Grid-tied"]),
            new("system_size_kwp", SProject, "System size (peak)", "number", Unit: "kWp", Helper: "Leave blank to calculate from module quantity × wattage."),
            new("backup_source", SProject, "Backup / grid supply", "dropdown", true, Options: ["KPLC grid", "Generator", "KPLC grid and generator", "None"]),
            new("commissioning_date", SProject, "Commissioning date", "date", Helper: "Leave blank to use the job's completion date."),

            new("module_make_model", SModules, "PV module make / model", "text", true),
            new("module_wattage", SModules, "PV module rating", "number", true, Unit: "Wp"),
            new("module_qty", SModules, "Number of PV modules", "number", true),
            new("pv_status", SModules, "PV modules status", "dropdown", Options: Status),
            new("module_serials", SModules, "PV module serial numbers", "multilinetext", Helper: "Optional. Listed in Annex A of the report."),
            new("mounting_type", SModules, "Mounting type", "dropdown", true, Options: ["Roof-mounted", "Ground-mounted", "Carport"]),
            new("mounting_location", SModules, "Mounting location", "text", true, Helper: "e.g. main building roof"),
            new("tilt_angle", SModules, "Array tilt angle", "number", Unit: "°"),
            new("array_orientation", SModules, "Array orientation", "dropdown", Options: ["North", "South", "East", "West", "East–West"]),
            new("mount_model", SModules, "Mounting structure model", "text"),
            new("mount_qty", SModules, "Mounting structure quantity", "number"),
            new("mount_status", SModules, "Mounting structure status", "dropdown", Options: Status),
        };

        for (var i = 1; i <= StringCount; i++)
        {
            var required = i == 1;
            var p = $"String {i}";
            f.Add(new($"s{i}_qty", SStrings, $"{p} – number of modules", "number", required));
            f.Add(new($"s{i}_cfg", SStrings, $"{p} – configuration (series × parallel)", "text", required, Helper: "e.g. 10S × 1P"));
            f.Add(new($"s{i}_kwp", SStrings, $"{p} – power", "number", required, Unit: "kWp"));
            f.Add(new($"s{i}_vmp", SStrings, $"{p} – Vmp", "number", Unit: "V"));
            f.Add(new($"s{i}_imp", SStrings, $"{p} – Imp", "number", Unit: "A"));
            f.Add(new($"s{i}_voc", SStrings, $"{p} – Voc", "number", Unit: "V"));
            f.Add(new($"s{i}_mppt", SStrings, $"{p} – inverter / MPPT", "text", Helper: "e.g. INV1 / MPPT1"));
        }

        f.AddRange(
        [
            new("inverter_make_model", SInverter, "Inverter make / model", "text", true),
            new("inverter_rating_kw", SInverter, "Inverter rating", "number", true, Unit: "kW"),
            new("inverter_qty", SInverter, "Number of inverters", "number", true),
            new("inv_serials", SInverter, "Inverter serial number(s)", "text"),
            new("inv_status", SInverter, "Inverter status", "dropdown", Options: Status),
            new("inverter_location", SInverter, "Inverter location", "text", true, Helper: "e.g. power room"),

            new("battery_make_model", SBattery, "Battery make / model", "text", true),
            new("battery_capacity_kwh", SBattery, "Battery bank capacity", "number", true, Unit: "kWh"),
            new("battery_chemistry", SBattery, "Battery chemistry", "dropdown", Options: ["LiFePO4", "Lithium-ion (NMC)", "Lead-acid", "Gel"]),
            new("battery_qty", SBattery, "Number of battery modules", "number"),
            new("bat_serials", SBattery, "Battery serial number(s)", "text"),
            new("bat_status", SBattery, "Battery status", "dropdown", Options: Status),
            new("battery_location", SBattery, "Battery location", "text", Helper: "e.g. power room"),

            new("dc_cable_size", SProtection, "DC cable size", "number", Unit: "mm²"),
            new("ac_cable_size", SProtection, "AC cable size", "number", Unit: "mm²"),
            new("ac_tie_in_point", SProtection, "AC tie-in point", "text", Helper: "e.g. main distribution board"),
            new("dcp_model", SProtection, "DC protection (MCB / SPD) model", "text"),
            new("dcp_rating", SProtection, "DC protection rating", "text"),
            new("dcp_qty", SProtection, "DC protection quantity", "number"),
            new("dcp_status", SProtection, "DC protection status", "dropdown", Options: Status),
            new("acp_model", SProtection, "AC protection (MCB / SPD) model", "text"),
            new("acp_rating", SProtection, "AC protection rating", "text"),
            new("acp_qty", SProtection, "AC protection quantity", "number"),
            new("acp_status", SProtection, "AC protection status", "dropdown", Options: Status),
            new("co_model", SProtection, "Changeover switch model", "text"),
            new("co_rating", SProtection, "Changeover switch rating", "text"),
            new("co_qty", SProtection, "Changeover switch quantity", "number"),
            new("co_status", SProtection, "Changeover switch status", "dropdown", Options: Status),
        ]);

        foreach (var (key, test, criterion, unit) in Tests)
        {
            var valueType = unit is null ? "text" : "number";
            decimal? min = key == "t_insulation" ? 1 : null;
            f.Add(new($"{key}_value", STests, $"{test} – measured / observed", valueType, Unit: unit, Helper: $"Acceptance: {criterion}", Min: min));
            f.Add(new($"{key}_result", STests, $"{test} – result", "passfail", true, Options: PassFail, NoteOnFail: true));
        }
        f.Add(new("commissioning_summary", STests, "Commissioning summary", "multilinetext"));

        f.AddRange(
        [
            new("monitoring_platform", SMonitoring, "Monitoring platform", "text", true, Helper: "e.g. SolarMan, Huawei FusionSolar"),
            new("monitoring_url", SMonitoring, "Web portal / mobile app", "text"),
            new("monitoring_plant_name", SMonitoring, "Plant / station name", "text"),
            new("mon_model", SMonitoring, "Monitoring device model", "text"),
            new("mon_qty", SMonitoring, "Monitoring device quantity", "number"),
            new("mon_serial", SMonitoring, "Data logger / dongle serial no.", "text"),
            new("mon_status", SMonitoring, "Monitoring device status", "dropdown", Options: Status),
            new("monitoring_comms", SMonitoring, "Communication method", "dropdown", Options: ["Wi-Fi", "GSM / 4G", "Ethernet", "RS485"]),
            new("monitoring_account_owner", SMonitoring, "Monitoring account owner", "text"),
            new("monitoring_username", SMonitoring, "Monitoring username / login email", "text"),
            new("monitoring_password", SMonitoring, "Monitoring password", "text", Helper: "Printed in the report handed to the client."),
            new("monitoring_plexus_access", SMonitoring, "Plexus O&M access", "dropdown", Options: ["Granted", "Pending", "Not granted"]),

            new("additional_observations", SClose, "Additional observations", "multilinetext"),
            new("recommendations", SClose, "Recommendations", "multilinetext"),
            new("conclusion_notes", SClose, "Conclusion notes", "multilinetext"),
            new("plexus_rep_title", SClose, "Plexus representative designation", "text", Helper: "e.g. Lead Technician. The name comes from the technician's signature."),
        ]);

        return f;
    }

    public static PmTemplateWriteCommand BuildCommand()
    {
        var sectionOrder = Fields.Select(x => x.Section).Distinct().Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index + 1);
        var checklist = Fields.Select((field, index) => new PmTemplateChecklistItemCommand(
            field.Section,
            sectionOrder[field.Section],
            field.Prompt,
            field.Helper,
            field.Type,
            field.Unit,
            field.Min,
            field.Max,
            DefaultValue: null,
            Required: field.Required,
            EvidenceRequired: false,
            TechnicianRemarksAllowed: field.Type == "passfail",
            RequiresNoteOnFail: field.NoteOnFail,
            Order: index + 1,
            Options: field.Options)).ToList();

        return new PmTemplateWriteCommand(
            Name,
            Category,
            "Installation, testing and commissioning of a solar PV system. Produces the Solar Power Installation Report. "
                + "Photos: caption them " + string.Join(", ", PhotoSlots.Select(x => $"\"{x.Caption}\"")) + ".",
            AssetCategoryId: null,
            Checklist: checklist,
            IsActive: true,
            Code: Code,
            WorkType: "BreakdownCorrective");
    }
}
