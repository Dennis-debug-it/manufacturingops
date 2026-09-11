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
        var host = settings.SmtpHost ?? options.Host;
        var port = settings.SmtpPort > 0 ? settings.SmtpPort : options.Port;
        var username = settings.SmtpUsername ?? options.Username;
        var secret = settings.SmtpPasswordSecret ?? options.Password;
        var secureMode = settings.SecureMode;

        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning("SMTP host is not configured. Skipping email send.");
            return;
        }

        logger.LogInformation(
            "Sending email using delivery mode {DeliveryMode}, host {Host}, port {Port}, secure mode {SecureMode}, sender {SenderEmail}.",
            settings.DeliveryMode,
            host,
            port,
            secureMode,
            settings.SenderEmail ?? message.FromAddress);

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(message.FromName ?? settings.SenderName ?? "Ecosys", message.FromAddress ?? settings.SenderEmail ?? username ?? "noreply@ecosys.local"));
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
            logger.LogWarning(
                ex,
                "Email send failed with category {ErrorCategory}. Host {Host}, port {Port}, secure mode {SecureMode}.",
                category,
                host,
                port,
                secureMode);
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
        var host = settings.SmtpHost ?? options.Host;
        var port = settings.SmtpPort > 0 ? settings.SmtpPort : options.Port;
        var username = settings.SmtpUsername ?? options.Username;
        var secret = settings.SmtpPasswordSecret ?? options.Password;
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
