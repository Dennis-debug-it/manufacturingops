using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrderEvent : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? Reason { get; set; }
    public string? Note { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public User? ActorUser { get; set; }
}
