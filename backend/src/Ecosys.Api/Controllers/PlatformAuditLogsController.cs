using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformReadOnlyAccess")]
[Route("api/platform/audit-logs")]
public sealed class PlatformAuditLogsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PlatformAuditLogResponse>>> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? actorUserId,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] string? severity,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AuditLogs
            .Include(x => x.Tenant)
            .Include(x => x.User)
            .AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            var toExclusive = to.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < toExclusive);
        }

        if (actorUserId.HasValue)
        {
            query = query.Where(x => x.UserId == actorUserId.Value);
        }

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim().ToLowerInvariant();
            query = query.Where(x => x.Action.ToLower().Contains(normalizedAction));
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalizedEntityType = entityType.Trim().ToLowerInvariant();
            query = query.Where(x => x.EntityName.ToLower().Contains(normalizedEntityType));
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var normalizedSeverity = severity.Trim().ToLowerInvariant();
            query = query.Where(x => x.Severity.ToLower() == normalizedSeverity);
        }

        var logs = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new PlatformAuditLogResponse(
                x.Id,
                x.TenantId == PlatformConstants.RootTenantId ? null : x.TenantId,
                x.UserId,
                x.ActorName ?? (x.User != null ? x.User.FullName : "System"),
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Details,
                x.IpAddress,
                x.UserAgent,
                x.Severity,
                x.CreatedAt,
                x.Tenant != null ? x.Tenant.Name : null))
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlatformAuditLogResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var log = await dbContext.AuditLogs
            .Include(x => x.Tenant)
            .Include(x => x.User)
            .Where(x => x.Id == id)
            .Select(x => new PlatformAuditLogResponse(
                x.Id,
                x.TenantId == PlatformConstants.RootTenantId ? null : x.TenantId,
                x.UserId,
                x.ActorName ?? (x.User != null ? x.User.FullName : "System"),
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Details,
                x.IpAddress,
                x.UserAgent,
                x.Severity,
                x.CreatedAt,
                x.Tenant != null ? x.Tenant.Name : null))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Audit log entry was not found.");

        return Ok(log);
    }
}

public sealed record PlatformAuditLogResponse(
    Guid Id,
    Guid? TenantId,
    Guid? ActorUserId,
    string ActorName,
    string Action,
    string EntityType,
    string EntityId,
    string? Description,
    string? IpAddress,
    string? UserAgent,
    string Severity,
    DateTime CreatedAt,
    string? TenantName);
