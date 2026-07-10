namespace Ecosys.Shared.Contracts.Integration;

/// <summary>
/// Per-tenant report template configuration stored as JSON on the Tenant record.
/// All fields are optional — defaults are applied by the renderer when null.
/// </summary>
public sealed class ReportTemplateConfig
{
    /// <summary>Selected document layout preset name.</summary>
    public string LayoutStyle { get; set; } = "Modern";

    /// <summary>Header layout style used by the document preview and renderer.</summary>
    public string HeaderStyle { get; set; } = "Logo left, company details right";

    /// <summary>Footer layout style used by the document preview and renderer.</summary>
    public string FooterStyle { get; set; } = "Contact footer";

    /// <summary>Ordered list of section IDs to include; missing IDs use defaults.</summary>
    public List<ReportSection> Sections { get; set; } = DefaultSections();

    /// <summary>Font family used for body text. Must be a QuestPDF-safe font name.</summary>
    public string FontFamily { get; set; } = "Helvetica";

    /// <summary>Override primary color for report (null = use tenant PrimaryColor).</summary>
    public string? ReportPrimaryColor { get; set; }

    /// <summary>Override secondary color (null = use tenant SecondaryColor).</summary>
    public string? ReportSecondaryColor { get; set; }

    /// <summary>Custom footer message, e.g. "Thank you for choosing Acme Corp."</summary>
    public string? FooterText { get; set; }

    /// <summary>Optional diagonal watermark text, e.g. "CONFIDENTIAL".</summary>
    public string? WatermarkText { get; set; }

    /// <summary>Position of the company logo in the header.</summary>
    public LogoAlignment LogoAlignment { get; set; } = LogoAlignment.Left;

    /// <summary>Whether to show the company name in the header.</summary>
    public bool ShowCompanyName { get; set; } = true;

    /// <summary>Whether to show company contact info (phone, email, address) in the header.</summary>
    public bool ShowCompanyContact { get; set; } = true;

    /// <summary>Whether the logo should be shown in the document preview and rendered output.</summary>
    public bool ShowLogo { get; set; } = true;

    /// <summary>Whether to show a tax breakdown section in the document preview and rendered output.</summary>
    public bool ShowTaxBreakdown { get; set; } = true;

    /// <summary>Whether bank details should be shown in the document preview and rendered output.</summary>
    public bool ShowBankDetails { get; set; }

    /// <summary>Whether a signature block should be shown in the document preview and rendered output.</summary>
    public bool ShowSignatureBlock { get; set; }

    /// <summary>Whether terms and conditions should be shown in the document preview and rendered output.</summary>
    public bool ShowTerms { get; set; } = true;

    /// <summary>Default footer note used in the document preview.</summary>
    public string? DefaultFooterNote { get; set; }

    /// <summary>Default terms text used in the document preview.</summary>
    public string? DefaultTermsText { get; set; }

    /// <summary>Whether to reserve a stamp or seal area on generated documents.</summary>
    public bool ShowStampArea { get; set; } = true;

    /// <summary>Controls spacing density in the generated document preview.</summary>
    public string PageDensity { get; set; } = "Comfortable";

    /// <summary>Default quote terms text displayed in the document preview.</summary>
    public string? QuoteTerms { get; set; }

    /// <summary>Default invoice payment terms text displayed in the document preview.</summary>
    public string? InvoiceTerms { get; set; }

    /// <summary>Warranty note displayed in customer-facing document previews.</summary>
    public string? WarrantyNote { get; set; }

    /// <summary>Late payment note displayed in customer-facing document previews.</summary>
    public string? LatePaymentNote { get; set; }

    /// <summary>Service report disclaimer displayed in customer-facing document previews.</summary>
    public string? ServiceReportDisclaimer { get; set; }

    /// <summary>Footer legal text displayed in customer-facing document previews.</summary>
    public string? FooterLegalText { get; set; }

    /// <summary>Bank name shown in customer-facing document previews.</summary>
    public string? BankName { get; set; }

    /// <summary>Bank account name shown in customer-facing document previews.</summary>
    public string? AccountName { get; set; }

    /// <summary>Bank account number shown in customer-facing document previews.</summary>
    public string? AccountNumber { get; set; }

    /// <summary>Bank branch shown in customer-facing document previews.</summary>
    public string? Branch { get; set; }

    /// <summary>SWIFT/BIC shown in customer-facing document previews.</summary>
    public string? Swift { get; set; }

