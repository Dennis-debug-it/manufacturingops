using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class AssetDocumentTypes
{
    public const string Manual = "Manual";
    public const string Drawing = "Drawing";
    public const string Datasheet = "Datasheet";
    public const string Certificate = "Certificate";
    public const string Procedure = "Procedure";
    public const string Photo = "Photo";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = [Manual, Drawing, Datasheet, Certificate, Procedure, Photo, Other];

    public static string? Find(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A controlled document on an asset (manual, drawing, certificate…). Each upload is a new
/// version; older versions stay downloadable so a technician can see what changed.
/// </summary>
public sealed class AssetDocument : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AssetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DocumentType { get; set; } = AssetDocumentTypes.Manual;
    public string? Description { get; set; }
    public int CurrentVersion { get; set; }
    /// <summary>For certificates and similar: when the document stops being valid.</summary>
    public DateTime? ExpiryDate { get; set; }
    public bool IsArchived { get; set; }

    public Tenant? Tenant { get; set; }
    public Asset? Asset { get; set; }
    public ICollection<AssetDocumentVersion> Versions { get; set; } = new List<AssetDocumentVersion>();
}

public sealed class AssetDocumentVersion : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public int VersionNumber { get; set; }
    public Guid AttachmentId { get; set; }
    public string? Notes { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }

    public Tenant? Tenant { get; set; }
    public AssetDocument? Document { get; set; }
    public Attachment? Attachment { get; set; }
}
