using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class KnowledgeDocument : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public string? TagsJson { get; set; }
    public Guid? LinkedClientId { get; set; }
    public Guid? LinkedSiteId { get; set; }
    public Guid? LinkedSystemId { get; set; }
    public Guid? LinkedAssetId { get; set; }
    public Guid? LinkedWorkOrderId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "draft";
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string? ExtractedText { get; set; }

    public Tenant? Tenant { get; set; }
    public KnowledgeCategory? Category { get; set; }
    public Client? LinkedClient { get; set; }
    public Site? LinkedSite { get; set; }
    public OperationalSystem? LinkedSystem { get; set; }
    public Asset? LinkedAsset { get; set; }
    public WorkOrder? LinkedWorkOrder { get; set; }
    public User? UploadedByUser { get; set; }
    public ICollection<KnowledgeDocumentChunk> Chunks { get; set; } = new List<KnowledgeDocumentChunk>();
}
