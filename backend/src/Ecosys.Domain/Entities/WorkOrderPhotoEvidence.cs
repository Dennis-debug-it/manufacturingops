using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class WorkOrderPhotoEvidence : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid AttachmentId { get; set; }
    public string Caption { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public bool IncludeInReport { get; set; } = true;
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public Attachment? Attachment { get; set; }
    public User? UploadedByUser { get; set; }
}
