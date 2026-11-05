using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

// ── Request / Response models ────────────────────────────────────────────────

/// <param name="InAppEnabled">Show it in the bell.</param>
/// <param name="Channels">The channels this key can use ("InApp", "Push", "Email", "WhatsApp"); only these are worth a toggle.</param>
public sealed record UserNotificationPreferenceResponse(
    string NotificationKey,
    string Label,
    string Category,
    bool EmailEnabled,
    bool InAppEnabled,
    bool PushEnabled,
    bool WhatsAppEnabled,
    bool SmsEnabled,
    IReadOnlyList<string> Channels);

/// <summary>Push and WhatsApp are optional so an older client that only knows email and in-app leaves them as they are.</summary>
public sealed record UpdateNotificationPreferenceInput(
    string NotificationKey,
    bool EmailEnabled,
    bool InAppEnabled,
    bool? PushEnabled = null,
    bool? WhatsAppEnabled = null,
    bool? SmsEnabled = null);

// ── Controller ───────────────────────────────────────────────────────────────

[ApiController]
[Authorize]
[Route("api/me/notification-preferences")]
public sealed class UserNotificationPreferencesController(
    AppDbContext db,
    ITenantContext tenantContext) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>
    /// Returns all notification types with the current user's saved preferences.
    /// A missing row means the key's default channels (<see cref="NotificationDescriptor.DefaultChannels"/>).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserNotificationPreferenceResponse>>> Get(
        CancellationToken cancellationToken)
    {
        var userId = UserId;
        var tenantId = TenantId;

        var saved = await db.UserNotificationPreferences
            .Where(p => p.TenantId == tenantId && p.UserId == userId)
            .ToListAsync(cancellationToken);

        var savedByKey = saved.ToDictionary(p => p.NotificationKey, StringComparer.OrdinalIgnoreCase);
        // WhatsApp is offered only for keys this workspace allows it on, and only when it has the feature.
        var whatsApp = await WhatsAppTenantPolicy.LoadAsync(db, tenantId, cancellationToken);

        var result = NotificationKeyDescriptor.List()
            .Select(descriptor =>
            {
                var channels = NotificationPreferences.Resolve(descriptor, savedByKey.GetValueOrDefault(descriptor.Key));
                return new UserNotificationPreferenceResponse(
                    descriptor.Key,
                    descriptor.Label,
                    descriptor.Category,
                    EmailEnabled: channels.HasFlag(NotificationChannels.Email),
                    InAppEnabled: channels.HasFlag(NotificationChannels.InApp),
                    PushEnabled: channels.HasFlag(NotificationChannels.Push),
                    WhatsAppEnabled: channels.HasFlag(NotificationChannels.WhatsApp),
                    SmsEnabled: channels.HasFlag(NotificationChannels.Sms),
                    Channels: Enum.GetValues<NotificationChannels>()
                        .Where(c => c != NotificationChannels.None && descriptor.Channels.HasFlag(c))
                        .Where(c => c != NotificationChannels.WhatsApp || whatsApp.Allows(descriptor.Key))
                        .Select(c => c.ToString())
                        .ToList());
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// Bulk upsert: replaces or creates preference rows for each supplied key.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] IReadOnlyCollection<UpdateNotificationPreferenceInput> inputs,
        CancellationToken cancellationToken)
    {
        var userId = UserId;
        var tenantId = TenantId;

        if (inputs.Any(i => NotificationKeyDescriptor.Find(i.NotificationKey) is null))
            return BadRequest(new { message = "Unknown notification type." });

        var keys = inputs.Select(i => i.NotificationKey).ToList();

        var existing = await db.UserNotificationPreferences
            .Where(p => p.TenantId == tenantId && p.UserId == userId && keys.Contains(p.NotificationKey))
            .ToListAsync(cancellationToken);

        var existingByKey = existing.ToDictionary(p => p.NotificationKey, StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            if (!existingByKey.TryGetValue(input.NotificationKey, out var pref))
            {
                // A new row starts from the key's defaults, so a field the client left out keeps its default.
                var descriptor = NotificationKeyDescriptor.Find(input.NotificationKey)!;
                var defaults = descriptor.DefaultChannels;
                pref = new UserNotificationPreference
                {
                    TenantId = tenantId,
                    UserId = userId,
                    NotificationKey = descriptor.Key,
                    PushEnabled = defaults.HasFlag(NotificationChannels.Push),
                    WhatsAppEnabled = defaults.HasFlag(NotificationChannels.WhatsApp),
                };
                db.UserNotificationPreferences.Add(pref);
                existingByKey[input.NotificationKey] = pref;
            }

            pref.EmailEnabled = input.EmailEnabled;
            pref.InAppEnabled = input.InAppEnabled;
            pref.PushEnabled = input.PushEnabled ?? pref.PushEnabled;
            pref.WhatsAppEnabled = input.WhatsAppEnabled ?? pref.WhatsAppEnabled;
            pref.SmsEnabled = input.SmsEnabled ?? pref.SmsEnabled;
        }

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
