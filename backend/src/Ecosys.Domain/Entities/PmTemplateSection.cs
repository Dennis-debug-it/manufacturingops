using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PmTemplateSection : AuditableEntity
{
    public Guid PmTemplateId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public PmTemplate? PmTemplate { get; set; }
    public ICollection<PmTemplateQuestion> Questions { get; set; } = new List<PmTemplateQuestion>();
}
