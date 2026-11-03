using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Options;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Net.Sockets;
using System.Security.Authentication;

namespace Ecosys.Infrastructure.Integrations;

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> smtpOptions, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(
        EmailMessage message,
        EmailDeliverySettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings.DeliveryMode == EmailDeliveryMode.Disabled)
        {
            logger.LogInformation("Email delivery is disabled. Skipping message send.");
            return;
        }

        if (settings.DeliveryMode == EmailDeliveryMode.Api)
        {
            throw new NotSupportedException("Generic Email API delivery mode is not implemented yet.");
        }

        var options = smtpOptions.Value;
        var host = FirstNonBlank(settings.SmtpHost, options.Host);
        var port = settings.SmtpPort > 0 ? settings.SmtpPort : options.Port;
        var username = FirstNonBlank(settings.SmtpUsername, options.Username);
        var secret = FirstNonBlank(settings.SmtpPasswordSecret, options.Password);
        var secureMode = settings.SecureMode;

        if (string.IsNullOrWhiteSpace(host))
        {
            // Nothing in the database row and nothing in Smtp:* / ECOSYS_SMTP_* — genuinely
            // unconfigured. Throwing rather than returning is deliberate: a silent return let the
            // outbox mark the message Sent and let password reset report success (EC-P1-06d).
            logger.LogWarning("SMTP host is not configured in either the tenant email settings or the Smtp configuration section. Email was not sent.");
            throw new EmailDeliveryException(
                EmailErrorCategories.ConnectionFailed,
                "SMTP host is not configured in either the tenant email settings or the Smtp configuration section.");
        }

        // The message's From is the sender identity; the SMTP username is only the transport login. They are
        // deliberately different (tenant mail is sent as {slug}@serviceops... through the platform account),
        // so the login never replaces a From the caller supplied.
        var fromAddress = message.FromAddress ?? settings.SenderEmail ?? username ?? "noreply@ecosys.local";

        logger.LogInformation(
            "Sending email using delivery mode {DeliveryMode}, host {Host}, port {Port}, secure mode {SecureMode}, sender {SenderEmail}, authenticated as {SmtpUsername}.",
            settings.DeliveryMode,
            host,
            port,
            secureMode,
            fromAddress,
            username);

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(message.FromName ?? settings.SenderName ?? "Ecosys", fromAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.ToAddress));
        if (message.CcAddresses is { Count: > 0 })
        {
            foreach (var ccAddress in message.CcAddresses.Where(address => !string.IsNullOrWhiteSpace(address)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                mimeMessage.Cc.Add(MailboxAddress.Parse(ccAddress));
            }
        }

        var replyTo = message.ReplyToEmail ?? settings.ReplyToEmail;
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            mimeMessage.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        }

        mimeMessage.Subject = message.Subject;

        var textPart = new TextPart(message.IsHtml ? "html" : "plain") { Text = message.Body };

        if (message.Attachments is { Count: > 0 })
        {
            var multipart = new Multipart("mixed");
            multipart.Add(textPart);
            foreach (var attachment in message.Attachments)
            {
                var part = new MimePart(ContentType.Parse(attachment.ContentType))
                {
                    Content = new MimeContent(new System.IO.MemoryStream(attachment.Content)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = attachment.Filename,
                };
                multipart.Add(part);
            }
            mimeMessage.Body = multipart;
        }
        else
        {
            mimeMessage.Body = textPart;
        }

        try
        {
            using var client = new SmtpClient();
            client.Timeout = Math.Max(5, settings.TimeoutSeconds) * 1000;
            await client.ConnectAsync(host, port, ResolveSecureSocketOptions(secureMode, settings.EnableSsl), cancellationToken);

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(secret))
            {
                await client.AuthenticateAsync(username, secret, cancellationToken);
            }

            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            var category = CategorizeError(ex);
            if (category == EmailErrorCategories.SenderRejected)
            {
                // Infrastructure, not a code fault: the transport does not know this sender. Reported as-is
                // rather than silently rewriting From to an address the server does accept.
                logger.LogError(
                    ex,
                    "Mail server {Host} refused sender {SenderEmail} (authenticated as {SmtpUsername}). The address must be provisioned on the mail server and the SMTP account allowed to send as it.",
                    host,
                    fromAddress,
                    username);
            }
            else
            {
                logger.LogWarning(
                    ex,
                    "Email send failed with category {ErrorCategory}. Host {Host}, port {Port}, secure mode {SecureMode}.",
                    category,
                    host,
                    port,
                    secureMode);
            }

            throw new EmailDeliveryException(category, ex.Message, ex);
        }
    }

    public async Task<EmailVerificationResult> VerifyAsync(
        EmailDeliverySettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings.DeliveryMode == EmailDeliveryMode.Disabled)
        {
            return new EmailVerificationResult(true, null, null);
        }

        if (settings.DeliveryMode == EmailDeliveryMode.Api)
        {
            return new EmailVerificationResult(false, EmailErrorCategories.ProviderRejectedMessage, "Generic Email API verification is not implemented yet.");
        }

        var options = smtpOptions.Value;
        var host = FirstNonBlank(settings.SmtpHost, options.Host);
        var port = settings.SmtpPort > 0 ? settings.SmtpPort : options.Port;
        var username = FirstNonBlank(settings.SmtpUsername, options.Username);
        var secret = FirstNonBlank(settings.SmtpPasswordSecret, options.Password);
        var secureMode = settings.SecureMode;

        if (string.IsNullOrWhiteSpace(host))
        {
            return new EmailVerificationResult(false, EmailErrorCategories.ConnectionFailed, "SMTP host is not configured.");
        }

        logger.LogInformation(
            "Verifying email delivery settings with mode {DeliveryMode}, host {Host}, port {Port}, secure mode {SecureMode}, sender {SenderEmail}.",
            settings.DeliveryMode,
            host,
            port,
            secureMode,
            settings.SenderEmail);

        try
        {
            using var client = new SmtpClient();
            client.Timeout = Math.Max(5, settings.TimeoutSeconds) * 1000;
            await client.ConnectAsync(host, port, ResolveSecureSocketOptions(secureMode, settings.EnableSsl), cancellationToken);
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(secret))
            {
                await client.AuthenticateAsync(username, secret, cancellationToken);
            }

            await client.DisconnectAsync(true, cancellationToken);
            return new EmailVerificationResult(true, null, null);
        }
        catch (Exception ex)
        {
            var category = CategorizeError(ex);
            logger.LogWarning(
                ex,
                "Email verification failed with category {ErrorCategory}. Host {Host}, port {Port}, secure mode {SecureMode}.",
                category,
                host,
                port,
                secureMode);
            return new EmailVerificationResult(false, category, ex.Message);
        }
    }

    public async Task<EmailVerificationResult> VerifySenderAsync(
        EmailDeliverySettings settings,
        string senderAddress,
        CancellationToken cancellationToken = default)
    {
        if (settings.DeliveryMode != EmailDeliveryMode.Smtp)
        {
            return new EmailVerificationResult(false, EmailErrorCategories.ProviderRejectedMessage, "Sender verification is only supported for SMTP delivery.");
        }

        if (!MailboxAddress.TryParse(senderAddress, out var sender))
        {
            return new EmailVerificationResult(false, EmailErrorCategories.SenderRejected, $"'{senderAddress}' is not a valid email address.");
        }

        var options = smtpOptions.Value;
        var host = FirstNonBlank(settings.SmtpHost, options.Host);
        var port = settings.SmtpPort > 0 ? settings.SmtpPort : options.Port;
        var username = FirstNonBlank(settings.SmtpUsername, options.Username);
        var secret = FirstNonBlank(settings.SmtpPasswordSecret, options.Password);
        if (string.IsNullOrWhiteSpace(host))
        {
            return new EmailVerificationResult(false, EmailErrorCategories.ConnectionFailed, "SMTP host is not configured.");
        }

        // Sender verification (e.g. Exim's "Sender verify failed") runs at RCPT time, so a recipient is
        // needed; the platform's own address is used when there is one. RSET discards the transaction.
        var probeRecipient = FirstNonBlank(settings.SenderEmail, sender.Address)!;
        try
        {
            using var client = new ProbeSmtpClient();
            client.Timeout = Math.Max(5, settings.TimeoutSeconds) * 1000;
            await client.ConnectAsync(host, port, ResolveSecureSocketOptions(settings.SecureMode, settings.EnableSsl), cancellationToken);
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(secret))
            {
                await client.AuthenticateAsync(username, secret, cancellationToken);
            }

            var mailFrom = await client.CommandAsync($"MAIL FROM:<{sender.Address}>", cancellationToken);
            var rcptTo = IsPositive(mailFrom) ? await client.CommandAsync($"RCPT TO:<{probeRecipient}>", cancellationToken) : null;
            await client.CommandAsync("RSET", cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            var refusal = !IsPositive(mailFrom) ? mailFrom : rcptTo is not null && !IsPositive(rcptTo) ? rcptTo : null;
            if (refusal is null)
            {
                return new EmailVerificationResult(true);
            }

            var text = $"{(int)refusal.StatusCode} {refusal.Response}";
            // A refused MAIL FROM is a sender refusal; a refused RCPT is one only when the server says so.
            var category = ReferenceEquals(refusal, mailFrom) || LooksLikeSenderRefusal(refusal.Response)
                ? EmailErrorCategories.SenderRejected
                : EmailErrorCategories.RecipientRejected;
            logger.LogWarning("Sender probe for {SenderEmail} via {Host} (authenticated as {SmtpUsername}) was refused: {Response}", sender.Address, host, username, text);
            return new EmailVerificationResult(false, category, text);
        }
        catch (Exception ex)
        {
            var category = CategorizeError(ex);
            logger.LogWarning(ex, "Sender probe for {SenderEmail} via {Host} failed with category {ErrorCategory}.", sender.Address, host, category);
            return new EmailVerificationResult(false, category, ex.Message);
        }
    }

    private static bool IsPositive(SmtpResponse response) => (int)response.StatusCode is >= 200 and < 400;

    /// <summary>
    /// Servers that verify senders commonly refuse at RCPT time, which MailKit reports as a recipient
    /// failure — Exim's "Sender verify failed" is the case that hit production. The server text is the
    /// only reliable signal that it is actually the sender being refused.
    /// </summary>
    internal static bool LooksLikeSenderRefusal(string? serverText) =>
        !string.IsNullOrWhiteSpace(serverText)
        && (serverText.Contains("sender verif", StringComparison.OrdinalIgnoreCase)
            || serverText.Contains("sender address rejected", StringComparison.OrdinalIgnoreCase)
            || serverText.Contains("valid sender", StringComparison.OrdinalIgnoreCase)
            || serverText.Contains("not owned by", StringComparison.OrdinalIgnoreCase)
            || serverText.Contains("send as", StringComparison.OrdinalIgnoreCase));

    private sealed class ProbeSmtpClient : SmtpClient
    {
        public Task<SmtpResponse> CommandAsync(string command, CancellationToken cancellationToken) =>
            SendCommandAsync(command, cancellationToken);
    }

    /// <summary>
    /// The settings row is written with empty strings by the settings screen long before anyone
    /// fills it in, so <c>??</c> kept those empty strings in preference to Smtp:* / ECOSYS_SMTP_*.
    /// Blank means absent here (EC-P1-06d).
    /// </summary>
    private static string? FirstNonBlank(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? (string.IsNullOrWhiteSpace(fallback) ? null : fallback) : preferred;

    private static SecureSocketOptions ResolveSecureSocketOptions(EmailSecureMode secureMode, bool enableSsl) =>
        secureMode switch
        {
            EmailSecureMode.None => SecureSocketOptions.None,
            EmailSecureMode.StartTls => SecureSocketOptions.StartTls,
            EmailSecureMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => enableSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None
        };

    private static string CategorizeError(Exception exception)
    {
        if (exception is OperationCanceledException || exception is TimeoutException)
        {
            return EmailErrorCategories.Timeout;
        }

        if (exception is SslHandshakeException || exception is System.Security.Authentication.AuthenticationException)
        {
            return EmailErrorCategories.TlsSslFailed;
        }

        if (exception is MailKit.Security.AuthenticationException || exception is ServiceNotAuthenticatedException)
        {
            return EmailErrorCategories.AuthenticationFailed;
        }

        if (exception is SmtpCommandException smtpCommandException)
        {
            if (LooksLikeSenderRefusal(smtpCommandException.Message))
            {
                return EmailErrorCategories.SenderRejected;
            }

            return smtpCommandException.ErrorCode switch
            {
                SmtpErrorCode.SenderNotAccepted => EmailErrorCategories.SenderRejected,
                SmtpErrorCode.RecipientNotAccepted => EmailErrorCategories.RecipientRejected,
                _ => EmailErrorCategories.ProviderRejectedMessage
            };
        }

        if (exception is SmtpProtocolException || exception is SocketException)
        {
            return EmailErrorCategories.ConnectionFailed;
        }

        return EmailErrorCategories.UnknownSmtpError;
    }
}
