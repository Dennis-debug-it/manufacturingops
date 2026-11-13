using Ecosys.Shared.Errors;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>What is printed on one visitor badge. <paramref name="QrPayload"/> carries only the badge token.</summary>
public sealed record VisitorBadge(
    string CompanyName, string FactoryName, string BadgeNumber, string QrPayload, string FullName, string? Company, string HostName,
    string Purpose, DateTime ValidUntilLocal, IReadOnlyList<string> Zones, string? EscortNote);

public interface IVisitorBadgePdfRenderer
{
    byte[] Render(IReadOnlyList<VisitorBadge> badges);
}

/// <summary>Visitor badges, A4 with four A6 badges a page, to cut and put in a holder.</summary>
internal sealed class VisitorBadgePdfRenderer : IVisitorBadgePdfRenderer
{
    public byte[] Render(IReadOnlyList<VisitorBadge> badges)
    {
        if (badges.Count == 0) throw new BusinessRuleException("There are no badges to print.");
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document =>
        {
            foreach (var page in badges.Chunk(4))
            {
                document.Page(p =>
                {
                    p.Size(PageSizes.A4);
                    p.Margin(6, Unit.Millimetre);
                    p.DefaultTextStyle(x => x.FontSize(10));
                    p.Content().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                        foreach (var badge in page)
                        {
                            table.Cell().Height(140, Unit.Millimetre).Padding(2, Unit.Millimetre)
                                .Border(0.75f).BorderColor(Colors.Grey.Medium).Padding(5, Unit.Millimetre)
                                .Element(cell => RenderBadge(cell, badge));
                        }
                    });
                });
            }
        }).GeneratePdf();
    }

    private static void RenderBadge(IContainer container, VisitorBadge badge)
    {
        var restricted = badge.Zones.Count > 0;
        container.Column(column =>
        {
            column.Spacing(3, Unit.Millimetre);
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(badge.CompanyName).FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(badge.FactoryName).SemiBold();
                });
                row.ConstantItem(28, Unit.Millimetre).Background(restricted ? Colors.Orange.Lighten3 : Colors.Green.Lighten4)
                    .AlignCenter().AlignMiddle().Padding(2).Text(badge.Purpose == "Contractor work" ? "CONTRACTOR" : "VISITOR").FontSize(9).Bold();
            });
            column.Item().Text(badge.FullName).FontSize(18).Bold().ClampLines(2);
            if (!string.IsNullOrWhiteSpace(badge.Company)) column.Item().Text(badge.Company).FontSize(11).ClampLines(1);
            column.Item().AlignCenter().Width(45, Unit.Millimetre).Image(AssetLabelPdfRenderer.QrPng(badge.QrPayload)).FitWidth();
            column.Item().AlignCenter().Text(badge.BadgeNumber).FontSize(9).FontColor(Colors.Grey.Darken2);
            column.Item().Text(text =>
            {
                text.Span("Host: ").SemiBold();
                text.Span(badge.HostName);
            });
            column.Item().Text(text =>
            {
                text.Span("Valid until: ").SemiBold();
                text.Span($"{badge.ValidUntilLocal:ddd dd MMM, HH:mm}");
            });
            column.Item().Text(text =>
            {
                text.Span("May enter: ").SemiBold();
                text.Span(restricted ? "Public areas, " + string.Join(", ", badge.Zones) : "Public areas only");
            });
            if (badge.EscortNote is not null) column.Item().Text(badge.EscortNote).FontSize(9).Bold().FontColor(Colors.Red.Darken2);
            column.Item().Text("Wear this badge where it can be seen and hand it back at the gate when you leave.").FontSize(7).FontColor(Colors.Grey.Darken1);
        });
    }
}
