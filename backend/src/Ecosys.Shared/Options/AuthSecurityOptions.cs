namespace Ecosys.Shared.Options;

public sealed class AuthSecurityOptions
{
    public const string SectionName = "AuthSecurity";

    public int FailedLoginThreshold { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int RateLimitPermitLimit { get; set; } = 20;
    public int RateLimitWindowSeconds { get; set; } = 60;
    public int RateLimitQueueLimit { get; set; } = 0;

    /// <summary>
    /// How long a self-service password reset link stays valid. Kept generous because the link has to
    /// survive SMTP queueing and retry delays before the recipient ever sees it.
    /// </summary>
    public int PasswordResetExpiryMinutes { get; set; } = 120;

    /// <summary>How long an account-setup (invitation) link stays valid.</summary>
    public int AccountSetupExpiryHours { get; set; } = 72;
}
