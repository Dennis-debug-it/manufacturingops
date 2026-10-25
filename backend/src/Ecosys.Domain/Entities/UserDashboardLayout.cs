using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A user's own arrangement of their dashboard (R8). <see cref="LayoutJson"/> holds
/// <c>{ "items": [{ "key", "size" }], "seenDefaults": [key] }</c>: the ordered widgets and the role-default
/// widgets the user has already been offered, so a widget newly added to their role's default appears
/// once at the end. No row means the role default. "Reset to default" deletes the row.
/// </summary>
public sealed class UserDashboardLayout : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string LayoutJson { get; set; } = "{}";

    public Tenant? Tenant { get; set; }
    public User? User { get; set; }
}
