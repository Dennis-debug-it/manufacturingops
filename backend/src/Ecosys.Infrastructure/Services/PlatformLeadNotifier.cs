using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public interface IPlatformLeadNotifier
{
    /// <summary>
    /// Emails the platform owner about a new lead (a demo request or a self-service trial). Best
    /// effort: a missing platform mailbox or a failed send never fails the caller.
    /// </summary>
    Task TryNotifyAsync(PlatformLead lead, Guid? triggeredByUserId, CancellationToken cancellationToken = default);
}

internal sealed class PlatformLeadNotifier(
    AppDbContext dbContext,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService,
    ILogger<PlatformLeadNotifier> logger) : IPlatformLeadNotifier
{
    public async Task TryNotifyAsync(PlatformLead lead, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        var platformEmail = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == PlatformConstants.RootTenantId, cancellationToken);
        var ownerEmail = NormalizeOptional(platformEmail?.ReplyToEmail) ?? NormalizeOptional(platformEmail?.SenderAddress);
        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            return;
        }

        try
        {
            if (platformEmail is null || string.IsNullOrWhiteSpace(platformEmail.Host))
            {
                return;
            }

            var template = await emailTemplateService.RenderPlatformTemplateAsync(
                "workspace-request-received",
                EmailTemplateVariables.WithRecipientAndActorAliases(
                    lead.ContactPersonName,
                    lead.ContactPersonName,
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["companyName"] = lead.CompanyName,
                        ["leadCompanyName"] = lead.CompanyName,
                        ["email"] = lead.Email,
                        ["contactEmail"] = lead.Email,
                        ["phone"] = lead.Phone,
                        ["contactPhone"] = lead.Phone,
                        ["country"] = lead.Country ?? "Not provided",
                        ["industry"] = lead.Industry ?? "Not provided",
                        ["message"] = lead.Message ?? "Not provided",
                        ["contactName"] = lead.ContactPersonName,
                        ["submittedAt"] = lead.CreatedAt.ToString("u"),
                        ["platformName"] = "Ecosys",
                    }),
                cancellationToken);
            var finalSubject = await emailSubjectRuleService.BuildFinalSubjectAsync(
                PlatformConstants.RootTenantId,
                "platform.lead.received",
                template.Subject,
                cancellationToken: cancellationToken);

            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(
                    PlatformConstants.RootTenantId,
                    "platform.lead.received",
                    "workspace-request-received",
                    ownerEmail,
                    null,
                    template.SenderNameOverride ?? platformEmail.SenderName,
                    platformEmail.SenderAddress,
                    template.ReplyToOverride ?? lead.Email,
                    finalSubject,
                    template.HtmlBody,
                    template.TextBody,
                    triggeredByUserId),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to send platform lead notification email for lead {LeadId}.", lead.Id);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
