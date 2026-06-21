using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class Asset : TenantEntity
{
    public Guid CustomerId { get; set; }
    public Guid LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public bool IsActive { get; set; } = true;

    public Customer? Customer { get; set; }
    public Location? Location { get; set; }
}
