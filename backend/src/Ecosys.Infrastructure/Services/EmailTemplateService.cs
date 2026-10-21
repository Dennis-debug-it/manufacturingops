using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public interface IEmailTemplateService
{
    Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListPlatformTemplatesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListTenantTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<EmailTemplateDescriptor> GetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default);
    Task<EmailTemplateDescriptor> GetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default);
    Task<EmailTemplateDescriptor> SavePlatformTemplateAsync(string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<EmailTemplateDescriptor> SaveTenantTemplateAsync(Guid tenantId, string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<EmailTemplateDescriptor> ResetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default);
    Task<EmailTemplateDescriptor> ResetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default);
    Task<RenderedEmailTemplate> RenderPlatformTemplateAsync(string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default);
    Task<RenderedEmailTemplate> RenderTenantTemplateAsync(Guid tenantId, string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default);
}

public sealed record EmailTemplateDescriptor(
    string EventKey,
    string TemplateName,
    string Subject,
    string HtmlBody,
    string TextBody,
    bool Enabled,
    string? SenderNameOverride,
    string? ReplyToOverride,
    IReadOnlyCollection<string> AvailablePlaceholders,
    IReadOnlyCollection<string> RequiredPlaceholders,
    bool SupportsTenantOverride,
    bool IsOverride,
    string Source,
    Guid? LastUpdatedBy,
    DateTime? LastUpdatedAt);

public sealed record EmailTemplateUpdateRequest(
    string TemplateName,
    string Subject,
    string HtmlBody,
    string? TextBody,
    bool Enabled,
    string? SenderNameOverride,
    string? ReplyToOverride);

public sealed record RenderedEmailTemplate(
    string EventKey,
    string TemplateName,
    string Subject,
    string HtmlBody,
    string TextBody,
    string? SenderNameOverride,
    string? ReplyToOverride,
    bool Enabled);

