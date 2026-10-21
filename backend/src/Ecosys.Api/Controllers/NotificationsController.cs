using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

public sealed record NotificationPageResponse(IReadOnlyList<InboxNotification> Items, bool HasMore);
public sealed record UnreadCountResponse(int Count);

/// <summary>
/// The signed-in user's bell (Release 3 design 2.4). A user only ever sees their own rows; there is
/// no admin view of other people's inboxes. Every change tells the user's other tabs the new count.
/// </summary>
[ApiController]
[Authorize]
[Route("api/me/notifications")]
public sealed class NotificationsController(
    AppDbContext db,
    IInboxNotifier inboxNotifier,
    ITenantContext tenantContext) : TenantAwareControllerBase(tenantContext)
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    /// <summary>Newest first. Page with <paramref name="before"/> = the last item's createdAt.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationPageResponse>> List(
        [FromQuery] bool unreadOnly, [FromQuery] DateTime? before, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        var query = Mine().Where(x => x.ArchivedAt == null);
        if (unreadOnly)
            query = query.Where(x => x.ReadAt == null);
        if (before.HasValue)
        {
            var cursor = DateTime.SpecifyKind(before.Value.ToUniversalTime(), DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt < cursor);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(take + 1)
            .ToListAsync(cancellationToken);
        return Ok(new NotificationPageResponse(rows.Take(take).Select(x => x.ToInbox()).ToList(), rows.Count > take));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> UnreadCount(CancellationToken cancellationToken) =>
        Ok(new UnreadCountResponse(await Mine().Unread().CountAsync(cancellationToken)));

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<UnreadCountResponse>> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var row = await Mine().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
            return NotFound();
        row.ReadAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await PublishCountAsync(cancellationToken));
    }

    [HttpPost("read-all")]
    public async Task<ActionResult<UnreadCountResponse>> MarkAllRead(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var unread = await Mine().Unread().ToListAsync(cancellationToken);
        foreach (var row in unread)
            row.ReadAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await PublishCountAsync(cancellationToken));
    }

    /// <summary>Hides it from the bell (it is deleted with the rest after 90 days). Archiving also marks it read.</summary>
    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<UnreadCountResponse>> Archive(Guid id, CancellationToken cancellationToken)
    {
        var row = await Mine().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
            return NotFound();
        var now = DateTime.UtcNow;
        row.ReadAt ??= now;
        row.ArchivedAt ??= now;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await PublishCountAsync(cancellationToken));
    }

    private IQueryable<Domain.Entities.Notification> Mine()
    {
        var tenantId = TenantId;
        var userId = UserId;
        return db.Notifications.Where(x => x.TenantId == tenantId && x.UserId == userId);
    }

    private async Task<UnreadCountResponse> PublishCountAsync(CancellationToken cancellationToken)
    {
        var count = await Mine().Unread().CountAsync(cancellationToken);
        await inboxNotifier.UnreadCountChangedAsync(UserId, count, cancellationToken);
        return new UnreadCountResponse(count);
    }
}
