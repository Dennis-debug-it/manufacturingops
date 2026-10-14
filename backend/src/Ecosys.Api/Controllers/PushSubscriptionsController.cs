using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

public sealed record PushPublicKeyResponse(string PublicKey);
public sealed record PushSubscriptionKeysInput(string? P256dh, string? Auth);
public sealed record SavePushSubscriptionInput(string? Endpoint, PushSubscriptionKeysInput? Keys);
public sealed record DeletePushSubscriptionInput(string? Endpoint);

/// <summary>
/// Lets a signed-in user's browser register for push notifications (currently: new job assignments).
/// </summary>
[ApiController]
[Authorize]
[Route("api/me/push")]
public sealed class PushSubscriptionsController(
    AppDbContext db,
    IVapidKeyProvider vapidKeyProvider,
    ITenantContext tenantContext) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>
    /// The server POSTs to the stored endpoint, so only the browsers' real push services are accepted;
    /// anything else would let a user make the API call arbitrary URLs (SSRF).
    /// </summary>
    private static readonly string[] AllowedPushHostSuffixes =
    [
        "fcm.googleapis.com",          // Chrome, Edge (Chromium), Samsung Internet, Opera
        "android.googleapis.com",
        "push.services.mozilla.com",   // Firefox
        "push.apple.com",              // Safari (macOS, iOS home-screen apps)
        "notify.windows.com",          // legacy Edge
    ];

    [HttpGet("public-key")]
    public async Task<ActionResult<PushPublicKeyResponse>> GetPublicKey(CancellationToken cancellationToken)
    {
        _ = TenantId;
        var keys = await vapidKeyProvider.GetAsync(cancellationToken);
        return Ok(new PushPublicKeyResponse(keys.PublicKey));
    }

    [HttpPost("subscriptions")]
    public async Task<IActionResult> Save([FromBody] SavePushSubscriptionInput input, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var userId = UserId;

        var endpoint = input.Endpoint?.Trim();
        var p256dh = input.Keys?.P256dh?.Trim();
        var auth = input.Keys?.Auth?.Trim();
        if (!IsAllowedEndpoint(endpoint) || string.IsNullOrEmpty(p256dh) || string.IsNullOrEmpty(auth)
            || p256dh.Length > 200 || auth.Length > 100)
        {
            return BadRequest(new { message = "This browser's push subscription isn't supported." });
        }

        // Endpoints are unique per browser; if this device was signed in as someone else, it moves to this user.
        var existing = await db.PushSubscriptions
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Endpoint == endpoint, cancellationToken);
        if (existing is null)
        {
            existing = new PushSubscription { Endpoint = endpoint! };
            db.PushSubscriptions.Add(existing);
        }

        existing.TenantId = tenantId;
        existing.UserId = userId;
        existing.P256dh = p256dh;
        existing.Auth = auth;
        var userAgent = Request.Headers.UserAgent.ToString();
        existing.UserAgent = userAgent.Length > 500 ? userAgent[..500] : userAgent;

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("subscriptions")]
    public async Task<IActionResult> Delete([FromBody] DeletePushSubscriptionInput input, CancellationToken cancellationToken)
    {
        var tenantId = TenantId;
        var userId = UserId;
        var endpoint = input.Endpoint?.Trim();

        var existing = await db.PushSubscriptions
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.UserId == userId && x.Endpoint == endpoint, cancellationToken);
        if (existing is not null)
        {
            db.PushSubscriptions.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    public static bool IsAllowedEndpoint(string? endpoint)
    {
        if (string.IsNullOrEmpty(endpoint) || endpoint.Length > 2000
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var host = uri.IdnHost.ToLowerInvariant();
        return AllowedPushHostSuffixes.Any(suffix => host == suffix || host.EndsWith("." + suffix, StringComparison.Ordinal));
    }
}
