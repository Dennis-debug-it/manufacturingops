using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformSettingsAccess")]
[Route("api/platform/announcements")]
public sealed class PlatformAnnouncementsController(
    AppDbContext dbContext,
    IAuditLogService auditLogService,
    ITenantContext tenantContext) : ControllerBase
{
    private static readonly string[] AllowedAudiences = ["AllTenants", "TrialTenants", "ActiveTenants"];

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AnnouncementResponse>>> List(CancellationToken cancellationToken)
    {
        var items = await dbContext.PlatformAnnouncements.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AnnouncementResponse(x.Id, x.Title, x.Body, x.Audience, x.PublishedAt, x.ExpiresAt, x.Status))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>Currently-published announcements (used by tenant workspaces).</summary>
    [AllowAnonymous]
    [EnableRateLimiting("PublicEndpoints")]
    [HttpGet("/api/announcements/active")]
    public async Task<ActionResult<IReadOnlyCollection<AnnouncementResponse>>> Active(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var items = await dbContext.PlatformAnnouncements.AsNoTracking()
            .Where(x => x.Status == "Published" && (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderByDescending(x => x.PublishedAt)
            .Select(x => new AnnouncementResponse(x.Id, x.Title, x.Body, x.Audience, x.PublishedAt, x.ExpiresAt, x.Status))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<AnnouncementResponse>> Create([FromBody] UpsertAnnouncementRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var publish = request.Publish ?? false;
        var announcement = new PlatformAnnouncement
        {
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            Audience = NormalizeAudience(request.Audience),
            Status = publish ? "Published" : "Draft",
            PublishedAt = publish ? DateTime.UtcNow : null,
            ExpiresAt = request.ExpiresAt is { } e ? DateTime.SpecifyKind(e, DateTimeKind.Utc) : null,
        };
        dbContext.PlatformAnnouncements.Add(announcement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.announcement.created", nameof(PlatformAnnouncement), announcement.Id.ToString(), $"Announcement '{announcement.Title}' created.", cancellationToken);
        return Ok(Map(announcement));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AnnouncementResponse>> Update(Guid id, [FromBody] UpsertAnnouncementRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var announcement = await Load(id, cancellationToken);
        announcement.Title = request.Title.Trim();
        announcement.Body = request.Body.Trim();
        announcement.Audience = NormalizeAudience(request.Audience);
        announcement.ExpiresAt = request.ExpiresAt is { } e ? DateTime.SpecifyKind(e, DateTimeKind.Utc) : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(announcement));
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<AnnouncementResponse>> Publish(Guid id, CancellationToken cancellationToken)
    {
        var announcement = await Load(id, cancellationToken);
        announcement.Status = "Published";
        announcement.PublishedAt ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.announcement.published", nameof(PlatformAnnouncement), id.ToString(), $"Announcement '{announcement.Title}' published.", cancellationToken);
        return Ok(Map(announcement));
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<AnnouncementResponse>> Archive(Guid id, CancellationToken cancellationToken)
    {
        var announcement = await Load(id, cancellationToken);
        announcement.Status = "Archived";
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(announcement));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var announcement = await Load(id, cancellationToken);
        dbContext.PlatformAnnouncements.Remove(announcement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(null, tenantContext.UserId, "platform.announcement.deleted", nameof(PlatformAnnouncement), id.ToString(), $"Announcement '{announcement.Title}' deleted.", cancellationToken);
        return NoContent();
    }

    private async Task<PlatformAnnouncement> Load(Guid id, CancellationToken ct) =>
        await dbContext.PlatformAnnouncements.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Announcement not found.");

    private static void Validate(UpsertAnnouncementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) throw new BusinessRuleException("Title is required.");
        if (string.IsNullOrWhiteSpace(request.Body)) throw new BusinessRuleException("Body is required.");
    }

    private static string NormalizeAudience(string? audience) =>
        AllowedAudiences.FirstOrDefault(a => string.Equals(a, audience?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "AllTenants";

    private static AnnouncementResponse Map(PlatformAnnouncement x) =>
        new(x.Id, x.Title, x.Body, x.Audience, x.PublishedAt, x.ExpiresAt, x.Status);
}

public sealed record AnnouncementResponse(Guid Id, string Title, string Body, string Audience, DateTime? PublishedAt, DateTime? ExpiresAt, string Status);
public sealed record UpsertAnnouncementRequest([Required] string Title, [Required] string Body, string? Audience, DateTime? ExpiresAt, bool? Publish);
