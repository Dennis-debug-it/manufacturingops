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
        new("auth.signup.verify-email", "Self sign-up email confirmation", "signup-verify-email", "Person signing up", "Platform SMTP", "Active", "Sends the confirmation link that creates a self-service trial workspace.", ["Email"], "Used by the Start free trial page."),
        new("platform.lead.received", "Workspace request received", "workspace-request-received", "Platform support or admin email", "Platform SMTP", "Active", "Alerts platform staff when a workspace request is submitted.", ["Email"], "Lead notification delivery is active."),
        new("users.invited", "User invited / account setup", "account-setup-link", "Newly created user", "Platform or tenant SMTP", "Active", "Sends a first-time account setup link when an administrator creates or re-invites a user.", ["Email"], "Uses the secure password setup flow without emailing plaintext passwords."),
        new("auth.password-reset.requested", "Self-service password reset requested", "password-reset-link", "Requesting user", "Platform or tenant SMTP", "Active", "Sends a self-service reset link when a user requests password recovery.", ["Email"], "Currently used by the forgot-password flow."),
        new("user.password-reset.admin", "Admin password reset", "password-reset", "Selected user", "Platform or tenant SMTP", "Active", "Sends an administrator-triggered password reset notification.", ["Email"], "Currently used by admin reset actions."),
        new("email-intake.email-intake-received", "Email request received (auto-reply)", "email-intake-received", "Person who emailed the intake mailbox", "Tenant SMTP", "Active", "Confirms an emailed request and gives its work order number.", ["Email"], "Sent by email intake when auto-replies are on."),
        new("email-intake.email-intake-reply-added", "Email added to existing job (auto-reply)", "email-intake-reply-added", "Person who emailed the intake mailbox", "Tenant SMTP", "Active", "Confirms an emailed reply was added to its work order.", ["Email"], "Sent by email intake when auto-replies are on."),
        new("email-intake.email-intake-unrecognised", "Unrecognised email sender (auto-reply)", "email-intake-unrecognised", "Person who emailed the intake mailbox", "Tenant SMTP", "Active", "Tells a sender who isn't a known client that their email wasn't logged.", ["Email"], "Sent by email intake when auto-replies are on."),
        new("project.member-added", "Added to a project team", "project.member-added", "The added team member", "Tenant SMTP", "Active", "Tells a user they have joined a project team, and in which role.", ["Email", "In-App"], "Wired to the Projects team screen."),
        new("project.task-assigned", "Project task assigned", "project.task-assigned", "Task assignee", "Tenant SMTP", "Active", "Tells a user a project task has been assigned to them.", ["Email", "In-App"], "Wired to project tasks."),
        new("project.variation-submitted", "Project variation submitted", "project.variation-submitted", "Admins, managers and the project manager", "Tenant SMTP", "Active", "Asks approvers to decide on a submitted variation.", ["Email", "In-App"], "Wired to variation submission."),
        new("project.variation-decided", "Project variation decided", "project.variation-decided", "Submitter and project manager", "Tenant SMTP", "Active", "Reports an approved or rejected variation.", ["Email", "In-App"], "Wired to variation decisions."),
        new("work-order.assigned", "Work order assigned", "work-order-assigned", "Assigned technician", "Tenant SMTP", "Active", "Sends an assignment notification to each newly assigned technician.", ["Email", "In-App"], "Wired to assignment workflow."),
        new("work-order.completed", "Work order completed", "work-order-completed", "Relevant stakeholders", "Tenant SMTP", "Active", "Sends a completion notification when a work order is closed.", ["Email", "In-App"], "Wired to the work order lifecycle flow."),
        new("csat.survey", "Customer satisfaction survey", "csat-survey", "Client contact", "Tenant SMTP", "Active", "Sends a one-click satisfaction survey link when a work order is completed.", ["Email"], "Wired to the work order completion flow."),
        new("work-order.on-hold", "Work order placed on hold", "work-order-on-hold", "Tenant admin users", "Tenant SMTP", "Active", "Notifies admins when a work order is placed on hold, including the hold reason.", ["Email"], "Wired to WorkOrderLifecycleService.ChangeStatusAsync."),
        new("work-order.overdue", "Work order overdue", "work-order-overdue", "Supervisor or admin", "Tenant SMTP", "Active", "Escalation email for WOs past their due date, sent during daily scheduler cycle.", ["Email", "In-App"], "Wired to PmSchedulerWorker daily cycle."),
        new("pm.due", "Preventive maintenance due", "pm-due", "Assigned group or supervisor", "Tenant SMTP", "PendingHook", "Reminder for preventive maintenance due dates.", ["Email", "In-App"], "Template available, dispatch hook pending."),
        new("pm.overdue", "Preventive maintenance overdue", "pm-overdue", "Assigned group or supervisor", "Tenant SMTP", "PendingHook", "Escalation email for overdue preventive maintenance activity.", ["Email", "In-App"], "Template can be activated when PM enforcement is completed."),
        new("work-order.started", "Work order started", "work-order-started", "Tenant admin users", "Tenant SMTP", "Active", "Notifies admin users when a technician starts work on a work order.", ["Email"], "Wired to WorkOrderLifecycleService.StartAsync."),
        new("work-order.closed", "Work order closed", "work-order-closed", "Client contact", "Tenant SMTP", "Active", "Notifies the client contact when a work order is closed.", ["Email"], "Wired to WorkOrderLifecycleService.ChangeStatusAsync."),
        new("work-order.cancelled", "Work order cancelled", "work-order-cancelled", "Assigned technicians", "Tenant SMTP", "Active", "Notifies assigned technicians when a work order is cancelled.", ["Email"], "Wired to WorkOrderLifecycleService.ChangeStatusAsync."),
        new("invoice.sent", "Invoice sent to client", "invoice-sent", "Client contact", "Tenant SMTP", "Active", "Notifies the client contact when an invoice is sent.", ["Email"], "Wired to TenantInvoiceController.SendInvoice."),
        new("service-report.sent", "Service report sent to client", "service-report-sent", "Client contact", "Tenant SMTP", "Active", "Emails the approved service report PDF to the work order's client contact.", ["Email"], "Wired to the service report queue Send action."),
        new("quotation.sent", "Quotation sent to client", "quotation-sent", "Client contact", "Tenant SMTP", "Active", "Notifies the client contact when a quotation is sent.", ["Email"], "Wired to TenantQuotationController.SendQuotation."),
        new("platform.invoice.issued", "Platform subscription invoice issued", "platform-invoice-issued", "Tenant billing contact", "Platform SMTP", "Active", "Automatically dispatches a platform subscription invoice to the tenant as soon as it is created.", ["Email"], "Wired to PlatformFinanceController invoice creation."),
        new("platform.payment.received", "Platform subscription payment receipt", "platform-payment-receipt", "Tenant billing contact", "Platform SMTP", "Active", "Sends a receipt when a payment is recorded against a platform subscription invoice.", ["Email"], "Wired to PlatformFinanceController.CreatePayment."),
        new("platform.subscription.extended", "Platform subscription extended/reactivated", "platform-subscription-extended", "Tenant billing contact", "Platform SMTP", "Active", "Confirms the subscription extension (and reactivation, if suspended) triggered by a received payment.", ["Email"], "Wired to PlatformBillingLifecycleService.ApplyPaymentReceivedAsync."),
        new("platform.subscription.grace-warning", "Platform subscription grace-period warning", "platform-subscription-grace-warning", "Tenant billing contact", "Platform SMTP", "Active", "Warns the tenant once their subscription is overdue and running on its grace period.", ["Email"], "Wired to the daily SubscriptionLifecycleWorker cycle."),
        new("platform.subscription.suspended", "Platform subscription suspended", "platform-subscription-suspended", "Tenant billing contact", "Platform SMTP", "Active", "Notifies the tenant once their subscription is suspended for exceeding the grace period.", ["Email"], "Wired to the daily SubscriptionLifecycleWorker cycle."),
    ];

    public IReadOnlyCollection<EmailNotificationDefinition> List() => Definitions;
}
