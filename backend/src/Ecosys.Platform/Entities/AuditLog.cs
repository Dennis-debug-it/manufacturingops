using Ecosys.Shared.Common;

namespace Ecosys.Platform.Entities;

public sealed class AuditLog : TenantEntity
{
    public Guid? UserId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
}
