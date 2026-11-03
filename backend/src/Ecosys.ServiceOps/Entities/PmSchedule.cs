using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class PmSchedule : TenantEntity
{
    public Guid CustomerId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid WorkOrderTypeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int IntervalDays { get; set; }
    public DateTime NextRunUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
