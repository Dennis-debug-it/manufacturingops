using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
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

    /// <summary>Notification and email-template key for a sent quotation the client has not answered.</summary>
    public const string QuoteAwaitingAnswer = "crm.quote.awaiting-answer";

    /// <summary>Notification and email-template key for an open deal nobody has touched for the workspace's stall period.</summary>
    public const string DealStalled = "crm.deal.stalled";

    /// <summary>Notification and email-template key for the Monday summary of the past week's sales.</summary>
    public const string WeeklySummary = "crm.weekly-summary";
}

public sealed record SalesReminderRunResult(int FollowUpReminders, int TenderReminders, int QuoteReminders = 0, int RenewalDealsOpened = 0, int RenewalDealsClosed = 0, int StalledReminders = 0, int WeeklySummaries = 0, int SequenceSteps = 0);

public interface ISalesReminderService
{
    /// <summary>Opens and closes renewal deals, then sends every sales reminder due at <paramref name="utcNow"/>. Safe to run repeatedly.</summary>
    Task<SalesReminderRunResult> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reminds people about sales work with a date on it, by email and push to the person who owns it:
/// <list type="bullet">
/// <item>A follow-up logged on a deal, once its due time arrives, then once a day while it is still
/// open, three reminders at most. Marking it done, or closing the deal, stops them.</item>
/// <item>An RFP/RFQ not yet submitted, three days and again one day before its submission deadline.</item>
/// <item>A sent quotation the client has not answered: once it has waited three days, and again two
/// days before it expires. It goes to the owner of the deal it was raised from, else whoever sent it.</item>
/// <item>An open deal with nothing logged for the workspace's stall period (Sales settings), once per
/// quiet spell: any activity on the deal starts the clock again.</item>
/// <item>On Monday from 07:00 workspace time, a summary of the past seven days to everyone who works
/// the pipeline: their own results, and the team's too for admins and sales managers.</item>
/// </list>
/// Before reminding, it keeps renewal deals in step with contracts: a contract that enters its
/// renewal notice period gets a renewal deal (unless the workspace turned that off in Sales
/// settings), owned by the best available person (<see cref="ISalesOwnerResolver"/>) and with a
/// follow-up due straight away, so the owner hears about it in this same run. Renewing the contract
/// wins the deal; letting it expire, or terminating or cancelling it, loses it.
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
    ISalesOwnerResolver salesOwnerResolver,
    IRolePermissionService rolePermissionService,
    IDynamicAccessService dynamicAccessService,
    ILogger<SalesReminderService> logger) : ISalesReminderService
{
    public static readonly TimeSpan FollowUpRepeatInterval = TimeSpan.FromHours(24);
    public const int MaxFollowUpReminders = 3;
    public static readonly TimeSpan TenderFirstWarning = TimeSpan.FromDays(3);
    public static readonly TimeSpan TenderFinalWarning = TimeSpan.FromDays(1);
    public static readonly TimeSpan QuoteChaseAfter = TimeSpan.FromDays(3);
    public static readonly TimeSpan QuoteExpiryWarning = TimeSpan.FromDays(2);
    public const int DefaultStalledAfterDays = 14;

    private static readonly string[] TenderStatusesAwaitingSubmission = ["Received", "Reviewing", "Preparing"];
    private static readonly string[] ClosedDealStages = ["Won", "Lost"];

    private readonly Dictionary<Guid, bool> _crmEnabledByTenant = [];

    public const string RenewalSource = "Contract renewal";
    /// <summary>Most service contracts are renewed, so a renewal deal starts well ahead of a cold lead.</summary>
    public const int RenewalProbability = 60;

    private static readonly ContractStatus[] RenewableStatuses = [ContractStatus.Active, ContractStatus.ExpiringSoon, ContractStatus.RenewalInProgress];
    private static readonly ContractStatus[] LapsedStatuses = [ContractStatus.Expired, ContractStatus.Terminated, ContractStatus.Cancelled];

    public async Task<SalesReminderRunResult> RunAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var (opened, closed) = await SyncRenewalDealsAsync(utcNow, cancellationToken);
        // Sequence steps first, so a task a step adds is reminded in this same run.
        var sequenceSteps = await RunSequencesAsync(utcNow, cancellationToken);
        var followUps = await RemindFollowUpsAsync(utcNow, cancellationToken);
        var tenders = await RemindTenderDeadlinesAsync(utcNow, cancellationToken);
        var quotes = await RemindUnansweredQuotesAsync(utcNow, cancellationToken);
        var stalled = await RemindStalledDealsAsync(utcNow, cancellationToken);
        var summaries = await SendWeeklySummariesAsync(utcNow, cancellationToken);
        return new SalesReminderRunResult(followUps, tenders, quotes, opened, closed, stalled, summaries, sequenceSteps);
    }

    private async Task<int> RunSequencesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var tenantIds = await dbContext.SalesSequenceEnrollments.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Status == SalesSequenceStatuses.Active && x.NextDueAt != null && x.NextDueAt <= now)
            .Select(x => x.TenantId).Distinct().ToListAsync(cancellationToken);
        var enabled = new HashSet<Guid>();
        foreach (var tenantId in tenantIds)
            if (await CrmEnabledAsync(tenantId, cancellationToken))
                enabled.Add(tenantId);
        return enabled.Count == 0 ? 0 : await SalesSequences.RunDueAsync(dbContext, emailOutboxService, enabled, now, cancellationToken);
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

    private async Task<(int Opened, int Closed)> SyncRenewalDealsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var today = now.Date;
        // Notice periods differ per contract, so the window is narrowed to a year here and checked exactly below.
        var due = (await dbContext.Contracts
            .IgnoreQueryFilters()
            .Include(x => x.Client)
            .Where(x => !x.IsArchived && RenewableStatuses.Contains(x.Status) && x.RenewalNoticeDays > 0
                && x.EndDate >= today && x.EndDate <= today.AddDays(366)
                && !dbContext.SalesOpportunities.IgnoreQueryFilters().Any(o => o.TenantId == x.TenantId && o.RenewalOfContractId == x.Id))
            .OrderBy(x => x.EndDate)
            .Take(500)
            .ToListAsync(cancellationToken))
            .Where(x => x.EndDate.Date <= today.AddDays(x.RenewalNoticeDays))
            .ToList();

        // Each workspace's pipelines, loaded before anything is added (loading may create the default).
        var pipelinesByTenant = new Dictionary<Guid, IReadOnlyList<SalesPipelineView>>();
        foreach (var tenantId in due.Select(x => x.TenantId).Distinct())
            pipelinesByTenant[tenantId] = await SalesPipelineCatalog.LoadAsync(dbContext, tenantId, cancellationToken);

        var opened = 0;
        var settingsByTenant = new Dictionary<Guid, bool>();
        foreach (var contract in due)
        {
            if (!settingsByTenant.TryGetValue(contract.TenantId, out var enabled))
            {
                enabled = await dbContext.TenantSalesSettings.IgnoreQueryFilters().AsNoTracking()
                    .Where(x => x.TenantId == contract.TenantId)
                    .Select(x => (bool?)x.CreateRenewalDeals)
                    .FirstOrDefaultAsync(cancellationToken) ?? true;
                settingsByTenant[contract.TenantId] = enabled;
            }
            if (!enabled || !await CrmEnabledAsync(contract.TenantId, cancellationToken))
                continue;

            var owner = await salesOwnerResolver.ResolveAsync(
                contract.TenantId, [contract.RenewalOwnerUserId, contract.AccountManagerUserId], contract.ClientId,
                new SalesLeadFacts(RenewalSource, null, contract.Client?.Location), cancellationToken);
            var client = contract.Client?.ClientName ?? "the client";
            // Renewals go to a pipeline kept for maintenance contracts where there is one.
            var pipelines = pipelinesByTenant[contract.TenantId];
            var pipeline = pipelines.FirstOrDefault(x => x.IsActive && string.Equals(x.Module, ServiceOpsPlans.Maintenance, StringComparison.OrdinalIgnoreCase))
                ?? SalesPipelineCatalog.Default(pipelines);
            var stage = pipeline.Find("Qualified") ?? pipeline.FirstOpen;
            var deal = new SalesOpportunity
            {
                TenantId = contract.TenantId,
                Title = $"Renewal: {contract.Name}",
                Stage = stage.Key,
                PipelineId = pipeline.Id,
                ClientId = contract.ClientId,
                Source = RenewalSource,
                Description = $"Contract {contract.ContractNumber} ends on {contract.EndDate:d MMM yyyy}.",
                EstimatedValue = contract.ContractValue ?? 0,
                Currency = string.IsNullOrWhiteSpace(contract.Currency) ? "KES" : contract.Currency,
                Probability = RenewalProbability,
                ExpectedCloseDate = DateTime.SpecifyKind(contract.EndDate.Date, DateTimeKind.Utc),
                OwnerUserId = owner?.Id,
                OwnerName = owner?.FullName,
                RenewalOfContractId = contract.Id,
                LastActivityAt = now,
            };
            dbContext.SalesOpportunities.Add(deal);
            dbContext.SalesActivities.Add(new SalesActivity
            {
                TenantId = deal.TenantId, OpportunityId = deal.Id, Type = "System", OccurredAt = now,
                Summary = $"Renewal deal opened: contract {contract.ContractNumber} ends on {contract.EndDate:d MMM yyyy}.",
            });
            dbContext.SalesActivities.Add(new SalesActivity
            {
                TenantId = deal.TenantId, OpportunityId = deal.Id, Type = "FollowUp", OccurredAt = now, DueAt = now,
                Summary = $"Talk to {client} about renewing {contract.Name}.",
            });
            opened++;
        }
        if (opened > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        // Contracts that have since been renewed or have lapsed settle their open renewal deals.
        var settled = await dbContext.SalesOpportunities
            .IgnoreQueryFilters()
            .Where(x => x.RenewalOfContractId != null && x.Stage != "Won" && x.Stage != "Lost")
            .Join(dbContext.Contracts.IgnoreQueryFilters(), deal => deal.RenewalOfContractId, contract => contract.Id, (deal, contract) => new { deal, contract })
            .Where(x => x.contract.TenantId == x.deal.TenantId
                && (x.contract.Status == ContractStatus.Renewed || LapsedStatuses.Contains(x.contract.Status)))
            .Take(200)
            .ToListAsync(cancellationToken);
        foreach (var item in settled)
        {
            var deal = item.deal;
            var contract = item.contract;
            string summary;
            if (contract.Status == ContractStatus.Renewed)
            {
                var renewedValue = contract.RenewedToContractId is null ? null : await dbContext.Contracts.IgnoreQueryFilters()
                    .Where(x => x.Id == contract.RenewedToContractId)
                    .Select(x => new { x.ContractValue, x.Currency })
                    .FirstOrDefaultAsync(cancellationToken);
                deal.Stage = "Won";
                deal.Probability = 100;
                deal.WonAt = now;
                if (renewedValue?.ContractValue is > 0)
                {
                    deal.EstimatedValue = renewedValue.ContractValue.Value;
                    deal.Currency = renewedValue.Currency;
                }
                summary = $"Contract {contract.ContractNumber} was renewed. Deal marked Won.";
            }
            else
            {
                deal.Stage = "Lost";
                deal.Probability = 0;
                deal.LostAt = now;
                deal.LostReason = $"Contract {contract.ContractNumber} {contract.Status.ToString().ToLowerInvariant()} without renewal.";
                summary = deal.LostReason + " Deal marked Lost.";
            }
            deal.LastActivityAt = now;
            dbContext.SalesActivities.Add(new SalesActivity
            {
                TenantId = deal.TenantId, OpportunityId = deal.Id, Type = "System", OccurredAt = now, Summary = summary,
            });
        }
        if (settled.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        return (opened, settled.Count);
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

    private async Task<int> SendWeeklySummariesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var tenantIds = await dbContext.SalesOpportunities.IgnoreQueryFilters().AsNoTracking()
            .Select(x => x.TenantId).Distinct().ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var tenantId in tenantIds)
        {
            var local = await tenantTimeZoneService.ToTenantTimeAsync(now, tenantId, cancellationToken);
            if (local.DayOfWeek != DayOfWeek.Monday || local.Hour < WeeklySummaryHour)
                continue;
            var week = DateTime.SpecifyKind(local.Date, DateTimeKind.Utc);
            var settings = await dbContext.TenantSalesSettings.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
            if (settings is { SendWeeklySummary: false } || settings?.WeeklySummaryWeek == week)
                continue;
            if (!await CrmEnabledAsync(tenantId, cancellationToken))
                continue;

            // Marked first, so a failure while sending never repeats the batch.
            if (settings is null)
            {
                settings = new TenantSalesSettings { TenantId = tenantId };
                dbContext.TenantSalesSettings.Add(settings);
            }
            settings.WeeklySummaryWeek = week;
            await dbContext.SaveChangesAsync(cancellationToken);

            var from = now.AddDays(-7);
            var converter = await SalesCurrencyConverter.LoadAsync(dbContext, tenantId, cancellationToken);
            var stalledAfter = settings.StalledAfterDays;
            var deals = (await dbContext.SalesOpportunities.IgnoreQueryFilters().AsNoTracking()
                    .Where(x => x.TenantId == tenantId)
                    .Select(x => new { x.OwnerUserId, x.Stage, x.EstimatedValue, x.Currency, x.CreatedAt, x.WonAt, x.LostAt, Touched = x.LastActivityAt ?? x.CreatedAt })
                    .ToListAsync(cancellationToken))
                .Select(x => new WeekDeal(x.OwnerUserId, x.Stage, converter.Convert(x.EstimatedValue, x.Currency) ?? 0, x.CreatedAt >= from,
                    x.Stage == "Won" && x.WonAt >= from, x.Stage == "Lost" && x.LostAt >= from,
                    !ClosedDealStages.Contains(x.Stage) && stalledAfter > 0 && x.Touched <= now.AddDays(-stalledAfter)))
                .ToList();
            var overdue = await dbContext.SalesActivities.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.DueAt < now && x.CompletedAt == null && x.Opportunity != null && !ClosedDealStages.Contains(x.Opportunity.Stage))
                .Select(x => x.Opportunity!.OwnerUserId)
                .ToListAsync(cancellationToken);

            var weekLabel = $"{local.Date.AddDays(-7):d MMM} – {local.Date.AddDays(-1):d MMM yyyy}";
            foreach (var person in await SalesOwnerDirectory.ListAsync(dbContext, rolePermissionService, tenantId, cancellationToken))
            {
                var recipient = await LoadRecipientAsync(tenantId, person.Id, cancellationToken);
                if (recipient is null)
                    continue;
                var mine = WeekFigures.For(deals.Where(x => x.OwnerUserId == person.Id), overdue.Count(x => x == person.Id), converter.ReportingCurrency);
                var seesTeam = string.Equals(person.Role, Shared.Auth.AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
                    || (await dynamicAccessService.GetEffectiveAccessAsync(tenantId, person.Id, cancellationToken))
                        .Any(x => x.Allowed && string.Equals(x.Capability, Shared.Auth.PermissionNames.CrmViewAll, StringComparison.OrdinalIgnoreCase));
                var team = seesTeam ? WeekFigures.For(deals, overdue.Count, converter.ReportingCurrency) : null;

                await DeliverAsync(recipient, SalesReminderKeys.WeeklySummary,
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["OwnerName"] = recipient.FullName,
                        ["Week"] = weekLabel,
                        ["Summary"] = mine.Text,
                        ["TeamSummary"] = team is null ? string.Empty : $"Whole team: {team.Text}",
                        ["PipelineUrl"] = AbsoluteUrl("/crm"),
                    },
                    new PushMessage(
                        Title: $"Your sales week: {mine.WonCount} won",
                        Body: mine.Text,
                        Url: "/crm/reports",
                        Tag: $"crm-weekly-{week:yyyyMMdd}"),
                    cancellationToken);
                sent++;
            }
        }
        return sent;
    }

    public const int WeeklySummaryHour = 7;

    private sealed record WeekDeal(Guid? OwnerUserId, string Stage, decimal Value, bool New, bool WonThisWeek, bool LostThisWeek, bool Stalled);

    private sealed record WeekFigures(int WonCount, string Text)
    {
        public static WeekFigures For(IEnumerable<WeekDeal> deals, int overdueFollowUps, string currency)
        {
            var list = deals.ToList();
            var won = list.Where(x => x.WonThisWeek).ToList();
            var open = list.Where(x => !ClosedDealStages.Contains(x.Stage)).ToList();
            var text = $"{won.Count} won ({currency} {won.Sum(x => x.Value):N0}), {list.Count(x => x.LostThisWeek)} lost, {list.Count(x => x.New)} new. "
                + $"Open pipeline: {open.Count} deals, {currency} {open.Sum(x => x.Value):N0}. "
                + $"Overdue follow-ups: {overdueFollowUps}. Gone quiet: {open.Count(x => x.Stalled)}.";
            return new WeekFigures(won.Count, text);
        }
    }

    private async Task<int> RemindStalledDealsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var thresholds = await dbContext.TenantSalesSettings.IgnoreQueryFilters().AsNoTracking()
            .ToDictionaryAsync(x => x.TenantId, x => x.StalledAfterDays, cancellationToken);
        var shortest = thresholds.Values.Where(x => x > 0).DefaultIfEmpty(DefaultStalledAfterDays).Min();
        shortest = Math.Min(shortest, DefaultStalledAfterDays);
        var quietSince = now.AddDays(-shortest);

        var candidates = await dbContext.SalesOpportunities
            .IgnoreQueryFilters()
            .Include(x => x.Client)
            .Where(x => !ClosedDealStages.Contains(x.Stage) && x.OwnerUserId != null
                && (x.LastActivityAt ?? x.CreatedAt) <= quietSince
                && (x.StallReminderSentAt == null || x.StallReminderSentAt < (x.LastActivityAt ?? x.CreatedAt)))
            .OrderBy(x => x.LastActivityAt ?? x.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var deal in candidates)
        {
            var threshold = thresholds.TryGetValue(deal.TenantId, out var days) ? days : DefaultStalledAfterDays;
            var lastTouched = deal.LastActivityAt ?? deal.CreatedAt;
            if (threshold <= 0 || lastTouched > now.AddDays(-threshold))
                continue;
            if (!await CrmEnabledAsync(deal.TenantId, cancellationToken))
                continue;

            deal.StallReminderSentAt = now;
            var owner = await LoadRecipientAsync(deal.TenantId, deal.OwnerUserId!.Value, cancellationToken);
            if (owner is null)
                continue;

            var idleDays = (int)(now - lastTouched).TotalDays;
            var url = $"/crm/opportunities/{deal.Id}";
            var customer = deal.Client?.ClientName ?? deal.CompanyName ?? deal.ContactName ?? "—";
            await DeliverAsync(owner, SalesReminderKeys.DealStalled,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["OwnerName"] = owner.FullName,
                    ["OpportunityTitle"] = deal.Title,
                    ["CustomerName"] = customer,
                    ["Stage"] = deal.Stage,
                    ["IdleDays"] = idleDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["OpportunityUrl"] = AbsoluteUrl(url),
                },
                new PushMessage(
                    Title: $"Gone quiet: {deal.Title}",
                    Body: $"{customer} · nothing logged for {idleDays} days",
                    Url: url,
                    Tag: $"crm-stalled-{deal.Id}"),
                cancellationToken);
            sent++;
        }

        if (candidates.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return sent;
    }

    private async Task<int> RemindUnansweredQuotesAsync(DateTime now, CancellationToken cancellationToken)
    {
        var chaseSentBefore = now - QuoteChaseAfter;
        var expiryWarningFrom = now + QuoteExpiryWarning;
        var candidates = await dbContext.TenantQuotations
            .IgnoreQueryFilters()
            .Include(x => x.Client)
            .Include(x => x.SalesOpportunity)
            .Where(x => x.Status == "Sent" && x.ChaseReminderStage < 2 && x.ValidUntil > now
                && ((x.SentAt ?? x.QuoteDate) <= chaseSentBefore || x.ValidUntil <= expiryWarningFrom))
            .OrderBy(x => x.ValidUntil)
            .Take(500)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var quote in candidates)
        {
            // One reminder per window: a quote already close to expiry gets only the final one.
            var stage = quote.ValidUntil <= expiryWarningFrom ? 2 : 1;
            if (quote.ChaseReminderStage >= stage)
                continue;
            if (!await CrmEnabledAsync(quote.TenantId, cancellationToken))
                continue;

            quote.ChaseReminderStage = stage;
            var ownerId = quote.SalesOpportunity?.OwnerUserId ?? quote.SentByUserId;
            var owner = ownerId is null ? null : await LoadRecipientAsync(quote.TenantId, ownerId.Value, cancellationToken);
            if (owner is null)
                continue;

            const string url = "/crm/follow-ups";
            var waitingDays = Math.Max(0, (int)(now - (quote.SentAt ?? quote.QuoteDate)).TotalDays);
            var client = quote.Client?.ClientName ?? "—";
            var amount = $"{quote.Currency} {quote.TotalAmount:N2}";
            var expiresIn = DescribeDueIn(quote.ValidUntil - now);
            var validUntil = await tenantTimeZoneService.FormatForTenantAsync(quote.ValidUntil, quote.TenantId, cancellationToken: cancellationToken);

            await DeliverAsync(owner, SalesReminderKeys.QuoteAwaitingAnswer,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["OwnerName"] = owner.FullName,
                    ["QuoteNumber"] = quote.QuoteNumber,
                    ["ClientName"] = client,
                    ["Amount"] = amount,
                    ["WaitingDays"] = waitingDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["ExpiresIn"] = expiresIn,
                    ["ValidUntil"] = validUntil,
                    ["OpportunityTitle"] = quote.SalesOpportunity?.Title ?? "—",
                    ["FollowUpsUrl"] = AbsoluteUrl(url),
                },
                new PushMessage(
                    Title: stage == 2 ? $"Quote {quote.QuoteNumber} expires {expiresIn}" : $"No answer yet on quote {quote.QuoteNumber}",
                    Body: $"{client} · {amount} · sent {waitingDays} day{(waitingDays == 1 ? "" : "s")} ago",
                    Url: url,
                    Tag: $"crm-quote-{quote.Id}"),
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
                if (result.WeeklySummaries > 0)
                    logger.LogInformation("Weekly sales summaries sent: {Count}.", result.WeeklySummaries);
                if (result.RenewalDealsOpened + result.RenewalDealsClosed > 0)
                    logger.LogInformation("Renewal deals: {Opened} opened, {Closed} settled from their contracts.", result.RenewalDealsOpened, result.RenewalDealsClosed);
                if (result.FollowUpReminders + result.TenderReminders + result.QuoteReminders + result.StalledReminders > 0)
                    logger.LogInformation("Sales reminders sent: {FollowUps} follow-up(s), {Tenders} RFP/RFQ deadline(s), {Quotes} unanswered quote(s), {Stalled} stalled deal(s).", result.FollowUpReminders, result.TenderReminders, result.QuoteReminders, result.StalledReminders);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Sales reminder cycle failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
