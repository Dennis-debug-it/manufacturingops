using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrderSignature : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public string SignatureType { get; set; } = "Technician";
    public string SignerName { get; set; } = string.Empty;
    public string? SignerRole { get; set; }
    public string SignatureDataUrl { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public Guid CapturedByUserId { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public User? CapturedByUser { get; set; }
}
