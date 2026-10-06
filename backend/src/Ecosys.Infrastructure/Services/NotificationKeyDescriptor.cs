namespace Ecosys.Infrastructure.Services;

public sealed record NotificationDescriptor(
    string Key,
    string Label,
    string Category);

public static class NotificationKeyDescriptor
{
    private static readonly IReadOnlyList<NotificationDescriptor> All =
    [
        new("work-order.assigned",  "Work order assigned to me",        "Work Orders"),
        new("work-order.started",   "Work order started (admin alert)",  "Work Orders"),
        new("work-order.on-hold",   "Work order placed on hold",         "Work Orders"),
        new("work-order.overdue",   "Work order overdue",                "Work Orders"),
        new("work-order.completed", "Work order completed",              "Work Orders"),
        new("work-order.closed",    "Work order closed (client notif.)", "Work Orders"),
        new("work-order.cancelled", "Work order cancelled",              "Work Orders"),
        new("pm.due",               "Preventive maintenance due",        "Preventive Maintenance"),
        new("pm.overdue",           "Preventive maintenance overdue",    "Preventive Maintenance"),
        new("service-report.sent",  "Service report sent to client",     "Work Orders"),
        new("invoice.sent",         "Invoice sent to client",            "Finance"),
        new("quotation.sent",       "Quotation sent to client",          "Finance"),
        new("users.invited",        "User invited / account setup",      "Account"),
        new("auth.password-reset.requested", "Password reset requested", "Account"),
        new("user.password-reset.admin",     "Admin password reset",     "Account"),
        new("tenant.onboarding",    "Workspace provisioned",             "System"),
        new("platform.lead.received", "Workspace request received",      "System"),
    ];

    public static IReadOnlyList<NotificationDescriptor> List() => All;

    public static NotificationDescriptor? Find(string key) =>
        All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
}
