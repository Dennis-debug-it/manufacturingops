using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class KnowledgeArticle : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Body { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string Status { get; set; } = "Draft";
    public string Visibility { get; set; } = "Internal";
    public Guid CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? SourceWorkOrderId { get; set; }
    public int ViewCount { get; set; }
    public int HelpfulCount { get; set; }
    public int NotHelpfulCount { get; set; }

    public Tenant? Tenant { get; set; }
    public KnowledgeCategory? Category { get; set; }
    public User? CreatedByUser { get; set; }
    public User? UpdatedByUser { get; set; }
    public WorkOrder? SourceWorkOrder { get; set; }
    public ICollection<KnowledgeArticleVersion> Versions { get; set; } = new List<KnowledgeArticleVersion>();
    public ICollection<KnowledgeArticleTag> ArticleTags { get; set; } = new List<KnowledgeArticleTag>();
    public ICollection<KnowledgeArticleFeedback> Feedback { get; set; } = new List<KnowledgeArticleFeedback>();
}
