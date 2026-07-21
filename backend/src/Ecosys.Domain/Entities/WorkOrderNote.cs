using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrderNote : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsClientVisible { get; set; }
    public Guid CreatedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public User? CreatedByUser { get; set; }
}
