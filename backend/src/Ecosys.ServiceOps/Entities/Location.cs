using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class Location : TenantEntity
{
    public Guid CustomerId { get; set; }
    public Guid? ParentLocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }

    public Customer? Customer { get; set; }
    public Location? ParentLocation { get; set; }
    public ICollection<Location> Children { get; set; } = new List<Location>();
}
