using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>The sources "My calendar" merges (design 3.4), each gated by its own module and permission.</summary>
public static class CalendarSources
{
    public const string Activities = "activities";
    public const string ActionItems = "action-items";
    public const string WorkOrders = "work-orders";
    public const string Maintenance = "maintenance";
    public const string Projects = "projects";
    public const string Contracts = "contracts";

    public static readonly string[] All = [Activities, ActionItems, WorkOrders, Maintenance, Projects, Contracts];

    /// <summary>Sources about the tenant rather than one person; the team view leaves them out.</summary>
    public static readonly string[] TenantWide = [Maintenance, Contracts];
}

/// <summary>
/// One entry on a calendar. <see cref="Uid"/> is stable for the underlying record (and, for a contract,
/// which of its dates), so an external calendar updates the event instead of duplicating it. All-day
/// items carry their date as UTC midnight. <see cref="UserId"/> is the person the item is for.
/// </summary>
public sealed record CalendarItem(
    string Uid,
    string Source,
    string Title,
    DateTime StartsAt,
    DateTime EndsAt,
    bool AllDay,
    string? Location,
    string? Url,
    Guid? UserId,
    string? Status);

public interface ICalendarService
{
    /// <summary>The sources this viewer may see: a module the tenant lacks or a permission the viewer lacks removes a source.</summary>
    IReadOnlyList<string> AvailableSources(AccessViewer viewer);

    /// <summary>
    /// Items between <paramref name="from"/> and <paramref name="to"/> (UTC). With <paramref name="userIds"/>
    /// naming anyone but the viewer it is the team view, which needs calendar.team and leaves out tenant-wide sources.
    /// </summary>
    Task<IReadOnlyList<CalendarItem>> GetItemsAsync(AccessViewer viewer, DateTime from, DateTime to, IReadOnlyCollection<string>? sources,
        IReadOnlyCollection<Guid>? userIds, CancellationToken cancellationToken = default);
}

internal sealed class CalendarService(AppDbContext dbContext, ILinkedRecordService linkedRecords) : ICalendarService
{
    public IReadOnlyList<string> AvailableSources(AccessViewer viewer)
    {
        var sources = new List<string>();
        if (viewer.Can(PermissionNames.ActivitiesView))
            sources.AddRange([CalendarSources.Activities, CalendarSources.ActionItems]);
        var operations = viewer.HasModule(ServiceOpsPlans.Operations);
        if (operations && viewer.Can(PermissionNames.CanViewWorkOrders))
            sources.Add(CalendarSources.WorkOrders);
        // The plan list is for the people who plan maintenance, as on the Maintenance schedules screen.
        if (operations && (viewer.Can(PermissionNames.CanManageSettings) || viewer.Can(PermissionNames.CanAssignWorkOrders)))
            sources.Add(CalendarSources.Maintenance);
        if (viewer.HasModule(ServiceOpsPlans.Projects) && viewer.Can(PermissionNames.ProjectsView))
            sources.Add(CalendarSources.Projects);
        if (operations)
            sources.Add(CalendarSources.Contracts);
        return sources;
    }

