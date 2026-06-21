using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

// ── Request / Response models ────────────────────────────────────────────────

public sealed record UserNotificationPreferenceResponse(
    string NotificationKey,
    string Label,
    string Category,
    bool EmailEnabled,
    bool InAppEnabled);

public sealed record UpdateNotificationPreferenceInput(
    string NotificationKey,
    bool EmailEnabled,
    bool InAppEnabled);

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
    /// Missing rows default to enabled = true for both channels.
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

        var result = NotificationKeyDescriptor.List()
            .Select(descriptor =>
            {
                var pref = savedByKey.GetValueOrDefault(descriptor.Key);
                return new UserNotificationPreferenceResponse(
                    descriptor.Key,
                    descriptor.Label,
                    descriptor.Category,
                    EmailEnabled: pref?.EmailEnabled ?? true,
                    InAppEnabled: pref?.InAppEnabled ?? true);
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

        var keys = inputs.Select(i => i.NotificationKey).ToList();

        var existing = await db.UserNotificationPreferences
            .Where(p => p.TenantId == tenantId && p.UserId == userId && keys.Contains(p.NotificationKey))
            .ToListAsync(cancellationToken);

        var existingByKey = existing.ToDictionary(p => p.NotificationKey, StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            if (existingByKey.TryGetValue(input.NotificationKey, out var pref))
            {
                pref.EmailEnabled = input.EmailEnabled;
                pref.InAppEnabled = input.InAppEnabled;
            }
            else
            {
                db.UserNotificationPreferences.Add(new UserNotificationPreference
                {
                    TenantId = tenantId,
                    UserId = userId,
                    NotificationKey = input.NotificationKey,
                    EmailEnabled = input.EmailEnabled,
                    InAppEnabled = input.InAppEnabled,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
