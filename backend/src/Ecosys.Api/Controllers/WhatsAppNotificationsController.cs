using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecosys.Api.Controllers;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <param name="Available">WhatsApp can be used now: the workspace has it and the platform number is set up.</param>
/// <param name="SharedNumber">The Ecosys number messages come from.</param>
/// <param name="BlockedByStop">The number replied STOP; confirming it again lifts that.</param>
/// <param name="PendingNumber">A code was sent to this number and can still be entered.</param>
public sealed record WhatsAppMyStatusResponse(
    bool Available,
    bool FeatureEnabled,
    bool Configured,
    string? SharedNumber,
    string? Number,
    DateTime? VerifiedAt,
    bool OptedIn,
    bool BlockedByStop,
    string? PendingNumber,
    DateTime? PendingExpiresAt,
    string DefaultCallingCode);

public sealed record WhatsAppStartRequest(string PhoneNumber);
public sealed record WhatsAppVerifyRequest(string Code);
public sealed record WhatsAppStartResponse(string PhoneNumber, DateTime ExpiresAt);

public sealed record WhatsAppKeySettingResponse(string Key, string Label, string Audience, string TemplateName, bool Allowed, bool IsDefault);

public sealed record WhatsAppTenantSettingsResponse(
    bool FeatureEnabled,
    bool Configured,
    string? SharedNumber,
    int MonthlyCap,
    int UsedThisMonth,
    IReadOnlyList<WhatsAppKeySettingResponse> Keys);

public sealed record SaveWhatsAppTenantSettingsRequest(IReadOnlyList<string> AllowedKeys);

public sealed record WhatsAppOutboxItemResponse(
    Guid Id,
    string Key,
    string Label,
    string TemplateName,
    string To,
    string? RecipientName,
    string Status,
    int AttemptCount,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeliveredAt,
    DateTime? ReadAt,
    DateTime? FailedAt,
    string? Error);

public sealed record WhatsAppOutboxPageResponse(IReadOnlyList<WhatsAppOutboxItemResponse> Items, bool HasMore);

/// <param name="ConsentMethod">InPerson, Phone or InWriting: how the contact agreed.</param>
public sealed record RecordWhatsAppConsentRequest(string? PhoneNumber, bool ReceivesWhatsApp, string? ConsentMethod);

public sealed record ContactWhatsAppResponse(
    Guid ContactId,
    string? Number,
    bool ReceivesWhatsApp,
    DateTime? ConsentAt,
    string? ConsentMethod,
    string? ConsentRecordedBy,
    DateTime? VerifiedAt,
    bool BlockedByStop);

internal static class WhatsAppStatus
{
    public static async Task<WhatsAppMyStatusResponse> BuildAsync(
        AppDbContext db, IWhatsAppAccountResolver resolver, IOptions<WhatsAppOptions> options, WhatsAppSubject subject,
        string? number, DateTime? verifiedAt, bool optedIn, CancellationToken cancellationToken)
    {
        var policy = await WhatsAppTenantPolicy.LoadAsync(db, subject.TenantId, cancellationToken);
        var configured = resolver.Resolve(subject.TenantId) is not null;
        var blocked = number is not null && await db.WhatsAppOptOuts.AnyAsync(x => x.PhoneNumber == number, cancellationToken);
        var now = DateTime.UtcNow;
        var pending = await db.WhatsAppVerificationCodes.IgnoreQueryFilters()
            .Where(x => x.TenantId == subject.TenantId && x.UserId == subject.UserId && x.ClientPortalUserId == subject.ClientPortalUserId
                && x.ConsumedAt == null && x.ExpiresAt > now && x.AttemptCount < WhatsAppVerificationLimits.MaxAttempts)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.PhoneNumber, x.ExpiresAt })
            .FirstOrDefaultAsync(cancellationToken);
        return new WhatsAppMyStatusResponse(
            policy.FeatureEnabled && configured, policy.FeatureEnabled, configured, options.Value.DisplayPhoneNumber,
            number, verifiedAt, optedIn && verifiedAt.HasValue && !blocked, blocked,
            pending?.PhoneNumber, pending?.ExpiresAt, WhatsAppPhoneNumbers.CallingCodeFor(policy.Country));
    }
}