    public async Task<IReadOnlyList<CalendarItem>> GetItemsAsync(AccessViewer viewer, DateTime from, DateTime to, IReadOnlyCollection<string>? sources,
        IReadOnlyCollection<Guid>? userIds, CancellationToken cancellationToken = default)
    {
        var tenantId = viewer.TenantId;
        var people = (userIds is { Count: > 0 } ? userIds : [viewer.UserId]).Distinct().ToList();
        var team = people.Any(x => x != viewer.UserId);
        if (team)
        {
            viewer.Ensure(PermissionNames.CalendarTeam);
            if (people.Count > 50)
                throw new BusinessRuleException("The team view shows at most 50 people.");
            var known = await dbContext.Users.CountAsync(x => x.TenantId == tenantId && people.Contains(x.Id), cancellationToken);
            if (known != people.Count)
                throw new NotFoundException("User not found.");
        }

        var allowed = AvailableSources(viewer).Where(x => !team || !CalendarSources.TenantWide.Contains(x));
        var wanted = (sources is { Count: > 0 } ? allowed.Where(x => sources.Contains(x, StringComparer.OrdinalIgnoreCase)) : allowed).ToHashSet();
        var fromDate = DateOnly.FromDateTime(from);
        var toDate = DateOnly.FromDateTime(to);
        var items = new List<CalendarItem>();

        if (wanted.Contains(CalendarSources.Activities))
        {
            var activities = await dbContext.Activities.Include(x => x.Attendees)
                .Where(x => x.TenantId == tenantId && x.Status != ActivityStatuses.Cancelled && x.StartsAt < to && x.EndsAt >= from
                    && (people.Contains(x.OrganizerUserId) || x.Attendees.Any(a => a.UserId != null && people.Contains(a.UserId.Value))))
                .ToListAsync(cancellationToken);
            foreach (var activity in activities)
            foreach (var person in people.Where(p => activity.OrganizerUserId == p || activity.Attendees.Any(a => a.UserId == p)))
                items.Add(new CalendarItem($"activity-{activity.Id}", CalendarSources.Activities, activity.Title, activity.StartsAt, activity.EndsAt,
                    activity.AllDay, activity.Location, $"/activities/{activity.Id}", person, activity.Status));
        }

        if (wanted.Contains(CalendarSources.ActionItems))
        {
            var actionItems = await dbContext.ActionItems
                .Where(x => x.TenantId == tenantId && x.Status == ActionItemStatuses.Open && x.AssigneeUserId != null && people.Contains(x.AssigneeUserId.Value)
                    && x.DueDate != null && x.DueDate >= fromDate && x.DueDate <= toDate)
                .Select(x => new { x.Id, x.Title, x.DueDate, x.AssigneeUserId, x.ActivityId })
                .ToListAsync(cancellationToken);
            items.AddRange(actionItems.Select(x => Day($"action-item-{x.Id}", CalendarSources.ActionItems, $"Action: {x.Title}", x.DueDate!.Value,
                x.ActivityId.HasValue ? $"/activities/{x.ActivityId}" : "/action-items", x.AssigneeUserId)));
        }

        if (wanted.Contains(CalendarSources.WorkOrders))
        {
            var technicians = await dbContext.Technicians
                .Where(x => x.TenantId == tenantId && x.UserId != null && people.Contains(x.UserId.Value))
                .Select(x => new { x.Id, UserId = x.UserId!.Value })
                .ToListAsync(cancellationToken);
            var technicianIds = technicians.Select(x => x.Id).ToList();
            if (technicianIds.Count > 0)
            {
                var workOrders = await dbContext.WorkOrders
                    .Where(x => x.TenantId == tenantId && x.ScheduledStartAt != null && x.ScheduledStartAt < to && (x.ScheduledEndAt ?? x.ScheduledStartAt) >= from
                        && x.Status != "Cancelled"
                        && ((x.AssignedTechnicianId != null && technicianIds.Contains(x.AssignedTechnicianId.Value))
                            || (x.LeadTechnicianId != null && technicianIds.Contains(x.LeadTechnicianId.Value))
                            || x.TechnicianAssignments.Any(a => technicianIds.Contains(a.TechnicianId))))
                    .Select(x => new
                    {
                        x.Id, x.WorkOrderNumber, x.Title, x.Status, x.ScheduledStartAt, x.ScheduledEndAt, x.AssignedTechnicianId, x.LeadTechnicianId,
                        SiteName = x.Site != null ? x.Site.SiteName : null,
                        Crew = x.TechnicianAssignments.Select(a => a.TechnicianId).ToList(),
                    })
                    .ToListAsync(cancellationToken);
                foreach (var workOrder in workOrders)
                {
                    var assigned = workOrder.Crew.Append(workOrder.AssignedTechnicianId ?? Guid.Empty).Append(workOrder.LeadTechnicianId ?? Guid.Empty).ToHashSet();
                    foreach (var person in technicians.Where(t => assigned.Contains(t.Id)).Select(t => t.UserId).Distinct())
                        items.Add(new CalendarItem($"work-order-{workOrder.Id}", CalendarSources.WorkOrders, $"{workOrder.WorkOrderNumber} {workOrder.Title}",
                            workOrder.ScheduledStartAt!.Value, workOrder.ScheduledEndAt ?? workOrder.ScheduledStartAt.Value.AddHours(1), false,
                            workOrder.SiteName, $"/work-orders/{workOrder.Id}", person, workOrder.Status));
                }
            }
        }

        if (wanted.Contains(CalendarSources.Maintenance))
        {
            var plans = await dbContext.PreventiveMaintenancePlans
                .Where(x => x.TenantId == tenantId && x.Status == "Active" && x.NextPmDate != null && x.NextPmDate >= from && x.NextPmDate < to)
                .Select(x => new { x.Id, x.NextPmDate, AssetName = x.Asset != null ? x.Asset.AssetName : null, SiteName = x.Site != null ? x.Site.SiteName : null })
                .ToListAsync(cancellationToken);
            items.AddRange(plans.Select(x => Day($"pm-plan-{x.Id}-{x.NextPmDate:yyyyMMdd}", CalendarSources.Maintenance, $"PM due: {x.AssetName ?? "asset"}",
                DateOnly.FromDateTime(x.NextPmDate!.Value), "/maintenance/calendar", viewer.UserId, x.SiteName)));
        }

        if (wanted.Contains(CalendarSources.Projects))
        {
            var visible = (await linkedRecords.VisibleProjectsAsync(viewer, cancellationToken))
                .Where(x => x.Stage!.Category != ProjectStageCategories.Cancelled && x.Stage.Category != ProjectStageCategories.Closed);
            var milestones = await dbContext.ProjectMilestones
                .Where(x => x.TenantId == tenantId && visible.Any(p => p.Id == x.ProjectId && p.ProjectManagerUserId != null && people.Contains(p.ProjectManagerUserId.Value))
                    && x.PlannedDate != null && x.PlannedDate >= fromDate && x.PlannedDate <= toDate
                    && x.Status != ProjectMilestoneStatuses.Completed && x.Status != ProjectMilestoneStatuses.Verified)
                .Select(x => new { x.Id, x.Name, x.PlannedDate, x.ProjectId, x.Project!.ProjectNumber, x.Project.ProjectManagerUserId, x.Status })
                .ToListAsync(cancellationToken);
            items.AddRange(milestones.Select(x => Day($"milestone-{x.Id}", CalendarSources.Projects, $"{x.ProjectNumber} milestone: {x.Name}", x.PlannedDate!.Value,
                $"/projects/{x.ProjectId}?tab=milestones", x.ProjectManagerUserId, status: x.Status)));
            var tasks = await dbContext.ProjectTasks
                .Where(x => x.TenantId == tenantId && visible.Any(p => p.Id == x.ProjectId) && x.AssigneeUserId != null && people.Contains(x.AssigneeUserId.Value)
                    && x.DueDate != null && x.DueDate >= fromDate && x.DueDate <= toDate
                    && x.Status != ProjectTaskStatuses.Done && x.Status != ProjectTaskStatuses.Cancelled)
                .Select(x => new { x.Id, x.Title, x.DueDate, x.ProjectId, x.Project!.ProjectNumber, x.AssigneeUserId, x.Status })
                .ToListAsync(cancellationToken);
            items.AddRange(tasks.Select(x => Day($"project-task-{x.Id}", CalendarSources.Projects, $"{x.ProjectNumber} task: {x.Title}", x.DueDate!.Value,
                $"/projects/{x.ProjectId}?tab=tasks", x.AssigneeUserId, status: x.Status)));
        }

        if (wanted.Contains(CalendarSources.Contracts))
        {
            // Contract owners see their own; whoever manages contracts sees every live one.
            var userId = viewer.UserId;
            var all = viewer.Can(PermissionNames.CanManageSettings);
            var noticeHorizon = to.AddDays(366);
            var contracts = await dbContext.Contracts
                .Where(x => x.TenantId == tenantId && ContractCalendarRules.Live.Contains(x.Status) && x.EndDate >= from && x.EndDate < noticeHorizon
                    && (all || x.RenewalOwnerUserId == userId || x.AccountManagerUserId == userId))
                .Select(x => new { x.Id, x.ContractNumber, x.Name, x.EndDate, x.RenewalNoticeDays })
                .ToListAsync(cancellationToken);
            foreach (var contract in contracts)
            {
                var expiry = DateOnly.FromDateTime(contract.EndDate);
                if (expiry >= fromDate && expiry <= toDate)
                    items.Add(Day($"contract-expiry-{contract.Id}", CalendarSources.Contracts, $"{contract.ContractNumber} expires: {contract.Name}", expiry,
                        $"/contracts/{contract.Id}", userId));
                var notice = expiry.AddDays(-Math.Max(0, contract.RenewalNoticeDays));
                if (contract.RenewalNoticeDays > 0 && notice >= fromDate && notice <= toDate)
                    items.Add(Day($"contract-renewal-{contract.Id}", CalendarSources.Contracts, $"{contract.ContractNumber} renewal notice: {contract.Name}", notice,
                        $"/contracts/{contract.Id}", userId));
            }
        }

        return items.OrderBy(x => x.StartsAt).ThenBy(x => x.AllDay ? 0 : 1).ThenBy(x => x.Title).ToList();
    }

