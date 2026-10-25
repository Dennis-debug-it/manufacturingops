using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

public static class SalesReminderKeys
{
    /// <summary>Notification and email-template key for a deal follow-up that has come due.</summary>
    public const string FollowUpDue = "crm.follow-up.due";

    /// <summary>Notification and email-template key for an RFP/RFQ whose submission deadline is near.</summary>
    public const string TenderDeadline = "crm.tender.deadline";
}

public sealed record SalesReminderRunResult(int FollowUpReminders, int TenderReminders);

public interface ISalesReminderService
{
    /// <summary>Sends every sales reminder that is due at <paramref name="utcNow"/>. Safe to run repeatedly.</summary>
    Task<SalesReminderRunResult> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reminds people about sales work with a date on it, by email and push to the person who owns it:
/// <list type="bullet">
/// <item>A follow-up logged on a deal, once its due time arrives, then once a day while it is still
/// open, three reminders at most. Marking it done, or closing the deal, stops them.</item>
/// <item>An RFP/RFQ not yet submitted, three days and again one day before its submission deadline.</item>
/// </list>
/// Each person's notification settings are honoured per channel. Only workspaces whose plan includes
/// the sales CRM (Suite) are reminded, and the reminder state is saved per item so a restart, a second
/// instance or an hourly re-run never sends the same reminder twice.
/// </summary>
internal sealed class SalesReminderService(
    AppDbContext dbContext,
    ILicenseGuardService licenseGuardService,
    ITenantTimeZoneService tenantTimeZoneService,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService,
    IPushNotificationQueue pushNotificationQueue,
    IOptions<PublicAppOptions> publicAppOptions,
    ILogger<SalesReminderService> logger) : ISalesReminderService
{
    public static readonly TimeSpan FollowUpRepeatInterval = TimeSpan.FromHours(24);
    public const int MaxFollowUpReminders = 3;
    public static readonly TimeSpan TenderFirstWarning = TimeSpan.FromDays(3);
    public static readonly TimeSpan TenderFinalWarning = TimeSpan.FromDays(1);

    private static readonly string[] TenderStatusesAwaitingSubmission = ["Received", "Reviewing", "Preparing"];
    private static readonly string[] ClosedDealStages = ["Won", "Lost"];

    private readonly Dictionary<Guid, bool> _crmEnabledByTenant = [];

    public async Task<SalesReminderRunResult> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var followUps = await RemindFollowUpsAsync(utcNow, cancellationToken);
        var tenders = await RemindTenderDeadlinesAsync(utcNow, cancellationToken);
        return new SalesReminderRunResult(followUps, tenders);
    }

