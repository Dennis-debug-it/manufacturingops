namespace Ecosys.Shared.Contracts.Integration;

/// <summary>
/// Per-tenant report template configuration stored as JSON on the Tenant record.
/// All fields are optional — defaults are applied by the renderer when null.
/// </summary>
public sealed class ReportTemplateConfig
{
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