internal sealed class EmailTemplateService(AppDbContext dbContext) : IEmailTemplateService
{
    private const string PlatformCategory = "email-templates:platform";
    private const string TenantCategoryPrefix = "email-templates:tenant:";
    private static readonly Regex PlaceholderRegex = new(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<string, BuiltInEmailTemplate> BuiltIns =
        new Dictionary<string, BuiltInEmailTemplate>(StringComparer.OrdinalIgnoreCase)
        {
            ["user-credentials"] = new(
                "user-credentials",
                "User credentials",
                "Your {{platformName}} account is ready",
                """
                <p>Hello {{fullName}},</p>
                <p>Your {{platformName}} account for {{companyName}} is ready.</p>
                <p><strong>Login URL:</strong> {{loginUrl}}<br />
                <strong>Username:</strong> {{email}}<br />
                <strong>Temporary Password:</strong> {{temporaryPassword}}</p>
                <p>For security, you will be asked to change your password after signing in.</p>
                <p>Regards,<br />{{supportEmail}}</p>
                """,
                """
                Hello {{fullName}},

                Your {{platformName}} account for {{companyName}} is ready.

                Login URL: {{loginUrl}}
                Username: {{email}}
                Temporary Password: {{temporaryPassword}}

                For security, you will be asked to change your password after signing in.

                Regards,
                {{supportEmail}}
                """,
                ["fullName", "companyName", "platformName", "loginUrl", "email", "temporaryPassword", "supportEmail"],
                ["fullName", "email", "loginUrl"],
                true),
            ["resend-credentials"] = new(
                "resend-credentials",
                "Credentials resent",
                "Your {{platformName}} sign-in details",
                """
                <p>Hello {{fullName}},</p>
                <p>Your Ecosys sign-in details have been refreshed.</p>
                <p><strong>Login URL:</strong> {{loginUrl}}<br />
                <strong>Username:</strong> {{email}}<br />
                <strong>Temporary Password:</strong> {{temporaryPassword}}</p>
                <p>For security, you will be asked to change your password after signing in.</p>
                <p>Regards,<br />{{supportEmail}}</p>
                """,
                """
                Hello {{fullName}},

                Your Ecosys sign-in details have been refreshed.

                Login URL: {{loginUrl}}
                Username: {{email}}
                Temporary Password: {{temporaryPassword}}

                For security, you will be asked to change your password after signing in.

                Regards,
                {{supportEmail}}
                """,
                ["fullName", "companyName", "platformName", "loginUrl", "email", "temporaryPassword", "supportEmail"],
                ["fullName", "email", "loginUrl"],
                true),
            ["password-reset"] = new(
                "password-reset",
                "Admin password reset",
                "Your {{platformName}} password was reset",
                """
                <p>Hello {{fullName}},</p>
                <p>Your {{platformName}} password has been reset by an administrator.</p>
                <p><strong>Login URL:</strong> {{loginUrl}}<br />
                <strong>Username:</strong> {{email}}<br />
                <strong>Temporary Password:</strong> {{temporaryPassword}}</p>
                <p>For security, you will be asked to change your password after signing in.</p>
                <p>Regards,<br />{{supportEmail}}</p>
                """,
                """
                Hello {{fullName}},

                Your {{platformName}} password has been reset by an administrator.

                Login URL: {{loginUrl}}
                Username: {{email}}
                Temporary Password: {{temporaryPassword}}

                For security, you will be asked to change your password after signing in.

                Regards,
                {{supportEmail}}
                """,
                ["fullName", "platformName", "loginUrl", "email", "temporaryPassword", "supportEmail"],
                ["fullName", "email", "loginUrl"],
                true),
            ["signup-verify-email"] = new(
                "signup-verify-email",
                "Self sign-up: confirm email",
                "Confirm your email to start your {{platformName}} trial",
                """
                <p>Hello {{fullName}},</p>
                <p>Thanks for signing up {{companyName}} for {{platformName}}.</p>
                <p><a href="{{verifyLink}}">Confirm your email and open your workspace</a></p>
                <p>Your 14-day free trial starts when you open the link. It expires at {{expiresAt}}. If you did not sign up, you can ignore this message.</p>
                <p>Need help? Contact {{supportEmail}}.</p>
                """,
                """
                Hello {{fullName}},

                Thanks for signing up {{companyName}} for {{platformName}}.
                Confirm your email and open your workspace: {{verifyLink}}
                Your 14-day free trial starts when you open the link. It expires at {{expiresAt}}.
                If you did not sign up, you can ignore this message.
                Need help? Contact {{supportEmail}}.
                """,
                ["fullName", "companyName", "platformName", "verifyLink", "supportEmail", "expiresAt"],
                ["fullName", "verifyLink"],
                true),
            ["password-reset-link"] = new(
                "password-reset-link",
                "Self-service password reset",
                "Reset your {{platformName}} password",
                """
                <p>Hello {{fullName}},</p>
                <p>We received a request to reset your {{platformName}} password.</p>
                <p><a href="{{resetLink}}">Reset your password</a></p>
                <p>This link expires at {{expiresAt}}. If you did not request a reset, you can ignore this message.</p>
                <p>Need help? Contact {{supportEmail}}.</p>
                """,
                """
                Hello {{fullName}},

                We received a request to reset your {{platformName}} password.
                Reset your password: {{resetLink}}
                This link expires at {{expiresAt}}.
                Need help? Contact {{supportEmail}}.
                """,
                ["fullName", "platformName", "resetLink", "supportEmail", "expiresAt"],
                ["fullName", "resetLink"],
                true),
            ["account-setup-link"] = new(
                "account-setup-link",
                "Account setup / user invitation",
                "Welcome to Ecosys — Set up your account",
                """
                <p>Hello {{fullName}},</p>
                <p>An account has been created for you on {{tenantName}} / {{platformName}}.</p>
                <p>To activate your account, please set your password using the link below.</p>
                <p><a href="{{setPasswordUrl}}">Set your password</a></p>
                <p>This invitation is valid for {{invitationLifetimeHours}} hours. Expires: {{expiresAt}}. If it expires, request a new invitation or ask your administrator to resend it.</p>
                <p>Need help? Contact {{supportEmail}}.</p>
                """,
                """
                Hello {{fullName}},

                An account has been created for you on {{tenantName}} / {{platformName}}.
                To activate your account, please set your password using the link below.

                Set your password: {{setPasswordUrl}}

                This invitation is valid for {{invitationLifetimeHours}} hours. Expires: {{expiresAt}}. If it expires, request a new invitation or ask your administrator to resend it.
                Need help? Contact {{supportEmail}}.
                """,
                ["fullName", "tenantName", "platformName", "setPasswordUrl", "supportEmail", "expiresAt", "invitationLifetimeHours", "role"],
                ["fullName", "setPasswordUrl"],
                true),
            ["workspace-request-received"] = new(
                "workspace-request-received",
                "Platform lead received",
                "New {{platformName}} workspace request from {{leadCompanyName}}",
                """
                <p>A new {{platformName}} workspace request has been submitted.</p>
                <p><strong>Company:</strong> {{leadCompanyName}}<br />
                <strong>Contact:</strong> {{contactName}}<br />
                <strong>Email:</strong> {{contactEmail}}<br />
                <strong>Phone:</strong> {{contactPhone}}<br />
                <strong>Industry:</strong> {{industry}}</p>
                <p><strong>Message:</strong><br />{{message}}</p>
                <p><strong>Submitted:</strong> {{submittedAt}}</p>
                """,
                """
                A new {{platformName}} workspace request has been submitted.

                Company: {{leadCompanyName}}
                Contact: {{contactName}}
                Email: {{contactEmail}}
                Phone: {{contactPhone}}
                Industry: {{industry}}
                Message: {{message}}
                Submitted: {{submittedAt}}
                """,
                ["platformName", "leadCompanyName", "contactName", "contactEmail", "contactPhone", "industry", "message", "submittedAt"],
                ["leadCompanyName", "contactName", "contactEmail"],
                false),
            ["tenant-onboarding"] = new(
                "tenant-onboarding",
                "Tenant welcome / onboarding",
                "Your {{platformName}} workspace is ready",
                """
                <p>Hello {{fullName}},</p>
                <p>Your {{platformName}} workspace for {{companyName}} is ready.</p>
                <p><strong>Tenant:</strong> {{tenantName}}<br />
                <strong>Login URL:</strong> {{loginUrl}}</p>
                <p>Contact {{supportEmail}} if you need help getting started.</p>
                """,
                """
                Hello {{fullName}},

                Your {{platformName}} workspace for {{companyName}} is ready.

                Tenant: {{tenantName}}
                Login URL: {{loginUrl}}

                Contact {{supportEmail}} if you need help getting started.

                Regards,
                {{supportEmail}}
                """,
                ["fullName", "companyName", "tenantName", "platformName", "loginUrl", "supportEmail"],
                ["fullName", "tenantName", "loginUrl"],
                false),
            ["test-email"] = new(
                "test-email",
                "Test email",
                "{{platformName}} email delivery test",
                "<p>Hello from {{platformName}}.</p><p>This test email was sent at {{sentAt}} by {{senderName}}.</p>",
                "Hello from {{platformName}}.\nThis test email was sent at {{sentAt}} by {{senderName}}.",
                ["platformName", "senderName", "sentAt"],
                [],
                false),
            ["work-order-assigned"] = new("work-order-assigned", "Work order assigned email", "Work order {{WorkOrderNumber}} assigned", "<p>Hello {{AssignedTo}},</p><p>Work order <strong>{{WorkOrderNumber}}</strong> has been assigned to you.</p><p><strong>Priority:</strong> {{Priority}}<br /><strong>Due Date:</strong> {{DueDate}}<br /><strong>Asset:</strong> {{AssetName}}</p>", "Hello {{AssignedTo}},\n\nWork order {{WorkOrderNumber}} has been assigned to you.\nPriority: {{Priority}}\nDue Date: {{DueDate}}\nAsset: {{AssetName}}", ["AssignedTo", "WorkOrderNumber", "Priority", "DueDate", "AssetName"], ["AssignedTo", "WorkOrderNumber"], true),
            ["pm-due"] = new("pm-due", "Preventive maintenance due", "Preventive maintenance due for {{assetName}}", "<p>Preventive maintenance is due for <strong>{{assetName}}</strong>.</p><p><strong>Assigned Group:</strong> {{fullName}}<br /><strong>Due Date:</strong> {{dueDate}}</p>", "Preventive maintenance is due for {{assetName}}.\nAssigned Group: {{fullName}}\nDue Date: {{dueDate}}", ["fullName", "assetName", "dueDate", "platformName"], ["assetName"], true),
            ["material-request"] = new("material-request", "Material request", "Material request submitted", "<p>A material request has been submitted.</p><p><strong>Work Order:</strong> {{workOrderNumber}}<br /><strong>Requested By:</strong> {{fullName}}</p>", "A material request has been submitted.\nWork Order: {{workOrderNumber}}\nRequested By: {{fullName}}", ["fullName", "workOrderNumber", "platformName"], ["workOrderNumber"], true),
            ["smtp-failure-alert"] = new("smtp-failure-alert", "SMTP failure alert", "{{platformName}} email delivery warning", "<p>An outbound email failure needs attention.</p><p><strong>Error:</strong> {{message}}</p>", "An outbound email failure needs attention.\nError: {{message}}", ["platformName", "message", "senderName"], ["message"], false),
            ["work-order-reassigned"] = new("work-order-reassigned", "Work order reassigned email", "Work order {{WorkOrderNumber}} reassigned", "<p>Hello {{AssignedTo}},</p><p>Work order <strong>{{WorkOrderNumber}}</strong> has been reassigned.</p><p><strong>Priority:</strong> {{Priority}}<br /><strong>Due Date:</strong> {{DueDate}}<br /><strong>Asset:</strong> {{AssetName}}</p>", "Hello {{AssignedTo}},\n\nWork order {{WorkOrderNumber}} has been reassigned.\nPriority: {{Priority}}\nDue Date: {{DueDate}}\nAsset: {{AssetName}}", ["AssignedTo", "WorkOrderNumber", "Priority", "DueDate", "AssetName"], ["AssignedTo", "WorkOrderNumber"], true),
            ["work-order-overdue"] = new("work-order-overdue", "Work order overdue/escalation email", "Work order {{WorkOrderNumber}} needs attention", "<p>Work order <strong>{{WorkOrderNumber}}</strong> requires attention.</p><p><strong>Assigned To:</strong> {{AssignedTo}}<br /><strong>Priority:</strong> {{Priority}}<br /><strong>Due Date:</strong> {{DueDate}}<br /><strong>Asset:</strong> {{AssetName}}</p>", "Work order {{WorkOrderNumber}} requires attention.\nAssigned To: {{AssignedTo}}\nPriority: {{Priority}}\nDue Date: {{DueDate}}\nAsset: {{AssetName}}", ["AssignedTo", "WorkOrderNumber", "Priority", "DueDate", "AssetName"], ["WorkOrderNumber"], true),
            ["work-order-completed"] = new("work-order-completed", "Work order completed/closed email", "Work order {{WorkOrderNumber}} completed", "<p>Work order <strong>{{WorkOrderNumber}}</strong> has been completed.</p><p><strong>Assigned To:</strong> {{AssignedTo}}<br /><strong>Asset:</strong> {{AssetName}}</p>", "Work order {{WorkOrderNumber}} has been completed.\nAssigned To: {{AssignedTo}}\nAsset: {{AssetName}}", ["AssignedTo", "WorkOrderNumber", "AssetName"], ["WorkOrderNumber"], true),
            ["csat-survey"] = new("csat-survey", "Customer satisfaction survey", "How did we do on {{WorkOrderNumber}}?", "<p>Hello {{ClientName}},</p><p>Work order <strong>{{WorkOrderNumber}}</strong> has been completed. We'd love your feedback — it takes less than a minute.</p><p><a href=\"{{SurveyLink}}\">Rate this job</a></p><p>Thank you for helping us improve.</p>", "Hello {{ClientName}},\n\nWork order {{WorkOrderNumber}} has been completed. We'd love your feedback — it takes less than a minute.\n\nRate this job: {{SurveyLink}}\n\nThank you for helping us improve.", ["ClientName", "WorkOrderNumber", "SurveyLink"], ["WorkOrderNumber", "SurveyLink"], true),
            ["work-order-on-hold"] = new("work-order-on-hold", "Work order placed on hold", "Work order {{WorkOrderNumber}} is on hold", "<p>Work order <strong>{{WorkOrderNumber}}</strong> has been placed on hold.</p><p><strong>Reason:</strong> {{HoldReason}}<br /><strong>Note:</strong> {{HoldNote}}<br /><strong>Client:</strong> {{ClientName}}<br /><strong>Asset:</strong> {{AssetName}}</p>", "Work order {{WorkOrderNumber}} is on hold.\nReason: {{HoldReason}}\nNote: {{HoldNote}}\nClient: {{ClientName}}\nAsset: {{AssetName}}", ["WorkOrderNumber", "HoldReason", "HoldNote", "ClientName", "AssetName"], ["WorkOrderNumber", "HoldReason"], true),
            ["pm-due-reminder"] = new("pm-due-reminder", "Preventive maintenance due reminder", "Preventive maintenance due for {{AssetName}}", "<p>Preventive maintenance is due for <strong>{{AssetName}}</strong>.</p><p><strong>Assigned To:</strong> {{AssignedTo}}<br /><strong>Due Date:</strong> {{DueDate}}</p>", "Preventive maintenance is due for {{AssetName}}.\nAssigned To: {{AssignedTo}}\nDue Date: {{DueDate}}", ["AssignedTo", "AssetName", "DueDate"], ["AssetName"], true),
            ["material-request-submitted"] = new("material-request-submitted", "Material request submitted", "Material request submitted for {{WorkOrderNumber}}", "<p>A material request was submitted.</p><p><strong>Work Order:</strong> {{WorkOrderNumber}}<br /><strong>Assigned To:</strong> {{AssignedTo}}</p>", "A material request was submitted.\nWork Order: {{WorkOrderNumber}}\nAssigned To: {{AssignedTo}}", ["AssignedTo", "WorkOrderNumber"], ["WorkOrderNumber"], true),
            ["material-request-approved-rejected"] = new("material-request-approved-rejected", "Material request approved/rejected", "Material request update for {{WorkOrderNumber}}", "<p>A material request has been updated.</p><p><strong>Work Order:</strong> {{WorkOrderNumber}}<br /><strong>Assigned To:</strong> {{AssignedTo}}</p>", "A material request has been updated.\nWork Order: {{WorkOrderNumber}}\nAssigned To: {{AssignedTo}}", ["AssignedTo", "WorkOrderNumber"], ["WorkOrderNumber"], true),
            ["smtp-test"] = new(
                "smtp-test",
                "SMTP test email",
                "Ecosys email delivery test",
                "<p>This is a test email from Ecosys.</p><p>If you received this message, outbound email delivery is working.</p>",
                "This is a test email from Ecosys.\nIf you received this message, outbound email delivery is working.",
                [],
                [],
                false),
            ["work-order.assigned"] = new("work-order.assigned", "Work order assigned notification", "Work order {{WorkOrderNumber}} assigned to you", "<p>Hello {{AssignedTo}},</p><p>Work order <strong>{{WorkOrderNumber}}</strong> has been assigned to you.</p><p><strong>Client:</strong> {{ClientName}}<br /><strong>Due Date:</strong> {{DueDate}}<br /><strong>Assigned By:</strong> {{AssignedBy}}</p>", "Hello {{AssignedTo}},\n\nWork order {{WorkOrderNumber}} has been assigned to you.\nClient: {{ClientName}}\nDue Date: {{DueDate}}\nAssigned By: {{AssignedBy}}", ["AssignedTo", "WorkOrderNumber", "ClientName", "DueDate", "AssignedBy"], ["AssignedTo", "WorkOrderNumber"], true),
            ["work-order.started"] = new("work-order.started", "Work order started notification", "Work order {{WorkOrderNumber}} is now in progress", "<p>Work order <strong>{{WorkOrderNumber}}</strong> has been started.</p><p><strong>Technician:</strong> {{TechnicianName}}<br /><strong>Started At:</strong> {{StartedAt}}</p>", "Work order {{WorkOrderNumber}} has been started.\nTechnician: {{TechnicianName}}\nStarted At: {{StartedAt}}", ["WorkOrderNumber", "TechnicianName", "StartedAt"], ["WorkOrderNumber"], true),
            ["work-order.closed"] = new("work-order.closed", "Work order closed notification", "Work order {{WorkOrderNumber}} closed", "<p>Dear {{ClientName}},</p><p>Work order <strong>{{WorkOrderNumber}}</strong> has been closed.</p><p><strong>Summary:</strong> {{CompletionSummary}}</p>", "Dear {{ClientName}},\n\nWork order {{WorkOrderNumber}} has been closed.\nSummary: {{CompletionSummary}}", ["WorkOrderNumber", "ClientName", "CompletionSummary"], ["WorkOrderNumber"], true),
            ["work-order.cancelled"] = new("work-order.cancelled", "Work order cancelled notification", "Work order {{WorkOrderNumber}} has been cancelled", "<p>Work order <strong>{{WorkOrderNumber}}</strong> has been cancelled.</p><p><strong>Reason:</strong> {{Reason}}</p>", "Work order {{WorkOrderNumber}} has been cancelled.\nReason: {{Reason}}", ["WorkOrderNumber", "Reason"], ["WorkOrderNumber"], true),
            ["email-intake-received"] = new(
                "email-intake-received",
                "Email request received (auto-reply)",
                "Re: {{originalSubject}} [{{workOrderNumber}}]",
                "<p>Hello {{fullName}},</p><p>Thank you for contacting {{companyName}}. We've received your request and logged it as <strong>{{workOrderNumber}}</strong>.</p><p>Our team will be in touch shortly. To add anything, just reply to this email and keep <strong>{{workOrderNumber}}</strong> in the subject.</p><p>Regards,<br />{{companyName}}</p>",
                "Hello {{fullName}},\n\nThank you for contacting {{companyName}}. We've received your request and logged it as {{workOrderNumber}}.\n\nOur team will be in touch shortly. To add anything, just reply to this email and keep {{workOrderNumber}} in the subject.\n\nRegards,\n{{companyName}}",
                ["fullName", "companyName", "workOrderNumber", "workOrderTitle", "originalSubject"],
                ["workOrderNumber"],
                true),
            ["email-intake-reply-added"] = new(
                "email-intake-reply-added",
                "Email added to an existing job (auto-reply)",
                "Re: {{originalSubject}}",
                "<p>Hello {{fullName}},</p><p>Thank you — your message has been added to <strong>{{workOrderNumber}}</strong> ({{workOrderTitle}}). Our team has been notified.</p><p>Regards,<br />{{companyName}}</p>",
                "Hello {{fullName}},\n\nThank you — your message has been added to {{workOrderNumber}} ({{workOrderTitle}}). Our team has been notified.\n\nRegards,\n{{companyName}}",
                ["fullName", "companyName", "workOrderNumber", "workOrderTitle", "workOrderStatus", "originalSubject"],
                ["workOrderNumber"],
                true),
            ["email-intake-unrecognised"] = new(
                "email-intake-unrecognised",
                "Email from an unrecognised sender (auto-reply)",
                "Re: {{originalSubject}}",
                "<p>Hello {{fullName}},</p><p>Thank you for your email to {{companyName}}. We couldn't match your email address to a customer account, so your message hasn't been logged as a service request yet.</p><p>Please reply from the email address registered with us, or call us so we can help.</p><p>Regards,<br />{{companyName}}</p>",
                "Hello {{fullName}},\n\nThank you for your email to {{companyName}}. We couldn't match your email address to a customer account, so your message hasn't been logged as a service request yet.\n\nPlease reply from the email address registered with us, or call us so we can help.\n\nRegards,\n{{companyName}}",
                ["fullName", "companyName", "originalSubject"],
                [],
                true),
            ["work-order.client.started"] = new(
                "work-order.client.started",
                "Client notification — work started",
                "Work has started on your job {{WoNumber}}",
                "<p>Dear {{ClientName}},</p><p>We wanted to let you know that work has started on your job <strong>{{WoNumber}}</strong> — {{Title}}.</p><p><strong>Technician:</strong> {{TechnicianName}}<br /><strong>Estimated Duration:</strong> {{EstimatedDuration}}<br /><strong>Site:</strong> {{SiteName}}</p><p>If you have any questions, please don't hesitate to contact us.</p>",
                "Dear {{ClientName}},\n\nWork has started on your job {{WoNumber}} — {{Title}}.\n\nTechnician: {{TechnicianName}}\nEstimated Duration: {{EstimatedDuration}}\nSite: {{SiteName}}\n\nIf you have any questions, please don't hesitate to contact us.",
                ["WoNumber", "Title", "TechnicianName", "EstimatedDuration", "SiteName", "ClientName"],
                ["WoNumber", "ClientName"],
                true),
            ["work-order.client.on-hold"] = new(
                "work-order.client.on-hold",
                "Client notification — job on hold",
                "Update on your job {{WoNumber}}",
                "<p>Dear {{ClientName}},</p><p>We wanted to update you that job <strong>{{WoNumber}}</strong> — {{Title}} has been temporarily placed on hold.</p><p><strong>Reason:</strong> {{HoldReason}}<br /><strong>Note:</strong> {{HoldNote}}</p><p>We apologise for any inconvenience. Our team will follow up with you shortly. If you need to speak with someone, please contact us at {{ContactInfo}}.</p>",
                "Dear {{ClientName}},\n\nJob {{WoNumber}} — {{Title}} has been temporarily placed on hold.\n\nReason: {{HoldReason}}\nNote: {{HoldNote}}\n\nWe apologise for any inconvenience. Please contact us at {{ContactInfo}} if you have questions.",
                ["WoNumber", "Title", "HoldReason", "HoldNote", "ClientName", "ContactInfo"],
                ["WoNumber", "ClientName", "HoldReason"],
                true),
            ["work-order.client.completed"] = new(
                "work-order.client.completed",
                "Client notification — job completed",
                "Job {{WoNumber}} has been completed",
                "<p>Dear {{ClientName}},</p><p>We are pleased to inform you that job <strong>{{WoNumber}}</strong> — {{Title}} has been successfully completed.</p><p><strong>Completed:</strong> {{CompletionDate}}<br /><strong>Technician(s):</strong> {{TechnicianNames}}<br /><strong>Summary:</strong> {{Summary}}</p><p>A full report is available on request. Thank you for choosing our services — your satisfaction is our priority. Please don't hesitate to reach out if you have any feedback or follow-up needs.</p>",
                "Dear {{ClientName}},\n\nJob {{WoNumber}} — {{Title}} has been successfully completed.\n\nCompleted: {{CompletionDate}}\nTechnician(s): {{TechnicianNames}}\nSummary: {{Summary}}\n\nA full report is available on request. Thank you for choosing our services.",
                ["WoNumber", "Title", "CompletionDate", "TechnicianNames", "Summary", "ClientName"],
                ["WoNumber", "ClientName"],
                true),
            ["crm.follow-up.due"] = new("crm.follow-up.due", "Sales follow-up due", "Follow-up due: {{OpportunityTitle}}",
                "<p>Hello {{OwnerName}},</p><p>A follow-up on <strong>{{OpportunityTitle}}</strong> ({{CustomerName}}) is due.</p><p><strong>To do:</strong> {{FollowUp}}<br /><strong>Due:</strong> {{DueAt}}<br /><strong>Stage:</strong> {{Stage}}</p><p><a href=\"{{OpportunityUrl}}\">Open the deal</a> and mark it done when it's handled.</p>",
                "Hello {{OwnerName}},\n\nA follow-up on {{OpportunityTitle}} ({{CustomerName}}) is due.\nTo do: {{FollowUp}}\nDue: {{DueAt}}\nStage: {{Stage}}\n\nOpen the deal: {{OpportunityUrl}}",
                ["OwnerName", "OpportunityTitle", "CustomerName", "FollowUp", "DueAt", "Stage", "OpportunityUrl"], ["OpportunityTitle"], true),
            ["crm.tender.deadline"] = new("crm.tender.deadline", "RFP/RFQ deadline approaching", "{{TenderType}} due {{DueIn}}: {{TenderTitle}}",
                "<p>Hello {{OwnerName}},</p><p>The {{TenderType}} <strong>{{TenderTitle}}</strong> ({{TenderReference}}) from {{IssuerName}} has not been submitted yet and is due <strong>{{DueIn}}</strong>.</p><p><strong>Deadline:</strong> {{Deadline}}<br /><strong>How to submit:</strong> {{SubmissionMethod}}<br /><strong>Status:</strong> {{Status}}</p><p><a href=\"{{TenderUrl}}\">Open the request</a></p>",
                "Hello {{OwnerName}},\n\nThe {{TenderType}} {{TenderTitle}} ({{TenderReference}}) from {{IssuerName}} has not been submitted yet and is due {{DueIn}}.\nDeadline: {{Deadline}}\nHow to submit: {{SubmissionMethod}}\nStatus: {{Status}}\n\nOpen the request: {{TenderUrl}}",
                ["OwnerName", "TenderType", "TenderTitle", "TenderReference", "IssuerName", "DueIn", "Deadline", "SubmissionMethod", "Status", "TenderUrl"], ["TenderTitle"], true),
            ["invoice.sent"] = new("invoice.sent", "Invoice sent to client", "Invoice {{InvoiceNumber}} — {{CompanyName}}", "<p>Dear {{CompanyName}},</p><p>Please find your invoice <strong>{{InvoiceNumber}}</strong> below.</p><p><strong>Amount:</strong> {{TotalAmount}}<br /><strong>Due Date:</strong> {{DueDate}}</p><p>Please ensure payment is made by the due date. Contact us if you have any questions.</p>", "Dear {{CompanyName}},\n\nPlease find your invoice {{InvoiceNumber}} below.\nAmount: {{TotalAmount}}\nDue Date: {{DueDate}}\n\nPlease ensure payment is made by the due date.", ["InvoiceNumber", "CompanyName", "TotalAmount", "DueDate"], ["InvoiceNumber", "CompanyName"], true),
            ["service-report.sent"] = new("service-report.sent", "Service report sent to client", "Service report {{ReportNumber}} — {{WorkOrderNumber}}", "<p>Dear {{ClientName}},</p><p>Please find attached the service report <strong>{{ReportNumber}}</strong> for work order <strong>{{WorkOrderNumber}}</strong> — {{Title}}.</p><p><strong>Site:</strong> {{SiteName}}<br /><strong>Completed:</strong> {{CompletionDate}}</p><p>Contact us if you have any questions about the work carried out.</p>", "Dear {{ClientName}},\n\nPlease find attached the service report {{ReportNumber}} for work order {{WorkOrderNumber}} — {{Title}}.\nSite: {{SiteName}}\nCompleted: {{CompletionDate}}\n\nContact us if you have any questions about the work carried out.", ["ReportNumber", "WorkOrderNumber", "Title", "SiteName", "CompletionDate", "ClientName"], ["WorkOrderNumber", "ClientName"], true),
            ["quotation.sent"] = new("quotation.sent", "Quotation sent to client", "Quotation {{QuoteNumber}} — {{CompanyName}}", "<p>Dear {{CompanyName}},</p><p>Please find your quotation <strong>{{QuoteNumber}}</strong> below.</p><p><strong>Total Amount:</strong> {{TotalAmount}}<br /><strong>Valid Until:</strong> {{ValidUntil}}</p><p>Please review and let us know if you have any questions.</p>", "Dear {{CompanyName}},\n\nPlease find your quotation {{QuoteNumber}} below.\nTotal Amount: {{TotalAmount}}\nValid Until: {{ValidUntil}}\n\nPlease review and let us know if you have any questions.", ["QuoteNumber", "CompanyName", "TotalAmount", "ValidUntil"], ["QuoteNumber", "CompanyName"], true),
            ["platform-invoice-issued"] = new(
                "platform-invoice-issued",
                "Platform subscription invoice issued",
                "Invoice {{InvoiceNumber}} for your Ecosys subscription",
                "<p>Dear {{CompanyName}},</p><p>A new invoice has been issued for your Ecosys subscription.</p><p><strong>Invoice Number:</strong> {{InvoiceNumber}}<br /><strong>Amount Due:</strong> {{TotalAmount}}<br /><strong>Due Date:</strong> {{DueDate}}</p><p>Please arrange payment by the due date to keep your workspace active without interruption.</p>",
                "Dear {{CompanyName}},\n\nA new invoice has been issued for your Ecosys subscription.\nInvoice Number: {{InvoiceNumber}}\nAmount Due: {{TotalAmount}}\nDue Date: {{DueDate}}\n\nPlease arrange payment by the due date to keep your workspace active without interruption.",
                ["InvoiceNumber", "CompanyName", "TotalAmount", "DueDate"],
                ["InvoiceNumber", "CompanyName"],
                false),
            ["platform-payment-receipt"] = new(
                "platform-payment-receipt",
                "Platform subscription payment receipt",
                "Receipt {{PaymentNumber}} — payment received",
                "<p>Dear {{CompanyName}},</p><p>We have received your payment. Thank you.</p><p><strong>Receipt Number:</strong> {{PaymentNumber}}<br /><strong>Amount Paid:</strong> {{Amount}}<br /><strong>Applied to Invoice:</strong> {{InvoiceNumber}}<br /><strong>Date:</strong> {{PaidAt}}</p>",
                "Dear {{CompanyName}},\n\nWe have received your payment. Thank you.\nReceipt Number: {{PaymentNumber}}\nAmount Paid: {{Amount}}\nApplied to Invoice: {{InvoiceNumber}}\nDate: {{PaidAt}}",
                ["PaymentNumber", "CompanyName", "Amount", "InvoiceNumber", "PaidAt"],
                ["PaymentNumber", "CompanyName"],
                false),
            ["platform-subscription-extended"] = new(
                "platform-subscription-extended",
                "Platform subscription extended/reactivated",
                "Your Ecosys subscription has been {{ActionWord}}",
                "<p>Dear {{CompanyName}},</p><p>Your Ecosys subscription has been {{ActionWord}} following your recent payment.</p><p><strong>Plan:</strong> {{PlanName}}<br /><strong>Active Until:</strong> {{NewExpiryDate}}</p><p>Thank you for staying with Ecosys.</p>",
                "Dear {{CompanyName}},\n\nYour Ecosys subscription has been {{ActionWord}} following your recent payment.\nPlan: {{PlanName}}\nActive Until: {{NewExpiryDate}}\n\nThank you for staying with Ecosys.",
                ["CompanyName", "PlanName", "NewExpiryDate", "ActionWord"],
                ["CompanyName", "NewExpiryDate"],
                false),
            ["platform-subscription-grace-warning"] = new(
                "platform-subscription-grace-warning",
                "Platform subscription grace-period warning",
                "Payment overdue — your Ecosys workspace enters grace period",
                "<p>Dear {{CompanyName}},</p><p>Your Ecosys subscription payment is overdue. Your workspace remains active during a grace period.</p><p><strong>Grace Period Ends:</strong> {{GraceEndsAt}}</p><p>Please settle the outstanding invoice before this date to avoid suspension of your workspace.</p>",
                "Dear {{CompanyName}},\n\nYour Ecosys subscription payment is overdue. Your workspace remains active during a grace period.\nGrace Period Ends: {{GraceEndsAt}}\n\nPlease settle the outstanding invoice before this date to avoid suspension of your workspace.",
                ["CompanyName", "GraceEndsAt"],
                ["CompanyName", "GraceEndsAt"],
                false),
            ["platform-subscription-suspended"] = new(
                "platform-subscription-suspended",
                "Platform subscription suspended",
                "Your Ecosys workspace has been suspended",
                "<p>Dear {{CompanyName}},</p><p>Your Ecosys workspace has been suspended because payment was not received within the grace period.</p><p><strong>Suspended Since:</strong> {{SuspendedAt}}</p><p>Please settle your outstanding balance to have your workspace automatically reactivated.</p>",
                "Dear {{CompanyName}},\n\nYour Ecosys workspace has been suspended because payment was not received within the grace period.\nSuspended Since: {{SuspendedAt}}\n\nPlease settle your outstanding balance to have your workspace automatically reactivated.",
                ["CompanyName", "SuspendedAt"],
                ["CompanyName"],
                false),
        };

    public async Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListPlatformTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var stored = await LoadStoredTemplatesAsync(PlatformCategory, cancellationToken);
        return BuiltIns.Values
            .Where(item => !item.SupportsTenantOverride)
            .Select(item => ToDescriptor(item, stored.FirstOrDefault(x => Matches(x.EventKey, item.EventKey)), false))
            .ToList();
    }

