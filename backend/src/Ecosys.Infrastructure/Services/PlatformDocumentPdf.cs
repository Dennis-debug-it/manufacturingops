using System.Globalization;
using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ecosys.Infrastructure.Services;

/// <summary>One line on a quotation or invoice.</summary>
public sealed record PlatformDocumentLine(string Description, decimal Quantity, decimal UnitPrice, decimal Amount);

/// <summary>
/// What a platform document says: the figures and parties. How it looks comes from a
/// <see cref="PlatformDocumentTemplate"/> (see <see cref="PlatformDocumentPdf"/>).
/// </summary>
public sealed record PlatformDocumentData(
    string Type,
    string Number,
    DateTime Date,
    string? SecondDateLabel,
    DateTime? SecondDate,
    string CustomerName,
    string? CustomerEmail,
    string Currency,
    IReadOnlyList<PlatformDocumentLine> Lines,
    decimal Subtotal,
    decimal DiscountRate,
    decimal DiscountAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal Total,
    decimal? AmountPaid = null,
    decimal? Balance = null,
    string? Notes = null,
    string? Status = null,
    // Receipts
    string? PaymentMethod = null,
    string? PaymentReference = null,
    string? AppliesTo = null);

/// <summary>The issuer: the platform operator's name and logo.</summary>
public sealed record PlatformDocumentIssuer(string Name, byte[]? Logo);

