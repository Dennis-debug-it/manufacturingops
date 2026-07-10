using System.Reflection;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformSettingsAccess")]
[Route("api/platform/health")]
public sealed class PlatformHealthController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlatformHealthResponse>> Get(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";

        // ── Database connectivity ────────────────────────────────────────────
        var dbUp = await dbContext.Database.CanConnectAsync(cancellationToken);

        // ── Email outbox / delivery ──────────────────────────────────────────
        var queueDepth = await dbContext.EmailOutboxMessages
            .CountAsync(x => x.Status == "Pending" || x.Status == "Sending" || x.Status == "Retrying", cancellationToken);
        var failedLast24h = await dbContext.EmailOutboxMessages
            .CountAsync(x => x.Status == "Failed" && x.UpdatedAt >= now.AddDays(-1), cancellationToken);
        var lastSentAt = await dbContext.EmailOutboxMessages
            .Where(x => x.SentAt != null).OrderByDescending(x => x.SentAt).Select(x => x.SentAt).FirstOrDefaultAsync(cancellationToken);

        var emailStatus = !dbUp ? "Down" : failedLast24h > 10 ? "Degraded" : "Healthy";

        // ── PM engine (most recent PM-generated work order) ──────────────────
        var lastPmAt = await dbContext.WorkOrders
            .Where(x => x.IsPreventiveMaintenance).OrderByDescending(x => x.CreatedAt).Select(x => (DateTime?)x.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        var services = new List<ServiceHealth>
        {
            new("API", "Healthy", now, $"v{version}"),
            new("Database", dbUp ? "Healthy" : "Down", now, dbUp ? "PostgreSQL reachable" : "Cannot connect"),
            new("Email Delivery", emailStatus, now, $"{queueDepth} queued · {failedLast24h} failed (24h)"),
            new("PM Engine", "Healthy", now, lastPmAt is { } p ? $"Last PM WO {p:dd MMM yyyy}" : "No PM work orders yet"),
        };

        var jobs = new List<BackgroundJob>
        {
            new("Email Outbox Dispatch", lastSentAt, failedLast24h > 0 ? "Failed" : "Success", null),
            new("PM Generation", lastPmAt, "Success", null),
        };

        return Ok(new PlatformHealthResponse(version, now, queueDepth, failedLast24h, services, jobs));
    }
}

public sealed record ServiceHealth(string Name, string Status, DateTime LastCheckedAt, string? Detail);
public sealed record BackgroundJob(string Name, DateTime? LastRunAt, string Status, double? DurationSeconds);
public sealed record PlatformHealthResponse(
    string Version, DateTime CheckedAt, int QueueDepth, int RecentErrors,
    IReadOnlyList<ServiceHealth> Services, IReadOnlyList<BackgroundJob> Jobs);
