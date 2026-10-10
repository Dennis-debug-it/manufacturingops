using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Ecosys.Infrastructure.Services;

public static class EmailIntakeOutcomes
{
    public const string WorkOrderCreated = "WorkOrderCreated";
    public const string AddedToWorkOrder = "AddedToWorkOrder";
    public const string Rejected = "Rejected";
    public const string Ignored = "Ignored";
    public const string Failed = "Failed";
}

public static class EmailIntakeTemplates
{
    public const string Received = "email-intake-received";
    public const string ReplyAdded = "email-intake-reply-added";
    public const string Unrecognised = "email-intake-unrecognised";
}

public interface IEmailIntakeService
{
    /// <summary>Reads every enabled workspace mailbox once.</summary>
    Task PollAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads one workspace's mailbox. Returns how many new emails were handled.</summary>
    Task<int> PollTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Turns one email into a work order, a note on an existing one, or a recorded rejection.</summary>
    Task<EmailIntakeMessage?> ProcessMessageAsync(EmailIntakeSetting settings, MimeMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Email intake: each workspace connects a mailbox it already has; new emails become work orders.
/// A reply that names an existing work order number (sent by that job's client) is added to that job
/// instead. Emails from senders who aren't a known client are created against the default client
/// only when the workspace allows it; otherwise they're recorded as not recognised. Each outcome can
/// send an automatic reply from an editable template.
/// </summary>
internal sealed partial class EmailIntakeService(
    AppDbContext dbContext,
    IEmailIntakeMailbox mailbox,
    ISecretEncryptionService secretEncryptionService,
    ILicenseGuardService licenseGuardService,
    IDocumentNumberingService documentNumberingService,
    IEmailTemplateService emailTemplateService,
    IEmailOutboxService emailOutboxService,
    IAuditLogService auditLogService,
    ILogger<EmailIntakeService> logger) : IEmailIntakeService
{
    private const int MaxMessagesPerRead = 25;
    /// <summary>Automatic replies to one address per hour, so two auto-responders can't ping-pong.</summary>
    private const int MaxAutoRepliesPerSenderPerHour = 3;

    private static readonly HashSet<string> FreeMailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "yahoo.com", "yahoo.co.uk", "outlook.com", "hotmail.com", "live.com",
        "msn.com", "icloud.com", "me.com", "aol.com", "proton.me", "protonmail.com", "gmx.com", "zoho.com", "yandex.com",
    };

    /// <summary>Used when a workspace hasn't written its own priority keywords.</summary>
    public static readonly IReadOnlyList<(string Keyword, string Priority)> DefaultPriorityRules =
        [("emergency", WorkOrderPriorities.Critical), ("urgent", WorkOrderPriorities.High)];

    public async Task PollAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await dbContext.EmailIntakeSettings.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsEnabled && x.Host != "" && x.Username != null && x.EncryptedPassword != null)
            .Select(x => x.TenantId)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            try
            {
                await PollTenantAsync(tenantId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Email intake failed for tenant {TenantId}.", tenantId);
            }
            finally
            {
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    public async Task<int> PollTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var settings = await dbContext.EmailIntakeSettings.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (settings is null || !settings.IsEnabled || !await TenantAcceptsIntakeAsync(tenantId, cancellationToken))
        {
            return 0;
        }

        var password = secretEncryptionService.Decrypt(settings.EncryptedPassword);
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(password))
        {
            return 0;
        }

        // One read per mailbox at a time: the background check and "Check now" can overlap, and so
        // can two API instances.
        if (!await TryLockTenantAsync(tenantId, cancellationToken))
        {
            return 0;
        }

        try
        {
            return await ReadMailboxAsync(settings, password, cancellationToken);
        }
        finally
        {
            await UnlockTenantAsync(tenantId);
        }
    }

