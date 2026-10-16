using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Ecosys.Infrastructure.Reports;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class SolarInstallationReportTests
{
    private static readonly byte[] Image = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-image.png"));

    internal static SolarReportSource SampleSource(bool withPhotos = true)
    {
        string Prompt(string key) => SolarInstallationTemplate.Fields.Single(x => x.Key == key).Prompt;
        var answers = new Dictionary<string, string>
        {
            ["system_type"] = "Hybrid",
            ["backup_source"] = "KPLC grid",
            ["module_make_model"] = "Jinko Tiger Neo JKM550N",
            ["module_wattage"] = "550",
            ["module_qty"] = "20",
            ["pv_status"] = "Installed & tested",
            ["module_serials"] = "JK0001, JK0002, JK0003",
            ["mounting_type"] = "Roof-mounted",
            ["mounting_location"] = "main building roof",
            ["tilt_angle"] = "10",
            ["array_orientation"] = "North",
            ["s1_qty"] = "10", ["s1_cfg"] = "10S × 1P", ["s1_kwp"] = "5.5", ["s1_vmp"] = "410", ["s1_imp"] = "13.1", ["s1_voc"] = "498", ["s1_mppt"] = "INV1 / MPPT1",
            ["s2_qty"] = "10", ["s2_cfg"] = "10S × 1P", ["s2_kwp"] = "5.5", ["s2_vmp"] = "409", ["s2_imp"] = "13.0", ["s2_voc"] = "497", ["s2_mppt"] = "INV1 / MPPT2",
            ["inverter_make_model"] = "Deye SUN-10K-SG04LP3", ["inverter_rating_kw"] = "10", ["inverter_qty"] = "1", ["inv_serials"] = "DY2310001", ["inverter_location"] = "power room",
            ["battery_make_model"] = "Dyness BX51100", ["battery_capacity_kwh"] = "15", ["battery_chemistry"] = "LiFePO4", ["battery_qty"] = "3", ["battery_location"] = "power room",
            ["dc_cable_size"] = "6", ["ac_cable_size"] = "10", ["ac_tie_in_point"] = "main distribution board",
            ["t_polarity_value"] = "Correct on all strings", ["t_polarity_result"] = "Pass",
            ["t_insulation_value"] = "25", ["t_insulation_result"] = "Pass",
            ["t_earth_value"] = "3.2", ["t_earth_result"] = "Pass",
            ["t_transfer_result"] = "Fail",
            ["monitoring_platform"] = "SolarMan", ["monitoring_username"] = "client@example.com", ["monitoring_password"] = "Secret#123",
            ["recommendations"] = "Clean panels quarterly.",
            ["plexus_rep_title"] = "Lead Technician",
        };

        return new SolarReportSource(
            "Plexus Energy Limited", "+254 700 000000", "support@example.com", Image,
            "WO-2026-0042", "Acme Schools", "Acme Primary – Karen", "Nairobi", "Jane Technician",
            new DateTime(2026, 10, 14, 15, 0, 0), new DateTime(2026, 10, 16),
            answers.Select(x => new SolarReportAnswer(Prompt(x.Key), x.Value, x.Key == "t_transfer_result" ? "Changeover relay sticking; replaced on site" : null)).ToList(),
            withPhotos
                ? [new("PV Panels", Image), new("earthing test results – 3.2 ohm", Image), new("Roof access ladder", Image)]
                : [],
            new SolarReportSignature("Jane Technician", "Technician", new DateTime(2026, 10, 14), Image),
            new SolarReportSignature("John Client", "Facilities Manager", new DateTime(2026, 10, 15), Image));
    }

    [Fact]
    public void Template_IsAValidChecklistWithUniqueQuestions()
    {
        var command = SolarInstallationTemplate.BuildCommand();

        PmTemplateService.Validate(command);
        Assert.Equal(SolarInstallationTemplate.Code, command.Code);
        Assert.Equal(command.Checklist.Count, command.Checklist.Select(x => x.Question.ToLowerInvariant()).Distinct().Count());
        Assert.Equal(SolarInstallationTemplate.Fields.Count, SolarInstallationTemplate.Fields.Select(x => x.Key).Distinct().Count());
    }

    [Fact]
    public void Data_MapsAnswersByQuestion_AndFillsJobDetailsAndDerivedValues()
    {
        var data = SolarInstallationReportData.Create(SampleSource());

        Assert.Equal("Hybrid", data.Get("system_type"));
        Assert.Equal("11", data.Get("system_size_kwp"));            // 20 × 550 Wp, not entered
        Assert.Equal("Acme Schools", data.Get("client_name"));
        Assert.Equal("WO-2026-0042", data.Get("project_ref"));
        Assert.Equal("14 October 2026", data.Get("commissioning_date")); // falls back to completion
        Assert.Equal("15 October 2026", data.Get("handover_date"));      // client signed the next day
        Assert.Equal("Jane Technician", data.Get("plexus_rep_name"));
        Assert.Equal("John Client", data.Get("client_rep_name"));
        Assert.Equal("Secret#123", data.Get("monitoring_password"));
        Assert.Equal([1, 2], data.Strings);
        Assert.Equal("Changeover relay sticking; replaced on site", data.TestRemarks["t_transfer"]);
        Assert.Equal(SolarInstallationReportData.Empty, data.Show("co_model"));

        // Photos land in their captioned slot (case/prefix-insensitive); the rest go to "Additional photos".
        Assert.NotNull(data.SlotPhotos.Single(x => x.Caption == "PV Panels").ImageBytes);
        Assert.NotNull(data.SlotPhotos.Single(x => x.Caption == "Earthing Test Results").ImageBytes);
        Assert.Null(data.SlotPhotos.Single(x => x.Caption == "Earthing").ImageBytes);
        Assert.Equal("Roof access ladder", Assert.Single(data.OtherPhotos).Caption);
    }

    [Fact]
    public void Pdf_Renders()
    {
        var pdf = SolarInstallationPdfRenderer.Render(SolarInstallationReportData.Create(SampleSource()));

        Assert.True(pdf.Length > 10_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        WriteSample("solar-report.pdf", pdf);
    }

    [Fact]
    public void Pdf_RendersWithNothingFilledIn()
    {
        var empty = SampleSource(withPhotos: false) with { Answers = [], TechnicianSignature = null, ClientSignature = null, LogoBytes = null };

        var pdf = SolarInstallationPdfRenderer.Render(SolarInstallationReportData.Create(empty));

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Word_FillsTheClientTemplate()
    {
        var docx = SolarInstallationDocxRenderer.Render(SolarInstallationReportData.Create(SampleSource()));
        WriteSample("solar-report.docx", docx);

        using var doc = WordprocessingDocument.Open(new MemoryStream(docx), false);
        var main = doc.MainDocumentPart!;
        var body = main.Document.Body!.InnerText;
        var footer = string.Concat(main.FooterParts.Select(f => f.Footer.InnerText));

        Assert.DoesNotContain("{{", body + footer);
        Assert.Contains("Acme Primary – Karen", footer);
        Assert.Contains("INSTALLATION AND TESTING OF 11 kWp Hybrid SOLAR PV SYSTEM", body);
        Assert.Contains("Jinko Tiger Neo JKM550N", body);
        Assert.Contains("Secret#123", body);
        Assert.Contains("String 2", body);
        Assert.DoesNotContain("String 3", body);                       // unused strings are dropped
        Assert.DoesNotContain("add or delete rows", body);
        Assert.Contains("except those marked Fail in Section 4", body); // a test failed
        Assert.Contains("Annex A – PV Module Serial Numbers", body);
        Assert.DoesNotContain("— %", body);
        Assert.DoesNotContain(main.Document.Body!.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>(),
            p => p.ParagraphProperties?.NumberingProperties is not null && p.InnerText.Trim().Length == 0); // no empty bullets
        // Cover + header logos, 2 matched photos and 2 signatures.
        Assert.True(main.ImageParts.Count() >= 5);
        // The merge must not make the file any less valid than the client's template (which already
        // carries Word 2010 table attributes the 2007 schema doesn't declare).
        static HashSet<string> SchemaErrors(WordprocessingDocument d) =>
            new OpenXmlValidator().Validate(d).Where(e => e.ErrorType == ValidationErrorType.Schema).Select(e => e.Description ?? string.Empty).ToHashSet();
        using var template = WordprocessingDocument.Open(
            typeof(SolarInstallationDocxRenderer).Assembly.GetManifestResourceStream("Ecosys.Infrastructure.Reports.Templates.solar-installation-report.docx")!, false);
        Assert.Subset(SchemaErrors(template), SchemaErrors(doc));
    }

    [Fact]
    public void Word_KeepsFormattingAroundPlaceholdersSplitAcrossRuns()
    {
        var paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
            new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("Site: {{si")),
            new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("te_name}} and ")),
            new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text("{{client_name}}!")));

        SolarInstallationDocxRenderer.ReplaceInParagraph(paragraph, new System.Text.RegularExpressions.Regex(@"\{\{(\w+)\}\}"),
            m => m.Groups[1].Value == "site_name" ? "Karen" : "Acme");

        var runs = paragraph.Elements<DocumentFormat.OpenXml.Wordprocessing.Run>().Select(r => r.InnerText).ToList();
        Assert.Equal(["Site: Karen", " and ", "Acme!"], runs);
    }

    [Fact]
    public void Word_RendersWithNothingFilledIn()
    {
        var empty = SampleSource(withPhotos: false) with { Answers = [], TechnicianSignature = null, ClientSignature = null, LogoBytes = null };

        var docx = SolarInstallationDocxRenderer.Render(SolarInstallationReportData.Create(empty));

        using var doc = WordprocessingDocument.Open(new MemoryStream(docx), false);
        Assert.DoesNotContain("{{", doc.MainDocumentPart!.Document.Body!.InnerText);
    }

    [Fact]
    public async Task Installer_CreatesThenVersionsTheTemplate_AndJobAnswersReachTheReport()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"solar-install-{Guid.NewGuid():N}").Options);
        var tenant = new Tenant { Name = "Plexus Energy", CompanyName = "Plexus Energy Limited" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        var installer = new ClientTemplateInstaller(db, new PmTemplateService(db));

        var first = await installer.InstallAsync(tenant.Id, SolarInstallationTemplate.LayoutKey, CancellationToken.None);
        var second = await installer.InstallAsync(tenant.Id, SolarInstallationTemplate.LayoutKey, CancellationToken.None);
        var listed = Assert.Single(await installer.ListAsync(tenant.Id, CancellationToken.None));

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.TemplateId, second.TemplateId);
        Assert.Equal(first.Version + 1, second.Version);
        Assert.Equal(second.Version, listed.InstalledVersion);
        Assert.Single(db.PmTemplates.IgnoreQueryFilters().Where(x => x.TenantId == tenant.Id));
        await Assert.ThrowsAsync<Ecosys.Shared.Errors.NotFoundException>(() => installer.InstallAsync(Guid.NewGuid(), SolarInstallationTemplate.LayoutKey, CancellationToken.None));

        // Attach it to a job the normal way and answer a few questions as the technician would.
        var workOrder = new WorkOrder { TenantId = tenant.Id, WorkOrderNumber = "WO-1", Title = "Install 11 kWp" };
        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync();
        await new PmWorkOrderChecklistService(db, new AuditLogService(db, new HttpContextAccessor()))
            .AttachPmTemplateToWorkOrderAsync(workOrder.Id, second.TemplateId, tenant.Id);
        var items = db.WorkOrderChecklistItems.Where(x => x.WorkOrderId == workOrder.Id).ToList();
        Assert.Equal(SolarInstallationTemplate.Fields.Count, items.Count);
        items.Single(x => x.QuestionText == "System type").ResponseValue = "Off-grid";
        items.Single(x => x.QuestionText == "Insulation resistance (DC) – measured / observed").ResponseValue = "40";

        var data = SolarInstallationReportData.Create(SampleSource() with
        {
            Answers = items.Select(x => new SolarReportAnswer(x.QuestionText, x.ResponseValue, x.Remarks)).ToList(),
        });
        Assert.Equal("Off-grid", data.Get("system_type"));
        Assert.Equal("40", data.Get("t_insulation_value"));
    }

    /// <summary>Set SOLAR_REPORT_SAMPLE_DIR to look at the generated files.</summary>
    internal static void WriteSample(string name, byte[] bytes)
    {
        var dir = Environment.GetEnvironmentVariable("SOLAR_REPORT_SAMPLE_DIR");
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), bytes);
        }
    }
}
