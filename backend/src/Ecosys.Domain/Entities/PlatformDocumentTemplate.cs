using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PlatformDocumentTemplate : AuditableEntity
{
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
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
