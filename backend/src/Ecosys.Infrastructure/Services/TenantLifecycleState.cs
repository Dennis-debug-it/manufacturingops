using Ecosys.Domain.Entities;

namespace Ecosys.Infrastructure.Services;

/// <summary>Values of <see cref="Tenant.Status"/>. Derived from the license — see <see cref="TenantLifecycleState"/>.</summary>
public static class TenantStatuses
{
    public const string Active = "Active";
    public const string Trial = "Trial";
    public const string Suspended = "Suspended";
    public const string Inactive = "Inactive";
}

/// <summary>Values of <see cref="TenantLicense.SuspensionReason"/>.</summary>
public static class LicenseSuspensionReasons
{
    /// <summary>A platform operator suspended the tenant; only an operator lifts it (Reactivate/Renew).</summary>
    public const string Manual = "Manual";

    /// <summary>
    /// SubscriptionLifecycleWorker suspended an unpaid subscription after its grace period; a payment,
    /// an extension to a future date, a renewal or an operator Reactivate lifts it.
    /// </summary>
    public const string NonPayment = "NonPayment";
}

/// <summary>
/// The one lifecycle rule. <see cref="TenantLicense"/> is the source of truth for a tenant's commercial
/// and access state; the lifecycle columns on <see cref="Tenant"/> (Status, IsActive, LicenseStatus,
/// PlanName, subscription/trial dates, seat display copies) are a projection of it that exists for
/// listing, login and legacy readers. Every writer — platform actions, the license console, the
/// subscription/trial workers and payment recording — changes the license and then calls
/// <see cref="Sync"/>, so the two can no longer disagree (audit TM-P1-01/02).
/// </summary>
public static class TenantLifecycleState
{
    public static string TenantStatusFor(string? licenseStatus)
    {
        if (string.Equals(licenseStatus, LicenseStatuses.Suspended, StringComparison.OrdinalIgnoreCase))
            return TenantStatuses.Suspended;
        if (string.Equals(licenseStatus, LicenseStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            return TenantStatuses.Inactive;
        if (LicenseStatuses.IsTrialStatus(licenseStatus)
            || string.Equals(licenseStatus, LicenseStatuses.TrialExpired, StringComparison.OrdinalIgnoreCase))
            return TenantStatuses.Trial;
        return TenantStatuses.Active;
    }

    /// <summary>True for Trial, TrialExpiringSoon and TrialExpired — a license that has never been paid for.</summary>
    public static bool IsAnyTrialStatus(string? licenseStatus) =>
        LicenseStatuses.IsTrialStatus(licenseStatus)
        || string.Equals(licenseStatus, LicenseStatuses.TrialExpired, StringComparison.OrdinalIgnoreCase);

    /// <summary>Projects the license onto the tenant row. Does not touch operator-supplied reasons for Suspended/Inactive.</summary>
    public static void Sync(Tenant tenant, TenantLicense license, DateTime utcNow)
    {
        var tenantStatus = TenantStatusFor(license.Status);
        tenant.Status = tenantStatus;
        tenant.LicenseStatus = license.Status;
        tenant.IsActive = tenantStatus is not (TenantStatuses.Suspended or TenantStatuses.Inactive);
        tenant.SubscriptionStartsAt = license.StartsAt;
        tenant.SubscriptionEndsAt = license.ExpiresAt;
        tenant.TrialEndsAt = license.TrialEndsAt ?? tenant.TrialEndsAt;
        tenant.MaxUsers = license.LicensedUsers;
        tenant.MaxBranches = license.MaxBranchesOverride;
        if (license.LicensePlan is not null)
        {
            tenant.PlanName = license.LicensePlan.DisplayName;
        }

        switch (tenantStatus)
        {
            case TenantStatuses.Suspended:
                tenant.SuspendedAt = license.SuspendedAt ?? tenant.SuspendedAt ?? utcNow;
                tenant.DeactivatedAt = null;
                tenant.DeactivatedByUserId = null;
                break;
            case TenantStatuses.Inactive:
                tenant.SuspendedAt = null;
                tenant.DeactivatedAt = license.CancelledAt ?? tenant.DeactivatedAt ?? utcNow;
                break;
            default:
                tenant.SuspendedAt = null;
                tenant.DeactivatedAt = null;
                tenant.DeactivatedByUserId = null;
                tenant.DeactivationReason = null;
                break;
        }
    }

    public static void Suspend(TenantLicense license, string reason, DateTime utcNow)
    {
        license.Status = LicenseStatuses.Suspended;
        license.SuspendedAt = utcNow;
        license.SuspensionReason = reason;
        license.CancelledAt = null;
    }

    /// <summary>
    /// Lifts a suspension back to the state the entitlement supports: Trial while the license is still a
    /// trial term (TrialEndsAt is the expiry), otherwise Active. An expired term is left to
    /// <see cref="LicenseGuardService"/> to report as read-only rather than hidden here.
    /// </summary>
    public static void LiftSuspension(TenantLicense license)
    {
        var isTrialTerm = license.TrialEndsAt.HasValue
            && (!license.ExpiresAt.HasValue || license.ExpiresAt == license.TrialEndsAt)
            && !license.IsPerpetual;
        license.Status = isTrialTerm ? LicenseStatuses.Trial : LicenseStatuses.Active;
        license.SuspendedAt = null;
        license.SuspensionReason = null;
    }

    public static bool IsSuspended(TenantLicense license) =>
        string.Equals(license.Status, LicenseStatuses.Suspended, StringComparison.OrdinalIgnoreCase);

    /// <summary>A manual suspension is an operator decision; only an operator lifts it.</summary>
    public static bool IsManualSuspension(TenantLicense license) =>
        IsSuspended(license) && string.Equals(license.SuspensionReason, LicenseSuspensionReasons.Manual, StringComparison.OrdinalIgnoreCase);
}
