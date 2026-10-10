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

    /// <summary>
    /// Why <see cref="Status"/> is Suspended: <c>Manual</c> (a platform operator decision, lifted only by
    /// an operator) or <c>NonPayment</c> (the subscription worker, lifted by payment, extension or
    /// renewal). Null whenever the license is not suspended.
    /// </summary>
    public string? SuspensionReason { get; set; }
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
    /// <summary>Licensed users given to a new license when the caller does not choose a number.</summary>
    public const int DefaultLicensedUsers = 10;

    /// <summary>
    /// The tenant's licensed-user (seat) limit — the single source of truth LicenseGuardService enforces.
    /// Always explicit and greater than zero (CK_tenant_licenses_LicensedUsers_Positive); plans carry no
    /// user limit. Active workspace users, pending invitees included, consume a seat; deactivated users
    /// and client-portal contacts do not.
    /// </summary>
    public int LicensedUsers { get; set; } = DefaultLicensedUsers;
    public int? MaxBranchesOverride { get; set; }
    /// <summary>No longer read: email intake is included with the Operations module. Kept so existing data loads.</summary>
    public bool? EmailIngestionOverride { get; set; }
    public bool? MonitoringIntegrationOverride { get; set; }

    public Tenant? Tenant { get; set; }
    public LicensePlan? LicensePlan { get; set; }
}
