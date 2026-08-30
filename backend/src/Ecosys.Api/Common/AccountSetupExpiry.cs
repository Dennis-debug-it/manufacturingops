using Ecosys.Shared.Options;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Shared resolution of the account-setup (invitation) link lifetime. An invitation is typically
/// opened hours or days after it is sent, so a too-short window reads to the recipient as "the link
/// expired immediately" — the configured value is floored rather than trusted blindly.
/// </summary>
internal static class AccountSetupExpiry
{
    private const int MinimumHours = 1;
    private const int DefaultHours = 72;

    public static int ResolveHours(AuthSecurityOptions options)
    {
        var configured = options.AccountSetupExpiryHours;
        return configured <= 0 ? DefaultHours : Math.Max(MinimumHours, configured);
    }
}
