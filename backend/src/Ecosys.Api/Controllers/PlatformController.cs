using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformReadOnlyAccess")]
[Route("api/platform")]
public sealed class PlatformController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<PlatformSummaryResponse>> GetSummary(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var activeSessions = ActiveSessionsQuery(now);

        var totalTenants = await TenantsQuery().CountAsync(cancellationToken);
        var activeTenants = await TenantsQuery().CountAsync(x => x.IsActive, cancellationToken);
        var activeUsersNow = await activeSessions.Select(x => x.UserId).Distinct().CountAsync(cancellationToken);
        var loggedInToday = await dbContext.UserSessions
            .Where(x => x.TenantId != PlatformConstants.RootTenantId && x.LoginAt >= todayStart)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        var tenantsWithActiveUsers = await activeSessions.Select(x => x.TenantId).Distinct().CountAsync(cancellationToken);

        return Ok(new PlatformSummaryResponse(totalTenants, activeTenants, activeUsersNow, loggedInToday, tenantsWithActiveUsers));
    }

    [HttpGet("tenants/{tenantId:guid}/sessions")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformSessionResponse>>> GetTenantSessions(Guid tenantId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sessions = await dbContext.UserSessions
            .Include(x => x.User)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.LoginAt)
            .Select(x => new PlatformSessionResponse(
                x.User != null ? x.User.FullName : string.Empty,
                x.User != null ? x.User.Email : string.Empty,
                x.User != null ? x.User.Role : string.Empty,
                x.User != null ? x.User.JobTitle : null,
                x.LoginAt,
                x.LastSeenAt,
                x.LogoutAt,
                x.IpAddress,
                x.UserAgent,
                !x.IsRevoked && !x.LogoutAt.HasValue && x.LastSeenAt >= now.AddMinutes(-15)))
            .ToListAsync(cancellationToken);

        return Ok(sessions);
    }

    [HttpGet("tenants/{tenantId:guid}/sessions/active-count")]
    public async Task<ActionResult<PlatformTenantActiveCountResponse>> GetTenantActiveCount(Guid tenantId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;

        var activeUsersNow = await ActiveSessionsQuery(now)
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var loggedInToday = await dbContext.UserSessions
            .Where(x => x.TenantId == tenantId && x.LoginAt >= todayStart)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var lastActivityAt = await dbContext.UserSessions
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.LastSeenAt)
            .Select(x => (DateTime?)x.LastSeenAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new PlatformTenantActiveCountResponse(tenantId, activeUsersNow, loggedInToday, lastActivityAt));
    }

    private IQueryable<Ecosys.Domain.Entities.Tenant> TenantsQuery() =>
        dbContext.Tenants.Where(x => x.Id != PlatformConstants.RootTenantId);

    private IQueryable<Ecosys.Domain.Entities.UserSession> ActiveSessionsQuery(DateTime now) =>
        dbContext.UserSessions.Where(x =>
            x.TenantId != PlatformConstants.RootTenantId
            && !x.IsRevoked
            && !x.LogoutAt.HasValue
            && x.LastSeenAt >= now.AddMinutes(-15));
}

public sealed record PlatformSummaryResponse(
    int TotalTenants,
    int ActiveTenants,
    int ActiveUsersNow,
    int LoggedInToday,
    int TenantsWithActiveUsers);

public sealed record PlatformSessionResponse(
    string UserFullName,
    string Email,
    string Role,
    string? JobTitle,
    DateTime LoginAt,
    DateTime LastSeenAt,
    DateTime? LogoutAt,
    string? IpAddress,
    string? UserAgent,
    bool IsActive);

public sealed record PlatformTenantActiveCountResponse(
    Guid TenantId,
    int ActiveUsersNow,
    int LoggedInToday,
    DateTime? LastActivityAt);
