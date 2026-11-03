using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PmReport : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid? PmTemplateId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string AnswersJson { get; set; } = "[]";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public PmTemplate? PmTemplate { get; set; }
}
