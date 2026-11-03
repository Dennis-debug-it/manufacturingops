using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public interface IAuditLogService
{
    Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default);
    Task LogAsync(
        Guid? tenantId,
        Guid? userId,
        string action,
        string entityName,
        string entityId,
        string? details,
        string severity = "Info",
        string? actorName = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
}

internal sealed class AuditLogService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor) : IAuditLogService
{
    public Task LogAsync(
        Guid? tenantId,
        Guid? userId,
        string action,
        string entityName,
        string entityId,
        string? details,
        CancellationToken cancellationToken = default) =>
        LogAsync(tenantId, userId, action, entityName, entityId, details, "Info", null, null, null, cancellationToken);

    public async Task LogAsync(
        Guid? tenantId,
        Guid? userId,
        string action,
        string entityName,
        string entityId,
        string? details,
        string severity = "Info",
        string? actorName = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedActorName = actorName;
        if (string.IsNullOrWhiteSpace(resolvedActorName) && userId.HasValue)
        {
            resolvedActorName = await dbContext.Users
                .Where(x => x.Id == userId.Value)
                .Select(x => x.FullName)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var httpContext = httpContextAccessor.HttpContext;
        dbContext.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            ActorName = string.IsNullOrWhiteSpace(resolvedActorName) ? "System" : resolvedActorName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress ?? httpContext?.Connection?.RemoteIpAddress?.ToString(),
            UserAgent = userAgent ?? httpContext?.Request?.Headers.UserAgent.ToString(),
            Severity = string.IsNullOrWhiteSpace(severity) ? "Info" : severity.Trim()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
