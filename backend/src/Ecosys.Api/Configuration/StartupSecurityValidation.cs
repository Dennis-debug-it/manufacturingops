using Ecosys.Shared.Options;

namespace Ecosys.Api.Configuration;

internal static class StartupSecurityValidation
{
    private const int MinimumJwtSigningKeyLength = 32;
    private const int MinimumPlatformAdminPasswordLength = 12;

    private static readonly HashSet<string> DisallowedJwtSigningKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ChangeThisSigningKeyForProductionOnly123!",
        "EcosysDevelopmentSigningKey-ReplaceInProduction-1234567890",
        "SET_IN_ENV",
        "changeme",
        "change-me",
        "default",
        "development-only"
    };

    private static readonly HashSet<string> DisallowedPlatformAdminEmails = new(StringComparer.OrdinalIgnoreCase)
    {
        "superadmin@ecosys.local",
        "admin@example.com",
        "SET_IN_ENV"
    };

    private static readonly HashSet<string> DisallowedPlatformAdminPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SuperAdmin123!",
        "ChangeMe123!",
        "SET_IN_ENV",
        "changeme",
        "default"
    };

    public static JwtOptions ValidateJwtOptions(JwtOptions? options)
    {
        options ??= new JwtOptions();
        var signingKey = options.SigningKey?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("Startup aborted: Jwt:SigningKey must be configured.");
        }

        if (signingKey.Length < MinimumJwtSigningKeyLength)
        {
            throw new InvalidOperationException($"Startup aborted: Jwt:SigningKey must be at least {MinimumJwtSigningKeyLength} characters long.");
        }

        if (DisallowedJwtSigningKeys.Contains(signingKey))
        {
            throw new InvalidOperationException("Startup aborted: Jwt:SigningKey must not use a known default or sentinel value.");
        }

        options.SigningKey = signingKey;
        return options;
    }

    public static PlatformAdminOptions ValidatePlatformAdminOptions(PlatformAdminOptions? options)
    {
        options ??= new PlatformAdminOptions();
        var email = options.Email?.Trim() ?? string.Empty;
        var password = options.Password?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Startup aborted: PlatformAdmin:Email must be configured.");
        }

        if (DisallowedPlatformAdminEmails.Contains(email))
        {
            throw new InvalidOperationException("Startup aborted: PlatformAdmin:Email must not use a known default or sentinel value.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Startup aborted: PlatformAdmin:Password must be configured.");
        }

        if (password.Length < MinimumPlatformAdminPasswordLength)
        {
            throw new InvalidOperationException($"Startup aborted: PlatformAdmin:Password must be at least {MinimumPlatformAdminPasswordLength} characters long.");
        }

        if (DisallowedPlatformAdminPasswords.Contains(password))
        {
            throw new InvalidOperationException("Startup aborted: PlatformAdmin:Password must not use a known default or sentinel value.");
        }

        options.Email = email.ToLowerInvariant();
        options.Password = password;
        options.FullName = string.IsNullOrWhiteSpace(options.FullName) ? "Platform SuperAdmin" : options.FullName.Trim();
        return options;
    }

    public static AuthSecurityOptions ValidateAuthSecurityOptions(AuthSecurityOptions? options)
    {
        options ??= new AuthSecurityOptions();

        if (options.FailedLoginThreshold <= 0)
        {
            throw new InvalidOperationException("Startup aborted: AuthSecurity:FailedLoginThreshold must be greater than zero.");
        }

        if (options.LockoutMinutes <= 0)
        {
            throw new InvalidOperationException("Startup aborted: AuthSecurity:LockoutMinutes must be greater than zero.");
        }

        if (options.RateLimitPermitLimit <= 0)
        {
            throw new InvalidOperationException("Startup aborted: AuthSecurity:RateLimitPermitLimit must be greater than zero.");
        }

        if (options.RateLimitWindowSeconds <= 0)
        {
            throw new InvalidOperationException("Startup aborted: AuthSecurity:RateLimitWindowSeconds must be greater than zero.");
        }

        if (options.RateLimitQueueLimit < 0)
        {
            throw new InvalidOperationException("Startup aborted: AuthSecurity:RateLimitQueueLimit must not be negative.");
        }

        if (options.InvitationLifetimeHours is < 1 or > 720 || options.PasswordResetLifetimeMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException("AuthSecurity token lifetimes must be positive: invitations up to 720 hours and password resets up to 1440 minutes.");
        }

        return options;
    }

    public static PublicAppOptions ValidatePublicAppOptions(PublicAppOptions? options, bool isProduction)
    {
        options ??= new PublicAppOptions();
        var configuredUrl = options.PublicUrl?.Trim() ?? string.Empty;

        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http")
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("Startup aborted: App:PublicUrl must be an absolute HTTP(S) URL without credentials, query, or fragment data.");
        }

        if (isProduction && (uri.Scheme != Uri.UriSchemeHttps || uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Startup aborted: App:PublicUrl must be a non-local HTTPS URL in production.");
        }

        options.PublicUrl = uri.AbsoluteUri.TrimEnd('/');
        return options;
    }
}
