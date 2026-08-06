namespace Ecosys.Shared.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Ecosys";
    public string Audience { get; set; } = "Ecosys.Clients";
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access-token lifetime. Kept short — a refresh token silently renews it (see
    /// <see cref="RefreshTokenDays"/>), so a stolen access token is only useful briefly.</summary>
    public int ExpiryMinutes { get; set; } = 30;

    /// <summary>Refresh-token lifetime for a normal sign-in (still spans a field shift).</summary>
    public int RefreshTokenDays { get; set; } = 1;

    /// <summary>Refresh-token lifetime when the user selected "Remember me".</summary>
    public int RefreshTokenRememberMeDays { get; set; } = 30;
}
