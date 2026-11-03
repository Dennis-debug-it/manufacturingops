using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class WorkOrderType : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
