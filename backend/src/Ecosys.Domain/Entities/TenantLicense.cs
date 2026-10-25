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
    /// <summary>Seats given to a new license when the caller does not choose a number.</summary>
    public const int DefaultOfficeSeats = 10;
    public const int DefaultFieldSeats = 10;

    /// <summary>
    /// Office seats: every active workspace user (pending invitees included) whose role is not
    /// Technician, Viewer or the legacy User role. Always greater than zero
    /// (CK_tenant_licenses_OfficeSeats_Positive) and at least the edition's minimum. LicenseGuardService
    /// is the single place that enforces it; see <c>SeatTypes</c> for the role mapping.
    /// </summary>
    public int OfficeSeats { get; set; } = DefaultOfficeSeats;

    /// <summary>Field seats: active technicians. Zero or more (CK_tenant_licenses_FieldSeats_NonNegative).</summary>
    public int FieldSeats { get; set; } = DefaultFieldSeats;
    public int? MaxBranchesOverride { get; set; }
    /// <summary>No longer read: email intake is included with the Operations module. Kept so existing data loads.</summary>
    public bool? EmailIngestionOverride { get; set; }
    public bool? MonitoringIntegrationOverride { get; set; }

    /// <summary>
    /// The optional WhatsApp add-on: null (none), <c>whatsapp_notifications</c> (reminders from the shared
    /// Ecosys number) or <c>whatsapp_business</c> (also the tenant's own number and two-way inbox). Off by
    /// default; Ecosys turns it on per tenant because Ecosys pays Meta per message.
    /// </summary>
    public string? WhatsAppAddOn { get; set; }

    /// <summary>Whether any WhatsApp add-on is on: reminders may be sent.</summary>
    public bool WhatsAppEnabled => WhatsAppAddOn is not null;

    public Tenant? Tenant { get; set; }
    public LicensePlan? LicensePlan { get; set; }
}
