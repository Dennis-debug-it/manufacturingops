using Ecosys.Api.Common;
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
    /// <summary>
    /// Ceiling on the legacy unpaged response. It predates EC-015 and is kept only so existing
    /// callers that send no <c>page</c>/<c>pageSize</c> keep receiving a bare array of the shape
    /// they expect; a caller that wants the rest of the set — and an honest total instead of a
    /// silent cut-off at this number — asks for a page.
    /// </summary>
    private const int UnpagedResultCap = 500;

    /// <summary>
    /// EC-015 (platform side): this endpoint already had filters but no paging — it returned the
    /// newest <see cref="UnpagedResultCap"/> matches with nothing in the response saying the set had
    /// been cut off. Sending <c>page</c>/<c>pageSize</c> now returns a <see cref="PagedResult{T}"/>
    /// with an accurate <c>totalCount</c>; omitting them keeps the old capped array for backward
    /// compatibility.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? actorUserId,
        [FromQuery] Guid? tenantId,
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] string? severity,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
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

        var projected = query
            .OrderByDescending(x => x.CreatedAt)
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
                x.Tenant != null ? x.Tenant.Name : null));

        if (QueryablePagingExtensions.IsPagingRequested(page, pageSize))
        {
            return Ok(await projected.ToPagedResultAsync(page, pageSize, cancellationToken));
        }

        var logs = await projected
            .Take(UnpagedResultCap)
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
