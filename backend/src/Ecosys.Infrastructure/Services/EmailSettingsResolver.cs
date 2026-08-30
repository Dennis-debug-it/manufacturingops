using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The row a message is actually sent with, plus whether that row belongs to the platform rather than
/// the tenant. Callers that report back to a human need the flag: telling someone their SMTP test
/// passed is misleading when the test ran against platform mail they do not control.
/// </summary>
public sealed record EffectiveEmailSettings(EmailSetting? Settings, bool UsesPlatformMail);

/// <summary>
/// Single source of truth for "which mailbox does this tenant send through?". A tenant only sends
/// through its own SMTP once a platform admin has granted the override; otherwise its row is inert
/// and the platform's row is used. This rule was previously copied into four call sites that had
/// already drifted apart, so every path now resolves through here.
/// </summary>
public interface IEmailSettingsResolver
{
    Task<EffectiveEmailSettings> ResolveAsync(Guid? tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Turns the <see cref="EmailSetting"/> row <see cref="IEmailSettingsResolver"/> picked — which on a
/// fresh database is no row at all — into the settings a send actually runs with, applying one
/// honest resolution order: the database row first, then the <c>Smtp:*</c> configuration section
/// (<c>ECOSYS_SMTP_*</c> in a container), and only when neither supplies a host is email genuinely
/// unconfigured. Every send path previously gave up the moment the row was missing or its host was
/// blank, so a deployment that passed ECOSYS_SMTP_HOST and nothing else never sent one message —
/// and self-service password reset still reported success (EC-P1-06d).
/// </summary>
public static class EmailDeliveryResolution
{
    public static EmailDeliverySettings? Resolve(EmailSetting? settings, string? decryptedSecret, SmtpOptions fallback)
    {
        var mode = ParseDeliveryMode(settings?.Provider);

        // Configuration fills gaps; it never overrides a choice. A row that names a non-SMTP
        // provider is an operator decision and is handed on untouched.
        if (mode != EmailDeliveryMode.Smtp)
        {
            return settings is null
                ? null
                : Build(mode, settings, decryptedSecret, settings.Host, settings.Port, settings.Username, settings.UseSsl);
        }

        // Blank counts as absent, not as "configured with nothing". The row is written with empty
        // strings by the settings screen long before anyone fills it in, and `??` would have kept
        // those empty strings in preference to the environment.
        var host = FirstNonBlank(settings?.Host, fallback.Host);
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var port = settings is { Port: > 0 } ? settings.Port : fallback.Port;
        var username = FirstNonBlank(settings?.Username, fallback.Username);
        var secret = FirstNonBlank(decryptedSecret, fallback.Password);
        var useSsl = settings?.UseSsl ?? fallback.UseSsl;

        return Build(mode, settings, secret, host, port, username, useSsl);
    }

    private static EmailDeliverySettings Build(
        EmailDeliveryMode mode,
        EmailSetting? settings,
        string? secret,
        string? host,
        int port,
        string? username,
        bool useSsl) =>
        new(
            mode,
            host,
            port,
            username,
            secret,
            settings?.SenderName,
            // Only fall back to the SMTP username when it is itself an address; many providers use a
            // bare account name, and handing that to MailboxAddress.Parse would fail the send.
            FirstNonBlank(settings?.SenderAddress, username?.Contains('@') == true ? username : null),
            settings?.ReplyToEmail,
            useSsl,
            EmailDeliveryModeResolver.ResolveSecureMode(port, useSsl),
            null,
            null,
            null,
            30,
            0);

    /// <summary>
    /// True when the row is present and explicitly switched off. A missing row is not a decision, so
    /// it is not treated as one — it falls through to the configuration fallback above.
    /// </summary>
    public static bool IsExplicitlyDisabled(EmailSetting? settings) =>
        settings is not null
        && (!settings.IsEnabled
            || string.Equals(settings.Provider, EmailDeliveryMode.Disabled.ToString(), StringComparison.OrdinalIgnoreCase));

    private static EmailDeliveryMode ParseDeliveryMode(string? value) =>
        Enum.TryParse<EmailDeliveryMode>(value, true, out var parsed) ? parsed : EmailDeliveryMode.Smtp;

    private static string? FirstNonBlank(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? (string.IsNullOrWhiteSpace(fallback) ? null : fallback) : preferred;
}

public sealed class EmailSettingsResolver(AppDbContext dbContext) : IEmailSettingsResolver
{
    public async Task<EffectiveEmailSettings> ResolveAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var scopeTenantId = tenantId ?? PlatformConstants.RootTenantId;

        if (scopeTenantId == PlatformConstants.RootTenantId)
        {
            var platformOwnSettings = await LoadAsync(PlatformConstants.RootTenantId, cancellationToken);
            return new EffectiveEmailSettings(platformOwnSettings, UsesPlatformMail: true);
        }

        var tenantSettings = await LoadAsync(scopeTenantId, cancellationToken);

        // A tenant with no row of its own has never been configured either way — platform mail is the
        // safe reading, and the alternative (refusing to send) silently strands password reset and
        // invitation emails.
        var inheritsPlatformMail = tenantSettings is null
            || (tenantSettings.UsePlatformDefaults && !tenantSettings.OverrideSmtpSettings);

        if (!inheritsPlatformMail)
        {
            return new EffectiveEmailSettings(tenantSettings, UsesPlatformMail: false);
        }

        var platformSettings = await LoadAsync(PlatformConstants.RootTenantId, cancellationToken);
        return new EffectiveEmailSettings(platformSettings, UsesPlatformMail: true);
    }

    private Task<EmailSetting?> LoadAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
}
