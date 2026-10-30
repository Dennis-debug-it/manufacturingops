using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

public static class WhatsAppOutboxStatuses
{
    public const string Pending = "Pending";
    public const string Sending = "Sending";
    public const string Sent = "Sent";
    public const string Delivered = "Delivered";
    public const string Read = "Read";
    public const string Failed = "Failed";
    /// <summary>Never sent and never will be: over the monthly cap, not configured, or opted out since it was queued.</summary>
    public const string Skipped = "Skipped";

    public static readonly IReadOnlyList<string> All = [Pending, Sending, Sent, Delivered, Read, Failed, Skipped];
}

/// <summary>What a tenant has switched on for WhatsApp, and its name for the first template parameter.</summary>
public sealed record WhatsAppTenantPolicy(bool FeatureEnabled, IReadOnlySet<string> AllowedKeys, string TenantName, string? Country)
{
    public bool Allows(string key) => FeatureEnabled && AllowedKeys.Contains(key);

    public static async Task<WhatsAppTenantPolicy> LoadAsync(AppDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => new { x.CompanyName, x.Name, x.Country })
            .SingleOrDefaultAsync(cancellationToken);
        var enabled = await db.TenantLicenses.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.WhatsAppAddOn != null, cancellationToken);
        var allowedJson = await db.WhatsAppSettings.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.AllowedNotificationKeys)
            .SingleOrDefaultAsync(cancellationToken);
        var name = !string.IsNullOrWhiteSpace(tenant?.CompanyName) ? tenant!.CompanyName : tenant?.Name ?? "Ecosys";
        return new WhatsAppTenantPolicy(enabled, ParseAllowedKeys(allowedJson), name.Trim(), tenant?.Country);
    }

    /// <summary>Null or unreadable JSON means the defaults; only keys in the catalogue survive.</summary>
    public static IReadOnlySet<string> ParseAllowedKeys(string? json)
    {
        IEnumerable<string> keys = WhatsAppTemplateCatalogue.DefaultAllowedKeys;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                keys = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch (JsonException)
            {
            }
        }
        return keys.Select(k => WhatsAppTemplateCatalogue.Find(k)?.Key).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

public enum WhatsAppQueueOutcome
{
    Queued,
    /// <summary>An earlier row with the same idempotency key exists.</summary>
    Duplicate,
    /// <summary>Over the tenant's monthly cap: a Skipped row records it in the log.</summary>
    OverCap,
    FeatureOff,
    KeyNotAllowed,
    NoTemplate,
    OptedOut,
    InvalidNumber,
}

public sealed record WhatsAppQueueResult(WhatsAppQueueOutcome Outcome, Guid? MessageId = null);

/// <param name="Key">A catalogue key; it picks the template.</param>
/// <param name="Values">Template values by name (TenantName is added).</param>
public sealed record WhatsAppQueueRequest(
    Guid TenantId,
    string Key,
    string ToPhone,
    string? RecipientName,
    IReadOnlyDictionary<string, string?> Values,
    Guid? RecipientUserId = null,
    Guid? RecipientPortalUserId = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? IdempotencyKey = null);

/// <summary>
/// Queues WhatsApp template messages (design 4.1). Every rule that decides whether a message may go is
/// checked here: the tenant flag, the tenant's allowed keys (not for the confirmation code), the
/// platform-wide STOP list and the monthly cap. Opt-in and consent are the caller's (they differ for staff
/// and client contacts).
/// </summary>
public interface IWhatsAppOutbox
{
    Task<WhatsAppQueueResult> QueueAsync(WhatsAppQueueRequest request, CancellationToken cancellationToken = default);

    /// <summary>Rows that count against the cap this calendar month (UTC): everything but Skipped.</summary>
    Task<int> CountThisMonthAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default);
}

