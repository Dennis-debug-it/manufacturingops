using System.Security.Cryptography;
using System.Text;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>Whose number is being confirmed: a tenant user (staff) or a client portal contact.</summary>
public sealed record WhatsAppSubject(Guid TenantId, Guid? UserId, Guid? ClientPortalUserId)
{
    public static WhatsAppSubject ForUser(Guid tenantId, Guid userId) => new(tenantId, userId, null);
    public static WhatsAppSubject ForPortalUser(Guid tenantId, Guid portalUserId) => new(tenantId, null, portalUserId);
}

public sealed record WhatsAppVerificationStarted(string PhoneNumber, DateTime ExpiresAt);

/// <summary>
/// Confirms a WhatsApp number with a one-time code sent over WhatsApp (design 4.2). Codes are six digits,
/// stored hashed, valid 10 minutes, with 5 attempts. A new code can be asked for once a minute, five
/// times an hour. Confirming opts the person in and lifts a STOP on that number: it is their fresh consent.
/// </summary>
public interface IWhatsAppVerificationService
{
    Task<WhatsAppVerificationStarted> StartAsync(WhatsAppSubject subject, string phoneNumber, CancellationToken cancellationToken = default);

    /// <summary>Returns the confirmed number, or throws a <see cref="BusinessRuleException"/> saying why not.</summary>
    Task<string> VerifyAsync(WhatsAppSubject subject, string code, CancellationToken cancellationToken = default);
}

