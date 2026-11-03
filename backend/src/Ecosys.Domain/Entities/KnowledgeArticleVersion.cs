using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class KnowledgeArticleVersion : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ArticleId { get; set; }
    public int VersionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Guid? UpdatedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public KnowledgeArticle? Article { get; set; }
    public User? UpdatedByUser { get; set; }
}