    private static CalendarItem Day(string uid, string source, string title, DateOnly date, string url, Guid? userId, string? location = null, string? status = null)
    {
        var day = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return new CalendarItem(uid, source, title, day, day, true, location, url, userId, status);
    }
}

public sealed record CalendarFeedLink(string Token, DateTime CreatedAt);

/// <summary>
/// The private, one-way calendar feed (design 3.5). The token is 32 random bytes shown once and stored
/// only as its SHA-256; creating a new link revokes the old one. The feed resolves a token to exactly one
/// user in one tenant and shows only what that user may see now, with titles, times and locations only.
/// </summary>
public interface ICalendarFeedService
{
    Task<CalendarFeedToken?> GetActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<CalendarFeedLink> CreateAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The ICS text for a token, or null for an unknown, revoked or inactive one.</summary>
    Task<string?> RenderAsync(string token, DateTime utcNow, CancellationToken cancellationToken = default);
}

internal sealed class CalendarFeedService(
    AppDbContext dbContext,
    ICalendarService calendarService,
    IAccessViewerFactory viewerFactory,
    ILicenseGuardService licenseGuardService) : ICalendarFeedService
{
    /// <summary>What the feed covers: a month back and six months ahead.</summary>
    public static readonly TimeSpan Past = TimeSpan.FromDays(31);
    public static readonly TimeSpan Ahead = TimeSpan.FromDays(183);

    public Task<CalendarFeedToken?> GetActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.CalendarFeedTokens.Where(x => x.TenantId == tenantId && x.UserId == userId && x.RevokedAt == null)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);

    public async Task<CalendarFeedLink> CreateAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        await RevokeAsync(tenantId, userId, cancellationToken);
        var token = WebEncoders(RandomNumberGenerator.GetBytes(32));
        var row = new CalendarFeedToken { TenantId = tenantId, UserId = userId, TokenHash = Hash(token) };
        dbContext.CalendarFeedTokens.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CalendarFeedLink(token, row.CreatedAt);
    }

    public async Task<bool> RevokeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var active = await dbContext.CalendarFeedTokens.Where(x => x.TenantId == tenantId && x.UserId == userId && x.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var row in active)
            row.RevokedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return active.Count > 0;
    }

    public async Task<string?> RenderAsync(string token, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
            return null;
        var hash = Hash(token.Trim());
        // The request is anonymous, so the token alone names the tenant and the user.
        var row = await dbContext.CalendarFeedTokens.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAt == null, cancellationToken);
        if (row is null)
            return null;
        var owner = await dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.Id == row.UserId && x.TenantId == row.TenantId && x.IsActive && x.Tenant!.IsActive)
            .Select(x => new { x.FullName, CompanyName = x.Tenant!.CompanyName })
            .SingleOrDefaultAsync(cancellationToken);
        if (owner is null || (await licenseGuardService.GetSnapshotAsync(row.TenantId, cancellationToken)).IsSuspended)
            return null;

        var viewer = await viewerFactory.ForUserAsync(row.TenantId, row.UserId, cancellationToken);
        var items = await calendarService.GetItemsAsync(viewer, utcNow - Past, utcNow + Ahead, null, null, cancellationToken);
        if (row.LastUsedAt is null || row.LastUsedAt < utcNow.AddHours(-1))
        {
            row.LastUsedAt = utcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return IcsCalendar.Write($"Ecosys – {owner.CompanyName}", items, utcNow);
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string WebEncoders(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>A minimal RFC 5545 writer for the one-way feed: summary, times and location, nothing else.</summary>
public static class IcsCalendar
{
    public static string Write(string calendarName, IEnumerable<CalendarItem> items, DateTime utcNow)
    {
        var builder = new StringBuilder();
        void Line(string text) => Fold(builder, text);
        Line("BEGIN:VCALENDAR");
        Line("VERSION:2.0");
        Line("PRODID:-//Ecosys//Calendar feed//EN");
        Line("CALSCALE:GREGORIAN");
        Line("METHOD:PUBLISH");
        Line($"X-WR-CALNAME:{Escape(calendarName)}");
        Line("REFRESH-INTERVAL;VALUE=DURATION:PT1H");
        var stamp = Utc(utcNow);
        // The same record can appear once per person in a team view; the feed is one person's, so one each.
        foreach (var item in items.DistinctBy(x => x.Uid))
        {
            Line("BEGIN:VEVENT");
            Line($"UID:{item.Uid}@ecosys");
            Line($"DTSTAMP:{stamp}");
            if (item.AllDay)
            {
                Line($"DTSTART;VALUE=DATE:{item.StartsAt:yyyyMMdd}");
                // DTEND is exclusive for dates.
                Line($"DTEND;VALUE=DATE:{item.EndsAt.Date.AddDays(1):yyyyMMdd}");
            }
            else
            {
                Line($"DTSTART:{Utc(item.StartsAt)}");
                Line($"DTEND:{Utc(item.EndsAt > item.StartsAt ? item.EndsAt : item.StartsAt.AddMinutes(30))}");
            }
            Line($"SUMMARY:{Escape(item.Title)}");
            if (!string.IsNullOrWhiteSpace(item.Location))
                Line($"LOCATION:{Escape(item.Location)}");
            Line("TRANSP:" + (item.AllDay ? "TRANSPARENT" : "OPAQUE"));
            Line("END:VEVENT");
        }
        Line("END:VCALENDAR");
        return builder.ToString();
    }

    private static string Utc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    public static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "");

    /// <summary>Lines longer than 75 octets continue on the next line after a space.</summary>
    private static void Fold(StringBuilder builder, string line)
    {
        var bytes = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (bytes + size > 75)
            {
                builder.Append("\r\n ");
                bytes = 1;
            }
            builder.Append(rune.ToString());
            bytes += size;
        }
        builder.Append("\r\n");
    }
}
