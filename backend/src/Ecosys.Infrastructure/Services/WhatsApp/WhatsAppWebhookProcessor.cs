using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

public sealed record WhatsAppWebhookResult(int StatusesApplied, int OptOuts, int OptIns, int OtherMessages);

/// <summary>
/// The shared number's webhook (design 4.3). Checks Meta's signature, records delivery status on outbox
/// rows by Meta's message id, and acts on STOP/START. Other replies are only logged here, platform-side:
/// one number serves every tenant, so a reply can't be tied to one tenant.
/// </summary>
public interface IWhatsAppWebhookProcessor
{
    /// <summary>The GET handshake: true when the token is the platform verify token.</summary>
    bool IsVerifyTokenValid(string? token);

    /// <summary><c>X-Hub-Signature-256</c> is <c>sha256=</c> + hex HMAC-SHA256 of the raw body with the app secret.</summary>
    bool IsSignatureValid(ReadOnlySpan<byte> body, string? signatureHeader);

    Task<WhatsAppWebhookResult> HandleAsync(JsonElement payload, CancellationToken cancellationToken = default);
}

internal sealed class WhatsAppWebhookProcessor(AppDbContext dbContext, IOptions<WhatsAppOptions> options, ILogger<WhatsAppWebhookProcessor> logger) : IWhatsAppWebhookProcessor
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase) { "STOP", "STOP ALL", "STOPALL", "UNSUBSCRIBE" };
    private static readonly HashSet<string> StartWords = new(StringComparer.OrdinalIgnoreCase) { "START", "UNSTOP" };

    public bool IsVerifyTokenValid(string? token)
    {
        var expected = options.Value.WebhookVerifyToken;
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(token))
            return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expected));
    }

    public bool IsSignatureValid(ReadOnlySpan<byte> body, string? signatureHeader) =>
        ComputeIsValid(options.Value.AppSecret, body, signatureHeader);

    internal static bool ComputeIsValid(string? appSecret, ReadOnlySpan<byte> body, string? signatureHeader)
    {
        const string prefix = "sha256=";
        if (string.IsNullOrEmpty(appSecret) || string.IsNullOrWhiteSpace(signatureHeader)
            || !signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(signatureHeader.AsSpan(prefix.Length).Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        return provided.Length == expected.Length && CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    public async Task<WhatsAppWebhookResult> HandleAsync(JsonElement payload, CancellationToken cancellationToken = default)
    {
        int statuses = 0, optOuts = 0, optIns = 0, other = 0;
        foreach (var value in Values(payload))
        {
            if (value.TryGetProperty("statuses", out var statusList) && statusList.ValueKind == JsonValueKind.Array)
                foreach (var status in statusList.EnumerateArray())
                    if (await ApplyStatusAsync(status, cancellationToken))
                        statuses++;

            if (value.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
            {
                foreach (var message in messages.EnumerateArray())
                {
                    var from = WhatsAppPhoneNumbers.Normalize("+" + GetString(message, "from"));
                    var text = ReadText(message)?.Trim();
                    if (from is not null && text is not null && StopWords.Contains(text))
                    {
                        await OptOutAsync(from, text.ToUpperInvariant(), cancellationToken);
                        optOuts++;
                    }
                    else if (from is not null && text is not null && StartWords.Contains(text))
                    {
                        await OptInAsync(from, cancellationToken);
                        optIns++;
                    }
                    else
                    {
                        // Not shown to any tenant (design 4.1). The content is not logged.
                        logger.LogInformation("WhatsApp reply from {Phone} ({Type}) received on the shared number; not routed to a tenant.",
                            WhatsAppPhoneNumbers.Mask(from), GetString(message, "type") ?? "unknown");
                        other++;
                    }
                }
            }
        }
        return new WhatsAppWebhookResult(statuses, optOuts, optIns, other);
    }

    private async Task<bool> ApplyStatusAsync(JsonElement status, CancellationToken cancellationToken)
    {
        var id = GetString(status, "id");
        var state = GetString(status, "status")?.ToLowerInvariant();
        if (string.IsNullOrEmpty(id) || state is null)
            return false;

        var row = await dbContext.WhatsAppOutboxMessages.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.ProviderMessageId == id, cancellationToken);
        if (row is null)
            return false;

        var at = long.TryParse(GetString(status, "timestamp"), out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : DateTime.UtcNow;
        switch (state)
        {
            case "sent":
                row.SentAt ??= at;
                break;
            case "delivered":
                row.DeliveredAt ??= at;
                if (row.Status is WhatsAppOutboxStatuses.Sent or WhatsAppOutboxStatuses.Sending or WhatsAppOutboxStatuses.Pending)
                    row.Status = WhatsAppOutboxStatuses.Delivered;
                break;
            case "read":
                row.DeliveredAt ??= at;
                row.ReadAt ??= at;
                if (row.Status != WhatsAppOutboxStatuses.Failed)
                    row.Status = WhatsAppOutboxStatuses.Read;
                break;
            case "failed":
                row.Status = WhatsAppOutboxStatuses.Failed;
                row.FailedAt ??= at;
                row.ErrorMessage = ReadStatusError(status) ?? "Meta could not deliver the message.";
                break;
            default:
                return false;
        }
        row.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task OptOutAsync(string phone, string keyword, CancellationToken cancellationToken)
    {
        if (!await dbContext.WhatsAppOptOuts.AnyAsync(x => x.PhoneNumber == phone, cancellationToken))
        {
            dbContext.WhatsAppOptOuts.Add(new WhatsAppOptOut { PhoneNumber = phone, Keyword = keyword, OptedOutAt = DateTime.UtcNow });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation("WhatsApp number {Phone} opted out of the shared number.", WhatsAppPhoneNumbers.Mask(phone));
    }

    private async Task OptInAsync(string phone, CancellationToken cancellationToken)
    {
        var rows = await dbContext.WhatsAppOptOuts.Where(x => x.PhoneNumber == phone).ToListAsync(cancellationToken);
        if (rows.Count > 0)
        {
            dbContext.WhatsAppOptOuts.RemoveRange(rows);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        logger.LogInformation("WhatsApp number {Phone} opted back in to the shared number.", WhatsAppPhoneNumbers.Mask(phone));
    }

    private static IEnumerable<JsonElement> Values(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            yield break;
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var change in changes.EnumerateArray())
                if (change.ValueKind == JsonValueKind.Object && change.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Object)
                    yield return value;
        }
    }

    /// <summary>A text message's body, or a quick-reply button's text.</summary>
    private static string? ReadText(JsonElement message)
    {
        if (message.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.Object)
            return GetString(text, "body");
        if (message.TryGetProperty("button", out var button) && button.ValueKind == JsonValueKind.Object)
            return GetString(button, "text");
        return null;
    }

    private static string? ReadStatusError(JsonElement status)
    {
        if (!status.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() == 0)
            return null;
        var first = errors[0];
        var title = GetString(first, "title") ?? GetString(first, "message");
        var code = first.TryGetProperty("code", out var c) ? c.ToString() : null;
        var text = $"{title} (code {code})";
        return text.Length <= 1000 ? text : text[..1000];
    }

    private static string? GetString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;
}