internal sealed class WhatsAppOutbox(AppDbContext dbContext, IOptions<WhatsAppOptions> options, ILogger<WhatsAppOutbox> logger, IOptions<PlatformFeatureOptions>? features = null) : IWhatsAppOutbox
{
    public async Task<WhatsAppQueueResult> QueueAsync(WhatsAppQueueRequest request, CancellationToken cancellationToken = default)
    {
        // Switched off for the whole platform (Features:WhatsApp): nothing is queued, for any workspace.
        if (features?.Value.WhatsApp == false)
            return new(WhatsAppQueueOutcome.FeatureOff);
        var template = WhatsAppTemplateCatalogue.Find(request.Key);
        if (template is null)
            return new(WhatsAppQueueOutcome.NoTemplate);

        var phone = WhatsAppPhoneNumbers.Normalize(request.ToPhone);
        if (phone is null)
            return new(WhatsAppQueueOutcome.InvalidNumber);

        var policy = await WhatsAppTenantPolicy.LoadAsync(dbContext, request.TenantId, cancellationToken);
        if (!policy.FeatureEnabled)
            return new(WhatsAppQueueOutcome.FeatureOff);
        var isVerification = template.Audience == WhatsAppAudiences.Verification;
        if (!isVerification && !policy.AllowedKeys.Contains(template.Key))
            return new(WhatsAppQueueOutcome.KeyNotAllowed);
        // Asking for a confirmation code is the person's own fresh request, so STOP doesn't block it;
        // confirming the number lifts STOP (WhatsAppVerificationService).
        if (!isVerification && await IsOptedOutAsync(dbContext, phone, cancellationToken))
            return new(WhatsAppQueueOutcome.OptedOut);

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        if (idempotencyKey is not null)
        {
            var existing = await dbContext.WhatsAppOutboxMessages.IgnoreQueryFilters()
                .Where(x => x.IdempotencyKey == idempotencyKey)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return new(WhatsAppQueueOutcome.Duplicate, existing);
        }

        var now = DateTime.UtcNow;
        var overCap = await CountThisMonthAsync(request.TenantId, now, cancellationToken) >= Math.Max(0, options.Value.MonthlyCapPerTenant);
        var message = new WhatsAppOutboxMessage
        {
            TenantId = request.TenantId,
            NotificationKey = template.Key,
            TemplateName = template.Name,
            Language = template.Language,
            ParametersJson = overCap ? null : JsonSerializer.Serialize(WhatsAppTemplateCatalogue.BuildParameters(template, policy.TenantName, request.Values)),
            ToPhone = phone,
            RecipientName = Clip(request.RecipientName, 200),
            RecipientUserId = request.RecipientUserId,
            RecipientPortalUserId = request.RecipientPortalUserId,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            Status = overCap ? WhatsAppOutboxStatuses.Skipped : WhatsAppOutboxStatuses.Pending,
            MaxAttempts = isVerification ? 2 : 5,
            NextAttemptAt = overCap ? null : now,
            ErrorMessage = overCap ? $"Monthly WhatsApp limit of {options.Value.MonthlyCapPerTenant} messages reached." : null,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
        };
        dbContext.WhatsAppOutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (overCap)
        {
            logger.LogInformation("WhatsApp {Key} for tenant {TenantId} skipped: monthly cap reached.", template.Key, request.TenantId);
            return new(WhatsAppQueueOutcome.OverCap, message.Id);
        }
        return new(WhatsAppQueueOutcome.Queued, message.Id);
    }

    public Task<int> CountThisMonthAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var monthStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return dbContext.WhatsAppOutboxMessages.IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && x.CreatedAt >= monthStart && x.Status != WhatsAppOutboxStatuses.Skipped, cancellationToken);
    }

    public static Task<bool> IsOptedOutAsync(AppDbContext db, string e164, CancellationToken cancellationToken) =>
        db.WhatsAppOptOuts.AnyAsync(x => x.PhoneNumber == e164, cancellationToken);

    private static string? Clip(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];
}

