using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Security;

public sealed class PasswordChangeEnforcementMiddleware(
    RequestDelegate next,
    ILogger<PasswordChangeEnforcementMiddleware> logger)
{
    private const string PasswordChangeOnlyScope = "password_change_only";
    // Signing out must also work, or a user who abandons the change leaves their session open.
    private static readonly string[] AllowedPaths = ["/api/auth/change-password", "/api/auth/logout"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var scope = context.User.FindFirst(TenantClaimTypes.Scope)?.Value;
            if (string.Equals(scope, PasswordChangeOnlyScope, StringComparison.OrdinalIgnoreCase))
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var isAllowed = context.Request.Method == HttpMethods.Post &&
                    AllowedPaths.Any(allowed => path.Equals(allowed, StringComparison.OrdinalIgnoreCase));

                if (!isAllowed)
                {
                    logger.LogWarning("Request to {Path} blocked; token requires password change.", path);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "You must change your password before continuing.",
                        errorCode = "PASSWORD_CHANGE_REQUIRED"
                    });
                    return;
                }
            }
        }

        await next(context);
    }
}
