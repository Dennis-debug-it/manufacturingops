namespace Ecosys.Shared.Contracts.Integration;

public enum EmailDeliveryMode
{
    Smtp,
    Api,
    Disabled
}

public enum EmailSecureMode
{
    Auto,
    None,
    StartTls,
    SslOnConnect
}

public static class EmailErrorCategories
{
    public const string ConnectionFailed = "Connection failed";
    public const string AuthenticationFailed = "Authentication failed";
    public const string TlsSslFailed = "TLS/SSL failed";
    public const string SenderRejected = "Sender rejected";
    public const string RecipientRejected = "Recipient rejected";
    public const string ProviderRejectedMessage = "Provider rejected message";
    public const string Timeout = "Timeout";
    public const string UnknownSmtpError = "Unknown SMTP error";
}

public sealed record EmailAttachment(
    string Filename,
    byte[] Content,
    string ContentType = "application/pdf");

public sealed record EmailMessage(
    string ToAddress,
    string Subject,
    string Body,
    string? FromName,
    string? FromAddress,
    string? ReplyToEmail = null,
    bool IsHtml = false,
    IReadOnlyList<EmailAttachment>? Attachments = null,
    IReadOnlyList<string>? CcAddresses = null);

public sealed record EmailDeliverySettings(
    EmailDeliveryMode DeliveryMode,
    string? SmtpHost,
    int SmtpPort,
    string? SmtpUsername,
    string? SmtpPasswordSecret,
    string? SenderName,
    string? SenderEmail,
    string? ReplyToEmail,
    bool EnableSsl,
    EmailSecureMode SecureMode,
    string? ApiEndpoint = null,
    string? ApiKeySecret = null,
    string? ApiProviderName = null,
    int TimeoutSeconds = 30,
    int MaxRetries = 0);

public sealed record EmailVerificationResult(
    bool Success,
    string? ErrorCategory = null,
    string? TechnicalMessage = null);

public sealed class EmailDeliveryException(string errorCategory, string technicalMessage, Exception? innerException = null)
    : Exception(technicalMessage, innerException)
{
    public string ErrorCategory { get; } = string.IsNullOrWhiteSpace(errorCategory)
        ? EmailErrorCategories.UnknownSmtpError
        : errorCategory;
}

public interface IEmailSender
{
    Task SendAsync(
        EmailMessage message,
        EmailDeliverySettings settings,
        CancellationToken cancellationToken = default);

    Task<EmailVerificationResult> VerifyAsync(
        EmailDeliverySettings settings,
        CancellationToken cancellationToken = default);
}
