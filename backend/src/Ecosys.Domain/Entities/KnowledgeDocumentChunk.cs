using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class KnowledgeDocumentChunk : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid? ArticleId { get; set; }
    public int? PageNumber { get; set; }
    public string? SectionTitle { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkOrder { get; set; }

    public Tenant? Tenant { get; set; }
    public KnowledgeDocument? Document { get; set; }
    public KnowledgeArticle? Article { get; set; }
}
