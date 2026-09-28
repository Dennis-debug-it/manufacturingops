namespace Ecosys.Shared.Options;

public sealed class AuthSecurityOptions
{
    public const string SectionName = "AuthSecurity";

    public int FailedLoginThreshold { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int RateLimitPermitLimit { get; set; } = 20;
    public int RateLimitWindowSeconds { get; set; } = 60;
    public int RateLimitQueueLimit { get; set; } = 0;
    public int InvitationLifetimeHours { get; set; } = 72;
    public int PasswordResetLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Anonymous self-service workspace creation (POST /api/auth/signup). Off by default: production
    /// tenants are provisioned by the platform so plan, seats and invitation are always set. Only
    /// development/test hosts enable it (the e2e helpers create throwaway workspaces through it).
    /// </summary>
    public bool AllowSelfSignup { get; set; }
}