// ── My WhatsApp (staff, office and technician app) ───────────────────────────

/// <summary>
/// The signed-in user's own WhatsApp opt-in (design 4.2): confirm a number with a code sent over
/// WhatsApp, then opt in or out. Shared: every plan, gated by the tenant's <c>whatsapp</c> flag.
/// </summary>
[ApiController]
[Authorize]
[Route("api/me/whatsapp")]
public sealed class MyWhatsAppController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IWhatsAppVerificationService verificationService,
    IWhatsAppAccountResolver accountResolver,
    IOptions<WhatsAppOptions> options) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<WhatsAppMyStatusResponse>> Get(CancellationToken cancellationToken) =>
        Ok(await StatusAsync(cancellationToken));

    [HttpPost("start")]
    public async Task<ActionResult<WhatsAppStartResponse>> Start([FromBody] WhatsAppStartRequest request, CancellationToken cancellationToken)
    {
        var started = await verificationService.StartAsync(WhatsAppSubject.ForUser(TenantId, UserId), request.PhoneNumber ?? string.Empty, cancellationToken);
        return Ok(new WhatsAppStartResponse(started.PhoneNumber, started.ExpiresAt));
    }

    [HttpPost("verify")]
    public async Task<ActionResult<WhatsAppMyStatusResponse>> Verify([FromBody] WhatsAppVerifyRequest request, CancellationToken cancellationToken)
    {
        await verificationService.VerifyAsync(WhatsAppSubject.ForUser(TenantId, UserId), request.Code ?? string.Empty, cancellationToken);
        return Ok(await StatusAsync(cancellationToken));
    }

    /// <summary>Back on, for a number confirmed before. A number that replied STOP must be confirmed again.</summary>
    [HttpPost("opt-in")]
    public async Task<ActionResult<WhatsAppMyStatusResponse>> OptIn(CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(cancellationToken);
        if (user.WhatsAppNumber is null || user.WhatsAppVerifiedAt is null)
            throw new BusinessRuleException("Confirm your WhatsApp number first.");
        if (await dbContext.WhatsAppOptOuts.AnyAsync(x => x.PhoneNumber == user.WhatsAppNumber, cancellationToken))
            throw new BusinessRuleException("This number replied STOP. Confirm it again with a new code to turn WhatsApp back on.");
        user.WhatsAppOptedInAt = DateTime.UtcNow;
        user.WhatsAppOptedOutAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await StatusAsync(cancellationToken));
    }

    [HttpPost("opt-out")]
    public async Task<ActionResult<WhatsAppMyStatusResponse>> OptOut(CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(cancellationToken);
        user.WhatsAppOptedInAt = null;
        user.WhatsAppOptedOutAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await StatusAsync(cancellationToken));
    }

    private async Task<User> LoadUserAsync(CancellationToken cancellationToken) =>
        await dbContext.Users.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == UserId, cancellationToken)
        ?? throw new NotFoundException("User not found.");

    private async Task<WhatsAppMyStatusResponse> StatusAsync(CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Id == UserId)
            .Select(x => new { x.WhatsAppNumber, x.WhatsAppVerifiedAt, x.WhatsAppOptedInAt })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("User not found.");
        return await WhatsAppStatus.BuildAsync(dbContext, accountResolver, options, WhatsAppSubject.ForUser(TenantId, UserId),
            user.WhatsAppNumber, user.WhatsAppVerifiedAt, user.WhatsAppOptedInAt.HasValue, cancellationToken);
    }
}

// ── Workspace WhatsApp settings (admin) ──────────────────────────────────────

