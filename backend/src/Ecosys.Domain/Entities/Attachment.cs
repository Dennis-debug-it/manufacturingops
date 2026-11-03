using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class Attachment : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public Guid UploadedByUserId { get; set; }
    /// <summary>Shown to the client in the portal. Only project documents use this; default is internal.</summary>
    public bool SharedWithClient { get; set; }

    public Tenant? Tenant { get; set; }
    public User? UploadedByUser { get; set; }
}