/// <summary>
/// Draws a quotation, invoice or receipt as a PDF in the style a document template sets: layout, accent colour,
/// page size, the company details, opening message, terms, sign-off and footer. Template text may use
/// {{placeholders}} (see <see cref="Placeholders"/>), filled from the document.
/// </summary>
public static class PlatformDocumentPdf
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{\s*([a-zA-Z0-9\._]+)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex HexColour = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    /// <summary>The placeholders template text can use, with what each becomes.</summary>
    public static readonly IReadOnlyList<(string Key, string Meaning)> Placeholders =
    [
        ("company.name", "Your company name"),
        ("customer.name", "Customer name"),
        ("customer.email", "Customer email"),
        ("document.number", "Document number"),
        ("document.date", "Document date"),
        ("document.dueDate", "Due date (invoice) or valid-until date (quotation)"),
        ("document.total", "Total, with currency"),
        ("document.balance", "Balance still owing (invoice)"),
        ("payment.method", "Payment method (receipt)"),
        ("payment.reference", "Payment reference (receipt)"),
    ];

    private static string M(decimal value) => value.ToString("#,0.00", CultureInfo.InvariantCulture);
    private static string D(DateTime value) => value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>The look a document gets when no template of its type has been designed yet.</summary>
    public static PlatformDocumentTemplate BuiltInTemplate(string type) => new()
    {
        Name = $"Standard {type.ToLowerInvariant()}",
        Type = type,
        Subject = DefaultTitle(type),
        BodyHtml = type switch
        {
            PlatformDocumentTypes.Quotation => "Thank you for your interest. Here is our quotation for {{customer.name}}.",
            PlatformDocumentTypes.Receipt => "Thank you for your payment.",
            _ => null,
        },
        TermsHtml = type switch
        {
            PlatformDocumentTypes.Invoice => "Payment is due by {{document.dueDate}}. Please quote {{document.number}} with your payment.",
            PlatformDocumentTypes.Quotation => "This quotation is valid until {{document.dueDate}}. Prices exclude VAT unless shown.",
            _ => null,
        },
        IsActive = true,
    };

    public static string DefaultTitle(string type) => type switch
    {
        PlatformDocumentTypes.Quotation => "QUOTATION",
        PlatformDocumentTypes.Receipt => "RECEIPT",
        _ => "INVOICE",
    };

    /// <summary>A made-up document of the type, for previewing a template in the designer.</summary>
    public static PlatformDocumentData Sample(string type)
    {
        var today = DateTime.UtcNow.Date;
        var lines = new List<PlatformDocumentLine>
        {
            new("Ecosys Factory Maintenance, Standard edition (per factory per year)", 2, 420_000m, 840_000m),
            new("Visitor Management add-on", 2, 90_000m, 180_000m),
            new("Implementation and training", 1, 150_000m, 150_000m),
        };
        var subtotal = lines.Sum(x => x.Amount);
        var discount = Math.Round(subtotal * 0.05m, 2);
        var tax = Math.Round((subtotal - discount) * 0.16m, 2);
        var total = subtotal - discount + tax;
        return type switch
        {
            PlatformDocumentTypes.Receipt => new PlatformDocumentData(type, "RCT-000042", today, null, null, "Kapkoros Tea Factory", "accounts@kapkoros.example",
                "KES", [], total, 0, 0, 0, 0, total, PaymentMethod: "M-Pesa", PaymentReference: "SKL4X2B9QZ", AppliesTo: "INV-000118"),
            PlatformDocumentTypes.Quotation => new PlatformDocumentData(type, "QUO-000057", today, "Valid until", today.AddDays(30), "Kapkoros Tea Factory", "accounts@kapkoros.example",
                "KES", lines, subtotal, 5, discount, 16, tax, total, Notes: "Pricing assumes both factories go live in the same month."),
            _ => new PlatformDocumentData(type, "INV-000118", today, "Due", today.AddDays(14), "Kapkoros Tea Factory", "accounts@kapkoros.example",
                "KES", lines, subtotal, 5, discount, 16, tax, total, AmountPaid: 0, Balance: total),
        };
    }

    public static byte[] Render(PlatformDocumentTemplate template, PlatformDocumentData data, PlatformDocumentIssuer issuer) =>
        Build(template, data, issuer).GeneratePdf();

    /// <summary>The first page as a PNG, for previews where a browser cannot show a PDF inline.</summary>
    public static byte[] RenderFirstPageImage(PlatformDocumentTemplate template, PlatformDocumentData data, PlatformDocumentIssuer issuer) =>
        Build(template, data, issuer).GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = 110 }).First();

    private static Document Build(PlatformDocumentTemplate template, PlatformDocumentData data, PlatformDocumentIssuer issuer)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var accent = HexColour.IsMatch(template.AccentColor ?? string.Empty) ? template.AccentColor! : PlatformDocumentTemplate.DefaultAccent;
        var layout = PlatformDocumentLayouts.All.Contains(template.Layout) ? template.Layout : PlatformDocumentLayouts.Classic;
        var values = Values(data, issuer);
        string? Fill(string? text) => string.IsNullOrWhiteSpace(text) ? null : PlaceholderRegex.Replace(text.Trim(), m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

        var title = Fill(template.Subject) ?? DefaultTitle(data.Type);
        var details = Fill(template.HeaderHtml);
        var intro = Fill(template.BodyHtml);
        var terms = Fill(template.TermsHtml);
        var footer = Fill(template.FooterHtml);
        var signature = Fill(template.SignatureHtml);
        var logo = template.ShowLogo && issuer.Logo is { Length: > 0 } ? issuer.Logo : null;
        var banner = layout == PlatformDocumentLayouts.Banner;
        var minimal = layout == PlatformDocumentLayouts.Minimal;

        var size = template.PageSize switch { "Letter" => PageSizes.Letter, _ => PageSizes.A4 };
        if (template.Orientation == "Landscape") size = size.Landscape();

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(size);
            page.Margin(minimal ? 32 : 40);
            page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Colors.Grey.Darken4));

            page.Header().Column(col =>
            {
                var head = banner ? col.Item().Background(accent).Padding(16) : col.Item();
                head.Row(row =>
                {
                    if (logo is not null)
                    {
                        row.ConstantItem(70).Height(48).AlignMiddle().Image(logo).FitArea();
                        row.ConstantItem(12);
                    }

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(issuer.Name).Bold().FontSize(15).FontColor(banner ? Colors.White : accent);
                        if (details is not null) c.Item().Text(details).FontSize(8.5f).FontColor(banner ? Colors.Grey.Lighten3 : Colors.Grey.Darken1);
                    });
                    row.ConstantItem(200).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text(title).Bold().FontSize(minimal ? 13 : 16).FontColor(banner ? Colors.White : accent);
                        c.Item().AlignRight().Text(data.Number).FontSize(10).FontColor(banner ? Colors.White : Colors.Grey.Darken4);
                        c.Item().AlignRight().Text($"Date {D(data.Date)}").FontColor(banner ? Colors.Grey.Lighten3 : Colors.Grey.Darken1);
                        if (data.SecondDate is { } second) c.Item().AlignRight().Text($"{data.SecondDateLabel} {D(second)}").FontColor(banner ? Colors.Grey.Lighten3 : Colors.Grey.Darken1);
                    });
                });
                if (!banner) col.Item().PaddingTop(10).LineHorizontal(minimal ? 0.5f : 1.5f).LineColor(minimal ? Colors.Grey.Lighten2 : accent);
            });

            page.Content().PaddingTop(14).Column(col =>
            {
                col.Spacing(12);
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(data.Type == PlatformDocumentTypes.Receipt ? "Received from" : data.Type == PlatformDocumentTypes.Quotation ? "Prepared for" : "Bill to")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        c.Item().Text(data.CustomerName).SemiBold().FontSize(11);
                        if (!string.IsNullOrWhiteSpace(data.CustomerEmail)) c.Item().Text(data.CustomerEmail);
                    });
                    if (data.Status is { } status && data.Type == PlatformDocumentTypes.Invoice && status is "Paid" or "Void")
                    {
                        row.ConstantItem(120).AlignRight().AlignMiddle().Border(1.5f).BorderColor(status == "Paid" ? Colors.Green.Darken2 : Colors.Red.Darken2)
                            .PaddingVertical(4).PaddingHorizontal(10).Text(status.ToUpperInvariant()).Bold().FontColor(status == "Paid" ? Colors.Green.Darken2 : Colors.Red.Darken2);
                    }
                });

                if (intro is not null) col.Item().Text(intro);

                if (data.Type == PlatformDocumentTypes.Receipt)
                {
                    col.Item().Background(minimal ? Colors.White : Colors.Grey.Lighten4).Border(minimal ? 0.5f : 0).BorderColor(Colors.Grey.Lighten2).Padding(14).Column(r =>
                    {
                        r.Spacing(4);
                        r.Item().Row(x => { x.RelativeItem().Text("Amount received"); x.ConstantItem(180).AlignRight().Text($"{data.Currency} {M(data.Total)}").Bold().FontSize(13).FontColor(accent); });
                        if (data.PaymentMethod is { } method) r.Item().Row(x => { x.RelativeItem().Text("Method"); x.ConstantItem(180).AlignRight().Text(method); });
                        if (data.PaymentReference is { } reference) r.Item().Row(x => { x.RelativeItem().Text("Reference"); x.ConstantItem(180).AlignRight().Text(reference); });
                        if (data.AppliesTo is { } invoice) r.Item().Row(x => { x.RelativeItem().Text("For invoice"); x.ConstantItem(180).AlignRight().Text(invoice); });
                        if (data.Balance is { } left) r.Item().Row(x => { x.RelativeItem().Text("Balance still owing"); x.ConstantItem(180).AlignRight().Text($"{data.Currency} {M(left)}"); });
                    });
                }
                else
                {
                    col.Item().Element(c => LinesTable(c, data, accent, minimal));
                    col.Item().AlignRight().Width(260).Column(t =>
                    {
                        t.Spacing(3);
                        void Row(string label, string value, bool strong = false)
                        {
                            t.Item().Row(r =>
                            {
                                var l = r.RelativeItem().Text(label);
                                var v = r.ConstantItem(130).AlignRight().Text(value);
                                if (strong) { l.Bold(); v.Bold().FontColor(accent); }
                            });
                        }

                        Row("Subtotal", M(data.Subtotal));
                        if (data.DiscountAmount > 0) Row($"Discount {data.DiscountRate.ToString("0.##", CultureInfo.InvariantCulture)}%", $"-{M(data.DiscountAmount)}");
                        if (data.TaxAmount > 0 || data.TaxRate > 0) Row($"VAT {data.TaxRate.ToString("0.##", CultureInfo.InvariantCulture)}%", M(data.TaxAmount));
                        t.Item().PaddingTop(2).LineHorizontal(0.75f).LineColor(Colors.Grey.Lighten1);
                        Row($"Total ({data.Currency})", M(data.Total), strong: true);
                        if (data.AmountPaid is > 0) Row("Paid", M(data.AmountPaid.Value));
                        if (data.Balance is { } balance && data.AmountPaid is > 0) Row("Balance due", M(balance), strong: true);
                    });
                }

                if (!string.IsNullOrWhiteSpace(data.Notes))
                {
                    col.Item().Column(n =>
                    {
                        n.Item().Text("Notes").SemiBold().FontColor(accent);
                        n.Item().Text(data.Notes);
                    });
                }

                if (terms is not null)
                {
                    col.Item().Column(n =>
                    {
                        n.Item().Text(data.Type == PlatformDocumentTypes.Receipt ? "Details" : "Terms and payment").SemiBold().FontColor(accent);
                        n.Item().Text(terms).FontSize(8.5f);
                    });
                }

                if (signature is not null) col.Item().PaddingTop(16).Text(signature);
            });

            page.Footer().Column(c =>
            {
                if (footer is not null) c.Item().AlignCenter().Text(footer).FontSize(8).FontColor(Colors.Grey.Darken1);
                c.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text(template.ShowPoweredByEcosys ? "Powered by Ecosys" : string.Empty).FontSize(7.5f).FontColor(Colors.Grey.Medium);
                    r.ConstantItem(80).AlignRight().Text(t => { t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1)); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
                });
            });
        }));
    }

    private static void LinesTable(IContainer container, PlatformDocumentData data, string accent, bool minimal)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c => { c.RelativeColumn(6); c.RelativeColumn(1.2f); c.RelativeColumn(2); c.RelativeColumn(2.2f); });
            IContainer Head(IContainer c) => minimal
                ? c.BorderBottom(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(4)
                : c.Background(accent).PaddingVertical(5).PaddingHorizontal(4);
            static IContainer Cell(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingHorizontal(4);
            table.Header(h =>
            {
                var colour = minimal ? Colors.Grey.Darken4 : Colors.White;
                h.Cell().Element(Head).Text("Description").SemiBold().FontColor(colour);
                h.Cell().Element(Head).AlignRight().Text("Qty").SemiBold().FontColor(colour);
                h.Cell().Element(Head).AlignRight().Text("Unit price").SemiBold().FontColor(colour);
                h.Cell().Element(Head).AlignRight().Text($"Amount ({data.Currency})").SemiBold().FontColor(colour);
            });
            foreach (var line in data.Lines)
            {
                table.Cell().Element(Cell).Text(line.Description);
                table.Cell().Element(Cell).AlignRight().Text(line.Quantity.ToString("0.##", CultureInfo.InvariantCulture));
                table.Cell().Element(Cell).AlignRight().Text(M(line.UnitPrice));
                table.Cell().Element(Cell).AlignRight().Text(M(line.Amount));
            }
        });
    }

    private static Dictionary<string, string> Values(PlatformDocumentData data, PlatformDocumentIssuer issuer) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["company.name"] = issuer.Name,
        ["customer.name"] = data.CustomerName,
        ["customer.email"] = data.CustomerEmail ?? string.Empty,
        ["document.number"] = data.Number,
        ["document.date"] = D(data.Date),
        ["document.dueDate"] = data.SecondDate is { } due ? D(due) : string.Empty,
        ["document.total"] = $"{data.Currency} {M(data.Total)}",
        ["document.balance"] = data.Balance is { } balance ? $"{data.Currency} {M(balance)}" : string.Empty,
        ["payment.method"] = data.PaymentMethod ?? string.Empty,
        ["payment.reference"] = data.PaymentReference ?? string.Empty,
    };
}
