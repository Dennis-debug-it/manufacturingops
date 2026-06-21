using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Security;

public sealed class UserSessionHeartbeatMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(60);

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, IUserSessionService userSessionService, ILogger<UserSessionHeartbeatMiddleware> logger)
    {
        if (tenantContext.IsAuthenticated && tenantContext.SessionId.HasValue)
        {
            try
            {
                var touched = await userSessionService.TouchAsync(tenantContext.SessionId.Value, UpdateInterval, context.RequestAborted);
                if (!touched)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to update user session heartbeat for session {SessionId}.", tenantContext.SessionId);
            }
        }

        await next(context);
    }
}