public interface IWhatsAppOutboxProcessor
{
    Task<int> ProcessPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sends due outbox rows. A row that fails with a retryable error goes back to Pending with a growing
/// delay (1, 5, 15, 60 minutes); out of attempts, or refused outright by Meta, it is Failed with the
/// reason, which the tenant's WhatsApp log shows. With no account configured, rows are Skipped.
/// </summary>
internal sealed class WhatsAppOutboxProcessor(
    AppDbContext dbContext,
    IWhatsAppAccountResolver accountResolver,
    IWhatsAppSender sender,
    ILogger<WhatsAppOutboxProcessor> logger) : IWhatsAppOutboxProcessor
{
    /// <summary>A Sending claim older than this was abandoned by an instance that stopped mid-send.</summary>
    public static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(10);

    public static DateTime NextAttemptAt(int attemptCount, DateTime utcNow) => utcNow.Add(attemptCount switch
    {
        1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(15),
        _ => TimeSpan.FromMinutes(60),
    });

    public async Task<int> ProcessPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        await ReleaseAbandonedAsync(utcNow, cancellationToken);

        var due = await dbContext.WhatsAppOutboxMessages.IgnoreQueryFilters()
            .Where(x => x.Status == WhatsAppOutboxStatuses.Pending && (x.NextAttemptAt == null || x.NextAttemptAt <= utcNow))
            .OrderBy(x => x.CreatedAt)
            .Take(Math.Max(1, batchSize))
            .ToListAsync(cancellationToken);

        var handled = 0;
        foreach (var message in due)
        {
            try
            {
                await ProcessAsync(message, utcNow, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "WhatsApp outbox message {MessageId} failed unexpectedly.", message.Id);
                RecordFailure(message, ex.Message, retryable: true, utcNow);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            handled++;
        }
        return handled;
    }

    private async Task ProcessAsync(WhatsAppOutboxMessage message, DateTime utcNow, CancellationToken cancellationToken)
    {
        var account = accountResolver.Resolve(message.TenantId);
        if (account is null)
        {
            logger.LogInformation("WhatsApp is not configured; {Key} message {MessageId} skipped.", message.NotificationKey, message.Id);
            Skip(message, "WhatsApp is not configured on this server.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var template = WhatsAppTemplateCatalogue.Find(message.NotificationKey);
        var isVerification = template?.Audience == WhatsAppAudiences.Verification;
        // STOP may have arrived since the row was queued.
        if (!isVerification && await WhatsAppOutbox.IsOptedOutAsync(dbContext, message.ToPhone, cancellationToken))
        {
            Skip(message, "The number replied STOP.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        message.Status = WhatsAppOutboxStatuses.Sending;
        message.LastAttemptAt = utcNow;
        message.UpdatedAt = utcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var parameters = ReadParameters(message.ParametersJson);
        var result = await sender.SendTemplateAsync(account, message.ToPhone, message.TemplateName, message.Language,
            parameters, template?.IsAuthentication == true ? parameters : null, cancellationToken);

        if (result.Succeeded)
        {
            message.Status = WhatsAppOutboxStatuses.Sent;
            message.AttemptCount += 1;
            message.SentAt = utcNow;
            message.NextAttemptAt = null;
            message.ErrorMessage = null;
            message.ProviderMessageId = result.MessageId;
            if (template?.IsAuthentication == true)
                message.ParametersJson = null; // the code is not kept once sent
        }
        else
        {
            RecordFailure(message, result.Error ?? "WhatsApp delivery failed.", result.Retryable, utcNow);
            if (message.Status == WhatsAppOutboxStatuses.Failed)
                logger.LogWarning("WhatsApp {Key} message {MessageId} failed: {Error}", message.NotificationKey, message.Id, message.ErrorMessage);
        }
        message.UpdatedAt = utcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ReleaseAbandonedAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        var cutoff = utcNow - ClaimTimeout;
        var abandoned = await dbContext.WhatsAppOutboxMessages.IgnoreQueryFilters()
            .Where(x => x.Status == WhatsAppOutboxStatuses.Sending && (x.LastAttemptAt == null || x.LastAttemptAt < cutoff))
            .ToListAsync(cancellationToken);
        if (abandoned.Count == 0)
            return;
        foreach (var message in abandoned)
            RecordFailure(message, "Sending was interrupted before it finished.", retryable: true, utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogWarning("Returned {Count} abandoned WhatsApp outbox message(s) to the queue.", abandoned.Count);
    }

    internal static void RecordFailure(WhatsAppOutboxMessage message, string error, bool retryable, DateTime utcNow)
    {
        message.AttemptCount += 1;
        message.ErrorMessage = error.Length <= 1000 ? error : error[..1000];
        if (!retryable || message.AttemptCount >= message.MaxAttempts)
        {
            message.Status = WhatsAppOutboxStatuses.Failed;
            message.FailedAt = utcNow;
            message.NextAttemptAt = null;
            if (WhatsAppTemplateCatalogue.Find(message.NotificationKey)?.IsAuthentication == true)
                message.ParametersJson = null;
        }
        else
        {
            message.Status = WhatsAppOutboxStatuses.Pending;
            message.NextAttemptAt = NextAttemptAt(message.AttemptCount, utcNow);
        }
    }

    private static void Skip(WhatsAppOutboxMessage message, string reason)
    {
        message.Status = WhatsAppOutboxStatuses.Skipped;
        message.NextAttemptAt = null;
        message.ErrorMessage = reason;
        if (WhatsAppTemplateCatalogue.Find(message.NotificationKey)?.IsAuthentication == true)
            message.ParametersJson = null;
    }

    private static IReadOnlyList<string> ReadParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}

/// <summary>Sends the WhatsApp outbox every 10 seconds. Leased: one instance sends.</summary>
public sealed class WhatsAppOutboxWorker(IServiceScopeFactory serviceScopeFactory, ILogger<WhatsAppOutboxWorker> logger, IOptions<PlatformFeatureOptions>? features = null) : BackgroundService
{
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (features?.Value.WhatsApp == false)
        {
            logger.LogInformation("WhatsApp is switched off (Features:WhatsApp); nothing is sent.");
            return;
        }
        logger.LogInformation("WhatsApp outbox worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int handled;
                do
                {
                    using var scope = serviceScopeFactory.CreateScope();
                    handled = await scope.ServiceProvider.GetRequiredService<IWhatsAppOutboxProcessor>()
                        .ProcessPendingBatchAsync(BatchSize, DateTime.UtcNow, stoppingToken);
                }
                while (handled == BatchSize && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WhatsApp outbox worker cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
