using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecosys.Api.Controllers;

/// <summary>
/// "My calendar" (design 3.4) and the caller's private feed link (3.5). The controller is Shared; each
/// source inside checks its own module and permission, so the calendar never shows what the caller can't open.
/// </summary>
[ApiController]
[Authorize]
public sealed class CalendarController(
    ITenantContext tenantContext,
    IAccessViewerFactory viewerFactory,
    ICalendarService calendarService,
    ICalendarFeedService feedService) : TenantAwareControllerBase(tenantContext)
{
    private const int MaxRangeDays = 100;

    [HttpGet("api/calendar")]
    public async Task<ActionResult<CalendarResponse>> Get(
        [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? sources, [FromQuery] string? userIds, CancellationToken cancellationToken)
    {
        EnsureTenantOperationalAccess();
        var start = DateTime.SpecifyKind(from, from.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : from.Kind).ToUniversalTime();
        var end = DateTime.SpecifyKind(to, to.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : to.Kind).ToUniversalTime();
        if (end <= start)
            throw new BusinessRuleException("The end of the range must be after its start.");
        if (end - start > TimeSpan.FromDays(MaxRangeDays))
            throw new BusinessRuleException($"Show at most {MaxRangeDays} days at a time.");

        var viewer = await viewerFactory.CurrentAsync(cancellationToken);
        var people = new List<Guid>();
        foreach (var part in (userIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            people.Add(Guid.TryParse(part, out var id) ? id : throw new BusinessRuleException("userIds must be a comma-separated list of ids."));
        var wanted = (sources ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var items = await calendarService.GetItemsAsync(viewer, start, end, wanted, people, cancellationToken);
        return Ok(new CalendarResponse(calendarService.AvailableSources(viewer), viewer.Can(PermissionNames.CalendarTeam), items));
    }

    [HttpGet("api/me/calendar-feed")]
    public async Task<ActionResult<CalendarFeedStatusResponse>> GetFeed(CancellationToken cancellationToken)
    {
        var row = await feedService.GetActiveAsync(TenantId, UserId, cancellationToken);
        return Ok(new CalendarFeedStatusResponse(row is not null, row?.CreatedAt, row?.LastUsedAt, null));
    }

    /// <summary>Creates a new link, revoking any earlier one. The link is only ever shown in this response.</summary>
    [HttpPost("api/me/calendar-feed")]
    public async Task<ActionResult<CalendarFeedStatusResponse>> CreateFeed(CancellationToken cancellationToken)
    {
        var link = await feedService.CreateAsync(TenantId, UserId, cancellationToken);
        return Ok(new CalendarFeedStatusResponse(true, link.CreatedAt, null, CalendarFeedController.PathFor(link.Token)));
    }

    [HttpDelete("api/me/calendar-feed")]
    public async Task<IActionResult> RevokeFeed(CancellationToken cancellationToken)
    {
        await feedService.RevokeAsync(TenantId, UserId, cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// The anonymous ICS feed a user subscribes to from Google or Outlook. The token in the URL is the only
/// credential; it resolves to one user in one tenant, and an unknown or revoked token is simply not found.
/// </summary>
[ApiController]
public sealed class CalendarFeedController(ICalendarFeedService feedService) : ControllerBase
{
    public static string PathFor(string token) => $"/api/calendar/feed/{token}.ics";

    [AllowAnonymous]
    [EnableRateLimiting("PublicEndpoints")]
    [HttpGet("api/calendar/feed/{token}.ics")]
    public async Task<IActionResult> Feed(string token, CancellationToken cancellationToken)
    {
        var ics = await feedService.RenderAsync(token, DateTime.UtcNow, cancellationToken);
        if (ics is null)
            return NotFound();
        Response.Headers.CacheControl = "private, max-age=300";
        return Content(ics, "text/calendar; charset=utf-8");
    }
}

public sealed record CalendarResponse(IReadOnlyList<string> Sources, bool CanViewTeam, IReadOnlyList<CalendarItem> Items);

/// <param name="Path">The feed path, returned only when a link is created.</param>
public sealed record CalendarFeedStatusResponse(bool Active, DateTime? CreatedAt, DateTime? LastUsedAt, string? Path);
