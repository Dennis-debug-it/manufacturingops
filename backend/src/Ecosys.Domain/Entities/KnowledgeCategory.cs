using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class KnowledgeCategory : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<KnowledgeArticle> Articles { get; set; } = new List<KnowledgeArticle>();
}
