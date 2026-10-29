using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Ecosys.Infrastructure.Integrations;
using Ecosys.Shared.Contracts.Integration;

namespace Ecosys.Tests;

/// <summary>The service report PDF follows the workspace's "Service Report" design and keeps every report photo.</summary>
public sealed class ServiceReportPdfTests
{
    [Fact]
    public void EveryPhotoMarkedForTheReportIsEmbedded_NotJustTheFirstFewPerCategory()
    {
        // Annotated photos are saved as "Other"; with the old per-category cap only the first 4 made it in.
        var photos = Enumerable.Range(1, 9).Select(i => new WorkOrderReportPdfPhoto($"Annotated {i}", null, Png((byte)(i * 20)))).ToList();
        var config = new ReportTemplateConfig { MaxPhotosPerPage = 4 };

        var pdf = new QuestPdfRenderer().RenderWorkOrderReportPdf(Model(config, [new("Other", photos)]));

        Assert.Equal(9, CountImages(pdf));
    }

    [Theory]
    [InlineData("Inline")]
    [InlineData("Appendix")]
    public void PhotosRenderInEitherLayout(string layout)
    {
        var photos = Enumerable.Range(1, 3).Select(i => new WorkOrderReportPdfPhoto($"Photo {i}", null, Png((byte)(i * 50)))).ToList();
        var config = new ReportTemplateConfig { PhotoLayout = layout, MaxPhotosPerPage = 1 };

        var pdf = new QuestPdfRenderer().RenderWorkOrderReportPdf(Model(config, [new("Before", photos)]));

        Assert.Equal(3, CountImages(pdf));
    }

    [Fact]
    public void PhotosAreLeftOutWhenTheDesignTurnsThemOff()
    {
        var photos = new List<WorkOrderReportPdfPhoto> { new("One", null, Png(10)) };

        var off = new QuestPdfRenderer().RenderWorkOrderReportPdf(Model(new ReportTemplateConfig { IncludePhotos = false }, [new("Before", photos)]));
        var hidden = new QuestPdfRenderer().RenderWorkOrderReportPdf(Model(
            new ReportTemplateConfig { Sections = ReportTemplateConfig.DefaultSections().Where(s => s.Id != "photos").ToList() },
            [new("Before", photos)]));

        Assert.Equal(0, CountImages(off));
        Assert.Equal(0, CountImages(hidden));
    }

    [Fact]
    public void RendersEveryDesignOption()
    {
        var sections = ReportTemplateConfig.DefaultSections();
        sections.Reverse();
        for (var i = 0; i < sections.Count; i++) sections[i].Order = i;
        var config = new ReportTemplateConfig
        {
            Sections = sections,
            LogoAlignment = LogoAlignment.Center,
            LogoWidth = 120,
            CompanyNameFontSize = 26,
            HeaderBackgroundColor = "#EEF2FF",
            IntroText = "Thank you for letting us look after your plant.",
            PageDensity = "Compact",
            ShowPageNumbers = false,
            ShowPoweredByEcosys = false,
            ShowSignatureBlock = true,
            ShowStampArea = true,
            SignatureFields = ["Technician", "Client"],
            SignaturePosition = "Right",
            PhotoLayout = "Appendix",
            WatermarkText = "Draft",
        };

        var pdf = new QuestPdfRenderer().RenderWorkOrderReportPdf(Model(config, [new("Before", [new("One", null, Png(90))])], logo: Png(200)));

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Equal(2, CountImages(pdf)); // logo + photo
    }

    [Fact]
    public void ADesignSavedBeforeTheEquipmentSection_GetsItAfterWorkDone_AndKeepsWhatWasSwitchedOff()
    {
        // Saved before "equipment" existed: findings switched off, the old timeline section still listed.
        const string saved = """
            {"sections":[
              {"id":"service-details","label":"Service Details","enabled":true,"order":0},
              {"id":"problem","label":"Problem Reported","enabled":true,"order":1},
              {"id":"findings","label":"Findings","enabled":false,"order":2},
              {"id":"work-done","label":"Work Done","enabled":true,"order":3},
              {"id":"timestamps","label":"Timeline","enabled":true,"order":4},
              {"id":"checklist","label":"Checklist Responses","enabled":true,"order":5},
              {"id":"materials","label":"Materials Used","enabled":true,"order":6},
              {"id":"photos","label":"Photo Evidence","enabled":true,"order":7},
              {"id":"signatures","label":"Signatures / Sign-off","enabled":true,"order":8}]}
            """;

        var config = System.Text.Json.JsonSerializer.Deserialize<ReportTemplateConfig>(saved, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var ids = config.Sections.OrderBy(s => s.Order).Select(s => s.Id).ToList();

        Assert.Equal(["service-details", "problem", "findings", "work-done", "equipment", "timestamps", "checklist", "materials", "photos", "signatures"], ids);
        Assert.True(config.Sections.Single(s => s.Id == "equipment").Enabled);
        Assert.False(config.Sections.Single(s => s.Id == "findings").Enabled);
        Assert.Equal(Enumerable.Range(0, ids.Count), config.Sections.OrderBy(s => s.Order).Select(s => s.Order));
    }

    [Fact]
    public void ListsTheSiteEquipment()
    {
        var equipment = new List<WorkOrderReportPdfEquipment>
        {
            new("AHU-03", "AST-0103", "Carrier 39HQ", "CA39-77120", "Roof", true),
            new("Chiller 1", null, null, null, null, false),
        };

        var pdf = new QuestPdfRenderer().RenderWorkOrderReportPdf(Model(new ReportTemplateConfig(), [], equipment: equipment));

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(pdf.Length > new QuestPdfRenderer().RenderWorkOrderReportPdf(Model(new ReportTemplateConfig(), [])).Length);
    }

    private static WorkOrderReportPdfModel Model(ReportTemplateConfig config, IReadOnlyCollection<WorkOrderReportPdfPhotoGroup> photos, byte[]? logo = null,
        IReadOnlyCollection<WorkOrderReportPdfEquipment>? equipment = null) =>
        new("Acme FM", null, logo, "#0F4C81", "#64748B", true, "+254 700 000 000", "ops@acme.test", "Nairobi",
            "WO-000078", "AHU-03 high static pressure", "Nairobi Hospital", "Main Campus", "AHU-03", null, "Jane Tech",
            null, null, "Tripping on high pressure", "Filters loaded", "Replaced filters", "10 Oct 2026 08:00 UTC",
            [new("Created", "10 Oct 2026 08:00")], [], [], photos, [], config, equipment);

    private static int CountImages(byte[] pdf) =>
        Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Subtype\s*/Image").Count;

    /// <summary>A small solid-colour PNG; a different shade per photo so the PDF can't share one image.</summary>
    private static byte[] Png(byte shade)
    {
        const int size = 8;
        var raw = new MemoryStream();
        for (var y = 0; y < size; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < size; x++) { raw.WriteByte(shade); raw.WriteByte((byte)(255 - shade)); raw.WriteByte(128); }
        }
        var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) raw.WriteTo(z);

        var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(png, "IHDR", [0, 0, 0, size, 0, 0, 0, size, 8, 2, 0, 0, 0]);
        Chunk(png, "IDAT", compressed.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void Chunk(Stream png, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        png.Write(BitConverter.GetBytes(data.Length).Reverse().ToArray());
        png.Write(typeBytes);
        png.Write(data);
        png.Write(BitConverter.GetBytes(Crc(typeBytes.Concat(data).ToArray())).Reverse().ToArray());
    }

    private static uint Crc(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }
        return ~crc;
    }
}
