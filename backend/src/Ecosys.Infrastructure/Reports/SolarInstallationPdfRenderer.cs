using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ecosys.Infrastructure.Reports;

/// <summary>
/// The Solar Power Installation Report as a PDF, following the client's Word template section by
/// section (same headings, wording, tables and colours). This is the version emailed to the client.
/// </summary>
public static class SolarInstallationPdfRenderer
{
    private const string Title = "#1F5C99";
    private const string Heading = "#2E74B5";
    private const string SubHeading = "#1F4D78";
    private const string HeaderFill = "#E8F3E2";
    private const string LabelFill = "#F4F4F4";
    private const string Rule = "#BFBFBF";

    public static byte[] Render(SolarInstallationReportData d)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        string V(string key) => d.Get(key);
        string S(string key) => d.Show(key);

        return Document.Create(document =>
        {
            // ── Cover ──────────────────────────────────────────────────────────
            document.Page(page =>
            {
                Setup(page);
                page.Content().PaddingTop(30).Column(col =>
                {
                    col.Spacing(10);
                    if (d.LogoBytes is { Length: > 0 })
                        col.Item().AlignCenter().Height(140).Image(d.LogoBytes).FitArea();
                    col.Item().PaddingTop(30).AlignCenter().Text("SOLAR POWER INSTALLATION REPORT").FontSize(22).Bold().FontColor(Title);
                    col.Item().AlignCenter().Text($"INSTALLATION AND TESTING OF {Join(V("system_size_kwp"), "kWp")} {V("system_type").ToUpperInvariant()} SOLAR PV SYSTEM")
                        .FontSize(13).Bold().AlignCenter();
                    col.Item().PaddingTop(30).PaddingHorizontal(40).Column(details =>
                    {
                        details.Spacing(6);
                        foreach (var (label, key) in new[] { ("CLIENT", "client_name"), ("SITE", "site_name"), ("COUNTY / REGION", "site_county"), ("PROJECT REF", "project_ref"), ("DATE", "report_date") })
                        {
                            details.Item().Text(t =>
                            {
                                t.Span($"{label}: ").Bold();
                                t.Span(S(key));
                            });
                        }
                    });
                    col.Item().PaddingTop(40).AlignCenter().Text($"Prepared by {d.CompanyName}").Italic();
                });
            });

            // ── Body ───────────────────────────────────────────────────────────
            document.Page(page =>
            {
                Setup(page);
                page.Header().PaddingBottom(10).Row(row =>
                {
                    row.RelativeItem();
                    if (d.LogoBytes is { Length: > 0 })
                        row.ConstantItem(65).Height(45).Image(d.LogoBytes).FitArea();
                });
                page.Footer().BorderTop(0.5f).BorderColor(Rule).PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text($"Solar Power Installation Report – {V("site_name")}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.AutoItem().Text(t =>
                    {
                        t.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Spacing(6);

                    H1(col, "1. Introduction");
                    col.Item().Text(t =>
                    {
                        t.Span("This report documents the installation, testing and commissioning of a ");
                        t.Span($"{Join(V("system_size_kwp"), "kWp")} {V("system_type")}").Bold();
                        t.Span(" solar photovoltaic (PV) system at ");
                        t.Span(V("site_name")).Bold();
                        t.Span($"{Prefix(", ", V("site_county"))}, for {V("client_name")}. The system is designed to supply the site's electrical loads from solar energy, with battery storage for backup");
                        var backup = V("backup_source");
                        t.Span(backup.Length == 0 || backup.Equals("None", StringComparison.OrdinalIgnoreCase)
                            ? "."
                            : $" and the {backup} as a secondary supply.");
                    });
                    col.Item().Text($"The installation was carried out by {d.CompanyName} in accordance with the approved design, the manufacturers' instructions and standard solar PV installation practice, to ensure optimal system performance, safety and reliability.");

                    H1(col, "2. System Description");
                    H2(col, "2.1 System Specifications");
                    KeyValueTable(col,
                    [
                        ("System Type", S("system_type")),
                        ("System Size (Peak)", Join(V("system_size_kwp"), "kWp", Empty)),
                        ("Mounting Structure", Pair(V("mounting_type"), V("mounting_location"), " – ")),
                        ("PV Modules", $"{S("module_qty")} × {Join(V("module_wattage"), "Wp", Empty)} {V("module_make_model")}".Trim()),
                        ("Inverters", $"{S("inverter_qty")} × {Join(V("inverter_rating_kw"), "kW", Empty)} {V("inverter_make_model")}".Trim()),
                        ("Battery Storage", $"{Join(V("battery_capacity_kwh"), "kWh", Empty)} {V("battery_make_model")}{Wrap(V("battery_chemistry"))}".Trim()),
                        ("Backup / Grid Supply", S("backup_source")),
                        ("Monitoring", S("monitoring_platform")),
                    ]);

                    H2(col, "2.2 Equipment Installed");
                    GridTable(col, ["Description", "Make / Model", "Rating", "Qty", "Serial No.", "Status"], [3, 3, 2, 1, 2.2f, 2.2f],
                    [
                        ["PV modules", S("module_make_model"), Join(V("module_wattage"), "Wp", Empty), S("module_qty"), d.Has("module_serials") ? "See Annex A" : Empty, S("pv_status")],
                        ["Inverter(s)", S("inverter_make_model"), Join(V("inverter_rating_kw"), "kW", Empty), S("inverter_qty"), S("inv_serials"), S("inv_status")],
                        ["Battery bank", S("battery_make_model"), Join(V("battery_capacity_kwh"), "kWh", Empty), S("battery_qty"), S("bat_serials"), S("bat_status")],
                        ["Mounting structure", S("mount_model"), Empty, S("mount_qty"), Empty, S("mount_status")],
                        ["DC protection (MCB / SPD)", S("dcp_model"), S("dcp_rating"), S("dcp_qty"), Empty, S("dcp_status")],
                        ["AC protection (MCB / SPD)", S("acp_model"), S("acp_rating"), S("acp_qty"), Empty, S("acp_status")],
                        ["Changeover switch", S("co_model"), S("co_rating"), S("co_qty"), Empty, S("co_status")],
                        ["Monitoring device", S("mon_model"), Empty, S("mon_qty"), S("mon_serial"), S("mon_status")],
                    ]);

                    H1(col, "3. Installation Works");
                    col.Item().Text("The installation activities carried out included the following:");
                    H2(col, "3.1 Mounting Structure");
                    Bullets(col,
                    [
                        $"{Or(V("mounting_type"), "The")} mounting structures were installed securely on the {Or(V("mounting_location"), "approved location")}.",
                        $"The array was set at a tilt of {Join(V("tilt_angle"), "°", Empty)} facing {Or(V("array_orientation"), Empty)} to maximise annual solar yield.",
                    ]);
                    H2(col, "3.2 PV Array");
                    Bullets(col,
                    [
                        $"{S("module_qty")} PV modules of {Join(V("module_wattage"), "Wp", Empty)} each, totalling {Join(V("system_size_kwp"), "kWp", Empty)}, were mounted on the installed structures.",
                        "Modules were securely fastened, wired and configured as follows:",
                    ]);
                    var stringRows = d.Strings
                        .Select(i => new[] { $"String {i}", S($"s{i}_qty"), S($"s{i}_cfg"), S($"s{i}_kwp"), S($"s{i}_vmp"), S($"s{i}_imp"), S($"s{i}_voc"), S($"s{i}_mppt") })
                        .Append(["Total", S("module_qty"), Empty, S("system_size_kwp"), Empty, Empty, Empty, Empty])
                        .ToArray();
                    GridTable(col, ["String", "No. of modules", "Config. (S × P)", "Power (kWp)", "Vmp (V)", "Imp (A)", "Voc (V)", "Inverter / MPPT"], [1.4f, 1.3f, 1.4f, 1.3f, 1, 1, 1, 1.6f], stringRows, boldLastRow: true);

                    H2(col, "3.3 Inverters");
                    Bullets(col,
                    [
                        $"{S("inverter_qty")} × {Join(V("inverter_rating_kw"), "kW", Empty)} {V("inverter_make_model")} inverter(s) were installed in the {Or(V("inverter_location"), Empty)}.",
                        "Proper clearances were maintained for heat dissipation and maintenance access.",
                    ]);
                    H2(col, "3.4 Battery Bank");
                    Bullets(col,
                    [
                        $"A {Join(V("battery_capacity_kwh"), "kWh", Empty)} {V("battery_make_model")} {V("battery_chemistry")} battery bank was installed in the {Or(V("battery_location"), Empty)} to provide energy storage and backup.",
                        "Battery communication (BMS) and protection connections were configured and verified with the inverter(s).",
                    ]);
                    H2(col, "3.5 Cabling and Protection");
                    Bullets(col,
                    [
                        $"{Join(V("dc_cable_size"), "mm²", Empty)} solar DC cables were run from the PV array to the inverter(s) via the DC board.",
                        $"{Join(V("ac_cable_size"), "mm²", Empty)} AC cables were run from the inverter(s) to the {Or(V("ac_tie_in_point"), Empty)}.",
                        "DC MCBs, AC MCBs, surge protection devices and a changeover switch were installed for safe operation and isolation.",
                    ]);
                    H2(col, "3.6 Earthing and Bonding");
                    Bullets(col,
                    [
                        "PV module frames, mounting structures, inverters and battery enclosures were bonded to a dedicated earthing system.",
                        "The earthing system was installed in line with applicable electrical safety standards; measured earth resistance is recorded in Section 4.",
                    ]);

                    H1(col, "4. Testing and Commissioning");
                    col.Item().Text("After installation, the system was tested and commissioned. The results are summarised below:");
                    var testRows = SolarInstallationTemplate.Tests.Select((test, index) =>
                    {
                        var measured = Join(V($"{test.Key}_value"), test.Unit ?? string.Empty, Empty);
                        if (d.TestRemarks.TryGetValue(test.Key, out var note)) measured = $"{measured} ({note})";
                        return new[] { (index + 1).ToString(), test.Test, test.Criterion, measured, S($"{test.Key}_result") };
                    }).ToArray();
                    GridTable(col, ["#", "Test", "Acceptance Criterion", "Measured / Observed", "Result"], [0.5f, 2.6f, 2.8f, 2.4f, 1.2f], testRows);
                    if (d.Has("commissioning_summary"))
                        col.Item().PaddingTop(4).Text(V("commissioning_summary"));

                    H1(col, "5. Monitoring Details");
                    col.Item().Text($"The system is connected to the {Or(V("monitoring_platform"), "monitoring")} platform for remote monitoring of generation, battery status, loads and alarms. Access details handed over to the client are recorded below.");
                    KeyValueTable(col,
                    [
                        ("Monitoring platform", S("monitoring_platform")),
                        ("Web portal / mobile app", S("monitoring_url")),
                        ("Plant / station name", S("monitoring_plant_name")),
                        ("Data logger / dongle serial no.", S("mon_serial")),
                        ("Communication method", S("monitoring_comms")),
                        ("Account owner", S("monitoring_account_owner")),
                        ("Username / login email", S("monitoring_username")),
                        ("Password (if applicable)", S("monitoring_password")),
                        ($"{FirstWord(d.CompanyName)} O&M access", S("monitoring_plexus_access")),
                    ]);

                    H1(col, "6. Operation and Troubleshooting");
                    H2(col, "6.1 Shutdown / Isolation Procedure");
                    var backupSupply = Or(V("backup_source"), "grid");
                    Lettered(col,
                    [
                        $"Switch off the inverter input MCB on the AC board to disconnect the {backupSupply} supply to the inverter(s).",
                        "Switch off all DC MCBs on the DC board to isolate solar power from the inverter(s).",
                        "Switch off the inverter output MCB on the AC board. This disconnects supply to the loads.",
                        "Turn off the inverter PV isolator switch.",
                        "Switch off all inverters using their ON/OFF buttons and wait until they power down completely.",
                        "Switch off the battery modules using their ON/OFF buttons and wait until all battery indicator LEDs go off.",
                    ]);
                    H2(col, "6.2 Start-up Procedure");
                    col.Item().Text("In case the system needs to be restarted:");
                    Lettered(col,
                    [
                        "Switch on the battery modules using their ON/OFF buttons and confirm all indicator LEDs light up.",
                        "Switch on all inverters and wait until they power up. Confirm no alarms are shown on any inverter display.",
                        "Switch on all DC MCBs on the DC board.",
                        "Turn on the inverter PV isolator switch and confirm the inverter(s) indicate solar charging.",
                        "Switch on the inverter input MCB on the AC board and confirm the inverter(s) are charging and supplying the loads.",
                        "Switch on the remaining inverter breakers on the AC board and set the changeover switch to position I (Solar).",
                        "Switch on the main breaker to the loads.",
                    ]);
                    H2(col, "6.3 Basic Diagnostics and Troubleshooting");
                    Lettered(col,
                    [
                        "The inverter performs continuous self-diagnosis and raises an audible/visible alarm when a fault is detected.",
                        $"Any fault code is shown on the inverter display and on the {Or(V("monitoring_platform"), "monitoring")} monitoring platform.",
                        "In the event of loss of power to the loads, check in this order: (i) all breakers on the consumer units are ON; (ii) all breakers on the AC board are ON; (iii) whether any inverter is displaying an alarm code – note it down; (iv) the changeover switch is in position I (Solar).",
                        $"If the fault persists, contact {FirstWord(d.CompanyName)} support on {Pair(V("support_phone"), V("support_email"), " / ")} for remote troubleshooting and technician deployment.",
                    ]);
                    col.Item().Text("In case of emergency, switch off the system using the shutdown procedure in Section 6.1.").Italic();

                    H1(col, "7. Site Photos");
                    PhotoGrid(col, d.SlotPhotos);
                    if (d.OtherPhotos.Count > 0)
                    {
                        col.Item().EnsureSpace(220);
                        H2(col, "Additional photos");
                        PhotoGrid(col, d.OtherPhotos.Select(p => (p.Caption, p.ImageBytes)).ToList());
                    }

                    H1(col, "8. Observations and Recommendations");
                    var observations = new List<string>
                    {
                        "All installed components were properly secured.",
                        "Wiring and cable management were completed in line with electrical safety standards.",
                        "The system was tested and found to be functioning as expected.",
                    };
                    if (d.Has("additional_observations")) observations.Add(V("additional_observations"));
                    Bullets(col, observations);
                    col.Item().Text(t =>
                    {
                        t.Span("Recommendations: ").Bold();
                        t.Span(Or(V("recommendations"), "None."));
                    });

                    H1(col, "9. Conclusion");
                    col.Item().Text($"The {Join(V("system_size_kwp"), "kWp")} {V("system_type")} solar PV system at {V("site_name")} was successfully installed, tested and commissioned on {V("commissioning_date")}. {d.TestsSentence} and the system was handed over to {V("client_name")} in good working condition, together with operating instructions and the support contacts above.");
                    if (d.Has("conclusion_notes"))
                        col.Item().Text(V("conclusion_notes"));

                    H1(col, "10. Handover and Sign-off");
                    col.Item().ShowEntire().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(1.4f); c.RelativeColumn(2); c.RelativeColumn(2); });
                        table.Cell().Element(HeaderCell).Text(string.Empty);
                        table.Cell().Element(HeaderCell).Text(d.CompanyName).Bold();
                        table.Cell().Element(HeaderCell).Text("Client Representative").Bold();
                        foreach (var (label, plexus, client) in new[]
                        {
                            ("Name", S("plexus_rep_name"), S("client_rep_name")),
                            ("Designation", S("plexus_rep_title"), S("client_rep_title")),
                        })
                        {
                            table.Cell().Element(LabelCell).Text(label).Bold();
                            table.Cell().Element(Cell).Text(plexus);
                            table.Cell().Element(Cell).Text(client);
                        }
                        table.Cell().Element(LabelCell).Text("Signature").Bold();
                        table.Cell().Element(Cell).Height(55).Element(c => Signature(c, d.TechnicianSignature));
                        table.Cell().Element(Cell).Height(55).Element(c => Signature(c, d.ClientSignature));
                        table.Cell().Element(LabelCell).Text("Date").Bold();
                        table.Cell().Element(Cell).Text(S("handover_date"));
                        table.Cell().Element(Cell).Text(S("handover_date"));
                    });

                    if (d.Has("module_serials"))
                    {
                        col.Item().PageBreak();
                        H1(col, "Annex A – PV Module Serial Numbers");
                        col.Item().Text(V("module_serials")).FontSize(9);
                    }
                });
            });
        }).GeneratePdf();
    }

    private static void Setup(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(56);
        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica").LineHeight(1.25f));
    }

    // EnsureSpace keeps a heading from being stranded at the foot of a page without its content.
    private static void H1(ColumnDescriptor col, string text) =>
        col.Item().EnsureSpace(90).PaddingTop(12).Text(text).FontSize(15).SemiBold().FontColor(Heading);

    private static void H2(ColumnDescriptor col, string text) =>
        col.Item().EnsureSpace(70).PaddingTop(6).Text(text).FontSize(12).SemiBold().FontColor(SubHeading);

    private static void Bullets(ColumnDescriptor col, IEnumerable<string> items)
    {
        foreach (var item in items)
            col.Item().PaddingLeft(12).Row(row =>
            {
                row.ConstantItem(12).Text("•");
                row.RelativeItem().Text(item);
            });
    }

    private static void Lettered(ColumnDescriptor col, IReadOnlyList<string> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var letter = (char)('a' + i);
            col.Item().PaddingLeft(12).Row(row =>
            {
                row.ConstantItem(18).Text($"{letter}.");
                row.RelativeItem().Text(items[i]);
            });
        }
    }

    private static IContainer Cell(IContainer c) => c.Border(0.5f).BorderColor(Rule).Padding(4);
    private static IContainer HeaderCell(IContainer c) => c.Border(0.5f).BorderColor(Rule).Background(HeaderFill).Padding(4);
    private static IContainer LabelCell(IContainer c) => c.Border(0.5f).BorderColor(Rule).Background(LabelFill).Padding(4);

    private static void KeyValueTable(ColumnDescriptor col, IReadOnlyList<(string Label, string Value)> rows) =>
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });
            foreach (var (label, value) in rows)
            {
                table.Cell().Element(LabelCell).Text(label).Bold();
                table.Cell().Element(Cell).Text(string.IsNullOrWhiteSpace(value) ? Empty : value);
            }
        });

    private static void GridTable(ColumnDescriptor col, string[] headers, float[] widths, string[][] rows, bool boldLastRow = false) =>
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                foreach (var width in widths) c.RelativeColumn(width);
            });
            table.Header(header =>
            {
                foreach (var h in headers)
                    header.Cell().Element(HeaderCell).Text(h).FontSize(8.5f).Bold();
            });
            for (var r = 0; r < rows.Length; r++)
            {
                var bold = boldLastRow && r == rows.Length - 1;
                foreach (var value in rows[r])
                {
                    var text = table.Cell().Element(Cell).Text(string.IsNullOrWhiteSpace(value) ? Empty : value).FontSize(8.5f);
                    if (bold) text.Bold();
                }
            }
        });

    private static void PhotoGrid(ColumnDescriptor col, IReadOnlyList<(string Caption, byte[]? ImageBytes)> photos)
    {
        foreach (var pair in photos.Chunk(2))
        {
            col.Item().ShowEntire().PaddingBottom(6).Row(row =>
            {
                row.Spacing(10);
                foreach (var (caption, bytes) in pair)
                {
                    row.RelativeItem().Column(cell =>
                    {
                        cell.Spacing(3);
                        if (bytes is { Length: > 0 })
                            cell.Item().Height(170).AlignCenter().Image(bytes).FitArea();
                        else
                            cell.Item().Height(170).Border(0.5f).BorderColor(Rule).AlignCenter().AlignMiddle()
                                .Text("No photo").FontSize(8).FontColor(Colors.Grey.Medium);
                        cell.Item().AlignCenter().Text(caption).Bold().FontSize(9);
                    });
                }
                if (pair.Length == 1) row.RelativeItem();
            });
        }
    }

    private static void Signature(IContainer container, SolarReportSignature? signature)
    {
        if (signature?.ImageBytes is { Length: > 0 } bytes)
            container.AlignLeft().Image(bytes).FitArea();
    }

    private const string Empty = SolarInstallationReportData.Empty;

    /// <summary>"5.4 kWp", or <paramref name="fallback"/> when there's no number.</summary>
    private static string Join(string value, string unit, string fallback = "") =>
        value.Length == 0 ? fallback : unit.Length == 0 ? value : unit == "°" ? $"{value}°" : $"{value} {unit}";

    private static string Or(string value, string fallback) => value.Length == 0 ? fallback : value;
    private static string Prefix(string prefix, string value) => value.Length == 0 ? string.Empty : prefix + value;
    private static string Wrap(string value) => value.Length == 0 ? string.Empty : $" ({value})";

    private static string Pair(string a, string b, string separator) =>
        string.Join(separator, new[] { a, b }.Where(x => x.Length > 0)) is { Length: > 0 } joined ? joined : Empty;

    private static string FirstWord(string companyName) =>
        companyName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? companyName;
}
