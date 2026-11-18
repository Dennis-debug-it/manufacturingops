using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;

namespace Ecosys.Tests.Factory;

/// <summary>The document designer's renderer draws every type in every layout, as a PDF and as a preview image.</summary>
public sealed class PlatformDocumentPdfTests
{
    public static TheoryData<string, string> TypesAndLayouts()
    {
        var data = new TheoryData<string, string>();
        foreach (var type in PlatformDocumentTypes.All)
        foreach (var layout in PlatformDocumentLayouts.All)
            data.Add(type, layout);
        return data;
    }

    [Theory]
    [MemberData(nameof(TypesAndLayouts))]
    public void Renders(string type, string layout)
    {
        var template = PlatformDocumentPdf.BuiltInTemplate(type);
        template.Layout = layout;
        template.AccentColor = "#1d4ed8";
        template.HeaderHtml = "P.O. Box 1, Nairobi";
        template.TermsHtml = "Pay to account {{document.number}}";
        var issuer = new PlatformDocumentIssuer("Ecosys", null);

        var pdf = PlatformDocumentPdf.Render(template, PlatformDocumentPdf.Sample(type), issuer);
        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);

        var png = PlatformDocumentPdf.RenderFirstPageImage(template, PlatformDocumentPdf.Sample(type), issuer);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
    }

    [Fact]
    public void ABadAccentColour_FallsBackRatherThanFailing()
    {
        var template = PlatformDocumentPdf.BuiltInTemplate(PlatformDocumentTypes.Invoice);
        template.AccentColor = "not-a-colour";
        var pdf = PlatformDocumentPdf.Render(template, PlatformDocumentPdf.Sample(PlatformDocumentTypes.Invoice), new PlatformDocumentIssuer("Ecosys", null));
        Assert.NotEmpty(pdf);
    }
}
