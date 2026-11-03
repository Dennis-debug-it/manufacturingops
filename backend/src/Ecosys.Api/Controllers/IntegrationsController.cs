using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public sealed class IntegrationsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IAuditLogService auditLogService,
    IHttpClientFactory httpClientFactory) : TenantAwareControllerBase(tenantContext)
{
    private static readonly string[] AllowedWebhookEvents =
    [
        "work_order.created",
        "work_order.completed",
        "invoice.issued",
        "invoice.paid",
        "quotation.accepted",
    ];

    // ── API Keys ─────────────────────────────────────────────────────────────

    [HttpGet("api-keys")]
    public async Task<ActionResult<IReadOnlyCollection<ApiKeyResponse>>> GetApiKeys(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var items = await dbContext.TenantApiKeys
            .Where(x => x.TenantId == TenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(x => MapApiKey(x)).ToList());
    }

    [HttpPost("api-keys")]
    public async Task<ActionResult<ApiKeyCreatedResponse>> CreateApiKey([FromBody] CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            throw new BusinessRuleException("A label is required to identify the key.");
        }

        var rawKey = GenerateApiKey();
        var item = new TenantApiKey
        {
            TenantId = TenantId,
            Label = request.Label.Trim(),
            KeyPrefix = rawKey[..12],
            KeyHash = HashSecret(rawKey),
            CreatedByUserId = UserId,
        };

        dbContext.TenantApiKeys.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "settings.api-key.created", nameof(TenantApiKey), item.Id.ToString(), $"API key '{item.Label}' created.", cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(GetApiKeys), new ApiKeyCreatedResponse(item.Id, item.Label, item.KeyPrefix, item.CreatedAt, item.LastUsedAt, "Active", rawKey));
    }

    [HttpDelete("api-keys/{id:guid}")]
    public async Task<IActionResult> RevokeApiKey(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var item = await dbContext.TenantApiKeys
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("API key was not found.");

        dbContext.TenantApiKeys.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "settings.api-key.revoked", nameof(TenantApiKey), item.Id.ToString(), $"API key '{item.Label}' revoked.", cancellationToken: cancellationToken);
        return NoContent();
    }

    // ── Webhooks ─────────────────────────────────────────────────────────────

    [HttpGet("webhooks")]
    public async Task<ActionResult<IReadOnlyCollection<WebhookResponse>>> GetWebhooks(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var items = await dbContext.TenantWebhooks
            .Where(x => x.TenantId == TenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(MapWebhook).ToList());
    }

    [HttpPost("webhooks")]
    public async Task<ActionResult<WebhookCreatedResponse>> CreateWebhook([FromBody] CreateWebhookRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessRuleException("A valid https:// endpoint URL is required.");
        }

        var events = (request.Events ?? []).Select(e => e.Trim()).Where(e => e.Length > 0).Distinct().ToList();
        if (events.Count == 0)
        {
            throw new BusinessRuleException("At least one event must be selected.");
        }

        var unknown = events.Where(e => !AllowedWebhookEvents.Contains(e)).ToList();
        if (unknown.Count > 0)
        {
            throw new BusinessRuleException($"Unknown event(s): {string.Join(", ", unknown)}.");
        }

        var rawSecret = GenerateApiKey();
        var item = new TenantWebhook
        {
            TenantId = TenantId,
            Url = request.Url.Trim(),
            EventsJson = JsonSerializer.Serialize(events),
            SecretHash = HashSecret(rawSecret),
            CreatedByUserId = UserId,
        };

        dbContext.TenantWebhooks.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "settings.webhook.created", nameof(TenantWebhook), item.Id.ToString(), $"Webhook '{item.Url}' created.", cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(GetWebhooks), new WebhookCreatedResponse(item.Id, item.Url, events, "Active", item.CreatedAt, rawSecret));
    }

    [HttpPut("webhooks/{id:guid}")]
    public async Task<ActionResult<WebhookResponse>> UpdateWebhook(Guid id, [FromBody] UpdateWebhookRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var item = await dbContext.TenantWebhooks
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Webhook was not found.");

        if (string.IsNullOrWhiteSpace(request.Url) || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessRuleException("A valid https:// endpoint URL is required.");
        }

        var events = (request.Events ?? []).Select(e => e.Trim()).Where(e => e.Length > 0).Distinct().ToList();
        if (events.Count == 0)
        {
            throw new BusinessRuleException("At least one event must be selected.");
        }

        var unknown = events.Where(e => !AllowedWebhookEvents.Contains(e)).ToList();
        if (unknown.Count > 0)
        {
            throw new BusinessRuleException($"Unknown event(s): {string.Join(", ", unknown)}.");
        }

        item.Url = request.Url.Trim();
        item.EventsJson = JsonSerializer.Serialize(events);
        item.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "settings.webhook.updated", nameof(TenantWebhook), item.Id.ToString(), $"Webhook '{item.Url}' updated.", cancellationToken: cancellationToken);
        return Ok(MapWebhook(item));
    }

    [HttpPost("webhooks/{id:guid}/test")]
    public async Task<ActionResult<WebhookDeliveryResponse>> TestWebhook(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var webhook = await dbContext.TenantWebhooks
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Webhook was not found.");

        var payload = JsonSerializer.Serialize(new
        {
            @event = "webhook.test",
            sentAt = DateTime.UtcNow,
            webhookId = webhook.Id,
        });

        var delivery = new TenantWebhookDelivery
        {
            TenantId = TenantId,
            WebhookId = webhook.Id,
            Event = "webhook.test",
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var client = httpClientFactory.CreateClient("WebhookDelivery");
            client.Timeout = TimeSpan.FromSeconds(10);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(webhook.Url, content, cancellationToken);
            stopwatch.Stop();

            delivery.ResponseStatusCode = (int)response.StatusCode;
            delivery.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            delivery.Status = response.IsSuccessStatusCode ? "Success" : "Failed";
            if (!response.IsSuccessStatusCode)
            {
                delivery.ErrorMessage = $"Endpoint responded with {(int)response.StatusCode} {response.StatusCode}.";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            stopwatch.Stop();
            delivery.Status = "Failed";
            delivery.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            delivery.ErrorMessage = ex is TaskCanceledException ? "Request timed out." : ex.Message;
        }

        dbContext.TenantWebhookDeliveries.Add(delivery);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "settings.webhook.tested", nameof(TenantWebhook), webhook.Id.ToString(), $"Test ping sent to webhook '{webhook.Url}' ({delivery.Status}).", cancellationToken: cancellationToken);

        return Ok(new WebhookDeliveryResponse(delivery.Id, delivery.Event, delivery.Status, delivery.ResponseStatusCode, delivery.ErrorMessage, delivery.DurationMs, delivery.AttemptedAt));
    }

    [HttpGet("webhooks/{id:guid}/delivery-log")]
    public async Task<ActionResult<IReadOnlyCollection<WebhookDeliveryResponse>>> GetWebhookDeliveryLog(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var exists = await dbContext.TenantWebhooks.AnyAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Webhook was not found.");
        }

        var deliveries = await dbContext.TenantWebhookDeliveries
            .Where(x => x.TenantId == TenantId && x.WebhookId == id)
            .OrderByDescending(x => x.AttemptedAt)
            .Take(50)
            .Select(x => new WebhookDeliveryResponse(x.Id, x.Event, x.Status, x.ResponseStatusCode, x.ErrorMessage, x.DurationMs, x.AttemptedAt))
            .ToListAsync(cancellationToken);

        return Ok(deliveries);
    }

    [HttpDelete("webhooks/{id:guid}")]
    public async Task<IActionResult> DeleteWebhook(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var item = await dbContext.TenantWebhooks
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Webhook was not found.");

        dbContext.TenantWebhooks.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "settings.webhook.deleted", nameof(TenantWebhook), item.Id.ToString(), $"Webhook '{item.Url}' deleted.", cancellationToken: cancellationToken);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string GenerateApiKey() => $"sk_live_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()}";

    private static string HashSecret(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim())));

    private static ApiKeyResponse MapApiKey(TenantApiKey x) =>
        new(x.Id, x.Label, x.KeyPrefix, x.CreatedAt, x.LastUsedAt, x.RevokedAt.HasValue ? "Revoked" : "Active");

    private static WebhookResponse MapWebhook(TenantWebhook x) =>
        new(x.Id, x.Url, JsonSerializer.Deserialize<List<string>>(x.EventsJson) ?? [], x.IsActive ? "Active" : "Disabled", x.CreatedAt);
}

public sealed record CreateApiKeyRequest(string Label);
public sealed record ApiKeyResponse(Guid Id, string Label, string KeyPrefix, DateTime CreatedAt, DateTime? LastUsedAt, string Status);
public sealed record ApiKeyCreatedResponse(Guid Id, string Label, string KeyPrefix, DateTime CreatedAt, DateTime? LastUsedAt, string Status, string ApiKey);

public sealed record CreateWebhookRequest(string Url, List<string>? Events);
public sealed record UpdateWebhookRequest(string Url, List<string>? Events, bool IsActive);
public sealed record WebhookResponse(Guid Id, string Url, IReadOnlyCollection<string> Events, string Status, DateTime CreatedAt);
public sealed record WebhookCreatedResponse(Guid Id, string Url, IReadOnlyCollection<string> Events, string Status, DateTime CreatedAt, string Secret);
public sealed record WebhookDeliveryResponse(Guid Id, string Event, string Status, int? ResponseStatusCode, string? ErrorMessage, int? DurationMs, DateTime AttemptedAt);
