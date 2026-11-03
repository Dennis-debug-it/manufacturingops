using Ecosys.Shared.Common;

namespace Ecosys.ServiceOps.Entities;

public sealed class SlaPolicy : TenantEntity
{
    public Guid? CustomerContractId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ResponseTargetMinutes { get; set; }
    public int ResolutionTargetMinutes { get; set; }
}
