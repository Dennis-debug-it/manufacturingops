namespace Ecosys.Shared.Options;

public sealed class AuthSecurityOptions
{
    public const string SectionName = "AuthSecurity";

    public int FailedLoginThreshold { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int RateLimitPermitLimit { get; set; } = 20;
    public int RateLimitWindowSeconds { get; set; } = 60;
    public int RateLimitQueueLimit { get; set; } = 0;
}
