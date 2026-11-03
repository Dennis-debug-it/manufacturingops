using Ecosys.Shared.Common;

namespace Ecosys.Platform.Entities;

public sealed class AppUser : TenantEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
