using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Meetings, site visits, inspections and calls with their attendees and minutes (Release 3 design 3.3).
/// Shared on every plan; who may see and change what is decided in <see cref="IActivityService"/>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/activities")]
public sealed class ActivitiesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAccessViewerFactory viewerFactory,
    IActivityService activityService,
    ILinkedRecordService linkedRecords) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ActivitySummaryResponse>>> List(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? entityType, [FromQuery] Guid? entityId,
        [FromQuery] string? status, [FromQuery] bool mine, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        var activities = await activityService.ListAsync(viewer, new ActivityListQuery(from, to, entityType, entityId, status, mine), cancellationToken);
        return Ok(await MapSummariesAsync(viewer, activities, cancellationToken));
    }

    /// <summary>People who can be invited: staff, consultants (with Projects) and, for a client, its portal contacts.</summary>
    [HttpGet("people")]
    public async Task<ActionResult<ActivityPeopleResponse>> People([FromQuery] Guid? clientId, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        viewer.Ensure(PermissionNames.ActivitiesView);
        var users = await dbContext.Users.Where(x => x.TenantId == TenantId && x.IsActive && x.Role != AppRoles.PortalClient)
            .OrderBy(x => x.FullName).Select(x => new ActivityPersonOption(x.Id, x.FullName, x.JobTitle ?? x.Role)).ToListAsync(cancellationToken);
        var consultants = viewer.HasModule(ServiceOpsPlans.Projects)
            ? await dbContext.Consultants.Where(x => x.TenantId == TenantId && x.IsActive).OrderBy(x => x.FirmName)
                .Select(x => new ActivityPersonOption(x.Id, x.ContactPerson ?? x.FirmName, x.ContactPerson == null ? x.Discipline : x.FirmName)).ToListAsync(cancellationToken)
            : [];
        var contacts = clientId.HasValue
            ? await dbContext.ClientPortalUsers.Where(x => x.TenantId == TenantId && x.ClientId == clientId && x.IsActive).OrderBy(x => x.FullName)
                .Select(x => new ActivityPersonOption(x.Id, x.FullName, x.Email)).ToListAsync(cancellationToken)
            : [];
        return Ok(new ActivityPeopleResponse(users, consultants, contacts));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActivityDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        return Ok(await MapDetailAsync(viewer, await activityService.LoadVisibleAsync(viewer, id, cancellationToken), cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ActivityDetailResponse>> Create([FromBody] SaveActivityRequest request, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        var activity = await activityService.ScheduleAsync(viewer, request.ToCommand(), cancellationToken);
        return Ok(await MapDetailAsync(viewer, activity, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ActivityDetailResponse>> Update(Guid id, [FromBody] SaveActivityRequest request, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        var activity = await activityService.UpdateAsync(viewer, id, request.ToCommand(), cancellationToken);
        return Ok(await MapDetailAsync(viewer, activity, cancellationToken));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ActivityDetailResponse>> Cancel(Guid id, [FromBody] CancelActivityRequest? request, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        return Ok(await MapDetailAsync(viewer, await activityService.CancelAsync(viewer, id, request?.Reason, cancellationToken), cancellationToken));
    }

    [HttpPost("{id:guid}/respond")]
    public async Task<ActionResult<ActivityDetailResponse>> Respond(Guid id, [FromBody] RespondToActivityRequest request, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        return Ok(await MapDetailAsync(viewer, await activityService.RespondAsync(viewer, id, request.Response, cancellationToken), cancellationToken));
    }

    [HttpPost("{id:guid}/held")]
    public async Task<ActionResult<ActivityDetailResponse>> MarkHeld(Guid id, [FromBody] MarkActivityHeldRequest request, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        var activity = await activityService.MarkHeldAsync(viewer, id, request.AttendedAttendeeIds ?? [], cancellationToken);
        return Ok(await MapDetailAsync(viewer, activity, cancellationToken));
    }

    [HttpPut("{id:guid}/minutes")]
    public async Task<ActionResult<ActivityDetailResponse>> SaveMinutes(Guid id, [FromBody] SaveMinutesRequest request, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        return Ok(await MapDetailAsync(viewer, await activityService.SaveMinutesAsync(viewer, id, request.Minutes, cancellationToken), cancellationToken));
    }

    [HttpPost("{id:guid}/publish-minutes")]
    public async Task<ActionResult<ActivityDetailResponse>> PublishMinutes(Guid id, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        return Ok(await MapDetailAsync(viewer, await activityService.PublishMinutesAsync(viewer, id, cancellationToken), cancellationToken));
    }

    [HttpPost("{id:guid}/minutes/addenda")]
    public async Task<ActionResult<ActivityDetailResponse>> AddAddendum(Guid id, [FromBody] AddMinutesAddendumRequest request, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        return Ok(await MapDetailAsync(viewer, await activityService.AddAddendumAsync(viewer, id, request.Text, cancellationToken), cancellationToken));
    }

    [HttpGet("{id:guid}/minutes.pdf")]
    public async Task<IActionResult> MinutesPdf(Guid id, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        var activity = await activityService.LoadVisibleAsync(viewer, id, cancellationToken);
        var pdf = await activityService.RenderMinutesPdfAsync(viewer, id, cancellationToken);
        return File(pdf, "application/pdf", $"Minutes-{activity.StartsAt:yyyyMMdd}.pdf");
    }

    // ── Mapping ──────────────────────────────────────────────────────────────────

    private Task<AccessViewer> ViewerAsync(CancellationToken cancellationToken)
    {
        EnsureTenantOperationalAccess();
        return viewerFactory.CurrentAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ActivitySummaryResponse>> MapSummariesAsync(AccessViewer viewer, IReadOnlyCollection<Activity> activities, CancellationToken cancellationToken)
    {
        var organizers = activities.Select(x => x.OrganizerUserId).Distinct().ToList();
        var names = await dbContext.Users.Where(x => organizers.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        var linked = await linkedRecords.DescribeAsync(viewer.TenantId, activities.Select(x => (x.EntityType, x.EntityId)), cancellationToken);
        return activities.Select(x => new ActivitySummaryResponse(
            x.Id, x.Type, x.Title, x.StartsAt, x.EndsAt, x.AllDay, x.Location, x.Status, x.OrganizerUserId, names.GetValueOrDefault(x.OrganizerUserId),
            Linked(linked, x.EntityType, x.EntityId), x.Attendees.Count, x.MinutesPublishedAt,
            x.Attendees.FirstOrDefault(a => a.UserId == viewer.UserId)?.Response)).ToList();
    }

    private async Task<ActivityDetailResponse> MapDetailAsync(AccessViewer viewer, Activity activity, CancellationToken cancellationToken)
    {
        var summary = (await MapSummariesAsync(viewer, [activity], cancellationToken)).Single();
        var names = await activityService.AttendeeNamesAsync(activity, cancellationToken);
        var contactIds = activity.Attendees.Where(x => x.ClientContactId.HasValue).Select(x => x.ClientContactId!.Value).ToList();
        var emails = await dbContext.ClientPortalUsers.Where(x => contactIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email, cancellationToken);
        var attendees = activity.Attendees
            .OrderBy(x => x.UserId.HasValue ? 0 : 1).ThenBy(x => names.GetValueOrDefault(x.Id))
            .Select(x => new ActivityAttendeeResponse(
                x.Id,
                x.UserId.HasValue ? "Staff" : x.ConsultantId.HasValue ? "Consultant" : x.ClientContactId.HasValue ? "ClientContact" : "External",
                x.UserId, x.ConsultantId, x.ClientContactId, names.GetValueOrDefault(x.Id, "Guest"),
                x.ExternalEmail ?? (x.ClientContactId is { } contact ? emails.GetValueOrDefault(contact) : null),
                x.UserId.HasValue ? x.Response : null, x.Attended))
            .ToList();
        var authorIds = activity.Addenda.Select(x => x.AuthorUserId).ToList();
        var authors = await dbContext.Users.Where(x => authorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        var addenda = activity.Addenda.OrderBy(x => x.CreatedAt)
            .Select(x => new MinutesAddendumResponse(x.Id, x.Text, authors.GetValueOrDefault(x.AuthorUserId), x.CreatedAt)).ToList();
        var items = await activityService.ListActionItemsAsync(viewer, new ActionItemListQuery(false, null, null, activity.Id, null), cancellationToken);
        var canManage = activityService.CanManage(viewer, activity);

        return new ActivityDetailResponse(summary, activity.Agenda, activity.Minutes, activity.ReminderMinutesBefore, activity.CancellationReason,
            attendees, addenda, await ActionItemsController.MapAsync(dbContext, linkedRecords, viewer, items, cancellationToken), canManage,
            activity.Attendees.Any(x => x.UserId == viewer.UserId));
    }

    internal static LinkedRecordResponse? Linked(IReadOnlyDictionary<(string EntityType, Guid EntityId), LinkedRecord> linked, string? entityType, Guid? entityId) =>
        entityType is not null && entityId is { } id && linked.TryGetValue((entityType, id), out var record)
            ? new LinkedRecordResponse(record.EntityType, record.EntityId, record.Label, record.Url)
            : entityType is not null && entityId is { } missing ? new LinkedRecordResponse(entityType, missing, entityType, null) : null;
}

public sealed record LinkedRecordResponse(string EntityType, Guid EntityId, string Label, string? Url);

public sealed record ActivitySummaryResponse(
    Guid Id, string Type, string Title, DateTime StartsAt, DateTime EndsAt, bool AllDay, string? Location, string Status,
    Guid OrganizerUserId, string? OrganizerName, LinkedRecordResponse? LinkedRecord, int AttendeeCount, DateTime? MinutesPublishedAt, string? MyResponse);

public sealed record ActivityAttendeeResponse(
    Guid Id, string Kind, Guid? UserId, Guid? ConsultantId, Guid? ClientContactId, string Name, string? Email, string? Response, bool Attended);

public sealed record MinutesAddendumResponse(Guid Id, string Text, string? AuthorName, DateTime CreatedAt);

public sealed record ActivityDetailResponse(
    ActivitySummaryResponse Summary, string? Agenda, string? Minutes, int? ReminderMinutesBefore, string? CancellationReason,
    IReadOnlyList<ActivityAttendeeResponse> Attendees, IReadOnlyList<MinutesAddendumResponse> Addenda, IReadOnlyList<ActionItemResponse> ActionItems,
    bool CanManage, bool IsAttendee);

public sealed record ActivityPersonOption(Guid Id, string Name, string? Detail);

public sealed record ActivityPeopleResponse(
    IReadOnlyList<ActivityPersonOption> Users, IReadOnlyList<ActivityPersonOption> Consultants, IReadOnlyList<ActivityPersonOption> ClientContacts);

public sealed record SaveActivityAttendeeRequest(Guid? UserId, Guid? ConsultantId, Guid? ClientContactId, string? ExternalName, string? ExternalEmail);

public sealed record SaveActivityRequest(
    string? Type, string Title, DateTime StartsAt, DateTime EndsAt, bool AllDay, string? Location, Guid? OrganizerUserId,
    string? EntityType, Guid? EntityId, string? Agenda, int? ReminderMinutesBefore, IReadOnlyList<SaveActivityAttendeeRequest>? Attendees)
{
    public SaveActivityCommand ToCommand() => new(Type, Title, StartsAt, EndsAt, AllDay, Location, OrganizerUserId, EntityType, EntityId, Agenda,
        ReminderMinutesBefore, Attendees?.Select(x => new ActivityAttendeeInput(x.UserId, x.ConsultantId, x.ClientContactId, x.ExternalName, x.ExternalEmail)).ToList());
}

public sealed record CancelActivityRequest(string? Reason);

public sealed record RespondToActivityRequest(string Response);

public sealed record MarkActivityHeldRequest(IReadOnlyList<Guid>? AttendedAttendeeIds);

public sealed record SaveMinutesRequest(string? Minutes);

public sealed record AddMinutesAddendumRequest(string Text);
