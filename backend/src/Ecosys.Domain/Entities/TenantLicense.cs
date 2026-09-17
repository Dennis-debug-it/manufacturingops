using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class TenantLicense : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid LicensePlanId { get; set; }
    public string Status { get; set; } = "Trial";
    public DateTime StartsAt { get; set; } = DateTime.UtcNow;
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string BillingCycle { get; set; } = "Monthly";
    public DateTime? NextBillingDate { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Notes { get; set; }
    public int GracePeriodDays { get; set; } = 7;

    /// <summary>
    /// Explicit opt-in to a never-expiring license (an internal/demo tenant, typically). Normal
    /// activation requires <see cref="ExpiresAt"/>; this must be true for <see cref="ExpiresAt"/> to
    /// be null, so a missing expiry is never accidentally perpetual. Existing tenants whose
    /// <see cref="ExpiresAt"/> was already null before this field existed were backfilled to
    /// <c>true</c> (see the AddTenantLicenseIsPerpetual migration) to preserve their behavior exactly.
    /// </summary>
    public bool IsPerpetual { get; set; }

    /// <summary>
    /// When the grace-period overdue warning email was last sent. Cleared whenever the subscription
    /// is extended so the next unpaid cycle gets its own warning instead of staying permanently
    /// suppressed by a stale timestamp from a prior overdue period.
    /// </summary>
    public DateTime? OverdueWarningSentAt { get; set; }
    public int? MaxUsersOverride { get; set; }
    public int? MaxBranchesOverride { get; set; }
    public int? MaxAssetsOverride { get; set; }
    public int? MonthlyWorkOrdersOverride { get; set; }
    public bool? EmailIngestionOverride { get; set; }
    public bool? MonitoringIntegrationOverride { get; set; }
    public bool? AdvancedReportsOverride { get; set; }
    public bool? ClientPortalOverride { get; set; }

    public Tenant? Tenant { get; set; }
    public LicensePlan? LicensePlan { get; set; }
}