    public async Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListTenantTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var platformTemplates = await LoadStoredTemplatesAsync(PlatformCategory, cancellationToken);
        var tenantTemplates = await LoadStoredTemplatesAsync(GetTenantCategory(tenantId), cancellationToken);
        return BuiltIns.Values
            .Where(item => item.SupportsTenantOverride)
            .Select(item =>
                ToDescriptor(
                    item,
                    tenantTemplates.FirstOrDefault(x => Matches(x.EventKey, item.EventKey))
                    ?? platformTemplates.FirstOrDefault(x => Matches(x.EventKey, item.EventKey)),
                    tenantTemplates.Any(x => Matches(x.EventKey, item.EventKey))))
            .ToList();
    }

    public async Task<EmailTemplateDescriptor> GetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default)
    {
        var builtIn = GetBuiltIn(eventKey);
        var stored = await LoadStoredTemplatesAsync(PlatformCategory, cancellationToken);
        return ToDescriptor(builtIn, stored.FirstOrDefault(x => Matches(x.EventKey, builtIn.EventKey)), false);
    }

    public async Task<EmailTemplateDescriptor> GetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default)
    {
        var builtIn = GetBuiltIn(eventKey);
        var platformTemplates = await LoadStoredTemplatesAsync(PlatformCategory, cancellationToken);
        var tenantTemplates = await LoadStoredTemplatesAsync(GetTenantCategory(tenantId), cancellationToken);
        var tenantTemplate = tenantTemplates.FirstOrDefault(x => Matches(x.EventKey, builtIn.EventKey));
        return ToDescriptor(builtIn, tenantTemplate ?? platformTemplates.FirstOrDefault(x => Matches(x.EventKey, builtIn.EventKey)), tenantTemplate is not null);
    }

    public async Task<EmailTemplateDescriptor> SavePlatformTemplateAsync(string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var builtIn = GetBuiltIn(eventKey);
        ValidateTemplateUpdate(builtIn, request);
        var templates = await LoadStoredTemplatesAsync(PlatformCategory, cancellationToken);
        UpsertTemplate(templates, builtIn.EventKey, request, actorUserId);
        await SaveStoredTemplatesAsync(PlatformCategory, templates, cancellationToken);
        return await GetPlatformTemplateAsync(eventKey, cancellationToken);
    }

    public async Task<EmailTemplateDescriptor> SaveTenantTemplateAsync(Guid tenantId, string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var builtIn = GetBuiltIn(eventKey);
        if (!builtIn.SupportsTenantOverride)
        {
            throw new Shared.Errors.BusinessRuleException("This email template can only be managed at platform level.");
        }

        ValidateTemplateUpdate(builtIn, request);
        var category = GetTenantCategory(tenantId);
        var templates = await LoadStoredTemplatesAsync(category, cancellationToken);
        UpsertTemplate(templates, builtIn.EventKey, request, actorUserId);
        await SaveStoredTemplatesAsync(category, templates, cancellationToken);
        return await GetTenantTemplateAsync(tenantId, eventKey, cancellationToken);
    }

    public async Task<EmailTemplateDescriptor> ResetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default)
    {
        var builtIn = GetBuiltIn(eventKey);
        var templates = await LoadStoredTemplatesAsync(PlatformCategory, cancellationToken);
        templates.RemoveAll(x => Matches(x.EventKey, builtIn.EventKey));
        await SaveStoredTemplatesAsync(PlatformCategory, templates, cancellationToken);
        return await GetPlatformTemplateAsync(eventKey, cancellationToken);
    }

    public async Task<EmailTemplateDescriptor> ResetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default)
    {
        var builtIn = GetBuiltIn(eventKey);
        var category = GetTenantCategory(tenantId);
        var templates = await LoadStoredTemplatesAsync(category, cancellationToken);
        templates.RemoveAll(x => Matches(x.EventKey, builtIn.EventKey));
        await SaveStoredTemplatesAsync(category, templates, cancellationToken);
        return await GetTenantTemplateAsync(tenantId, eventKey, cancellationToken);
    }

    public async Task<RenderedEmailTemplate> RenderPlatformTemplateAsync(string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default)
    {
        var descriptor = await GetPlatformTemplateAsync(eventKey, cancellationToken);
        return Render(descriptor, values);
    }

    public async Task<RenderedEmailTemplate> RenderTenantTemplateAsync(Guid tenantId, string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default)
    {
        var descriptor = await GetTenantTemplateAsync(tenantId, eventKey, cancellationToken);
        return Render(descriptor, values);
    }

    private static RenderedEmailTemplate Render(EmailTemplateDescriptor descriptor, IReadOnlyDictionary<string, string?> values)
    {
        var safeValues = values.ToDictionary(
            item => item.Key,
            item => item.Value ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);

        var renderedHtml = ReplacePlaceholders(descriptor.HtmlBody, safeValues, encodeHtml: true);
        var renderedText = ReplacePlaceholders(descriptor.TextBody, safeValues, encodeHtml: false);
        var renderedSubject = ReplacePlaceholders(descriptor.Subject, safeValues, encodeHtml: false);
        return new RenderedEmailTemplate(
            descriptor.EventKey,
            descriptor.TemplateName,
            renderedSubject,
            renderedHtml,
            renderedText,
            descriptor.SenderNameOverride,
            descriptor.ReplyToOverride,
            descriptor.Enabled);
    }

    private static string ReplacePlaceholders(string source, IReadOnlyDictionary<string, string> values, bool encodeHtml)
    {
        return PlaceholderRegex.Replace(source ?? string.Empty, match =>
        {
            var key = match.Groups[1].Value.Trim();
            if (!values.TryGetValue(key, out var value))
            {
                return string.Empty;
            }

            return encodeHtml ? WebUtility.HtmlEncode(value) : value;
        });
    }

    private static void ValidateTemplateUpdate(BuiltInEmailTemplate builtIn, EmailTemplateUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            throw new Shared.Errors.BusinessRuleException("Template name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            throw new Shared.Errors.BusinessRuleException("Subject is required.");
        }

        if (string.IsNullOrWhiteSpace(request.HtmlBody))
        {
            throw new Shared.Errors.BusinessRuleException("HTML body is required.");
        }

        foreach (var placeholder in builtIn.RequiredPlaceholders)
        {
            var token = $"{{{{{placeholder}}}}}";
            if (!request.Subject.Contains(token, StringComparison.OrdinalIgnoreCase)
                && !request.HtmlBody.Contains(token, StringComparison.OrdinalIgnoreCase)
                && !(request.TextBody?.Contains(token, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                throw new Shared.Errors.BusinessRuleException($"The {builtIn.TemplateName} template must include {token}.");
            }
        }
    }

    private async Task<List<StoredEmailTemplate>> LoadStoredTemplatesAsync(string category, CancellationToken cancellationToken)
    {
        var row = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == category, cancellationToken);
        if (row is null || string.IsNullOrWhiteSpace(row.JsonValue))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<StoredEmailTemplate>>(row.JsonValue, JsonOptions) ?? [];
    }

    private async Task SaveStoredTemplatesAsync(string category, List<StoredEmailTemplate> templates, CancellationToken cancellationToken)
    {
        var row = await dbContext.PlatformSettings.SingleOrDefaultAsync(x => x.Category == category, cancellationToken);
        if (row is null)
        {
            dbContext.PlatformSettings.Add(new PlatformSetting
            {
                Category = category,
                JsonValue = JsonSerializer.Serialize(templates, JsonOptions)
            });
        }
        else
        {
            row.JsonValue = JsonSerializer.Serialize(templates, JsonOptions);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void UpsertTemplate(List<StoredEmailTemplate> templates, string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId)
    {
        var existing = templates.FirstOrDefault(x => Matches(x.EventKey, eventKey));
        if (existing is null)
        {
            templates.Add(new StoredEmailTemplate(
                eventKey,
                request.TemplateName.Trim(),
                request.Subject.Trim(),
                request.HtmlBody.Trim(),
                string.IsNullOrWhiteSpace(request.TextBody) ? StripHtml(request.HtmlBody) : request.TextBody.Trim(),
                request.Enabled,
                request.SenderNameOverride?.Trim(),
                request.ReplyToOverride?.Trim(),
                actorUserId,
                DateTime.UtcNow));
        }
        else
        {
            var updated = existing with
            {
                TemplateName = request.TemplateName.Trim(),
                Subject = request.Subject.Trim(),
                HtmlBody = request.HtmlBody.Trim(),
                TextBody = string.IsNullOrWhiteSpace(request.TextBody) ? StripHtml(request.HtmlBody) : request.TextBody.Trim(),
                Enabled = request.Enabled,
                SenderNameOverride = request.SenderNameOverride?.Trim(),
                ReplyToOverride = request.ReplyToOverride?.Trim(),
                LastUpdatedBy = actorUserId,
                LastUpdatedAt = DateTime.UtcNow,
            };
            templates[templates.IndexOf(existing)] = updated;
        }
    }

    private static EmailTemplateDescriptor ToDescriptor(BuiltInEmailTemplate builtIn, StoredEmailTemplate? stored, bool isOverride)
    {
        return new EmailTemplateDescriptor(
            builtIn.EventKey,
            stored?.TemplateName ?? builtIn.TemplateName,
            stored?.Subject ?? builtIn.Subject,
            stored?.HtmlBody ?? builtIn.HtmlBody,
            stored?.TextBody ?? builtIn.TextBody,
            stored?.Enabled ?? true,
            stored?.SenderNameOverride,
            stored?.ReplyToOverride,
            builtIn.AvailablePlaceholders,
            builtIn.RequiredPlaceholders,
            builtIn.SupportsTenantOverride,
            isOverride,
            stored is null ? "built-in" : "stored",
            stored?.LastUpdatedBy,
            stored?.LastUpdatedAt);
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var text = Regex.Replace(html, "<[^>]+>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }

    private static string GetTenantCategory(Guid tenantId) =>
        TenantCategoryPrefix + tenantId.ToString("N");

    private static BuiltInEmailTemplate GetBuiltIn(string eventKey)
    {
        if (!BuiltIns.TryGetValue(eventKey, out var builtIn))
            throw new Shared.Errors.NotFoundException($"Email template '{eventKey}' not found.");

        return builtIn;
    }

    private static bool Matches(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

internal sealed record BuiltInEmailTemplate(
    string EventKey,
    string TemplateName,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyCollection<string> AvailablePlaceholders,
    IReadOnlyCollection<string> RequiredPlaceholders,
    bool SupportsTenantOverride);

internal sealed record StoredEmailTemplate(
    string EventKey,
    string TemplateName,
    string Subject,
    string HtmlBody,
    string TextBody,
    bool Enabled,
    string? SenderNameOverride,
    string? ReplyToOverride,
    Guid LastUpdatedBy,
    DateTime LastUpdatedAt);
