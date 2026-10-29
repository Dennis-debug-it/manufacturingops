using System.Net;
using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The tag in the subject of every email sent from a deal ("[Deal D-1A2B3C4D]"). Replies keep it, so
/// the intake mailbox can put them back on the deal; it is the first 8 hex digits of the deal's id.
/// </summary>
public static partial class SalesDealTags
{
    public static string For(Guid opportunityId) => $"[Deal D-{opportunityId.ToString("N")[..8].ToUpperInvariant()}]";

    /// <summary>The id prefix in a subject's tag (lower case), or null.</summary>
    public static string? Find(string? subject)
    {
        var match = TagPattern().Match(subject ?? string.Empty);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    public static string WithTag(string subject, Guid opportunityId)
    {
        var tag = For(opportunityId);
        return subject.Contains(tag, StringComparison.OrdinalIgnoreCase) ? subject : $"{subject.Trim()} {tag}";
    }

    [GeneratedRegex(@"\[Deal D-([0-9A-Fa-f]{8})\]")]
    private static partial Regex TagPattern();
}

public static class SalesEmailKeys
{
    /// <summary>An email a person writes on a deal (also a sequence's email steps). No template: the sender writes it.</summary>
    public const string DealEmail = "crm.deal.email";
    /// <summary>Tells the owner a lead came in from the website form or the sales mailbox.</summary>
    public const string LeadReceived = "crm.lead.received";
}

public static class SalesActivityKinds
{
    public const string EmailReceived = "EmailReceived";
}

/// <summary>Queues an email to a deal's contact and logs it on the deal.</summary>
public static class SalesDealEmail
{
    /// <summary>
    /// Replies go to the intake mailbox when the workspace has one, so they land back on the deal (see
    /// <see cref="SalesDealTags"/>); otherwise straight to the sender.
    /// </summary>
    public static async Task<SalesActivity> QueueAsync(
        AppDbContext dbContext, IEmailOutboxService outbox, SalesOpportunity opportunity,
        string to, string? toName, IReadOnlyCollection<string>? cc, string subject, string body,
        Guid? senderUserId, string? senderName, bool automatic, Guid? sequenceEnrollmentId, CancellationToken cancellationToken)
    {
        var recipient = to.Trim();
        if (!System.Net.Mail.MailAddress.TryCreate(recipient, out _))
            throw new BusinessRuleException($"'{to}' is not an email address.");
        if (string.IsNullOrWhiteSpace(subject)) throw new BusinessRuleException("Give the email a subject.");
        if (string.IsNullOrWhiteSpace(body)) throw new BusinessRuleException("Write the email.");
        var copies = (cc ?? []).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        if (copies.Any(x => !System.Net.Mail.MailAddress.TryCreate(x, out _)))
            throw new BusinessRuleException("One of the copy addresses is not an email address.");

        var tenantId = opportunity.TenantId;
        var intake = await dbContext.EmailIntakeSettings.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsEnabled && x.IntakeEmailAddress != null)
            .Select(x => x.IntakeEmailAddress)
            .FirstOrDefaultAsync(cancellationToken);
        var senderEmail = senderUserId is null ? null : await dbContext.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == senderUserId)
            .Select(x => x.Email)
            .FirstOrDefaultAsync(cancellationToken);
        var replyTo = intake ?? senderEmail;
        var taggedSubject = SalesDealTags.WithTag(subject.Trim(), opportunity.Id);
        var text = body.Trim();
        var html = string.Join("<br>", WebUtility.HtmlEncode(text).Split('\n').Select(x => x.TrimEnd('\r')));

        await outbox.QueueEmailAsync(new QueueEmailRequest(
            tenantId, SalesEmailKeys.DealEmail, SalesEmailKeys.DealEmail, recipient, toName,
            senderName, null, replyTo, taggedSubject, $"<div style=\"font-family:sans-serif;font-size:14px\">{html}</div>", text,
            senderUserId, CcAddresses: copies.Count == 0 ? null : copies,
            DocumentType: nameof(SalesOpportunity), DocumentId: opportunity.Id, TriggeredAutomatically: automatic), cancellationToken);

        var now = DateTime.UtcNow;
        var activity = new SalesActivity
        {
            TenantId = tenantId,
            OpportunityId = opportunity.Id,
            Type = "Email",
            Summary = Truncate($"Emailed {(string.IsNullOrWhiteSpace(toName) ? recipient : $"{toName} <{recipient}>")}: {subject.Trim()}\n\n{text}", 2000),
            OccurredAt = now,
            LoggedByUserId = senderUserId,
            LoggedByName = senderName,
            SequenceEnrollmentId = sequenceEnrollmentId,
        };
        dbContext.SalesActivities.Add(activity);
        opportunity.LastActivityAt = now;
        return activity;
    }

    public static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}

/// <summary>An email the intake mailbox received, as the sales side needs it.</summary>
public sealed record SalesInboundEmail(
    string FromAddress, string? FromName, IReadOnlyCollection<string> Recipients, string Subject, string Body, DateTime ReceivedAt);

/// <param name="Outcome">How the intake log records it (e.g. "AddedToDeal", "LeadCreated").</param>
public sealed record SalesInboundEmailResult(string Outcome, string Detail, Guid? OpportunityId, Guid? ClientId);

/// <summary>
/// Lets the email intake hand sales email to the CRM before it is treated as a service request: a reply
/// carrying a deal tag goes onto that deal, and mail to the workspace's lead address becomes a lead.
/// Returns null for everything else, which the intake then handles as usual.
/// </summary>
public interface ISalesInboundEmailHandler
{
    Task<SalesInboundEmailResult?> TryHandleAsync(Guid tenantId, SalesInboundEmail email, CancellationToken cancellationToken = default);
}

public static class SalesInboundOutcomes
{
    public const string AddedToDeal = "AddedToDeal";
    public const string LeadCreated = "LeadCreated";
}