    private async Task<int> ReadMailboxAsync(EmailIntakeSetting settings, string password, CancellationToken cancellationToken)
    {
        var tenantId = settings.TenantId;
        EmailIntakeFetchResult fetched;
        try
        {
            fetched = await mailbox.FetchNewAsync(
                new EmailIntakeMailboxSettings(settings.Host, settings.Port, settings.UseSsl, settings.Username, password),
                settings.ImapUidValidity, settings.ImapLastUid, MaxMessagesPerRead, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            settings.LastCheckedAt = DateTime.UtcNow;
            settings.IsConnectionHealthy = false;
            settings.LastError = Truncate($"Could not read the mailbox: {ex.Message}", 2000);
            await dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }

        var handled = 0;
        foreach (var (uid, message) in fetched.Messages)
        {
            try
            {
                if (await ProcessMessageAsync(settings, message, cancellationToken) is not null)
                {
                    handled++;
                }
            }
            catch (DbUpdateException ex) when (GetOrCreateExtensions.IsUniqueViolation(ex))
            {
                // Already handled (recorded under the same Message-ID): nothing to do.
                dbContext.ChangeTracker.Clear();
                settings = await dbContext.EmailIntakeSettings.IgnoreQueryFilters().SingleAsync(x => x.TenantId == tenantId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Email intake could not process message {Uid} for tenant {TenantId}.", uid, tenantId);
                dbContext.ChangeTracker.Clear();
                settings = await dbContext.EmailIntakeSettings.IgnoreQueryFilters().SingleAsync(x => x.TenantId == tenantId, cancellationToken);
                await RecordAsync(settings, message, EmailIntakeOutcomes.Failed, Truncate(ex.Message, 1000), null, null, false, cancellationToken);
            }

            // Saved after every message, so a crash part-way never handles one twice.
            settings.ImapUidValidity = fetched.UidValidity;
            settings.ImapLastUid = uid;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        settings.ImapUidValidity = fetched.UidValidity;
        settings.ImapLastUid = fetched.LastUid;
        settings.LastCheckedAt = DateTime.UtcNow;
        settings.IsConnectionHealthy = true;
        settings.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return handled;
    }

    public async Task<EmailIntakeMessage?> ProcessMessageAsync(EmailIntakeSetting settings, MimeMessage message, CancellationToken cancellationToken = default)
    {
        var tenantId = settings.TenantId;
        var from = message.From.Mailboxes.FirstOrDefault() ?? message.Sender;
        var messageId = MessageIdOf(message, from);
        if (await dbContext.EmailIntakeMessages.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.MessageId == messageId, cancellationToken))
        {
            return null;
        }

        if (from is null || string.IsNullOrWhiteSpace(from.Address))
        {
            return await RecordAsync(settings, message, EmailIntakeOutcomes.Ignored, "The email has no sender address.", null, null, false, cancellationToken);
        }

        var address = from.Address.Trim().ToLowerInvariant();
        var localPart = address.Split('@')[0];
        if (localPart is "mailer-daemon" or "postmaster" || message.Body is MultipartReport)
        {
            return await RecordAsync(settings, message, EmailIntakeOutcomes.Ignored, "Delivery report (bounce), not a request.", null, null, false, cancellationToken);
        }

        // Out-of-office and other automatic replies (often answering our own auto-reply) are not requests.
        if (IsAutomaticReply(message))
        {
            return await RecordAsync(settings, message, EmailIntakeOutcomes.Ignored, "Automatic reply (e.g. out of office), not a request.", null, null, false, cancellationToken);
        }

        if (await IsOwnAddressAsync(settings, address, cancellationToken))
        {
            return await RecordAsync(settings, message, EmailIntakeOutcomes.Ignored, "Sent from this workspace's own address.", null, null, false, cancellationToken);
        }

        var canAutoReply = settings.AutoReplyEnabled
            && !IsAutomated(message, localPart)
            && await dbContext.EmailIntakeMessages.IgnoreQueryFilters().CountAsync(
                x => x.TenantId == tenantId && x.FromAddress == address && x.AutoReplySent && x.CreatedAt > DateTime.UtcNow.AddHours(-1), cancellationToken)
                < MaxAutoRepliesPerSenderPerHour;

        var subject = string.IsNullOrWhiteSpace(message.Subject) ? "(no subject)" : message.Subject.Trim();
        var body = BodyText(message);
        var senderLabel = string.IsNullOrWhiteSpace(from.Name) ? address : $"{from.Name} <{address}>";
        var match = await MatchClientAsync(tenantId, address, cancellationToken);

        // A reply about an existing job, from that job's client, is added to the job.
        var existing = await FindReferencedWorkOrderAsync(tenantId, subject, cancellationToken);
        if (existing is not null && match.ClientId == existing.ClientId)
        {
            dbContext.WorkOrderEvents.Add(new WorkOrderEvent
            {
                TenantId = tenantId,
                WorkOrderId = existing.Id,
                EventType = "EmailReceived",
                Message = Truncate($"Email from {senderLabel}: {subject}", 1000),
                Note = Truncate(StripQuotedReply(body), 2000),
                OccurredAt = message.Date == DateTimeOffset.MinValue ? DateTime.UtcNow : message.Date.UtcDateTime,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            var replied = canAutoReply && await AutoReplyAsync(tenantId, EmailIntakeTemplates.ReplyAdded, address, from.Name, subject, existing, cancellationToken);
            return await RecordAsync(settings, message, EmailIntakeOutcomes.AddedToWorkOrder, $"Added to {existing.WorkOrderNumber}.", existing.Id, existing.ClientId, replied, cancellationToken);
        }

        var clientId = match.ClientId;
        if (clientId is null)
        {
            var domain = address.Split('@').LastOrDefault() ?? string.Empty;
            var allowedDomains = SplitList(settings.AllowedSenderDomains);
            var domainAllowed = allowedDomains.Count == 0 || allowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase);
            if (settings.CreateWorkOrderFromUnknownSender && settings.DefaultClientId.HasValue && domainAllowed)
            {
                clientId = settings.DefaultClientId;
            }
            else
            {
                var reason = !settings.CreateWorkOrderFromUnknownSender
                    ? "The sender isn't a known client contact."
                    : !settings.DefaultClientId.HasValue
                        ? "The sender isn't a known client contact and no default client is set."
                        : $"The sender's domain ({domain}) isn't in the allowed list.";
                var replied = canAutoReply && await AutoReplyAsync(tenantId, EmailIntakeTemplates.Unrecognised, address, from.Name, subject, null, cancellationToken);
                return await RecordAsync(settings, message, EmailIntakeOutcomes.Rejected, reason, null, null, replied, cancellationToken);
            }
        }

        if (!await dbContext.Clients.IgnoreQueryFilters().AnyAsync(x => x.Id == clientId && x.TenantId == tenantId && x.IsActive, cancellationToken))
        {
            return await RecordAsync(settings, message, EmailIntakeOutcomes.Rejected, "The matching client is inactive.", null, clientId, false, cancellationToken);
        }

        var workOrder = new WorkOrder
        {
            TenantId = tenantId,
            ClientId = clientId.Value,
            SiteId = match.ClientId == clientId ? match.SiteId : null,
            BranchId = settings.DefaultBranchId,
            AssignmentGroupId = settings.DefaultAssignmentGroupId,
            WorkOrderNumber = await documentNumberingService.GenerateAsync(tenantId, settings.DefaultBranchId, DocumentTypes.WorkOrder, cancellationToken),
            Title = Truncate(CleanSubject(subject), 200),
            Description = Truncate(string.IsNullOrWhiteSpace(body)
                ? $"Received by email from {senderLabel}."
                : $"{body}\n\n(Received by email from {senderLabel}.)", 4000),
            Priority = PriorityFor(settings, subject, body),
            Status = WorkOrderStatuses.New,
            WorkType = WorkOrderTypes.CorrectiveMaintenance,
        };
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            tenantId,
            null,
            "email-intake.work-order.created",
            nameof(WorkOrder),
            workOrder.Id.ToString(),
            $"Work order '{workOrder.WorkOrderNumber}' created from an email by '{address}'.",
            actorName: from.Name ?? address,
            cancellationToken: cancellationToken);

        var sent = canAutoReply && await AutoReplyAsync(tenantId, EmailIntakeTemplates.Received, address, from.Name, subject, workOrder, cancellationToken);
        return await RecordAsync(settings, message, EmailIntakeOutcomes.WorkOrderCreated, $"Created {workOrder.WorkOrderNumber}.", workOrder.Id, clientId, sent, cancellationToken);
    }

    private async Task<bool> TryLockTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational()) return true;
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@key)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = PostgresAdvisoryLockLease.KeyFor($"email-intake:{tenantId}");
        command.Parameters.Add(parameter);
        if (await command.ExecuteScalarAsync(cancellationToken) is true) return true;
        await dbContext.Database.CloseConnectionAsync();
        return false;
    }

    private async Task UnlockTenantAsync(Guid tenantId)
    {
        if (!dbContext.Database.IsRelational()) return;
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(@key)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "key";
            parameter.Value = PostgresAdvisoryLockLease.KeyFor($"email-intake:{tenantId}");
            command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync(CancellationToken.None);
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task<bool> TenantAcceptsIntakeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Tenants.AnyAsync(x => x.Id == tenantId && x.IsActive, cancellationToken))
        {
            return false;
        }

        try
        {
            await licenseGuardService.EnsureTenantCanMutateAsync(tenantId, cancellationToken);
            await licenseGuardService.EnsureFeatureEnabledAsync(tenantId, LicenseFeatures.EmailIngestion, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<bool> IsOwnAddressAsync(EmailIntakeSetting settings, string address, CancellationToken cancellationToken)
    {
        var identity = await dbContext.EmailSettings.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == settings.TenantId, cancellationToken);
        string?[] own = [identity?.SenderAddress, identity?.ManagedEmailAddress, identity?.CustomFromAddress, settings.IntakeEmailAddress, settings.Username];
        return own.Any(x => !string.IsNullOrWhiteSpace(x) && string.Equals(x.Trim(), address, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(Guid? ClientId, Guid? SiteId)> MatchClientAsync(Guid tenantId, string address, CancellationToken cancellationToken)
    {
        var portalContact = await dbContext.ClientPortalUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Email.ToLower() == address && x.IsActive)
            .Select(x => (Guid?)x.ClientId)
            .FirstOrDefaultAsync(cancellationToken);
        if (portalContact.HasValue) return (portalContact, null);

        var client = await dbContext.Clients.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && x.Email != null && x.Email.ToLower() == address)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (client.HasValue) return (client, null);

        var site = await dbContext.Sites.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ContactEmail != null && x.ContactEmail.ToLower() == address)
            .Select(x => new { x.ClientId, x.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (site is not null) return (site.ClientId, site.Id);

        // Same company domain as exactly one client (never for public mail providers).
        var domain = address.Split('@').LastOrDefault();
        if (string.IsNullOrWhiteSpace(domain) || FreeMailDomains.Contains(domain)) return (null, null);
        var suffix = "@" + domain;
        var byDomain = await dbContext.Clients.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && x.Email != null && x.Email.ToLower().EndsWith(suffix))
            .Select(x => x.Id)
            .Union(dbContext.ClientPortalUsers.IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.IsActive && x.Email.ToLower().EndsWith(suffix))
                .Select(x => x.ClientId))
            .Distinct()
            .Take(2)
            .ToListAsync(cancellationToken);
        return byDomain.Count == 1 ? (byDomain[0], null) : (null, null);
    }

    private async Task<WorkOrder?> FindReferencedWorkOrderAsync(Guid tenantId, string subject, CancellationToken cancellationToken)
    {
        var candidates = WorkOrderNumberPattern().Matches(subject).Select(m => m.Value.ToUpperInvariant()).Distinct().Take(5).ToList();
        if (candidates.Count == 0) return null;
        return await dbContext.WorkOrders.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && candidates.Contains(x.WorkOrderNumber.ToUpper()))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> AutoReplyAsync(Guid tenantId, string templateKey, string toAddress, string? toName, string originalSubject, WorkOrder? workOrder, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(x => x.Id == tenantId, cancellationToken);
            var template = await emailTemplateService.RenderTenantTemplateAsync(
                tenantId,
                templateKey,
                EmailTemplateVariables.WithRecipientAndActorAliases(
                    string.IsNullOrWhiteSpace(toName) ? toAddress : toName,
                    null,
                    new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["companyName"] = tenant.CompanyName ?? tenant.Name,
                        ["tenantName"] = tenant.Name,
                        ["platformName"] = "Ecosys",
                        // Without its "Re:"/"Fwd:" — the templates add their own "Re:".
                        ["originalSubject"] = CleanSubject(originalSubject),
                        ["workOrderNumber"] = workOrder?.WorkOrderNumber,
                        ["workOrderTitle"] = workOrder?.Title,
                        ["workOrderStatus"] = workOrder?.Status,
                    }),
                cancellationToken);
            if (!template.Enabled)
            {
                return false;
            }

            await emailOutboxService.QueueEmailAsync(
                new QueueEmailRequest(
                    tenantId,
                    $"email-intake.{templateKey}",
                    templateKey,
                    toAddress,
                    toName,
                    template.SenderNameOverride,
                    null,
                    template.ReplyToOverride,
                    template.Subject,
                    template.HtmlBody,
                    template.TextBody,
                    null),
                cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Email intake auto-reply {Template} to {Address} could not be queued.", templateKey, toAddress);
            return false;
        }
    }

    private async Task<EmailIntakeMessage> RecordAsync(EmailIntakeSetting settings, MimeMessage message, string outcome, string? detail, Guid? workOrderId, Guid? clientId, bool autoReplySent, CancellationToken cancellationToken)
    {
        var from = message.From.Mailboxes.FirstOrDefault() ?? message.Sender;
        var record = new EmailIntakeMessage
        {
            TenantId = settings.TenantId,
            MessageId = MessageIdOf(message, from),
            FromAddress = Truncate(from?.Address?.Trim().ToLowerInvariant() ?? "(unknown)", 320),
            FromName = string.IsNullOrWhiteSpace(from?.Name) ? null : Truncate(from.Name, 200),
            Subject = Truncate(string.IsNullOrWhiteSpace(message.Subject) ? "(no subject)" : message.Subject.Trim(), 500),
            ReceivedAt = message.Date == DateTimeOffset.MinValue ? DateTime.UtcNow : message.Date.UtcDateTime,
            Outcome = outcome,
            Detail = detail is null ? null : Truncate(detail, 1000),
            WorkOrderId = workOrderId,
            ClientId = clientId,
            AutoReplySent = autoReplySent,
        };
        dbContext.EmailIntakeMessages.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return record;
    }

    private static bool IsAutomaticReply(MimeMessage message)
    {
        var autoSubmitted = message.Headers["Auto-Submitted"]?.Trim();
        if (autoSubmitted is not null && autoSubmitted.StartsWith("auto-replied", StringComparison.OrdinalIgnoreCase)) return true;
        if (message.Headers.Contains("X-Autoreply") || message.Headers.Contains("X-Autorespond")) return true;
        var subject = message.Subject?.Trim() ?? string.Empty;
        return subject.StartsWith("Out of Office", StringComparison.OrdinalIgnoreCase)
            || subject.StartsWith("Automatic reply", StringComparison.OrdinalIgnoreCase)
            || subject.StartsWith("Auto-reply", StringComparison.OrdinalIgnoreCase)
            || subject.StartsWith("Autoreply", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Auto-responders, mailing lists and no-reply senders never get an automatic reply.</summary>
    private static bool IsAutomated(MimeMessage message, string localPart)
    {
        var autoSubmitted = message.Headers["Auto-Submitted"];
        if (!string.IsNullOrWhiteSpace(autoSubmitted) && !autoSubmitted.Trim().Equals("no", StringComparison.OrdinalIgnoreCase)) return true;
        var precedence = message.Headers["Precedence"]?.Trim().ToLowerInvariant();
        if (precedence is "bulk" or "list" or "junk" or "auto_reply") return true;
        if (message.Headers.Contains("X-Autoreply") || message.Headers.Contains("X-Autorespond") || message.Headers.Contains("List-Id")) return true;
        return localPart.Contains("noreply") || localPart.Contains("no-reply") || localPart.Contains("donotreply");
    }

    private static string PriorityFor(EmailIntakeSetting settings, string subject, string body)
    {
        var rules = ParsePriorityRules(settings.SubjectParsingRules);
        if (rules.Count == 0) rules = [.. DefaultPriorityRules];
        var haystack = subject;
        foreach (var (keyword, priority) in rules)
        {
            if (haystack.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return priority;
        }

        return WorkOrderPriorities.All.Contains(settings.DefaultPriority) ? settings.DefaultPriority : WorkOrderPriorities.Medium;
    }

    /// <summary>One rule per line or comma: "urgent = High", "chiller down => Critical".</summary>
    public static List<(string Keyword, string Priority)> ParsePriorityRules(string? rules)
    {
        var parsed = new List<(string, string)>();
        foreach (var line in (rules ?? string.Empty).Split(['\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(["=>", "=", ":"], 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0) continue;
            var priority = WorkOrderPriorities.All.FirstOrDefault(p => p.Equals(parts[1], StringComparison.OrdinalIgnoreCase));
            if (priority is not null) parsed.Add((parts[0], priority));
        }

        return parsed;
    }

    public static string CleanSubject(string subject)
    {
        var cleaned = subject.Trim();
        string previous;
        do
        {
            previous = cleaned;
            cleaned = ReplyPrefixPattern().Replace(cleaned, string.Empty).Trim();
        }
        while (cleaned != previous);
        return cleaned.Length == 0 ? "(no subject)" : cleaned;
    }

    public static string BodyText(MimeMessage message)
    {
        var text = message.TextBody;
        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            var html = Regex.Replace(message.HtmlBody, @"<(script|style)[^>]*>.*?</\1>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<(br|/p|/div|/li|/tr)[^>]*>", "\n", RegexOptions.IgnoreCase);
            text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", string.Empty));
        }

        return Regex.Replace((text ?? string.Empty).Replace("\r\n", "\n"), @"\n{3,}", "\n\n").Trim();
    }

    /// <summary>Keeps what the sender wrote, not the earlier messages quoted below it.</summary>
    public static string StripQuotedReply(string body)
    {
        var kept = new List<string>();
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('>') && kept.Count > 0) break;
            if (QuoteHeaderPattern().IsMatch(trimmed)) break;
            kept.Add(line);
        }

        var result = string.Join('\n', kept).Trim();
        return result.Length == 0 ? body.Trim() : result;
    }

    private static string MessageIdOf(MimeMessage message, MailboxAddress? from)
    {
        if (!string.IsNullOrWhiteSpace(message.MessageId)) return Truncate(message.MessageId.Trim(), 500);
        var basis = $"{from?.Address}|{message.Date:O}|{message.Subject}";
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(basis)));
    }

    private static List<string> SplitList(string? value) =>
        (value ?? string.Empty).Split([',', ';', '\n', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.TrimStart('@').ToLowerInvariant())
            .ToList();

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    [GeneratedRegex(@"^\s*((re|fw|fwd|aw|sv|wg)\s*(\[\d+\])?\s*:\s*)", RegexOptions.IgnoreCase)]
    private static partial Regex ReplyPrefixPattern();

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]{0,9}-\d[\d-]*\d\b", RegexOptions.IgnoreCase)]
    private static partial Regex WorkOrderNumberPattern();

    [GeneratedRegex(@"^(On .{1,200} wrote:|-{2,}\s*Original Message\s*-{2,}|From:\s.+|_{5,})$", RegexOptions.IgnoreCase)]
    private static partial Regex QuoteHeaderPattern();
}

/// <summary>Checks the connected mailboxes every minute.</summary>
public sealed class EmailIntakeWorker(IServiceScopeFactory serviceScopeFactory, ILogger<EmailIntakeWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Email intake worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IEmailIntakeService>().PollAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Email intake worker cycle failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
