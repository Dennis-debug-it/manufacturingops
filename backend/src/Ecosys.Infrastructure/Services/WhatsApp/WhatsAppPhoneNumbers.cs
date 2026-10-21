using System.Text;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// E.164 numbers for WhatsApp (design 4.2). A number written with a leading 0 is read as a national
/// number in the tenant's country (Kenya when unknown), so "0712 345 678" becomes "+254712345678".
/// </summary>
public static class WhatsAppPhoneNumbers
{
    public const string DefaultCallingCode = "254";

    private static readonly Dictionary<string, string> CallingCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kenya"] = "254", ["KE"] = "254",
        ["Uganda"] = "256", ["UG"] = "256",
        ["Tanzania"] = "255", ["TZ"] = "255",
        ["Rwanda"] = "250", ["RW"] = "250",
        ["Burundi"] = "257", ["BI"] = "257",
        ["Ethiopia"] = "251", ["ET"] = "251",
        ["South Sudan"] = "211", ["SS"] = "211",
        ["Somalia"] = "252", ["SO"] = "252",
        ["Nigeria"] = "234", ["NG"] = "234",
        ["Ghana"] = "233", ["GH"] = "233",
        ["Zambia"] = "260", ["ZM"] = "260",
        ["Malawi"] = "265", ["MW"] = "265",
        ["Zimbabwe"] = "263", ["ZW"] = "263",
        ["South Africa"] = "27", ["ZA"] = "27",
        ["United Kingdom"] = "44", ["GB"] = "44", ["UK"] = "44",
        ["United States"] = "1", ["US"] = "1",
        ["United Arab Emirates"] = "971", ["AE"] = "971",
        ["India"] = "91", ["IN"] = "91",
    };

    public static string CallingCodeFor(string? country) =>
        !string.IsNullOrWhiteSpace(country) && CallingCodes.TryGetValue(country.Trim(), out var code) ? code : DefaultCallingCode;

    /// <summary>Returns the number as +digits, or null when it can't be a valid E.164 number.</summary>
    public static string? Normalize(string? input, string? country = null)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var trimmed = input.Trim();
        var digits = new StringBuilder();
        foreach (var ch in trimmed)
        {
            if (char.IsAsciiDigit(ch)) digits.Append(ch);
            else if (ch is ' ' or '-' or '(' or ')' or '.' or '+') continue;
            else return null;
        }

        var number = digits.ToString();
        if (number.Length == 0)
            return null;

        if (trimmed.StartsWith('+'))
        {
            // Already international.
        }
        else if (number.StartsWith("00"))
        {
            number = number[2..];
        }
        else if (number.StartsWith('0'))
        {
            number = CallingCodeFor(country) + number[1..];
        }
        else
        {
            var code = CallingCodeFor(country);
            // "712345678" (a national number without its 0) or "254712345678" (international without +).
            if (!number.StartsWith(code) || number.Length <= 9)
                number = code + number;
        }

        // E.164: at most 15 digits, no leading zero; anything under 8 digits is not a real mobile.
        if (number.Length is < 8 or > 15 || number[0] == '0')
            return null;
        // Kenyan mobiles are +254 followed by 9 digits starting 7 or 1.
        if (number.StartsWith("254") && (number.Length != 12 || number[3] is not ('7' or '1')))
            return null;

        return "+" + number;
    }

    /// <summary>For logs: +2547••••5678.</summary>
    public static string Mask(string? e164) =>
        string.IsNullOrEmpty(e164) || e164.Length < 8 ? "••••" : $"{e164[..5]}••••{e164[^4..]}";
}
