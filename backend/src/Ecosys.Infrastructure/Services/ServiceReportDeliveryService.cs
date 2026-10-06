using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Emails a work order's service report PDF to the client. The recipient is the contact saved on the
/// work order, falling back to the client's own email — the same rule the CSAT survey uses — with the
/// client email copied when the two differ. Callers render the PDF; this only resolves and queues.
/// </summary>
public interface IServiceReportDeliveryService
{
    /// <returns>The address the report was queued to.</returns>
    Task<string> QueueAsync(Guid tenantId, Guid workOrderId, byte[] pdf, Guid? triggeredByUserId, CancellationToken cancellationToken = default);
}

public sealed class ServiceReportDeliveryService(
    AppDbContext dbContext,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService) : IServiceReportDeliveryService
{
    public const string EventKey = "service-report.sent";

    public async Task<string> QueueAsync(Guid tenantId, Guid workOrderId, byte[] pdf, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        var workOrder = await dbContext.WorkOrders
            .AsNoTracking()
            .Include(x => x.Client)
            .Include(x => x.Site)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == workOrderId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var contactEmail = NormalizeEmail(workOrder.ClientContactEmail);
        var clientEmail = NormalizeEmail(workOrder.Client?.Email);
        var to = contactEmail ?? clientEmail
            ?? throw new BusinessRuleException("The report cannot be sent: neither the work order nor the client has a contact email. Add one to the client and try again.");
        var cc = clientEmail is not null && !string.Equals(clientEmail, to, StringComparison.OrdinalIgnoreCase)
            ? new[] { clientEmail }
            : [];

        var reportNumber = string.IsNullOrWhiteSpace(workOrder.ReportNumber) ? workOrder.WorkOrderNumber : workOrder.ReportNumber;
        var clientName = workOrder.Client?.ClientName ?? "Valued Client";
        var template = await emailTemplateService.RenderTenantTemplateAsync(
            tenantId,
            EventKey,
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ReportNumber"] = reportNumber,
                ["WorkOrderNumber"] = workOrder.WorkOrderNumber,
                ["Title"] = workOrder.Title,
                ["SiteName"] = workOrder.Site?.SiteName ?? "—",
                ["CompletionDate"] = workOrder.CompletedAt?.ToString("yyyy-MM-dd") ?? "—",
                ["ClientName"] = clientName,
            },
            cancellationToken);

        if (!template.Enabled)
        {
            throw new BusinessRuleException("Service report delivery is disabled in the email templates settings.");
        }

        var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(tenantId, EventKey, template.Subject, cancellationToken: cancellationToken);
        await emailOutboxService.QueueEmailAsync(
            new QueueEmailRequest(
                tenantId, EventKey, EventKey,
                to, clientName,
                template.SenderNameOverride, null, template.ReplyToOverride,
                subject, template.HtmlBody, template.TextBody, triggeredByUserId,
                Attachments: [new EmailAttachment($"{reportNumber}-service-report.pdf", pdf, "application/pdf")],
                CcAddresses: cc,
                DocumentType: "ServiceReport",
                DocumentId: workOrder.Id,
                TriggeredAutomatically: triggeredByUserId is null),
            cancellationToken);

        return to;
    }

    private static string? NormalizeEmail(string? value)
    {
        var trimmed = value?.Trim();
        return !string.IsNullOrEmpty(trimmed) && System.Net.Mail.MailAddress.TryCreate(trimmed, out _) ? trimmed : null;
    }
}
