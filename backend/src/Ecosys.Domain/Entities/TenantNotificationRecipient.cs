using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class TenantNotificationRecipient : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string RecipientGroup { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