internal sealed class WhatsAppVerificationService(
    AppDbContext dbContext,
    IWhatsAppOutbox outbox,
    IWhatsAppAccountResolver accountResolver,
    TimeProvider timeProvider) : IWhatsAppVerificationService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    public const int MaxAttempts = 5;
    public static readonly TimeSpan ResendInterval = TimeSpan.FromSeconds(60);
    public const int MaxCodesPerHour = 5;

    public async Task<WhatsAppVerificationStarted> StartAsync(WhatsAppSubject subject, string phoneNumber, CancellationToken cancellationToken = default)
    {
        var policy = await WhatsAppTenantPolicy.LoadAsync(dbContext, subject.TenantId, cancellationToken);
        if (!policy.FeatureEnabled)
            throw new BusinessRuleException("WhatsApp messages aren't switched on for this workspace.");
        if (accountResolver.Resolve(subject.TenantId) is null)
            throw new BusinessRuleException("WhatsApp isn't available yet. Try again later.");
        var normalized = WhatsAppPhoneNumbers.Normalize(phoneNumber, policy.Country)
            ?? throw new BusinessRuleException("Enter a valid mobile number, for example 0712 345 678 or +254 712 345 678.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var recent = await Codes(subject).Where(x => x.CreatedAt > now.AddHours(-1)).Select(x => x.CreatedAt).ToListAsync(cancellationToken);
        if (recent.Count > 0 && recent.Max() > now - ResendInterval)
            throw new BusinessRuleException("A code was just sent. Wait a minute before asking for another.");
        if (recent.Count >= MaxCodesPerHour)
            throw new BusinessRuleException("Too many codes asked for. Try again in an hour.");

        // Only the newest code counts: retire the older ones.
        foreach (var open in await Codes(subject).Where(x => x.ConsumedAt == null).ToListAsync(cancellationToken))
            open.ConsumedAt = now;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var row = new WhatsAppVerificationCode
        {
            TenantId = subject.TenantId,
            UserId = subject.UserId,
            ClientPortalUserId = subject.ClientPortalUserId,
            PhoneNumber = normalized,
            ExpiresAt = now + Lifetime,
            CreatedAt = now,
        };
        row.CodeHash = Hash(row.Id, code);
        dbContext.WhatsAppVerificationCodes.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);

        var queued = await outbox.QueueAsync(new WhatsAppQueueRequest(
            subject.TenantId, WhatsAppTemplateCatalogue.VerificationKey, normalized, null,
            new Dictionary<string, string?> { ["Code"] = code },
            RecipientUserId: subject.UserId, RecipientPortalUserId: subject.ClientPortalUserId,
            EntityType: nameof(WhatsAppVerificationCode), EntityId: row.Id), cancellationToken);
        if (queued.Outcome == WhatsAppQueueOutcome.OverCap)
            throw new BusinessRuleException("This workspace has used its WhatsApp messages for the month. Try again next month.");
        if (queued.Outcome != WhatsAppQueueOutcome.Queued)
            throw new BusinessRuleException("The code couldn't be sent to that number.");

        return new WhatsAppVerificationStarted(normalized, row.ExpiresAt);
    }

    public async Task<string> VerifyAsync(WhatsAppSubject subject, string code, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var row = await Codes(subject).Where(x => x.ConsumedAt == null).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (row is null || row.ExpiresAt <= now)
            throw new BusinessRuleException("That code has expired. Ask for a new one.");
        if (row.AttemptCount >= MaxAttempts)
            throw new BusinessRuleException("Too many wrong attempts. Ask for a new code.");

        row.AttemptCount += 1;
        var cleaned = new string((code ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (cleaned.Length != 6 || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(row.Id, cleaned)), Encoding.ASCII.GetBytes(row.CodeHash)))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var left = MaxAttempts - row.AttemptCount;
            throw new BusinessRuleException(left > 0
                ? $"That code isn't right. {left} attempt{(left == 1 ? "" : "s")} left."
                : "Too many wrong attempts. Ask for a new code.");
        }

        row.ConsumedAt = now;
        if (subject.UserId is { } userId)
        {
            var user = await dbContext.Users.IgnoreQueryFilters().SingleAsync(x => x.TenantId == subject.TenantId && x.Id == userId, cancellationToken);
            user.WhatsAppNumber = row.PhoneNumber;
            user.WhatsAppVerifiedAt = now;
            user.WhatsAppOptedInAt = now;
            user.WhatsAppOptedOutAt = null;
            await EnableAllowedStaffKeysAsync(subject.TenantId, userId, cancellationToken);
        }
        else if (subject.ClientPortalUserId is { } portalUserId)
        {
            var contact = await dbContext.ClientPortalUsers.IgnoreQueryFilters().SingleAsync(x => x.TenantId == subject.TenantId && x.Id == portalUserId, cancellationToken);
            contact.WhatsAppNumber = row.PhoneNumber;
            contact.WhatsAppVerifiedAt = now;
            contact.WhatsAppConsentAt = now;
            contact.WhatsAppConsentMethod = WhatsAppConsentMethods.Portal;
            contact.WhatsAppConsentRecordedByUserId = null;
            contact.ReceivesWhatsApp = true;
        }

        var optOut = await dbContext.WhatsAppOptOuts.SingleOrDefaultAsync(x => x.PhoneNumber == row.PhoneNumber, cancellationToken);
        if (optOut is not null)
            dbContext.WhatsAppOptOuts.Remove(optOut);

        await dbContext.SaveChangesAsync(cancellationToken);
        return row.PhoneNumber;
    }

    /// <summary>
    /// Opting in means "send me WhatsApp": switch it on for the keys the workspace allows, so the person
    /// doesn't have to find each toggle. They can turn any of them off again on the preferences page.
    /// </summary>
    private async Task EnableAllowedStaffKeysAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var policy = await WhatsAppTenantPolicy.LoadAsync(dbContext, tenantId, cancellationToken);
        var keys = NotificationKeyDescriptor.List()
            .Where(d => d.Channels.HasFlag(NotificationChannels.WhatsApp) && policy.AllowedKeys.Contains(d.Key))
            .ToList();
        if (keys.Count == 0)
            return;
        var keyNames = keys.Select(k => k.Key).ToList();
        var existing = await dbContext.UserNotificationPreferences.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.UserId == userId && keyNames.Contains(x.NotificationKey))
            .ToDictionaryAsync(x => x.NotificationKey, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var descriptor in keys)
        {
            if (existing.TryGetValue(descriptor.Key, out var row))
            {
                row.WhatsAppEnabled = true;
                continue;
            }
            var defaults = descriptor.DefaultChannels;
            dbContext.UserNotificationPreferences.Add(new UserNotificationPreference
            {
                TenantId = tenantId,
                UserId = userId,
                NotificationKey = descriptor.Key,
                InAppEnabled = defaults.HasFlag(NotificationChannels.InApp),
                PushEnabled = defaults.HasFlag(NotificationChannels.Push),
                EmailEnabled = defaults.HasFlag(NotificationChannels.Email),
                WhatsAppEnabled = true,
            });
        }
    }

    private IQueryable<WhatsAppVerificationCode> Codes(WhatsAppSubject subject) =>
        dbContext.WhatsAppVerificationCodes.IgnoreQueryFilters()
            .Where(x => x.TenantId == subject.TenantId && x.UserId == subject.UserId && x.ClientPortalUserId == subject.ClientPortalUserId);

    internal static string Hash(Guid id, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{id:N}:{code}")));
}

public static class WhatsAppVerificationLimits
{
    public const int MaxAttempts = WhatsAppVerificationService.MaxAttempts;
}

public static class WhatsAppConsentMethods
{
    /// <summary>The contact confirmed their number with a code from the portal.</summary>
    public const string Portal = "Portal";
    public const string InPerson = "InPerson";
    public const string Phone = "Phone";
    public const string InWriting = "InWriting";

    /// <summary>What office staff can record for a contact who doesn't use the portal.</summary>
    public static readonly IReadOnlyList<string> RecordedByStaff = [InPerson, Phone, InWriting];
}
