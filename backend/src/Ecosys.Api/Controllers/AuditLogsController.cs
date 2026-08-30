using System.Text;
using Ecosys.Api.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/audit-logs")]
public sealed class AuditLogsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    /// <summary>
    /// Rows a single CSV export may cover. Audit logs are the fastest-growing table in the product
    /// (every login and every mutation writes one), so an "export everything" with no ceiling is the
    /// same unbounded-materialization defect the list endpoint had (EC-015). Past the ceiling the
    /// export refuses with an explanation rather than silently truncating — a short CSV that claims
    /// to be the whole filtered set is worse than an error.
    /// </summary>
    private const int MaxExportRows = 50_000;

    /// <summary>
    /// EC-015: this action used to take no parameters at all and serialize every audit row the
    /// tenant had ever written into one response. It now always returns a
    /// <see cref="PagedResult{T}"/> — paging is not opt-in here, because the failure mode of the
    /// unpaged path is unbounded and grows without limit. <c>page</c>/<c>pageSize</c> default to
    /// 1/50 and <c>pageSize</c> is capped at <see cref="QueryablePagingExtensions.MaxPageSize"/>.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> GetAll(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? action,
        [FromQuery] string? entityName,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewReports);

        var paged = await BuildFilteredQuery(from, to, action, entityName, search)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AuditLogResponse(
                x.Id,
                x.UserId,
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Details,
                x.CreatedAt))
            .ToPagedResultAsync(page, pageSize, cancellationToken);

        return Ok(paged);
    }

    /// <summary>
    /// Exports the whole filtered set as CSV, server-side. The page's Export control posts the same
    /// filters it is displaying, so "Export" keeps meaning "everything matching these filters" even
    /// though the table itself only ever holds one page.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? action,
        [FromQuery] string? entityName,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanViewReports);

        var query = BuildFilteredQuery(from, to, action, entityName, search);

        var totalCount = await query.CountAsync(cancellationToken);
        if (totalCount > MaxExportRows)
        {
            throw new BusinessRuleException(
                $"This export would cover {totalCount:N0} entries, above the {MaxExportRows:N0} limit. Narrow the date range or filters and try again.");
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AuditLogResponse(
                x.Id,
                x.UserId,
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Details,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("When,Action,Entity,Entity ID,Details");
        foreach (var row in rows)
        {
            csv.AppendLine(Csv.Row(new object?[]
            {
                row.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                row.Action,
                row.EntityName,
                row.EntityId,
                row.Details ?? string.Empty
            }));
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "audit-logs.csv");
    }

    /// <summary>
    /// The shared filter set for the list and the export, so the CSV can never cover a different
    /// population than the table it was exported from. Tenant scoping is applied here and nowhere
    /// else, which keeps it impossible for one of the two callers to forget it.
    /// </summary>
    private IQueryable<AuditLog> BuildFilteredQuery(
        DateTime? from,
        DateTime? to,
        string? action,
        string? entityName,
        string? search)
    {
        var query = dbContext.AuditLogs.Where(x => x.TenantId == TenantId);

        if (from.HasValue)
        {
            var fromUtc = DateTimeNormalizationHelper.NormalizeToUtc(from.Value);
            query = query.Where(x => x.CreatedAt >= fromUtc);
        }

        if (to.HasValue)
        {
            // `to` is a calendar day from a date picker, so the whole of that day is included.
            var toExclusive = DateTimeNormalizationHelper.NormalizeToUtc(to.Value.Date).AddDays(1);
            query = query.Where(x => x.CreatedAt < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim().ToLowerInvariant();
            query = query.Where(x => x.Action.ToLower().Contains(normalizedAction));
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var normalizedEntity = entityName.Trim().ToLowerInvariant();
            query = query.Where(x => x.EntityName.ToLower().Contains(normalizedEntity));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Same columns the page's search box used to scan client-side. It has to run in SQL now:
            // filtering the loaded page only would quietly turn a tenant-wide search into a
            // page-local one.
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Action.ToLower().Contains(normalizedSearch)
                || x.EntityName.ToLower().Contains(normalizedSearch)
                || x.EntityId.ToLower().Contains(normalizedSearch)
                || (x.Details != null && x.Details.ToLower().Contains(normalizedSearch)));
        }

        return query;
    }
}

public sealed record AuditLogResponse(
    Guid Id,
    Guid? UserId,
    string Action,
    string EntityName,
    string EntityId,
    string? Details,
    DateTime CreatedAt);
