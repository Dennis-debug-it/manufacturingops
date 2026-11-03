using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class AuditLog : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string Severity { get; set; } = "Info";

    public Tenant? Tenant { get; set; }
    public User? User { get; set; }
}
