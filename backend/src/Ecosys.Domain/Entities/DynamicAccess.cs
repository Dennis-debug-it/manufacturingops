using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PermissionGroup : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<GroupCapability> Capabilities { get; set; } = new List<GroupCapability>();
    public ICollection<UserPermissionGroup> Users { get; set; } = new List<UserPermissionGroup>();
}

public sealed class UserPermissionGroup : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid PermissionGroupId { get; set; }
    public User? User { get; set; }
    public PermissionGroup? PermissionGroup { get; set; }
}

public sealed class GroupCapability : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PermissionGroupId { get; set; }
    public string Capability { get; set; } = string.Empty;
    public PermissionGroup? PermissionGroup { get; set; }
}

public sealed class UserPermissionOverride : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Capability { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
    public User? User { get; set; }
}

public sealed class AccessScope : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? PermissionGroupId { get; set; }
    public string Capability { get; set; } = "*";
    public string ScopeType { get; set; } = "Organization";
    public Guid? ScopeEntityId { get; set; }
    public User? User { get; set; }
    public PermissionGroup? PermissionGroup { get; set; }
}

public sealed class PermissionThreshold : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? PermissionGroupId { get; set; }
    public string Capability { get; set; } = string.Empty;
    public decimal MaximumAmount { get; set; }
    public string Currency { get; set; } = "KES";
    public User? User { get; set; }
    public PermissionGroup? PermissionGroup { get; set; }
}
