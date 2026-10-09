using System.Security.Claims;
using Ecosys.Infrastructure.Security;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

/// <summary>
/// A temporary-password token may only change the password or sign out; everything else is refused,
/// which is why the app routes those users to /change-password before loading anything.
/// </summary>
public sealed class PasswordChangeEnforcementMiddlewareTests
{
    [Theory]
    [InlineData("POST", "/api/auth/change-password", true)]
    [InlineData("POST", "/api/auth/logout", true)]
    [InlineData("POST", "/api/auth/login", true)]
    [InlineData("GET", "/api/auth/me", false)]
    [InlineData("GET", "/api/workorders", false)]
    public async Task RestrictedToken_OnlyReachesChangePasswordAndLogout(string method, string path, bool allowed)
    {
        var reachedNext = false;
        var middleware = new PasswordChangeEnforcementMiddleware(_ => { reachedNext = true; return Task.CompletedTask; },
            NullLogger<PasswordChangeEnforcementMiddleware>.Instance);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(TenantClaimTypes.Scope, "password_change_only")], "Test")),
        };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(allowed, reachedNext);
        Assert.Equal(allowed ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }
}
