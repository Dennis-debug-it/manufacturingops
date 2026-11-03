using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class Customer : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
}
