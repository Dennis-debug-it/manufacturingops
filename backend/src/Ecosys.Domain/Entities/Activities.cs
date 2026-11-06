using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class ActivityTypes
{
    public const string Meeting = "Meeting";
    public const string SiteVisit = "SiteVisit";
    public const string Inspection = "Inspection";
    public const string Call = "Call";

    public static readonly string[] All = [Meeting, SiteVisit, Inspection, Call];
}

public static class ActivityStatuses
{
    public const string Scheduled = "Scheduled";
    public const string Held = "Held";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Scheduled, Held, Cancelled];
}

public static class ActivityAttendeeResponses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";

    public static readonly string[] All = [Pending, Accepted, Declined];
}

public static class ActionItemStatuses
{
    public const string Open = "Open";
    public const string Done = "Done";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Open, Done, Cancelled];
}

/// <summary>The records an activity or action item can be linked to.</summary>
public static class ActivityEntityTypes
{

    public const string Client = "Client";
    public const string Contract = "Contract";
    public const string WorkOrder = "WorkOrder";

    public static readonly string[] All = [Client, Contract, WorkOrder];
}

/// <summary>
/// A meeting, site visit, inspection or call (Release 3 design 3.2), optionally linked to a project,
/// client, contract or work order. Times are UTC; an all-day activity stores its dates as UTC midnight.
/// Minutes are written once it is Held and locked when published; corrections are addenda.
/// </summary>
public sealed class Activity : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Type { get; set; } = ActivityTypes.Meeting;
    public string Title { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool AllDay { get; set; }
    /// <summary>A place or a meeting link.</summary>
    public string? Location { get; set; }
    public Guid OrganizerUserId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string Status { get; set; } = ActivityStatuses.Scheduled;
    public string? Agenda { get; set; }
    /// <summary>Plain text with line breaks.</summary>
    public string? Minutes { get; set; }
    public DateTime? MinutesPublishedAt { get; set; }
    public Guid? MinutesPublishedByUserId { get; set; }
    public int? ReminderMinutesBefore { get; set; } = 30;
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public Tenant? Tenant { get; set; }
    public User? OrganizerUser { get; set; }
    public ICollection<ActivityAttendee> Attendees { get; set; } = new List<ActivityAttendee>();
    public ICollection<ActivityMinutesAddendum> Addenda { get; set; } = new List<ActivityMinutesAddendum>();
}

/// <summary>
/// Someone invited to an activity. Exactly one of <see cref="UserId"/> (staff), <see cref="ConsultantId"/>,
/// <see cref="ClientContactId"/> (a client portal contact) or <see cref="ExternalEmail"/>/<see cref="ExternalName"/>
/// is set. Only staff are notified (R4); the rest are recorded on the meeting.
/// </summary>
public sealed class ActivityAttendee : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ClientContactId { get; set; }
    public string? ExternalName { get; set; }
    public string? ExternalEmail { get; set; }
    /// <summary>Staff only.</summary>
    public string Response { get; set; } = ActivityAttendeeResponses.Pending;
    /// <summary>Ticked when the minutes are written.</summary>
    public bool Attended { get; set; }

    public Activity? Activity { get; set; }
    public User? User { get; set; }
    public ClientPortalUser? ClientContact { get; set; }
}

/// <summary>A correction to published minutes. Published minutes are never edited.</summary>
public sealed class ActivityMinutesAddendum : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActivityId { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid AuthorUserId { get; set; }

    public Activity? Activity { get; set; }
    public User? AuthorUser { get; set; }
}

/// <summary>
/// A follow-up with an owner and a due date, from a meeting or on its own (R3). Shown on the linked
/// record (a project's Tasks tab lists these beside its tasks) and in "My action items".
/// </summary>
public sealed class ActionItem : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>The meeting it came from, if any.</summary>
    public Guid? ActivityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? AssigneeUserId { get; set; }
    /// <summary>Someone without a login, e.g. "Consultant to confirm duct sizes".</summary>
    public string? AssigneeExternalName { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = ActionItemStatuses.Open;
    /// <summary>Copied from the activity, or set directly.</summary>
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public Activity? Activity { get; set; }
    public User? AssigneeUser { get; set; }
}

/// <summary>
/// Sent-marker for activity and action-item reminders, keyed by what the reminder was for (the
/// activity's start or the item's due date), so moving a meeting earns a fresh reminder and a
/// reminder is never sent twice for the same time.
/// </summary>
public sealed class ActivityReminderLog : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid EntityId { get; set; }
    public string NotificationKey { get; set; } = string.Empty;
    public DateTime ForTime { get; set; }
}

/// <summary>
/// A user's private calendar feed link (design 3.5). Only the SHA-256 of the token is stored; the
/// token itself is shown once. Revoked links stay as rows so their use can be audited.
/// </summary>
public sealed class CalendarFeedToken : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Lower-case hex SHA-256 of the token.</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public User? User { get; set; }
}