/// <summary>
/// Which notification keys may use WhatsApp, this month's usage against the cap, and the outbox log
/// (design 4.2). Tenants don't manage templates or numbers: the shared Ecosys number sends.
/// </summary>
[ApiController]
[Authorize]
[Route("api/whatsapp-notifications")]
public sealed class WhatsAppNotificationsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService,
    IWhatsAppOutbox outbox,
    IWhatsAppAccountResolver accountResolver,
    IOptions<WhatsAppOptions> options) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("settings")]
    public async Task<ActionResult<WhatsAppTenantSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        return Ok(await BuildSettingsAsync(cancellationToken));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<WhatsAppTenantSettingsResponse>> SaveSettings([FromBody] SaveWhatsAppTenantSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var selectable = WhatsAppTemplateCatalogue.Selectable().ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var unknown = (request.AllowedKeys ?? []).Where(k => !selectable.ContainsKey(k)).ToList();
        if (unknown.Count > 0)
            throw new BusinessRuleException($"Unknown WhatsApp notification: {string.Join(", ", unknown)}.");
        var keys = (request.AllowedKeys ?? []).Select(k => selectable[k].Key).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();

        var setting = await dbContext.WhatsAppSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (setting is null)
        {
            setting = new WhatsAppSetting { TenantId = TenantId };
            dbContext.WhatsAppSettings.Add(setting);
        }
        setting.AllowedNotificationKeys = JsonSerializer.Serialize(keys);
        setting.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "whatsapp.notifications.saved", nameof(WhatsAppSetting), setting.Id.ToString(),
            keys.Count == 0 ? "WhatsApp turned off for every notification." : $"WhatsApp allowed for: {string.Join(", ", keys)}.", cancellationToken);
        return Ok(await BuildSettingsAsync(cancellationToken));
    }

    [HttpGet("outbox")]
    public async Task<ActionResult<WhatsAppOutboxPageResponse>> Outbox(
        [FromQuery] string? status, [FromQuery] DateTime? before, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var take = Math.Clamp(limit ?? 50, 1, 100);
        var query = dbContext.WhatsAppOutboxMessages.AsNoTracking().Where(x => x.TenantId == TenantId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);
        if (before.HasValue)
        {
            var cutoff = DateTime.SpecifyKind(before.Value, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt < cutoff);
        }
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(take + 1).ToListAsync(cancellationToken);
        var items = rows.Take(take).Select(x => new WhatsAppOutboxItemResponse(
            x.Id, x.NotificationKey, WhatsAppTemplateCatalogue.Find(x.NotificationKey)?.Label ?? x.NotificationKey, x.TemplateName,
            WhatsAppPhoneNumbers.Mask(x.ToPhone), x.RecipientName, x.Status, x.AttemptCount, x.CreatedAt,
            x.SentAt, x.DeliveredAt, x.ReadAt, x.FailedAt, x.ErrorMessage)).ToList();
        return Ok(new WhatsAppOutboxPageResponse(items, rows.Count > take));
    }

    private async Task<WhatsAppTenantSettingsResponse> BuildSettingsAsync(CancellationToken cancellationToken)
    {
        var policy = await WhatsAppTenantPolicy.LoadAsync(dbContext, TenantId, cancellationToken);
        var keys = WhatsAppTemplateCatalogue.Selectable()
            .Select(x => new WhatsAppKeySettingResponse(x.Key, x.Label, x.Audience, x.Name, policy.AllowedKeys.Contains(x.Key),
                WhatsAppTemplateCatalogue.DefaultAllowedKeys.Contains(x.Key, StringComparer.OrdinalIgnoreCase)))
            .ToList();
        return new WhatsAppTenantSettingsResponse(
            policy.FeatureEnabled,
            accountResolver.Resolve(TenantId) is not null,
            options.Value.DisplayPhoneNumber,
            options.Value.MonthlyCapPerTenant,
            await outbox.CountThisMonthAsync(TenantId, DateTime.UtcNow, cancellationToken),
            keys);
    }
}

// ── Client contact consent (office) ──────────────────────────────────────────

