using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class CustomerContract : TenantEntity
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
