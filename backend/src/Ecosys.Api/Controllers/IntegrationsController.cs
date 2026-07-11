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
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
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
public sealed record WebhookResponse(Guid Id, string Url, IReadOnlyCollection<string> Events, string Status, DateTime CreatedAt);
public sealed record WebhookCreatedResponse(Guid Id, string Url, IReadOnlyCollection<string> Events, string Status, DateTime CreatedAt, string Secret);
