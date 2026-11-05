using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ecosys.Infrastructure.Services.Factory;

public interface IAssetTagNumberService
{
    /// <summary>
    /// Reserves <paramref name="count"/> consecutive tag numbers and saves the counter straight
    /// away, so two people issuing tags at once never get the same number. Numbers reserved but
    /// not used are skipped, never reused. Call it before adding other changes to the context.
    /// </summary>
    Task<IReadOnlyList<string>> ReserveAsync(Guid tenantId, int count, CancellationToken cancellationToken = default);

    /// <summary>Normalises a scanned or typed code: trims, upper-cases, and takes the last path segment of a scanned link.</summary>
    string NormalizeCode(string code);
}

internal sealed class AssetTagNumberService(AppDbContext dbContext, IFactorySettingsService factorySettingsService) : IAssetTagNumberService
{
    private const int MaxAttempts = 5;

    public async Task<IReadOnlyList<string>> ReserveAsync(Guid tenantId, int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0)
        {
            return [];
        }

        for (var attempt = 1; ; attempt++)
        {
            var settings = await factorySettingsService.GetOrCreateAsync(tenantId, cancellationToken);
            var first = settings.NextTagNumber;
            settings.NextTagNumber = first + count;
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return Enumerable.Range(first, count).Select(n => Format(settings.TagPrefix, n)).ToList();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                await dbContext.Entry(settings).ReloadAsync(cancellationToken);
            }
        }
    }

    public string NormalizeCode(string code)
    {
        var value = code.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            value = uri.Segments.LastOrDefault()?.Trim('/') ?? value;
        }

        return Uri.UnescapeDataString(value).Trim().ToUpperInvariant();
    }

    internal static string Format(string prefix, int number) => $"{prefix.Trim().ToUpperInvariant()}-{number:D6}";
}

public sealed record AssetLabel(string TagNumber, string Payload, string AssetName, string AssetCode, string? Location, string? Criticality);

public static class AssetLabelLayouts
{
    /// <summary>A4, 3 × 8 labels of about 70 × 37 mm (common self-adhesive sheets).</summary>
    public const string Small = "a4-24";
    /// <summary>A4, 2 × 5 labels of about 105 × 57 mm, for machines read from a distance.</summary>
    public const string Large = "a4-10";

    public static readonly IReadOnlyList<string> All = [Small, Large];
}

public interface IAssetLabelPdfRenderer
{
    byte[] Render(string companyName, IReadOnlyList<AssetLabel> labels, string layout);
}

/// <summary>Sheets of QR labels for printing on self-adhesive stock (scope Phase 1: batch tag printing).</summary>
internal sealed class AssetLabelPdfRenderer : IAssetLabelPdfRenderer
{
    public byte[] Render(string companyName, IReadOnlyList<AssetLabel> labels, string layout)
    {
        if (labels.Count == 0)
        {
            throw new BusinessRuleException("There are no labels to print.");
        }

        var (columns, rows) = layout switch
        {
            AssetLabelLayouts.Small => (3, 8),
            AssetLabelLayouts.Large => (2, 5),
            _ => throw new BusinessRuleException($"Label layout must be one of: {string.Join(", ", AssetLabelLayouts.All)}.")
        };
        var large = layout == AssetLabelLayouts.Large;
        var perPage = columns * rows;

        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document =>
        {
            foreach (var pageLabels in labels.Chunk(perPage))
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(8, Unit.Millimetre);
                    page.DefaultTextStyle(x => x.FontSize(large ? 10 : 7));
                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(definition =>
                        {
                            for (var i = 0; i < columns; i++)
                            {
                                definition.RelativeColumn();
                            }
                        });

                        foreach (var label in pageLabels)
                        {
                            table.Cell()
                                .Height(large ? 55 : 35, Unit.Millimetre)
                                .Padding(1, Unit.Millimetre)
                                .Border(0.5f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(2, Unit.Millimetre)
                                .Element(cell => RenderLabel(cell, companyName, label, large));
                        }
                    });
                });
            }
        }).GeneratePdf();
    }

    private static void RenderLabel(IContainer container, string companyName, AssetLabel label, bool large)
    {
        var qr = QrPng(label.Payload);
        container.Row(row =>
        {
            row.ConstantItem(large ? 45 : 28, Unit.Millimetre).AlignMiddle().Image(qr).FitArea();
            row.Spacing(2, Unit.Millimetre);
            row.RelativeItem().AlignMiddle().Column(column =>
            {
                column.Spacing(1);
                column.Item().Text(companyName).FontSize(large ? 8 : 5.5f).FontColor(Colors.Grey.Darken1);
                column.Item().Text(label.TagNumber).FontSize(large ? 16 : 10).Bold();
                column.Item().Text(label.AssetName).SemiBold().ClampLines(2);
                column.Item().Text(label.AssetCode).FontColor(Colors.Grey.Darken2);
                if (!string.IsNullOrWhiteSpace(label.Location))
                {
                    column.Item().Text(label.Location).FontSize(large ? 8 : 5.5f).ClampLines(1);
                }

                if (!string.IsNullOrWhiteSpace(label.Criticality))
                {
                    column.Item().Text($"Criticality {label.Criticality}").FontSize(large ? 8 : 5.5f).Bold();
                }
            });
        });
    }

    internal static byte[] QrPng(string payload)
    {
        using var generator = new QRCodeGenerator();
        // Level Q survives about a quarter of the label being dirty or scratched.
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(10);
    }
}
