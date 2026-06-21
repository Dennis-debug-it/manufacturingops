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

    public Tenant? Tenant { get; set; }
    public User? UploadedByUser { get; set; }
}
