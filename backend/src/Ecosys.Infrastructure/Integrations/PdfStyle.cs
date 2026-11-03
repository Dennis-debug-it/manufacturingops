using Ecosys.Shared.Contracts.Integration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ecosys.Infrastructure.Integrations;

/// <summary>
/// Resolves every styling decision for a generated PDF from a <see cref="ReportTemplateConfig"/>,
/// applying sensible defaults when the config is null or a field is unset. Centralising this here
/// keeps the individual document renderers free of hardcoded fonts, colours, sizes, and layout rules.
/// </summary>
internal sealed class PdfStyle
{
    private static readonly string[] SafeFonts = ["Helvetica", "Arial", "Times New Roman"];

    private readonly ReportTemplateConfig _cfg;

    private PdfStyle(ReportTemplateConfig cfg) => _cfg = cfg;

    public static PdfStyle From(ReportTemplateConfig? cfg) => new(cfg ?? new ReportTemplateConfig());

    public ReportTemplateConfig Config => _cfg;

    // ── Typography ──────────────────────────────────────────────────────────

    public string Font => SafeFonts.Contains(_cfg.FontFamily) ? _cfg.FontFamily : "Helvetica";
    public float BodyFontSize => Math.Clamp(_cfg.BodyFontSize, 7, 14);
    public float LineHeight => (float)Math.Clamp(_cfg.LineSpacing, 1.0, 2.0);

    /// <summary>Applies the resolved font family, body size, and line height as the page default.</summary>
    public void ApplyDefaultTextStyle(PageDescriptor page) =>
        page.DefaultTextStyle(x => x.FontSize(BodyFontSize).FontFamily(Font).LineHeight(LineHeight));

    // ── Colours ─────────────────────────────────────────────────────────────

    public string Primary(string fallback) =>
        !string.IsNullOrWhiteSpace(_cfg.ReportPrimaryColor) ? _cfg.ReportPrimaryColor! : fallback;

    public string Secondary(string fallback) =>
        !string.IsNullOrWhiteSpace(_cfg.ReportSecondaryColor) ? _cfg.ReportSecondaryColor! : fallback;

    public string? HeaderBackground =>
        string.IsNullOrWhiteSpace(_cfg.HeaderBackgroundColor) ? null : _cfg.HeaderBackgroundColor;

    // ── Watermark ───────────────────────────────────────────────────────────

    /// <summary>Renders the watermark honouring text, position, opacity, and colour. No-op when unset.</summary>
    public void ApplyWatermark(PageDescriptor page)
    {
        if (string.IsNullOrWhiteSpace(_cfg.WatermarkText))
            return;

        var text = _cfg.WatermarkText.ToUpperInvariant();
        var colour = ResolveWatermarkColour();
        var background = page.Background();

        switch (_cfg.WatermarkPosition?.Trim().ToLowerInvariant())
        {
            case "top":
                background.AlignTop().AlignCenter().PaddingTop(48)
                    .Text(text).FontSize(40).SemiBold().FontColor(colour);
                break;
            case "bottom":
                background.AlignBottom().AlignCenter().PaddingBottom(48)
                    .Text(text).FontSize(40).SemiBold().FontColor(colour);
                break;
            default: // diagonal / centre
                background.AlignCenter().AlignMiddle()
                    .Rotate(-30)
                    .Text(text).FontSize(64).SemiBold().FontColor(colour);
                break;
        }
    }

    private string ResolveWatermarkColour()
    {
        // Blend the chosen colour (or neutral grey) toward white per the opacity setting so the
        // watermark stays behind the content. Lower opacity → lighter tint.
        var baseColour = string.IsNullOrWhiteSpace(_cfg.WatermarkColor) ? "#9CA3AF" : _cfg.WatermarkColor!;
        var opacity = Math.Clamp(_cfg.WatermarkOpacity, 5, 60) / 100.0;
        return BlendTowardWhite(baseColour, opacity);
    }

    /// <summary>Mixes a hex colour toward white; alpha is the retained fraction of the original colour.</summary>
    private static string BlendTowardWhite(string hex, double alpha)
    {
        if (!TryParseHex(hex, out var r, out var g, out var b))
            return Colors.Grey.Lighten3;

        int Mix(int c) => (int)Math.Round(c * alpha + 255 * (1 - alpha));
        return $"#{Mix(r):X2}{Mix(g):X2}{Mix(b):X2}";
    }

    private static bool TryParseHex(string hex, out int r, out int g, out int b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        hex = hex.TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        if (hex.Length != 6) return false;
        try
        {
            r = Convert.ToInt32(hex[..2], 16);
            g = Convert.ToInt32(hex[2..4], 16);
            b = Convert.ToInt32(hex[4..6], 16);
            return true;
        }
        catch { return false; }
    }

    // ── Formatting ──────────────────────────────────────────────────────────

    public string FormatDate(DateTime date)
    {
        var fmt = string.IsNullOrWhiteSpace(_cfg.DateFormat) ? "dd MMM yyyy" : _cfg.DateFormat;
        try { return date.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture); }
        catch (FormatException) { return date.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture); }
    }

    private static readonly Dictionary<string, string> CurrencySymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["KES"] = "KSh", ["USD"] = "$", ["EUR"] = "€", ["GBP"] = "£",
        ["UGX"] = "USh", ["TZS"] = "TSh", ["NGN"] = "₦", ["ZAR"] = "R",
    };

    public string CurrencyLabel(string currencyCode)
    {
        if (string.Equals(_cfg.CurrencyDisplay, "Symbol", StringComparison.OrdinalIgnoreCase)
            && CurrencySymbols.TryGetValue(currencyCode, out var symbol))
            return symbol;
        return currencyCode;
    }

    public string Money(string currencyCode, decimal amount) =>
        $"{CurrencyLabel(currencyCode)} {amount:N2}";

    // ── Footer / page numbers / attribution ─────────────────────────────────

    public bool ShowPageNumbers => _cfg.ShowPageNumbers;
    public bool ShowPoweredBy => _cfg.ShowPoweredByEcosys;

    /// <summary>Aligns a footer container per the configured page-number position.</summary>
    public IContainer AlignPageNumber(IContainer container) =>
        (_cfg.PageNumberPosition?.Trim().ToLowerInvariant()) switch
        {
            "left" => container.AlignLeft(),
            "center" => container.AlignCenter(),
            _ => container.AlignRight(),
        };
}
