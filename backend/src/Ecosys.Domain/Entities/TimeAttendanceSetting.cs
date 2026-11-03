using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>Per-tenant time &amp; attendance policy (single row per tenant).</summary>
public sealed class TimeAttendanceSetting : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public int RoundingMinutes { get; set; } = 15;
    public decimal OvertimeThresholdHoursPerDay { get; set; } = 8;
    public int GraceMinutes { get; set; } = 10;
    public decimal AutoClockOutAfterHours { get; set; } = 12;

    public Tenant? Tenant { get; set; }
}
