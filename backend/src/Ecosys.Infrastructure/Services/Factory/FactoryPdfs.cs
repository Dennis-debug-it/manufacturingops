using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>Printable factory reports: the KPI board report, the pilot results and the audit evidence pack.</summary>
public static class FactoryPdfs
{
    private static string N(decimal? value, string format = "N0") => value is { } v ? v.ToString(format) : "—";

    public static byte[] KpiReport(string company, FactoryKpiReport k)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var unit = k.ProductionUnit;
        var sections = new List<(string Title, List<(string Label, string Value)> Rows)>
        {
            ("Downtime", [("Hours lost", N(k.Downtime.Hours, "N1")), ($"{unit} lost", N(k.Downtime.ProductionLost)), ("Value lost", k.Downtime.ValueLost is { } v ? $"{k.Currency} {v:N0}" : "—"),
                ("Peak season hours", N(k.Downtime.PeakHours, "N1")), ("Low season hours", N(k.Downtime.LowHours, "N1"))]),
            ("Costs", [("Labour", $"{k.Currency} {k.Costs.Labour:N0}"), ("Parts", $"{k.Currency} {k.Costs.Parts:N0}"), ("Contractors (net of VAT)", $"{k.Currency} {k.Costs.Contractor:N0}"),
                ("Total maintenance cost", $"{k.Currency} {k.Costs.Total:N0}"), ($"Production ({unit})", N(k.Costs.Production)), ($"Cost per {unit}", N(k.Costs.CostPerUnit, "N2")),
                ("Budget for the period", $"{k.Currency} {k.Costs.Budget.BudgetForPeriod:N0}"), ("Against budget", k.Costs.Budget.VariancePercent is { } bv ? $"{bv:+0.0;-0.0}%" : "—")]),
            ("Reliability", [("Breakdowns", k.Reliability.Breakdowns.ToString()), ("MTBF (h)", N(k.Reliability.MtbfHours, "N1")), ("MTTR (h)", N(k.Reliability.MttrHours, "N1")), ("Repeat failures", k.Reliability.RepeatFailures.ToString())]),
            ("Planning", [("PM compliance", k.Planning.PmCompliancePercent is { } pm ? $"{pm:0.0}%" : "—"), ("Overdue jobs", k.Planning.OverdueJobs.ToString()),
                ("Backlog (h)", N(k.Planning.BacklogHours, "N1")), ("Backlog (weeks)", N(k.Planning.BacklogWeeks, "N1")), ("Planned share", k.Planning.PlannedPercent is { } pp ? $"{pp:0}%" : "—")]),
            ("Weekly window", [("Windows", k.Window.Windows.ToString()), ("Plan completion", k.Window.CompletionPercent is { } wc ? $"{wc:0}%" : "—"), ("Jobs carried over", k.Window.CarriedOver.ToString()),
                ("Breakdowns on assets with deferred jobs", k.Window.BreakdownsOnDeferredAssets.ToString())]),
            ("Overhaul", [("Scope done", k.Overhaul.ScopeCompletePercent is { } os ? $"{os:0}% of {k.Overhaul.Items}" : "—"), ("Cost vs budget", k.Overhaul.Budget is { } ob ? $"{k.Overhaul.ActualCost:N0} / {ob:N0}" : N(k.Overhaul.ActualCost)),
                ("Awaiting recommissioning", k.Overhaul.AwaitingRecommissioning.ToString())]),
            ("Spares", [("Issued (value)", $"{k.Currency} {k.Spares.ConsumptionValue:N0}"), ("Critical spares out of stock", k.Spares.CriticalStockOuts.ToString()),
                ("Critical spares below minimum", k.Spares.CriticalBelowMinimum.ToString()), ("Stock value", $"{k.Currency} {k.Spares.StockValue:N0}")]),
            ("People", [("Hours logged", N(k.People.LoggedHours, "N1")), ("Overtime", k.People.OvertimePercent is { } op ? $"{k.People.OvertimeHours:N1} h ({op:0}%)" : N(k.People.OvertimeHours, "N1")),
                ("Utilisation", k.People.UtilisationPercent is { } pu ? $"{pu:0}%" : "—"), ("Certificates expiring / expired", $"{k.People.CertificatesExpiring} / {k.People.CertificatesExpired}")]),
            ("Energy", [("Electricity (kWh)", N(k.Energy.EnergyKwh)), ($"kWh per {unit}", N(k.Energy.KwhPerUnit, "N3")), ($"Fuel ({k.Energy.FuelUnit ?? "units"})", N(k.Energy.Fuel, "N1")),
                ($"Fuel per {unit}", N(k.Energy.FuelPerUnit, "N4"))]),
            ("Assets and compliance", [("Assets (operational / down)", $"{k.Assets.Total} ({k.Assets.Operational} / {k.Assets.Down})"), ("Statutory due in 30 days", k.Compliance.StatutoryDueSoon.ToString()),
                ("Statutory overdue", k.Compliance.StatutoryOverdue.ToString()), ("Open corrective actions", $"{k.Compliance.NonConformitiesOpen} ({k.Compliance.NonConformitiesOverdue} overdue)"),
                ("Next audit", k.Compliance.NextAuditOn is { } na ? $"{k.Compliance.NextAudit}, {na:dd MMM yyyy}" : "—")]),
        };