    /// <summary>Paybill or till number shown in customer-facing document previews.</summary>
    public string? Paybill { get; set; }

    /// <summary>Free-form payment instructions displayed in customer-facing document previews.</summary>
    public string? PaymentInstructions { get; set; }

    /// <summary>Default currency shown in the payment preview block.</summary>
    public string DefaultCurrency { get; set; } = "KES";

    // ── Branding & typography ───────────────────────────────────────────────

    /// <summary>Logo render width in points (24–160). Height scales proportionally.</summary>
    public int LogoWidth { get; set; } = 64;

    /// <summary>Font size (pt) for the company name in the header.</summary>
    public int CompanyNameFontSize { get; set; } = 18;

    /// <summary>Optional header block background colour (hex). Null = no fill.</summary>
    public string? HeaderBackgroundColor { get; set; }

    /// <summary>Body text font size in points.</summary>
    public int BodyFontSize { get; set; } = 10;

    /// <summary>Line height multiplier for body text (1.0 = single spacing).</summary>
    public double LineSpacing { get; set; } = 1.15;

    /// <summary>Header layout preset: Minimal, Standard, Corporate, or Custom.</summary>
    public string HeaderLayout { get; set; } = "Standard";

    /// <summary>Optional introduction paragraph shown above the document body.</summary>
    public string? IntroText { get; set; }

    // ── Watermark ───────────────────────────────────────────────────────────

    /// <summary>Watermark placement: Diagonal, Top, or Bottom.</summary>
    public string WatermarkPosition { get; set; } = "Diagonal";

    /// <summary>Watermark opacity as a percentage (5–60). Higher = more visible.</summary>
    public int WatermarkOpacity { get; set; } = 12;

    /// <summary>Watermark colour (hex). Null = neutral grey.</summary>
    public string? WatermarkColor { get; set; }

    // ── Page numbering ──────────────────────────────────────────────────────

    /// <summary>Whether to print page numbers in the footer.</summary>
    public bool ShowPageNumbers { get; set; } = true;

    /// <summary>Page number alignment: Left, Center, or Right.</summary>
    public string PageNumberPosition { get; set; } = "Right";

    // ── Formats ─────────────────────────────────────────────────────────────

    /// <summary>.NET date format string used for all dates, e.g. "dd MMM yyyy".</summary>
    public string DateFormat { get; set; } = "dd MMM yyyy";

    /// <summary>How to display currency: "Code" (KES) or "Symbol" (KSh).</summary>
    public string CurrencyDisplay { get; set; } = "Code";

    // ── Signature block ─────────────────────────────────────────────────────

    /// <summary>Signature field labels rendered in the sign-off block.</summary>
    public List<string> SignatureFields { get; set; } = ["Authorised Signature"];

    /// <summary>Signature block alignment: Left, Center, Right, or SpaceBetween.</summary>
    public string SignaturePosition { get; set; } = "SpaceBetween";

    // ── Evidence photos (service report) ────────────────────────────────────

    /// <summary>Whether to include evidence photos in service reports.</summary>
    public bool IncludePhotos { get; set; } = true;

    /// <summary>Photo layout: Inline (within sections) or Appendix (grouped at end).</summary>
    public string PhotoLayout { get; set; } = "Inline";

    /// <summary>Maximum evidence photos rendered per page.</summary>
    public int MaxPhotosPerPage { get; set; } = 4;

    // ── Attribution ─────────────────────────────────────────────────────────

    /// <summary>Whether to show the "Powered by Ecosys" attribution (plan-gated).</summary>
    public bool ShowPoweredByEcosys { get; set; } = true;

    public static List<ReportSection> DefaultSections() =>
    [
        new("service-details",  "Service Details",       true, 0),
        new("problem",          "Problem Reported",      true, 1),
        new("findings",         "Findings",              true, 2),
        new("work-done",        "Work Done",             true, 3),
        new("timestamps",       "Timestamps",            true, 4),
        new("checklist",        "Checklist Responses",   true, 5),
        new("materials",        "Materials Used",        true, 6),
        new("photos",           "Photo Evidence",        true, 7),
        new("signatures",       "Signatures / Sign-off", true, 8),
    ];
}

public sealed class ReportSection
{
    public ReportSection() { }
    public ReportSection(string id, string label, bool enabled, int order)
    {
        Id = id; Label = label; Enabled = enabled; Order = order;
    }

    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Order { get; set; }
}

public enum LogoAlignment { Left, Center, Right }
