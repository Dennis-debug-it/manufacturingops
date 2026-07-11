using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class TenantWebhook : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string EventsJson { get; set; } = "[]";
    public string SecretHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
}
