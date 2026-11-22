using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// How a platform document (quotation, invoice, receipt) looks: drawn by the document designer and used for the PDF.
/// The text parts are plain text with {{placeholders}}, kept in the *Html columns for history's sake:
/// HeaderHtml is the company details under the name, BodyHtml the opening message, TermsHtml the terms and payment
/// details, FooterHtml the page footer and SignatureHtml the sign-off. Subject is the document title.
/// </summary>
public sealed class PlatformDocumentTemplate : AuditableEntity
{
    public const string DefaultAccent = "#0f3d3e";

    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? HeaderHtml { get; set; }
    public string? BodyHtml { get; set; }
    public string? FooterHtml { get; set; }
    public string? TermsHtml { get; set; }
    public string? SignatureHtml { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public string PageSize { get; set; } = "A4";
    public string Orientation { get; set; } = "Portrait";
    public bool ShowLogo { get; set; } = true;
    public bool ShowTenantBranding { get; set; } = true;
    public bool ShowPoweredByEcosys { get; set; } = true;
    /// <summary>Hex colour for the title, table header and totals.</summary>
    public string AccentColor { get; set; } = DefaultAccent;
    /// <summary>One of <see cref="PlatformDocumentLayouts"/>.</summary>
    public string Layout { get; set; } = PlatformDocumentLayouts.Classic;
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

public static class PlatformDocumentLayouts
{
    /// <summary>Logo and company on the left, title on the right, a rule beneath.</summary>
    public const string Classic = "Classic";
    /// <summary>A solid band of the accent colour across the top.</summary>
    public const string Banner = "Banner";
    /// <summary>Plain and compact, no colour fills.</summary>
    public const string Minimal = "Minimal";

    public static readonly IReadOnlyList<string> All = [Classic, Banner, Minimal];
}

public static class PlatformDocumentTypes
{
    public const string Quotation = "Quotation";
    public const string Invoice = "Invoice";
    public const string Receipt = "Receipt";

    public static readonly IReadOnlyList<string> All = [Quotation, Invoice, Receipt];
}
