namespace Ecosys.Infrastructure.Services;

/// <summary>The ways a notification can reach a user. Stored per key as flags.</summary>
[Flags]
public enum NotificationChannels
{
    None = 0,
    /// <summary>The bell (a <c>Notification</c> row).</summary>
    InApp = 1,
    Push = 2,
    Email = 4,
    WhatsApp = 8,
}

/// <param name="Channels">
/// The channels this key can use at all. Keys sent through <see cref="INotificationDispatcher"/> use
/// the bell, push and email; the rest are emails sent by their own modules and offer only email.
/// </param>
/// <param name="DefaultChannels">What a user gets when they have no saved preference for the key.</param>
public sealed record NotificationDescriptor(
    string Key,
    string Label,
    string Category,
    NotificationChannels Channels,
    NotificationChannels DefaultChannels);

public static class NotificationKeyDescriptor
{
    /// <summary>The bell, push and email: what a dispatcher key offers and, unless noisy, turns on.</summary>
    public const NotificationChannels Staff = NotificationChannels.InApp | NotificationChannels.Push | NotificationChannels.Email;

    public const NotificationChannels AnyChannel = Staff | NotificationChannels.WhatsApp;

    private static readonly IReadOnlyList<NotificationDescriptor> All =
    [
        Inbox("work-order.assigned",  "Work order assigned to me",        "Work Orders", whatsApp: true),
        EmailOnly("work-order.started",   "Work order started (admin alert)",  "Work Orders"),
        EmailOnly("work-order.on-hold",   "Work order placed on hold",         "Work Orders"),
        EmailOnly("email-intake.email-intake-received", "Email request received (auto-reply)", "Email Intake"),
        EmailOnly("email-intake.email-intake-reply-added", "Email added to existing job (auto-reply)", "Email Intake"),
        EmailOnly("email-intake.email-intake-unrecognised", "Unrecognised email sender (auto-reply)", "Email Intake"),
        EmailOnly("work-order.overdue",   "Work order overdue",                "Work Orders"),
        EmailOnly("work-order.completed", "Work order completed",              "Work Orders"),
        EmailOnly("work-order.closed",    "Work order closed (client notif.)", "Work Orders"),
        EmailOnly("work-order.cancelled", "Work order cancelled",              "Work Orders"),
        EmailOnly("pm.due",               "Preventive maintenance due",        "Preventive Maintenance"),
        EmailOnly("pm.overdue",           "Preventive maintenance overdue",    "Preventive Maintenance"),
        Inbox("pm.contract-lapsed", "PM schedule on hold: contract lapsed", "Preventive Maintenance"),
        EmailOnly("service-report.sent",  "Service report sent to client",     "Work Orders"),
        Inbox("project.member-added",        "Added to a project team",        "Projects"),
        Inbox("project.task-assigned",       "Project task assigned to me",    "Projects"),
        Inbox("project.variation-submitted", "Project variation to review",    "Projects"),
        Inbox("project.variation-decided",   "Project variation decided",      "Projects"),
        Inbox("project.task-due", "Project task due soon", "Projects", whatsApp: true),
        Inbox("project.milestone-due", "Project milestone coming up", "Projects"),
        Inbox("project.milestone-overdue", "Project milestone overdue", "Projects"),
        Inbox("project.end-date-approaching", "Project end date approaching", "Projects"),
        Inbox("project.muster-missing", "Muster roll not marked", "Projects", whatsApp: true),
        Inbox("project.valuation-certified", "Project valuation certified", "Projects", whatsApp: true),
        Inbox("project.retention-due", "Project retention due for release", "Projects", whatsApp: true),
        Inbox("activity.scheduled", "Meeting or visit scheduled with me", "Calendar & Activities"),
        Inbox("activity.updated", "Meeting or visit moved or changed", "Calendar & Activities"),
        Inbox("activity.cancelled", "Meeting or visit cancelled", "Calendar & Activities"),
        Inbox("activity.reminder", "Meeting or visit starting soon", "Calendar & Activities", NotificationChannels.InApp | NotificationChannels.Push, whatsApp: true),
        Inbox("activity.minutes-published", "Meeting minutes published", "Calendar & Activities"),
        Inbox("action-item.assigned", "Action item assigned to me", "Calendar & Activities"),
        Inbox("action-item.due", "Action item due tomorrow", "Calendar & Activities", whatsApp: true),
        Inbox("inventory.request-raised", "Parts requested from stores", "Inventory"),
        Inbox("inventory.request-decided", "My parts request approved or rejected", "Inventory"),
        Inbox("inventory.request-dispatched", "My parts are on the way", "Inventory"),
        Inbox("inventory.low-stock", "A part fell to its reorder level", "Inventory"),
        Inbox("factory-job.reported", "An operator reported a fault", "Work Orders"),
        Inbox("asset.warranty-expiring", "An asset's warranty is about to expire", "Assets"),
        Inbox("asset.meter-out-of-range", "A meter reading is outside its limits", "Assets"),
        EmailOnly("invoice.sent",         "Invoice sent to client",            "Finance"),
        EmailOnly("quotation.sent",       "Quotation sent to client",          "Finance"),
        EmailOnly(SalesReminderKeys.FollowUpDue,     "Follow-up due on my deal",          "Sales CRM"),
        EmailOnly(SalesReminderKeys.WeeklySummary,    "My weekly sales summary",            "Sales CRM"),
        EmailOnly(SalesReminderKeys.DealStalled,      "My deal has gone quiet",             "Sales CRM"),
        EmailOnly(SalesReminderKeys.QuoteAwaitingAnswer, "Quote waiting for the client's answer", "Sales CRM"),
        EmailOnly(SalesReminderKeys.TenderDeadline,  "RFP/RFQ submission deadline near",  "Sales CRM"),
        EmailOnly("users.invited",        "User invited / account setup",      "Account"),
        EmailOnly("auth.password-reset.requested", "Password reset requested", "Account"),
        EmailOnly("user.password-reset.admin",     "Admin password reset",     "Account"),
        EmailOnly("tenant.onboarding",    "Workspace provisioned",             "System"),
        EmailOnly("platform.lead.received", "Workspace request received",      "System"),
        EmailOnly("auth.signup.verify-email", "Self sign-up email confirmation", "Account"),
    ];

    public static IReadOnlyList<NotificationDescriptor> List() => All;

    public static NotificationDescriptor? Find(string key) =>
        All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A key sent through the dispatcher. Pass <paramref name="defaults"/> for a noisy key that should
    /// start in the bell only (e.g. <c>NotificationChannels.InApp</c>); users can still turn the rest on.
    /// </summary>
    /// <param name="whatsApp">
    /// The key can also go by WhatsApp (Release 3 design 4.2): it has a template in
    /// <see cref="WhatsAppTemplateCatalogue"/>. Never on by default; a user opts in.
    /// </param>
    private static NotificationDescriptor Inbox(string key, string label, string category, NotificationChannels defaults = Staff, bool whatsApp = false) =>
        new(key, label, category, whatsApp ? Staff | NotificationChannels.WhatsApp : Staff, defaults & ~NotificationChannels.WhatsApp);

    private static NotificationDescriptor EmailOnly(string key, string label, string category) =>
        new(key, label, category, NotificationChannels.Email, NotificationChannels.Email);
}