/// <summary>
/// Office staff record that a client contact agreed to WhatsApp messages (design 4.2, R6): who recorded
/// it, when, and how. Client messages go only to contacts marked here (or who opted in from the portal).
/// </summary>
[ApiController]
[Authorize]
[Route("api/clients/{clientId:guid}/portal-users/{contactId:guid}/whatsapp")]
public sealed class ClientWhatsAppConsentController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<ContactWhatsAppResponse>> Get(Guid clientId, Guid contactId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        return Ok(await MapAsync(await LoadAsync(clientId, contactId, cancellationToken), cancellationToken));
    }

    [HttpPut]
    public async Task<ActionResult<ContactWhatsAppResponse>> Record(Guid clientId, Guid contactId, [FromBody] RecordWhatsAppConsentRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageAssets);
        var contact = await LoadAsync(clientId, contactId, cancellationToken);

        if (request.ReceivesWhatsApp)
        {
            var country = await dbContext.Tenants.Where(x => x.Id == TenantId).Select(x => x.Country).SingleOrDefaultAsync(cancellationToken);
            var number = WhatsAppPhoneNumbers.Normalize(request.PhoneNumber ?? contact.WhatsAppNumber ?? contact.Phone, country)
                ?? throw new BusinessRuleException("Enter the contact's WhatsApp number, for example 0712 345 678 or +254 712 345 678.");
            var method = WhatsAppConsentMethods.RecordedByStaff.FirstOrDefault(m => string.Equals(m, request.ConsentMethod?.Trim(), StringComparison.OrdinalIgnoreCase));
            var keepsPortalConsent = method is null && contact.WhatsAppConsentMethod == WhatsAppConsentMethods.Portal && contact.WhatsAppNumber == number;
            if (method is null && !keepsPortalConsent)
                throw new BusinessRuleException("Say how the contact agreed: in person, by phone or in writing.");

            if (!keepsPortalConsent)
            {
                contact.WhatsAppConsentAt = DateTime.UtcNow;
                contact.WhatsAppConsentRecordedByUserId = UserId;
                contact.WhatsAppConsentMethod = method;
                if (contact.WhatsAppNumber != number)
                    contact.WhatsAppVerifiedAt = null;
            }
            contact.WhatsAppNumber = number;
            contact.ReceivesWhatsApp = true;
        }
        else
        {
            // Consent history stays; the contact just stops getting messages.
            contact.ReceivesWhatsApp = false;
        }
        contact.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, request.ReceivesWhatsApp ? "client.contact.whatsapp.consent" : "client.contact.whatsapp.off",
            nameof(ClientPortalUser), contact.Id.ToString(),
            request.ReceivesWhatsApp
                ? $"Recorded WhatsApp consent for {contact.FullName} ({contact.WhatsAppConsentMethod})."
                : $"Stopped WhatsApp messages to {contact.FullName}.", cancellationToken);
        return Ok(await MapAsync(contact, cancellationToken));
    }

    private async Task<ClientPortalUser> LoadAsync(Guid clientId, Guid contactId, CancellationToken cancellationToken) =>
        await dbContext.ClientPortalUsers.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ClientId == clientId && x.Id == contactId, cancellationToken)
        ?? throw new NotFoundException("Contact not found.");

    private async Task<ContactWhatsAppResponse> MapAsync(ClientPortalUser contact, CancellationToken cancellationToken)
    {
        var recordedBy = contact.WhatsAppConsentRecordedByUserId is { } by
            ? await dbContext.Users.Where(x => x.TenantId == TenantId && x.Id == by).Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var blocked = contact.WhatsAppNumber is not null && await dbContext.WhatsAppOptOuts.AnyAsync(x => x.PhoneNumber == contact.WhatsAppNumber, cancellationToken);
        return new ContactWhatsAppResponse(contact.Id, contact.WhatsAppNumber, contact.ReceivesWhatsApp, contact.WhatsAppConsentAt,
            contact.WhatsAppConsentMethod, recordedBy, contact.WhatsAppVerifiedAt, blocked);
    }
}

// ── Portal contact's own opt-in ──────────────────────────────────────────────

