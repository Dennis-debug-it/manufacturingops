using System.Globalization;
using Ecosys.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ecosys.Infrastructure.Services;

/// <summary>A one- or two-page price quote: what is included, the yearly subscription, one-off fees, VAT and the pilot offer.</summary>
public static class QuotePdf
{
    private static string M(decimal value) => value.ToString("#,0", CultureInfo.InvariantCulture);

    public static byte[] Render(string vendor, PriceQuote quote, PricingResult result, string? terms)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var currency = result.Currency;
        var recurring = result.Lines.Where(x => x.Kind is PriceLineKinds.Subscription or PriceLineKinds.Discount).ToList();
        var oneOff = result.Lines.Where(x => x.Kind is PriceLineKinds.OneOff or PriceLineKinds.Hardware).ToList();
        var includes = new List<string>();
        if (quote.Edition is { } edition) includes.Add($"Maintenance {FactoryPricing.EditionName(edition)}");
        if (quote.Visitors) includes.Add("Visitor Management");
        if (quote.Security) includes.Add("Security Patrol");

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Colors.Grey.Darken4));
            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(vendor).Bold().FontSize(16).FontColor("#0f3d3e");
                        c.Item().Text("Factory Operations: maintenance, visitors and security").FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(190).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("QUOTATION").Bold().FontSize(13);
                        c.Item().AlignRight().Text(quote.QuoteNumber).FontSize(10);
                        c.Item().AlignRight().Text($"Date {quote.CreatedAt:dd MMM yyyy}");
                        c.Item().AlignRight().Text($"Valid until {quote.ValidUntil:dd MMM yyyy}");
                    });
                });
                col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });

            page.Content().PaddingTop(12).Column(col =>
            {
                col.Spacing(10);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Prepared for").FontColor(Colors.Grey.Darken1).FontSize(8.5f);
                        c.Item().Text(quote.CustomerName).SemiBold().FontSize(11);
                        if (!string.IsNullOrWhiteSpace(quote.ContactName)) c.Item().Text(quote.ContactName);
                        if (!string.IsNullOrWhiteSpace(quote.ContactEmail)) c.Item().Text(quote.ContactEmail);
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Scope").FontColor(Colors.Grey.Darken1).FontSize(8.5f);
                        c.Item().Text($"{string.Join(", ", includes)}");
                        c.Item().Text($"{quote.Factories} {(quote.Factories == 1 ? "factory" : "factories")}{(quote.OnPremises ? ", installed on your own servers" : ", hosted")}");
                        c.Item().Text($"Includes {result.IncludedOfficeUsers} office and {result.IncludedFieldUsers} field users (technicians, guards)");
                    });
                });

                col.Item().Text("Yearly subscription").SemiBold().FontSize(11);
                col.Item().Element(c => Lines(c, recurring, currency, "Subscription per year", result.AnnualSubscription));
                col.Item().Text($"Equivalent to {currency} {M(result.MonthlyEquivalent)} a month, or {currency} {M(result.PerFactoryPerMonth)} per factory per month.")
                    .FontColor(Colors.Grey.Darken1);

                if (oneOff.Count > 0)
                {
                    col.Item().PaddingTop(4).Text("One-off").SemiBold().FontSize(11);
                    col.Item().Element(c => Lines(c, oneOff, currency, "One-off total", result.OneOffTotal));
                }

                col.Item().PaddingTop(4).Background(Colors.Grey.Lighten4).Padding(10).Column(t =>
                {
                    t.Item().Row(r => { r.RelativeItem().Text("Year one, excluding VAT"); r.ConstantItem(120).AlignRight().Text($"{currency} {M(result.YearOneTotal)}"); });
                    t.Item().Row(r => { r.RelativeItem().Text($"VAT {result.VatPercent.ToString("0.##", CultureInfo.InvariantCulture)}%"); r.ConstantItem(120).AlignRight().Text($"{currency} {M(result.YearOneVat)}"); });
                    t.Item().Row(r => { r.RelativeItem().Text("Year one, including VAT").Bold(); r.ConstantItem(120).AlignRight().Text($"{currency} {M(result.YearOneTotalWithVat)}").Bold(); });
                    t.Item().PaddingTop(2).Text($"From year two: {currency} {M(result.AnnualSubscription)} a year plus VAT.").FontColor(Colors.Grey.Darken1);
                });

                if (result.Pilot is { } pilot)
                {
                    col.Item().Border(1).BorderColor("#0f3d3e").Padding(10).Column(p =>
                    {
                        p.Item().Text("Pilot").SemiBold().FontSize(11);
                        p.Item().Text($"One factory for {pilot.Months} months: {currency} {M(pilot.Fee)} plus VAT ({currency} {M(pilot.Vat)}), with implementation at cost. "
                            + (pilot.CreditedToYearOne ? "The pilot fee is taken off year one if you continue. " : string.Empty)
                            + "Success measures and the date to decide are agreed in writing before the pilot starts.");
                    });
                }

                if (!string.IsNullOrWhiteSpace(quote.Notes))
                {
                    col.Item().Text("Notes").SemiBold().FontSize(11);
                    col.Item().Text(quote.Notes);
                }
            });

            page.Footer().Column(c =>
            {
                if (!string.IsNullOrWhiteSpace(terms)) c.Item().Text(terms).FontSize(8).FontColor(Colors.Grey.Darken1);
                c.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text(quote.PreparedByName is { } by ? $"Prepared by {by}" : string.Empty).FontSize(8).FontColor(Colors.Grey.Darken1);
                    r.ConstantItem(80).AlignRight().Text(t => { t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1)); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
                });
            });
        })).GeneratePdf();
    }

    private static void Lines(IContainer container, IReadOnlyList<PriceLine> lines, string currency, string totalLabel, decimal total)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(6); c.RelativeColumn(1.4f); c.RelativeColumn(2); c.RelativeColumn(2.2f); });
            static IContainer Head(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(4);
            static IContainer Cell(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(4);
            table.Header(h =>
            {
                h.Cell().Element(Head).Text("Item").SemiBold();
                h.Cell().Element(Head).AlignRight().Text("Qty").SemiBold();
                h.Cell().Element(Head).AlignRight().Text("Unit price").SemiBold();
                h.Cell().Element(Head).AlignRight().Text($"Amount ({currency})").SemiBold();
            });
            foreach (var line in lines)
            {
                table.Cell().Element(Cell).Text(line.Description);
                table.Cell().Element(Cell).AlignRight().Text(line.Kind == PriceLineKinds.Discount ? "" : line.Quantity.ToString("0.##", CultureInfo.InvariantCulture));
                table.Cell().Element(Cell).AlignRight().Text(line.Kind == PriceLineKinds.Discount ? "" : M(line.UnitPrice));
                table.Cell().Element(Cell).AlignRight().Text(M(line.Amount));
            }

            table.Cell().ColumnSpan(3).PaddingVertical(4).Text(totalLabel).SemiBold();
            table.Cell().PaddingVertical(4).AlignRight().Text(M(total)).SemiBold();
        });
    }
}
