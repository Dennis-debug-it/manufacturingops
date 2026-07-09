using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class LeaveType : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPaid { get; set; } = true;
    public decimal AnnualAllowanceDays { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
