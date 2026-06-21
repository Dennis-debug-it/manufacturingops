using Ecosys.Shared.Contracts.Integration;

namespace Ecosys.Infrastructure.Services;

public interface IEmailNotificationRegistry
{
    IReadOnlyCollection<EmailNotificationDefinition> List();
}

public sealed record EmailNotificationDefinition(
    string EventKey,
    string DisplayName,
    string TemplateKey,
    string RecipientStrategy,
    string SenderScope,
    string DispatchStatus,
    string Description,
    IReadOnlyCollection<string> SupportedChannels,
    string Notes);

internal sealed class EmailNotificationRegistry : IEmailNotificationRegistry
{
    private static readonly IReadOnlyCollection<EmailNotificationDefinition> Definitions =
    [
        new("tenant.onboarding", "Tenant/workspace provisioned", "tenant-onboarding", "Tenant primary contact or admin", "Platform SMTP", "Active", "Welcomes a new tenant workspace when provisioning is complete.", ["Email"], "Currently available through platform provisioning flows."),
        new("platform.lead.received", "Workspace request received", "workspace-request-received", "Platform support or admin email", "Platform SMTP", "Active", "Alerts platform staff when a workspace request is submitted.", ["Email"], "Lead notification delivery is active."),
        new("users.invited", "User invited / account setup", "account-setup-link", "Newly created user", "Platform or tenant SMTP", "Active", "Sends a first-time account setup link when an administrator creates or re-invites a user.", ["Email"], "Uses the secure password setup flow without emailing plaintext passwords."),
        new("auth.password-reset.requested", "Self-service password reset requested", "password-reset-link", "Requesting user", "Platform or tenant SMTP", "Active", "Sends a self-service reset link when a user requests password recovery.", ["Email"], "Currently used by the forgot-password flow."),
        new("user.password-reset.admin", "Admin password reset", "password-reset", "Selected user", "Platform or tenant SMTP", "Active", "Sends an administrator-triggered password reset notification.", ["Email"], "Currently used by admin reset actions."),
        new("work-order.assigned", "Work order assigned", "work-order-assigned", "Assigned technician", "Tenant SMTP", "Active", "Sends an assignment notification to each newly assigned technician.", ["Email", "In-App"], "Wired to assignment workflow."),
        new("work-order.completed", "Work order completed", "work-order-completed", "Relevant stakeholders", "Tenant SMTP", "Active", "Sends a completion notification when a work order is closed.", ["Email", "In-App"], "Wired to the work order lifecycle flow."),
        new("work-order.on-hold", "Work order placed on hold", "work-order-on-hold", "Tenant admin users", "Tenant SMTP", "Active", "Notifies admins when a work order is placed on hold, including the hold reason.", ["Email"], "Wired to WorkOrderLifecycleService.ChangeStatusAsync."),
        new("work-order.overdue", "Work order overdue", "work-order-overdue", "Supervisor or admin", "Tenant SMTP", "Active", "Escalation email for WOs past their due date, sent during daily scheduler cycle.", ["Email", "In-App"], "Wired to PmSchedulerWorker daily cycle."),
        new("pm.due", "Preventive maintenance due", "pm-due", "Assigned group or supervisor", "Tenant SMTP", "PendingHook", "Reminder for preventive maintenance due dates.", ["Email", "In-App"], "Template available, dispatch hook pending."),
        new("pm.overdue", "Preventive maintenance overdue", "pm-overdue", "Assigned group or supervisor", "Tenant SMTP", "PendingHook", "Escalation email for overdue preventive maintenance activity.", ["Email", "In-App"], "Template can be activated when PM enforcement is completed."),
        new("work-order.started", "Work order started", "work-order-started", "Tenant admin users", "Tenant SMTP", "Active", "Notifies admin users when a technician starts work on a work order.", ["Email"], "Wired to WorkOrderLifecycleService.StartAsync."),
        new("work-order.closed", "Work order closed", "work-order-closed", "Client contact", "Tenant SMTP", "Active", "Notifies the client contact when a work order is closed.", ["Email"], "Wired to WorkOrderLifecycleService.ChangeStatusAsync."),
        new("work-order.cancelled", "Work order cancelled", "work-order-cancelled", "Assigned technicians", "Tenant SMTP", "Active", "Notifies assigned technicians when a work order is cancelled.", ["Email"], "Wired to WorkOrderLifecycleService.ChangeStatusAsync."),
        new("invoice.sent", "Invoice sent to client", "invoice-sent", "Client contact", "Tenant SMTP", "Active", "Notifies the client contact when an invoice is sent.", ["Email"], "Wired to TenantInvoiceController.SendInvoice."),
        new("quotation.sent", "Quotation sent to client", "quotation-sent", "Client contact", "Tenant SMTP", "Active", "Notifies the client contact when a quotation is sent.", ["Email"], "Wired to TenantQuotationController.SendQuotation."),
    ];

    public IReadOnlyCollection<EmailNotificationDefinition> List() => Definitions;
}
