namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Platform-wide switches for whole features, set in configuration ("Features" section). Off, a
/// feature does nothing: no background work, no messages, and its API refuses changes (reads still
/// answer, so nothing that is already stored disappears). The frontend's matching switches are in
/// shared/config/features.ts; flip both to bring a feature back.
/// </summary>
public sealed class PlatformFeatureOptions
{
    public const string SectionName = "Features";

    /// <summary>WhatsApp reminders, opt-ins, the WhatsApp inbox and WhatsApp on sales deals.</summary>
    public bool WhatsApp { get; set; } = true;

    /// <summary>
    /// The routes a switched-off feature refuses, or null. Only changes are refused; Meta's webhook keeps
    /// working so a STOP reply is still honoured.
    /// </summary>
    public string? RefusalFor(string method, string path)
    {
        if (HttpMethods.IsReadOnly(method))
            return null;
        var p = path.ToLowerInvariant();
        if (!WhatsApp && !p.StartsWith("/api/whatsapp/webhook")
            && (p.StartsWith("/api/whatsapp") || p.StartsWith("/api/me/whatsapp")
                || p.StartsWith("/api/platform/whatsapp")))
            return "WhatsApp is switched off.";
        return null;
    }

    private static class HttpMethods
    {
        public static bool IsReadOnly(string method) => method is "GET" or "HEAD" or "OPTIONS";
    }
}
