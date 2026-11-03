using Ecosys.Shared.Contracts.Integration;

namespace Ecosys.Infrastructure.Services;

public static class EmailDeliveryModeResolver
{
    public static EmailSecureMode ResolveSecureMode(int port, bool enableSsl, EmailSecureMode? requested = null)
    {
        if (requested.HasValue && requested.Value != EmailSecureMode.Auto)
        {
            return requested.Value;
        }

        if (port == 465)
        {
            return EmailSecureMode.SslOnConnect;
        }

        if (port == 587)
        {
            return EmailSecureMode.StartTls;
        }

        return enableSsl ? EmailSecureMode.StartTls : EmailSecureMode.None;
    }
}