        return Document.Create(doc => doc.Page(page =>
        {
            Page(page, company, $"Maintenance report · {k.ScopeName}", $"{k.From:dd MMM yyyy} – {k.To.AddDays(-1):dd MMM yyyy}");
            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(10);
                foreach (var chunk in sections.Chunk(2))
                {
                    col.Item().Row(row =>
                    {
                        row.Spacing(12);
                        foreach (var (title, rows) in chunk)
                        {
                            row.RelativeItem().Element(c => KeyValues(c, title, rows));
                        }

                        if (chunk.Length == 1) row.RelativeItem();
                    });
                }

                if (k.Costs.ByFactory.Count > 1 || k.Costs.BySection.Count > 0)
                {
                    col.Item().Text("Cost and downtime by section").SemiBold().FontSize(11);
                    col.Item().Element(c => Table(c, ["Section", "Labour", "Parts", "Contractor", "Total", "Downtime h", $"{unit} lost"],
                        k.Costs.BySection.Select(x => new[] { x.Name, N(x.Labour), N(x.Parts), N(x.Contractor), N(x.Total), N(x.DowntimeHours, "N1"), N(x.ProductionLost) }).ToList()));
                }

                if (k.Reliability.TopFailing.Count > 0)
                {
                    col.Item().Text("Top failing assets").SemiBold().FontSize(11);
                    col.Item().Element(c => Table(c, ["Asset", "Breakdowns", "Downtime h"],
                        k.Reliability.TopFailing.Select(x => new[] { $"{x.AssetCode} {x.AssetName}", x.Breakdowns.ToString(), N(x.DowntimeHours, "N1") }).ToList()));
                }

                if (k.Costs.Budget.Lines.Count > 0)
                {
                    col.Item().Text("Budget vs actual").SemiBold().FontSize(11);
                    col.Item().Element(c => Table(c, ["Factory", "Cost centre", "Category", "Budget (period)", "Actual"],
                        k.Costs.Budget.Lines.Select(x => new[] { x.Factory, x.CostCentre ?? "All", x.Category, N(x.BudgetForPeriod), N(x.Actual) }).ToList()));
                }
            });
        })).GeneratePdf();
    }

    public static byte[] Pilot(string company, PilotReport report)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(doc => doc.Page(page =>
        {
            Page(page, company, $"Pilot results · {report.ScopeName}", $"Baseline {report.BaselineFrom:dd MMM yyyy} – {report.BaselineTo.AddDays(-1):dd MMM yyyy}; pilot {report.From:dd MMM yyyy} – {report.To.AddDays(-1):dd MMM yyyy}");
            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(8);
                col.Item().Text("Rates are per 30 days where marked, so periods of different length compare fairly.").FontSize(8).FontColor(Colors.Grey.Darken1);
                col.Item().Element(c => Table(c, ["Measure", "Unit", "Baseline", "Pilot", "Change"],
                    report.Measures.Select(m => new[]
                    {
                        m.Measure, m.Unit, N(m.Baseline, "N2"), N(m.Current, "N2"),
                        m.ChangePercent is { } ch ? $"{ch:+0.0;-0.0;0}%{((ch < 0) == m.LowerIsBetter && ch != 0 ? " better" : ch == 0 ? "" : " worse")}" : "—",
                    }).ToList()));
            });
        })).GeneratePdf();
    }

    public static byte[] EvidencePack(string company, EvidencePack pack)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(doc => doc.Page(page =>
        {
            Page(page, company, $"Evidence pack · {pack.StandardName}", $"{pack.FactoryName} · {pack.From:dd MMM yyyy} – {pack.To.AddDays(-1):dd MMM yyyy}");
            page.Content().PaddingTop(8).Column(col =>
            {
                col.Spacing(8);
                col.Item().Text($"{pack.Covered} of {pack.Requirements.Count} requirements have evidence in the period.").FontSize(10);
                col.Item().Element(c => Table(c, ["Requirement", "Title", "Evidence", "Open findings"],
                    pack.Requirements.Select(r => new[] { r.Clause, r.Title, r.HasEvidence ? "Yes" : "GAP", r.OpenNonConformities.ToString() }).ToList()));
                foreach (var requirement in pack.Requirements)
                {
                    col.Item().PageBreak();
                    col.Item().Text($"{requirement.Clause} · {requirement.Title}").FontSize(12).SemiBold();
                    if (!string.IsNullOrWhiteSpace(requirement.Guidance)) col.Item().Text(requirement.Guidance).FontSize(8).FontColor(Colors.Grey.Darken1);
                    if (!requirement.HasEvidence) col.Item().Text("No evidence found for this period.").FontColor(Colors.Red.Darken2);
                    foreach (var section in requirement.Sections)
                    {
                        col.Item().PaddingTop(4).Text(section.Title).SemiBold();
                        col.Item().Text(section.Summary).FontSize(8);
                        if (section.Rows.Count > 0)
                        {
                            col.Item().Element(c => Table(c, section.Headers.ToArray(), section.Rows.Select(r => r.ToArray()).ToList()));
                            if (section.Count > section.Rows.Count) col.Item().Text($"First {section.Rows.Count} of {section.Count} shown.").FontSize(7).FontColor(Colors.Grey.Darken1);
                        }
                    }

                    if (requirement.Documents.Count > 0)
                    {
                        col.Item().PaddingTop(4).Text("Documents on file").SemiBold();
                        foreach (var document in requirement.Documents) col.Item().Text($"• {document}").FontSize(8);
                    }
                }
            });
        })).GeneratePdf();
    }

    private static void Page(PageDescriptor page, string company, string title, string subtitle)
    {
        page.Size(PageSizes.A4);
        page.Margin(28);
        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica"));
        page.Header().Column(h =>
        {
            h.Item().Text(company).FontSize(8).FontColor(Colors.Grey.Darken1);
            h.Item().Text(title).FontSize(15).SemiBold();
            h.Item().Text(subtitle).FontSize(9).FontColor(Colors.Grey.Darken2);
            h.Item().PaddingTop(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
        });
        page.Footer().AlignRight().Text(text =>
        {
            text.Span($"Generated {DateTime.UtcNow:dd MMM yyyy HH:mm} UTC · page ").FontSize(7);
            text.CurrentPageNumber().FontSize(7);
            text.Span(" of ").FontSize(7);
            text.TotalPages().FontSize(7);
        });
    }

    private static void KeyValues(IContainer container, string title, IReadOnlyList<(string Label, string Value)> rows) =>
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
        {
            c.Item().Text(title).SemiBold().FontSize(10);
            foreach (var (label, value) in rows)
            {
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text(label).FontColor(Colors.Grey.Darken2);
                    r.AutoItem().Text(value).SemiBold();
                });
            }
        });

    private static void Table(IContainer container, string[] headers, IReadOnlyList<string[]> rows) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                foreach (var _ in headers) cols.RelativeColumn();
            });
            table.Header(h =>
            {
                foreach (var header in headers) h.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(header).SemiBold().FontSize(8);
            });
            foreach (var row in rows)
            {
                foreach (var cell in row) table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(cell ?? string.Empty).FontSize(8);
            }
        });
}
