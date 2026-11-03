using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class KnowledgeArticleFeedback : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ArticleId { get; set; }
    public Guid? ClientPortalUserId { get; set; }
    public bool IsHelpful { get; set; }
    public string? Comment { get; set; }

    public Tenant? Tenant { get; set; }
    public KnowledgeArticle? Article { get; set; }
    public ClientPortalUser? ClientPortalUser { get; set; }
}
