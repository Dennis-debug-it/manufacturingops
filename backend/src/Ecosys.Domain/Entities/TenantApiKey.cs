using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class TenantApiKey : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
}
