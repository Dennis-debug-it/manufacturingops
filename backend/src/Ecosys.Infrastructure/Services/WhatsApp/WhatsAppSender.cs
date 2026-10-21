using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The shared Ecosys WhatsApp Business account (Release 3 design 4.1, R5). Platform configuration from
/// environment secrets (<c>WhatsApp__AccessToken</c> etc.), never tenant data.
/// </summary>
public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    /// <summary>Master switch for the platform. Off, or without a phone number id and token, nothing is sent.</summary>
    public bool Enabled { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? BusinessAccountId { get; set; }
    /// <summary>A permanent system-user token with whatsapp_business_messaging.</summary>
    public string? AccessToken { get; set; }
    /// <summary>The Meta app secret, used to check the webhook's X-Hub-Signature-256.</summary>
    public string? AppSecret { get; set; }
    /// <summary>The token Meta echoes on the webhook's GET handshake.</summary>
    public string? WebhookVerifyToken { get; set; }
    public string GraphApiVersion { get; set; } = "v21.0";
    public string BaseUrl { get; set; } = "https://graph.facebook.com";
    /// <summary>Messages per tenant per calendar month (UTC). Beyond it WhatsApp is skipped; email still goes.</summary>
    public int MonthlyCapPerTenant { get; set; } = 1000;
    /// <summary>The number people see, e.g. +254 700 000 000, shown on the tenant settings page.</summary>
    public string? DisplayPhoneNumber { get; set; }

    public bool IsConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(PhoneNumberId) && !string.IsNullOrWhiteSpace(AccessToken);
}

/// <summary>The account a tenant's messages are sent from.</summary>
public sealed record WhatsAppSendingAccount(string PhoneNumberId, string AccessToken, string GraphApiVersion, string BaseUrl, string? DisplayPhoneNumber);

/// <summary>
/// Picks the sending account for a tenant. Today every tenant gets the shared Ecosys account (R5); a
/// tenant-owned number would be resolved here later from <c>WhatsAppSetting</c>. Null = not configured.
/// </summary>
public interface IWhatsAppAccountResolver
{
    WhatsAppSendingAccount? Resolve(Guid tenantId);
}

internal sealed class SharedWhatsAppAccountResolver(IOptions<WhatsAppOptions> options) : IWhatsAppAccountResolver
{
    public WhatsAppSendingAccount? Resolve(Guid tenantId)
    {
        var o = options.Value;
        return o.IsConfigured
            ? new WhatsAppSendingAccount(o.PhoneNumberId!.Trim(), o.AccessToken!.Trim(), o.GraphApiVersion.Trim(), o.BaseUrl.Trim().TrimEnd('/'), o.DisplayPhoneNumber)
            : null;
    }
}

/// <param name="Retryable">A network error, 429 or 5xx: worth another attempt.</param>
public sealed record WhatsAppSendResult(bool Succeeded, string? MessageId, string? Error, bool Retryable)
{
    public static WhatsAppSendResult Sent(string messageId) => new(true, messageId, null, false);
    public static WhatsAppSendResult Failed(string error, bool retryable) => new(false, null, error, retryable);
}

public interface IWhatsAppSender
{
    /// <param name="buttonParameters">For an authentication template: the code again, for its copy-code button.</param>
    Task<WhatsAppSendResult> SendTemplateAsync(WhatsAppSendingAccount account, string toE164, string templateName, string language,
        IReadOnlyList<string> bodyParameters, IReadOnlyList<string>? buttonParameters, CancellationToken cancellationToken);
}

/// <summary>Meta WhatsApp Cloud API: <c>POST {base}/{version}/{phoneNumberId}/messages</c> with a template.</summary>
internal sealed class MetaWhatsAppSender(IHttpClientFactory httpClientFactory, ILogger<MetaWhatsAppSender> logger) : IWhatsAppSender
{
    public const string HttpClientName = "whatsapp-cloud-api";

    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public async Task<WhatsAppSendResult> SendTemplateAsync(WhatsAppSendingAccount account, string toE164, string templateName, string language,
        IReadOnlyList<string> bodyParameters, IReadOnlyList<string>? buttonParameters, CancellationToken cancellationToken)
    {
        var url = $"{account.BaseUrl}/{account.GraphApiVersion}/{Uri.EscapeDataString(account.PhoneNumberId)}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(BuildPayload(toE164, templateName, language, bodyParameters, buttonParameters), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var id = ReadMessageId(body);
                return id is null
                    ? WhatsAppSendResult.Failed("Meta accepted the request but returned no message id.", retryable: false)
                    : WhatsAppSendResult.Sent(id);
            }

            var retryable = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            return WhatsAppSendResult.Failed($"Meta returned {(int)response.StatusCode}: {ReadError(body)}", retryable);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "WhatsApp Cloud API request failed.");
            return WhatsAppSendResult.Failed($"Could not reach Meta: {ex.Message}", retryable: true);
        }
    }

    /// <summary>The JSON body Meta expects for a template message (public for tests).</summary>
    public static string BuildPayload(string toE164, string templateName, string language, IReadOnlyList<string> bodyParameters, IReadOnlyList<string>? buttonParameters)
    {
        var components = new List<object>();
        if (bodyParameters.Count > 0)
            components.Add(new { type = "body", parameters = bodyParameters.Select(p => new { type = "text", text = p }).ToArray() });
        if (buttonParameters is { Count: > 0 })
            components.Add(new { type = "button", sub_type = "url", index = "0", parameters = buttonParameters.Select(p => new { type = "text", text = p }).ToArray() });

        return JsonSerializer.Serialize(new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = toE164.TrimStart('+'),
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = language },
                components = components.Count > 0 ? components : null,
            },
        }, Json);
    }

    private static string? ReadMessageId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("id", out var id) ? id.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;
                var code = error.TryGetProperty("code", out var c) ? c.ToString() : null;
                return Clip($"{message} (code {code})");
            }
        }
        catch (JsonException)
        {
        }
        return Clip(body);
    }

    private static string Clip(string value) => value.Length <= 300 ? value : value[..300];
}