    private async Task<int> RemindFollowUpsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var repeatBefore = now - FollowUpRepeatInterval;
        var due = await dbContext.SalesActivities
            .IgnoreQueryFilters()
            .Include(x => x.Opportunity!).ThenInclude(x => x.Client)
            .Where(x => x.DueAt != null && x.DueAt <= now && x.CompletedAt == null
                && x.ReminderCount < MaxFollowUpReminders
                && (x.ReminderSentAt == null || x.ReminderSentAt <= repeatBefore)
                && x.Opportunity != null && !ClosedDealStages.Contains(x.Opportunity.Stage))
            .OrderBy(x => x.DueAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var activity in due)
        {
            var deal = activity.Opportunity!;
            if (!await CrmEnabledAsync(activity.TenantId, cancellationToken))
                continue;

            // Recorded first, so a failure while delivering is never retried into a burst of duplicates.
            activity.ReminderSentAt = now;
            activity.ReminderCount += 1;

            var ownerId = deal.OwnerUserId ?? activity.LoggedByUserId;
            var owner = ownerId is null ? null : await LoadRecipientAsync(activity.TenantId, ownerId.Value, cancellationToken);
            if (owner is null)
                continue;

            var url = $"/crm/opportunities/{deal.Id}";
            var customer = deal.Client?.ClientName ?? deal.CompanyName ?? deal.ContactName ?? "—";
            var overdue = activity.ReminderCount > 1;
            var dueText = await tenantTimeZoneService.FormatForTenantAsync(activity.DueAt!.Value, activity.TenantId, cancellationToken: cancellationToken);

            await DeliverAsync(owner, SalesReminderKeys.FollowUpDue,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["OwnerName"] = owner.FullName,
                    ["OpportunityTitle"] = deal.Title,
                    ["CustomerName"] = customer,
                    ["FollowUp"] = activity.Summary,
                    ["DueAt"] = dueText,
                    ["Stage"] = deal.Stage,
                    ["OpportunityUrl"] = AbsoluteUrl(url),
                },
                new PushMessage(
                    Title: overdue ? $"Follow-up overdue: {deal.Title}" : $"Follow-up due: {deal.Title}",
                    Body: $"{activity.Summary} · {customer}",
                    Url: url,
                    Tag: $"crm-follow-up-{activity.Id}"),
                cancellationToken);
            sent++;
        }

        if (due.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return sent;
    }

    private async Task<int> RemindTenderDeadlinesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var firstWarningFrom = now + TenderFirstWarning;
        var candidates = await dbContext.SalesTenders
            .IgnoreQueryFilters()
            .Include(x => x.Client)
            .Where(x => TenderStatusesAwaitingSubmission.Contains(x.Status)
                && x.SubmissionDeadline > now && x.SubmissionDeadline <= firstWarningFrom
                && x.DeadlineReminderStage < 2)
            .OrderBy(x => x.SubmissionDeadline)
            .Take(500)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var tender in candidates)
        {
            var remaining = tender.SubmissionDeadline - now;
            // One reminder per window: a request logged with a day to go gets only the final warning.
            var stage = remaining <= TenderFinalWarning ? 2 : 1;
            if (tender.DeadlineReminderStage >= stage)
                continue;
            if (!await CrmEnabledAsync(tender.TenantId, cancellationToken))
                continue;

            tender.DeadlineReminderStage = stage;
            if (tender.OwnerUserId is null)
                continue;
            var owner = await LoadRecipientAsync(tender.TenantId, tender.OwnerUserId.Value, cancellationToken);
            if (owner is null)
                continue;

            var url = $"/crm/tenders/{tender.Id}";
            var dueIn = DescribeDueIn(remaining);
            var deadlineText = await tenantTimeZoneService.FormatForTenantAsync(tender.SubmissionDeadline, tender.TenantId, cancellationToken: cancellationToken);
            var issuer = tender.Client?.ClientName ?? tender.IssuerName ?? "—";

            await DeliverAsync(owner, SalesReminderKeys.TenderDeadline,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["OwnerName"] = owner.FullName,
                    ["TenderType"] = tender.Type,
                    ["TenderTitle"] = tender.Title,
                    ["TenderReference"] = tender.Reference ?? "no reference",
                    ["IssuerName"] = issuer,
                    ["DueIn"] = dueIn,
                    ["Deadline"] = deadlineText,
                    ["SubmissionMethod"] = tender.SubmissionMethod ?? "Not recorded",
                    ["Status"] = tender.Status,
                    ["TenderUrl"] = AbsoluteUrl(url),
                },
                new PushMessage(
                    Title: $"{tender.Type} due {dueIn}: {tender.Title}",
                    Body: $"{issuer} · not submitted yet",
                    Url: url,
                    Tag: $"crm-tender-{tender.Id}"),
                cancellationToken);
            sent++;
        }

        if (candidates.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return sent;
    }

    /// <summary>"in 3 days", "tomorrow", "in 5 hours" — close enough for a nudge; the email carries the exact time.</summary>
    internal static string DescribeDueIn(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.FromHours(1)) return "within the hour";
        if (remaining < TimeSpan.FromHours(20)) return $"in {(int)Math.Ceiling(remaining.TotalHours)} hours";
        if (remaining < TimeSpan.FromHours(36)) return "tomorrow";
        return $"in {(int)Math.Round(remaining.TotalDays, MidpointRounding.AwayFromZero)} days";
    }

    private async Task<bool> CrmEnabledAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (_crmEnabledByTenant.TryGetValue(tenantId, out var enabled))
            return enabled;
        try
        {
            var snapshot = await licenseGuardService.GetSnapshotAsync(tenantId, cancellationToken);
            enabled = !snapshot.IsSuspended
                && ServiceOpsPlans.RequiredModules("SalesCrm", "/api/crm").All(module => snapshot.Modules.Contains(module, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not read the licence for tenant {TenantId}; skipping its sales reminders this cycle.", tenantId);
            enabled = false;
        }
        _crmEnabledByTenant[tenantId] = enabled;
        return enabled;
    }

    private sealed record Recipient(Guid TenantId, Guid UserId, string FullName, string? Email);

    private async Task<Recipient?> LoadRecipientAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == userId && x.IsActive)
            .Select(x => new { x.FullName, x.Email, x.HasEmail })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            return null;

        return new Recipient(tenantId, userId, user.FullName, user.HasEmail ? user.Email : null);
    }

    private async Task DeliverAsync(Recipient owner, string key, IReadOnlyDictionary<string, string?> values, PushMessage push, CancellationToken cancellationToken)
    {
        var preference = await dbContext.UserNotificationPreferences
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == owner.TenantId && x.UserId == owner.UserId && x.NotificationKey == key, cancellationToken);
        var wantsEmail = preference?.EmailEnabled ?? true;
        var wantsPush = preference?.InAppEnabled ?? true;

        if (wantsPush)
            pushNotificationQueue.Enqueue(owner.TenantId, [owner.UserId], push);

        if (!wantsEmail || string.IsNullOrWhiteSpace(owner.Email))
            return;
        try
        {
            var template = await emailTemplateService.RenderTenantTemplateAsync(owner.TenantId, key, values, cancellationToken);
            if (!template.Enabled)
                return;
            var subject = await emailSubjectRuleService.BuildFinalSubjectAsync(owner.TenantId, key, template.Subject, cancellationToken: cancellationToken);
            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(owner.TenantId, key, key, owner.Email!, owner.FullName,
                    template.SenderNameOverride, null, null, subject, template.HtmlBody, template.TextBody, null),
                cancellationToken);
        }
        catch (Exception exception)
        {
            // The push (if any) has gone; a broken template must not stop the rest of the batch.
            logger.LogWarning(exception, "Could not queue the {Key} email for user {UserId}.", key, owner.UserId);
        }
    }

    private string AbsoluteUrl(string path) => $"{publicAppOptions.Value.PublicUrl.TrimEnd('/')}{path}";
}

/// <summary>Runs <see cref="ISalesReminderService"/> every hour on a single instance.</summary>
public sealed class SalesReminderWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<SalesReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Sales reminder worker started.");
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISalesReminderService>().RunAsync(DateTime.UtcNow, stoppingToken);
                if (result.FollowUpReminders + result.TenderReminders > 0)
                    logger.LogInformation("Sales reminders sent: {FollowUps} follow-up(s), {Tenders} RFP/RFQ deadline(s).", result.FollowUpReminders, result.TenderReminders);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Sales reminder cycle failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
