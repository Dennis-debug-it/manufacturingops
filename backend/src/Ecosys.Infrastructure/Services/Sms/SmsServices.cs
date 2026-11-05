using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services.Sms;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    /// <summary>"AfricasTalking", or blank to send no SMS.</summary>
    public string? Provider { get; set; }
    public string? Username { get; set; }
    public string? ApiKey { get; set; }
    /// <summary>Registered sender name or short code; blank uses the provider's default.</summary>
    public string? SenderId { get; set; }
    public string BaseUrl { get; set; } = "https://api.africastalking.com";
}

/// <summary>Sends text messages. Behind an interface so the provider can change without touching callers.</summary>
public interface ISmsGateway
{
    bool IsConfigured { get; }

    /// <summary>Sends one message. Returns false (and logs) when it could not be sent; never throws for a provider failure.</summary>
    Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}

/// <summary>Used when no provider is set up: sends nothing.</summary>
internal sealed class DisabledSmsGateway : ISmsGateway
{
    public bool IsConfigured => false;
    public Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default) => Task.FromResult(false);
}

/// <summary>Africa's Talking bulk SMS (POST /version1/messaging, form-encoded, apiKey header).</summary>
internal sealed class AfricasTalkingSmsGateway(IHttpClientFactory httpClientFactory, IOptions<SmsOptions> options, ILogger<AfricasTalkingSmsGateway> logger) : ISmsGateway
{
    public const string HttpClientName = "africastalking-sms";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.Username) && !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        var settings = options.Value;
        var fields = new Dictionary<string, string>
        {
            ["username"] = settings.Username!,
            ["to"] = phoneNumber,
            ["message"] = message,
        };
        if (!string.IsNullOrWhiteSpace(settings.SenderId))
        {
            fields["from"] = settings.SenderId;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.BaseUrl.TrimEnd('/')}/version1/messaging")
        {
            Content = new FormUrlEncodedContent(fields)
        };
        request.Headers.Add("apiKey", settings.ApiKey);
        request.Headers.Add("Accept", "application/json");

        try
        {
            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            logger.LogWarning("SMS to {Phone} was refused: {Status}.", SmsText.Mask(phoneNumber), (int)response.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "SMS to {Phone} failed.", SmsText.Mask(phoneNumber));
            return false;
        }
    }
}

public static class SmsText
{
    /// <summary>Two SMS segments; longer alerts are cut, since the link in the app has the detail.</summary>
    public const int MaxLength = 300;

    /// <summary>
    /// Puts a number in international form for the gateway. Kenyan local numbers (07…, 01…) get
    /// +254; anything already starting with + is kept. Returns null for something that isn't a number.
    /// </summary>
    public static string? NormalizePhone(string? phone, string defaultCountryCode = "254")
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 9)
        {
            return null;
        }

        if (phone.TrimStart().StartsWith('+')) return $"+{digits}";
        if (digits.StartsWith("00")) return $"+{digits[2..]}";
        if (digits.StartsWith('0')) return $"+{defaultCountryCode}{digits[1..]}";
        if (digits.StartsWith(defaultCountryCode)) return $"+{digits}";
        return digits.Length == 9 ? $"+{defaultCountryCode}{digits}" : $"+{digits}";
    }

    public static string Compose(string title, string body)
    {
        var text = string.IsNullOrWhiteSpace(body) ? title : $"{title}: {body}";
        return text.Length <= MaxLength ? text : $"{text[..(MaxLength - 1)]}…";
    }

    public static string Mask(string phone) => phone.Length <= 4 ? "****" : $"{new string('*', phone.Length - 4)}{phone[^4..]}";
}

/// <summary>Delivers notifications whose resolved channels include SMS to the recipient's phone.</summary>
internal sealed class SmsNotificationChannel(ISmsGateway gateway) : INotificationChannel
{
    public NotificationChannels Channel => NotificationChannels.Sms;

    public async Task SendAsync(NotificationDelivery delivery, CancellationToken cancellationToken = default)
    {
        if (!gateway.IsConfigured || SmsText.NormalizePhone(delivery.Phone) is not { } phone)
        {
            return;
        }

        await gateway.SendAsync(phone, SmsText.Compose(delivery.Request.Title, delivery.Request.Body), cancellationToken);
    }
}
