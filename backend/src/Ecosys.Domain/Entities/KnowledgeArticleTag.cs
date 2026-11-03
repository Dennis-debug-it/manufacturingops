using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class KnowledgeArticleTag : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ArticleId { get; set; }
    public Guid TagId { get; set; }

    public Tenant? Tenant { get; set; }
    public KnowledgeArticle? Article { get; set; }
    public KnowledgeTag? Tag { get; set; }
}
