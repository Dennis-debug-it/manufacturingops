using System.Linq.Expressions;
using System.Text;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public static class ActivityNotificationKeys
{
    public const string Scheduled = "activity.scheduled";
    public const string Updated = "activity.updated";
    public const string Cancelled = "activity.cancelled";
    public const string Reminder = "activity.reminder";
    public const string MinutesPublished = "activity.minutes-published";
    public const string ActionItemAssigned = "action-item.assigned";
    public const string ActionItemDue = "action-item.due";
}

public sealed record ActivityAttendeeInput(Guid? UserId, Guid? ClientContactId, string? ExternalName, string? ExternalEmail);

public sealed record SaveActivityCommand(
    string? Type,
    string Title,
    DateTime StartsAt,
    DateTime EndsAt,
    bool AllDay,
    string? Location,
    Guid? OrganizerUserId,
    string? EntityType,
    Guid? EntityId,
    string? Agenda,
    int? ReminderMinutesBefore,
    IReadOnlyList<ActivityAttendeeInput>? Attendees);

/// <param name="Mine">Only activities the caller organises or attends (the default for anyone without activities.manage).</param>
public sealed record ActivityListQuery(DateTime? From, DateTime? To, string? EntityType, Guid? EntityId, string? Status, bool Mine);

public sealed record SaveActionItemCommand(
    string Title,
    Guid? AssigneeUserId,
    string? AssigneeExternalName,
    DateOnly? DueDate,
    string? Status,
    Guid? ActivityId,
    string? EntityType,
    Guid? EntityId);

/// <param name="Mine">Only items assigned to the caller.</param>
public sealed record ActionItemListQuery(bool Mine, string? EntityType, Guid? EntityId, Guid? ActivityId, string? Status);

/// <summary>
/// Meetings, visits, inspections and calls, their minutes and their action items (Release 3 design
/// 3.3). Everyone with activities.view may organise their own; activities.manage organises and changes
/// any. An activity is visible to its organiser and attendees, to activities.manage, and to anyone with
/// activities.view who can open the record it is linked to. Staff attendees are told about it through
/// <see cref="INotificationDispatcher"/>; other attendees are recorded only (R4).
/// </summary>
public interface IActivityService
{
    Task<IReadOnlyList<Activity>> ListAsync(AccessViewer viewer, ActivityListQuery query, CancellationToken cancellationToken = default);
    Task<Activity> LoadVisibleAsync(AccessViewer viewer, Guid activityId, CancellationToken cancellationToken = default);
    bool CanManage(AccessViewer viewer, Activity activity);

    Task<Activity> ScheduleAsync(AccessViewer viewer, SaveActivityCommand command, CancellationToken cancellationToken = default);
    Task<Activity> UpdateAsync(AccessViewer viewer, Guid activityId, SaveActivityCommand command, CancellationToken cancellationToken = default);
    Task<Activity> CancelAsync(AccessViewer viewer, Guid activityId, string? reason, CancellationToken cancellationToken = default);
    Task<Activity> RespondAsync(AccessViewer viewer, Guid activityId, string response, CancellationToken cancellationToken = default);

    /// <summary>Marks it Held and records who attended. Repeatable until the minutes are published.</summary>
    Task<Activity> MarkHeldAsync(AccessViewer viewer, Guid activityId, IReadOnlyCollection<Guid> attendedAttendeeIds, CancellationToken cancellationToken = default);
    Task<Activity> SaveMinutesAsync(AccessViewer viewer, Guid activityId, string? minutes, CancellationToken cancellationToken = default);

    /// <summary>Locks the minutes and emails them as a PDF to the staff attendees and the organiser.</summary>
    Task<Activity> PublishMinutesAsync(AccessViewer viewer, Guid activityId, CancellationToken cancellationToken = default);

    /// <summary>A correction to published minutes; the minutes themselves never change once published.</summary>
    Task<Activity> AddAddendumAsync(AccessViewer viewer, Guid activityId, string text, CancellationToken cancellationToken = default);
    Task<byte[]> RenderMinutesPdfAsync(AccessViewer viewer, Guid activityId, CancellationToken cancellationToken = default);

    /// <summary>A display name for each attendee row, whatever kind of attendee it is.</summary>
    Task<IReadOnlyDictionary<Guid, string>> AttendeeNamesAsync(Activity activity, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActionItem>> ListActionItemsAsync(AccessViewer viewer, ActionItemListQuery query, CancellationToken cancellationToken = default);
    Task<ActionItem> CreateActionItemAsync(AccessViewer viewer, SaveActionItemCommand command, CancellationToken cancellationToken = default);
    Task<ActionItem> UpdateActionItemAsync(AccessViewer viewer, Guid actionItemId, SaveActionItemCommand command, CancellationToken cancellationToken = default);
}

internal sealed class ActivityService(
    AppDbContext dbContext,
    ILinkedRecordService linkedRecords,
    ActivityNotifier notifier,
    IPdfRenderer pdfRenderer,
    IAuditLogService auditLogService) : IActivityService
{
    private const int MaxReminderMinutes = 7 * 24 * 60;

    // ── Activities ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Activity>> ListAsync(AccessViewer viewer, ActivityListQuery query, CancellationToken cancellationToken = default)
    {
        viewer.Ensure(PermissionNames.ActivitiesView);
        var tenantId = viewer.TenantId;
        var activities = dbContext.Activities.Include(x => x.Attendees).Where(x => x.TenantId == tenantId);
        if (query.EntityType is not null || query.EntityId.HasValue)
        {
            var (type, id) = await RequireOpenableRecordAsync(viewer, query.EntityType, query.EntityId, notFound: true, cancellationToken);
            activities = activities.Where(x => x.EntityType == type && x.EntityId == id);
        }
        else if (query.Mine || !viewer.Can(PermissionNames.ActivitiesManage))
        {
            activities = activities.Where(Involves(viewer.UserId));
        }

        if (query.From.HasValue) activities = activities.Where(x => x.EndsAt >= query.From.Value);
        if (query.To.HasValue) activities = activities.Where(x => x.StartsAt < query.To.Value);
        if (ActivityStatuses.All.FirstOrDefault(x => string.Equals(x, query.Status, StringComparison.OrdinalIgnoreCase)) is { } status)
            activities = activities.Where(x => x.Status == status);
        return await activities.OrderByDescending(x => x.StartsAt).Take(300).ToListAsync(cancellationToken);
    }

    public async Task<Activity> LoadVisibleAsync(AccessViewer viewer, Guid activityId, CancellationToken cancellationToken = default)
    {
        viewer.Ensure(PermissionNames.ActivitiesView);
        var activity = await dbContext.Activities.Include(x => x.Attendees).Include(x => x.Addenda)
            .SingleOrDefaultAsync(x => x.TenantId == viewer.TenantId && x.Id == activityId, cancellationToken);
        if (activity is null || !await CanSeeAsync(viewer, activity, cancellationToken))
            throw new NotFoundException("Activity not found.");
        return activity;
    }

    public bool CanManage(AccessViewer viewer, Activity activity) =>
        viewer.Can(PermissionNames.ActivitiesView)
        && (activity.OrganizerUserId == viewer.UserId || viewer.Can(PermissionNames.ActivitiesManage));

    public async Task<Activity> ScheduleAsync(AccessViewer viewer, SaveActivityCommand command, CancellationToken cancellationToken = default)
    {
        viewer.Ensure(PermissionNames.ActivitiesView);
        var activity = new Activity { TenantId = viewer.TenantId, Status = ActivityStatuses.Scheduled };
        await ApplyAsync(viewer, activity, command, cancellationToken);
        activity.Attendees = await BuildAttendeesAsync(viewer.TenantId, activity, command.Attendees, [], cancellationToken);
        dbContext.Activities.Add(activity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(viewer.TenantId, viewer.UserId, "activities.scheduled", nameof(Activity), activity.Id.ToString(),
            $"{activity.Type} \"{activity.Title}\" scheduled for {activity.StartsAt:yyyy-MM-dd HH:mm} UTC.", cancellationToken);

        await notifier.ActivityAsync(ActivityNotificationKeys.Scheduled, activity, StaffOf(activity), viewer.UserId, cancellationToken);
        return activity;
    }

    public async Task<Activity> UpdateAsync(AccessViewer viewer, Guid activityId, SaveActivityCommand command, CancellationToken cancellationToken = default)
    {
        var activity = await LoadManageableAsync(viewer, activityId, cancellationToken);
        if (activity.Status != ActivityStatuses.Scheduled)
            throw new BusinessRuleException("Only a scheduled activity can be changed.");

        var before = (activity.Title, activity.StartsAt, activity.EndsAt, activity.AllDay, activity.Location);
        var staffBefore = StaffOf(activity);
        await ApplyAsync(viewer, activity, command, cancellationToken);
        var kept = activity.Attendees.ToList();
        var next = await BuildAttendeesAsync(viewer.TenantId, activity, command.Attendees, kept, cancellationToken);
        foreach (var removed in kept.Where(x => !next.Contains(x)))
            dbContext.ActivityAttendees.Remove(removed);
        foreach (var added in next.Where(x => !kept.Contains(x)))
            dbContext.ActivityAttendees.Add(added);
        activity.Attendees = next;
        await dbContext.SaveChangesAsync(cancellationToken);

        var staffAfter = StaffOf(activity);
        var changed = before != (activity.Title, activity.StartsAt, activity.EndsAt, activity.AllDay, activity.Location);
        await notifier.ActivityAsync(ActivityNotificationKeys.Scheduled, activity, staffAfter.Except(staffBefore).ToList(), viewer.UserId, cancellationToken);
        await notifier.ActivityAsync(ActivityNotificationKeys.Cancelled, activity, staffBefore.Except(staffAfter).ToList(), viewer.UserId, cancellationToken,
            reason: "You were taken off the attendee list.");
        if (changed)
            await notifier.ActivityAsync(ActivityNotificationKeys.Updated, activity, staffAfter.Intersect(staffBefore).ToList(), viewer.UserId, cancellationToken);
        return activity;
    }

    public async Task<Activity> CancelAsync(AccessViewer viewer, Guid activityId, string? reason, CancellationToken cancellationToken = default)
    {
        var activity = await LoadManageableAsync(viewer, activityId, cancellationToken);
        if (activity.Status != ActivityStatuses.Scheduled)
            throw new BusinessRuleException(activity.Status == ActivityStatuses.Cancelled ? "This activity is already cancelled." : "A held activity can't be cancelled.");
        activity.Status = ActivityStatuses.Cancelled;
        activity.CancelledAt = DateTime.UtcNow;
        activity.CancellationReason = Clean(reason, 500, "The reason");
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(viewer.TenantId, viewer.UserId, "activities.cancelled", nameof(Activity), activity.Id.ToString(),
            $"{activity.Type} \"{activity.Title}\" cancelled.", cancellationToken);

        await notifier.ActivityAsync(ActivityNotificationKeys.Cancelled, activity, StaffOf(activity), viewer.UserId, cancellationToken,
            reason: activity.CancellationReason is null ? "" : $"Reason: {activity.CancellationReason}");
        return activity;
    }

    public async Task<Activity> RespondAsync(AccessViewer viewer, Guid activityId, string response, CancellationToken cancellationToken = default)
    {
        var activity = await LoadVisibleAsync(viewer, activityId, cancellationToken);
        var attendee = activity.Attendees.FirstOrDefault(x => x.UserId == viewer.UserId)
            ?? throw new BusinessRuleException("You are not on this activity's attendee list.");
        if (activity.Status != ActivityStatuses.Scheduled)
            throw new BusinessRuleException("This activity is no longer scheduled.");
        attendee.Response = ActivityAttendeeResponses.All.FirstOrDefault(x => string.Equals(x, response?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Reply Accepted, Declined or Pending.");
        await dbContext.SaveChangesAsync(cancellationToken);
        return activity;
    }

    public async Task<Activity> MarkHeldAsync(AccessViewer viewer, Guid activityId, IReadOnlyCollection<Guid> attendedAttendeeIds, CancellationToken cancellationToken = default)
    {
        var activity = await LoadManageableAsync(viewer, activityId, cancellationToken);
        if (activity.Status == ActivityStatuses.Cancelled)
            throw new BusinessRuleException("A cancelled activity can't be marked held.");
        if (activity.MinutesPublishedAt.HasValue)
            throw new BusinessRuleException("The minutes are published, so attendance is locked.");
        if (activity.StartsAt > DateTime.UtcNow)
            throw new BusinessRuleException("This activity hasn't started yet.");
        activity.Status = ActivityStatuses.Held;
        foreach (var attendee in activity.Attendees)
            attendee.Attended = attendedAttendeeIds.Contains(attendee.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
        return activity;
    }

    public async Task<Activity> SaveMinutesAsync(AccessViewer viewer, Guid activityId, string? minutes, CancellationToken cancellationToken = default)
    {
        var activity = await LoadManageableAsync(viewer, activityId, cancellationToken);
        EnsureMinutesEditable(activity);
        activity.Minutes = Clean(minutes, 20000, "The minutes");
        await dbContext.SaveChangesAsync(cancellationToken);
        return activity;
    }

    public async Task<Activity> PublishMinutesAsync(AccessViewer viewer, Guid activityId, CancellationToken cancellationToken = default)
    {
        var activity = await LoadManageableAsync(viewer, activityId, cancellationToken);
        EnsureMinutesEditable(activity);
        if (string.IsNullOrWhiteSpace(activity.Minutes))
            throw new BusinessRuleException("Write the minutes before publishing them.");
        activity.MinutesPublishedAt = DateTime.UtcNow;
        activity.MinutesPublishedByUserId = viewer.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(viewer.TenantId, viewer.UserId, "activities.minutes-published", nameof(Activity), activity.Id.ToString(),
            $"Minutes of \"{activity.Title}\" published.", cancellationToken);

        var pdf = await RenderPdfAsync(activity, cancellationToken);
        await notifier.ActivityAsync(ActivityNotificationKeys.MinutesPublished, activity, StaffOf(activity), viewer.UserId, cancellationToken,
            attachments: [new EmailAttachment(MinutesFileName(activity), pdf)]);
        return activity;
    }

    public async Task<Activity> AddAddendumAsync(AccessViewer viewer, Guid activityId, string text, CancellationToken cancellationToken = default)
    {
        var activity = await LoadManageableAsync(viewer, activityId, cancellationToken);
        if (!activity.MinutesPublishedAt.HasValue)
            throw new BusinessRuleException("The minutes aren't published yet; edit them directly.");
        var addendum = new ActivityMinutesAddendum
        {
            TenantId = viewer.TenantId,
            ActivityId = activity.Id,
            AuthorUserId = viewer.UserId,
            Text = Clean(text, 4000, "The addendum") ?? throw new BusinessRuleException("Write the correction."),
        };
        // The tracked activity picks the new row up in its Addenda.
        dbContext.ActivityMinutesAddenda.Add(addendum);
        await dbContext.SaveChangesAsync(cancellationToken);
        return activity;
    }

    public async Task<byte[]> RenderMinutesPdfAsync(AccessViewer viewer, Guid activityId, CancellationToken cancellationToken = default)
    {
        var activity = await LoadVisibleAsync(viewer, activityId, cancellationToken);
        if (string.IsNullOrWhiteSpace(activity.Minutes))
            throw new BusinessRuleException("This activity has no minutes yet.");
        return await RenderPdfAsync(activity, cancellationToken);
    }

    public Task<IReadOnlyDictionary<Guid, string>> AttendeeNamesAsync(Activity activity, CancellationToken cancellationToken = default) =>
        notifier.AttendeeNamesAsync(activity, cancellationToken);

    public static string MinutesFileName(Activity activity)
    {
        var slug = new string(activity.Title.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return $"Minutes-{activity.StartsAt:yyyyMMdd}-{(slug.Length > 40 ? slug[..40] : slug)}.pdf";
    }

    // ── Action items ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ActionItem>> ListActionItemsAsync(AccessViewer viewer, ActionItemListQuery query, CancellationToken cancellationToken = default)
    {
        viewer.Ensure(PermissionNames.ActivitiesView);
        var tenantId = viewer.TenantId;
        var userId = viewer.UserId;
        var items = dbContext.ActionItems.Where(x => x.TenantId == tenantId);
        if (query.EntityType is not null || query.EntityId.HasValue)
        {
            var (type, id) = await RequireOpenableRecordAsync(viewer, query.EntityType, query.EntityId, notFound: true, cancellationToken);
            items = items.Where(x => x.EntityType == type && x.EntityId == id);
        }
        else if (query.ActivityId.HasValue)
        {
            var activity = await LoadVisibleAsync(viewer, query.ActivityId.Value, cancellationToken);
            items = items.Where(x => x.ActivityId == activity.Id);
        }
        else if (query.Mine)
        {
            items = items.Where(x => x.AssigneeUserId == userId);
        }
        else if (!viewer.Can(PermissionNames.ActivitiesManage))
        {
            items = items.Where(x => x.AssigneeUserId == userId || x.CreatedByUserId == userId);
        }

        if (ActionItemStatuses.All.FirstOrDefault(x => string.Equals(x, query.Status, StringComparison.OrdinalIgnoreCase)) is { } status)
            items = items.Where(x => x.Status == status);
        return await items
            .OrderBy(x => x.Status == ActionItemStatuses.Open ? 0 : 1)
            .ThenBy(x => x.DueDate == null)
            .ThenBy(x => x.DueDate)
            .ThenByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    public async Task<ActionItem> CreateActionItemAsync(AccessViewer viewer, SaveActionItemCommand command, CancellationToken cancellationToken = default)
    {
        viewer.Ensure(PermissionNames.ActivitiesView);
        var item = new ActionItem { TenantId = viewer.TenantId, CreatedByUserId = viewer.UserId, Status = ActionItemStatuses.Open };
        if (command.ActivityId.HasValue)
        {
            // An item from a meeting belongs to that meeting's record; only its organiser (or a manager) adds them.
            var activity = await LoadManageableAsync(viewer, command.ActivityId.Value, cancellationToken);
            if (activity.Status == ActivityStatuses.Cancelled)
                throw new BusinessRuleException("A cancelled activity can't have action items.");
            item.ActivityId = activity.Id;
            item.EntityType = activity.EntityType;
            item.EntityId = activity.EntityId;
        }
        else if (command.EntityType is not null || command.EntityId.HasValue)
        {
            (item.EntityType, item.EntityId) = await RequireOpenableRecordAsync(viewer, command.EntityType, command.EntityId, notFound: false, cancellationToken);
        }

        await ApplyActionItemAsync(item, command, cancellationToken);
        dbContext.ActionItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        await notifier.ActionItemAssignedAsync(item, viewer.UserId, cancellationToken);
        return item;
    }

    public async Task<ActionItem> UpdateActionItemAsync(AccessViewer viewer, Guid actionItemId, SaveActionItemCommand command, CancellationToken cancellationToken = default)
    {
        viewer.Ensure(PermissionNames.ActivitiesView);
        var item = await dbContext.ActionItems.Include(x => x.Activity)
            .SingleOrDefaultAsync(x => x.TenantId == viewer.TenantId && x.Id == actionItemId, cancellationToken)
            ?? throw new NotFoundException("Action item not found.");
        var userId = viewer.UserId;
        var canEdit = item.AssigneeUserId == userId || item.CreatedByUserId == userId || item.Activity?.OrganizerUserId == userId
            || viewer.Can(PermissionNames.ActivitiesManage);
        if (!canEdit)
            throw new NotFoundException("Action item not found.");

        var assigneeBefore = item.AssigneeUserId;
        await ApplyActionItemAsync(item, command, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (item.AssigneeUserId != assigneeBefore)
            await notifier.ActionItemAssignedAsync(item, viewer.UserId, cancellationToken);
        return item;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Expression<Func<Activity, bool>> Involves(Guid userId) =>
        x => x.OrganizerUserId == userId || x.Attendees.Any(a => a.UserId == userId);

    /// <summary>The staff an activity concerns: the organiser and every attendee with a login.</summary>
    internal static List<Guid> StaffOf(Activity activity) =>
        activity.Attendees.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Append(activity.OrganizerUserId).Distinct().ToList();

    private async Task<bool> CanSeeAsync(AccessViewer viewer, Activity activity, CancellationToken cancellationToken)
    {
        if (activity.OrganizerUserId == viewer.UserId || activity.Attendees.Any(x => x.UserId == viewer.UserId) || viewer.Can(PermissionNames.ActivitiesManage))
            return true;
        return activity.EntityType is not null && activity.EntityId.HasValue
            && await linkedRecords.CanOpenAsync(viewer, activity.EntityType, activity.EntityId.Value, cancellationToken);
    }

    private async Task<Activity> LoadManageableAsync(AccessViewer viewer, Guid activityId, CancellationToken cancellationToken)
    {
        var activity = await LoadVisibleAsync(viewer, activityId, cancellationToken);
        if (!CanManage(viewer, activity))
            throw new ForbiddenException("Only the organiser can change this activity.");
        return activity;
    }

    private static void EnsureMinutesEditable(Activity activity)
    {
        if (activity.Status != ActivityStatuses.Held)
            throw new BusinessRuleException("Mark the activity as held before writing its minutes.");
        if (activity.MinutesPublishedAt.HasValue)
            throw new BusinessRuleException("The minutes are published and locked. Add an addendum to correct them.");
    }

    private async Task<(string Type, Guid Id)> RequireOpenableRecordAsync(AccessViewer viewer, string? entityType, Guid? entityId, bool notFound, CancellationToken cancellationToken)
    {
        var type = LinkedRecords.Normalize(entityType);
        if (type is null || !entityId.HasValue)
            throw new BusinessRuleException("Link a project, client, contract or work order.");
        if (!await linkedRecords.CanOpenAsync(viewer, type, entityId.Value, cancellationToken))
            throw notFound ? new NotFoundException("Record not found.") : new BusinessRuleException("Select a record you can open.");
        return (type, entityId.Value);
    }

    private async Task ApplyAsync(AccessViewer viewer, Activity activity, SaveActivityCommand command, CancellationToken cancellationToken)
    {
        activity.Title = Clean(command.Title, 200, "The title") ?? throw new BusinessRuleException("Give the activity a title.");
        activity.Type = string.IsNullOrWhiteSpace(command.Type)
            ? ActivityTypes.Meeting
            : ActivityTypes.All.FirstOrDefault(x => string.Equals(x, command.Type.Trim(), StringComparison.OrdinalIgnoreCase))
              ?? throw new BusinessRuleException("Choose a meeting, site visit, inspection or call.");

        // An all-day activity keeps dates only, stored as UTC midnight.
        var startsAt = DateTimeNormalization(command.StartsAt);
        var endsAt = DateTimeNormalization(command.EndsAt);
        if (command.AllDay)
        {
            startsAt = DateTime.SpecifyKind(startsAt.Date, DateTimeKind.Utc);
            endsAt = DateTime.SpecifyKind(endsAt.Date, DateTimeKind.Utc);
        }
        if (endsAt < startsAt)
            throw new BusinessRuleException("The end can't be before the start.");
        if (endsAt - startsAt > TimeSpan.FromDays(31))
            throw new BusinessRuleException("An activity can last at most 31 days.");
        activity.StartsAt = startsAt;
        activity.EndsAt = endsAt;
        activity.AllDay = command.AllDay;
        activity.Location = Clean(command.Location, 500, "The location");
        activity.Agenda = Clean(command.Agenda, 4000, "The agenda");
        if (command.ReminderMinutesBefore is < 0 or > MaxReminderMinutes)
            throw new BusinessRuleException("A reminder can be at most a week before.");
        activity.ReminderMinutesBefore = command.ReminderMinutesBefore;

        var organizer = command.OrganizerUserId ?? (activity.OrganizerUserId == Guid.Empty ? viewer.UserId : activity.OrganizerUserId);
        if (organizer != activity.OrganizerUserId && organizer != viewer.UserId)
        {
            // Anyone organises their own; organising for someone else needs activities.manage.
            viewer.Ensure(PermissionNames.ActivitiesManage);
            if (!await dbContext.Users.AnyAsync(x => x.TenantId == viewer.TenantId && x.Id == organizer && x.IsActive, cancellationToken))
                throw new BusinessRuleException("Select a valid organiser.");
        }
        activity.OrganizerUserId = organizer;

        if (command.EntityType is null && !command.EntityId.HasValue)
        {
            activity.EntityType = null;
            activity.EntityId = null;
        }
        else if (LinkedRecords.Normalize(command.EntityType) != activity.EntityType || command.EntityId != activity.EntityId)
        {
            (activity.EntityType, activity.EntityId) = await RequireOpenableRecordAsync(viewer, command.EntityType, command.EntityId, notFound: false, cancellationToken);
        }
    }

    private static DateTime DateTimeNormalization(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    /// <summary>
    /// The attendee list as requested. A row the activity already has (same person) is reused, so a
    /// staff member's reply survives an edit. The organiser is implied and never listed.
    /// </summary>
    private async Task<List<ActivityAttendee>> BuildAttendeesAsync(Guid tenantId, Activity activity, IReadOnlyList<ActivityAttendeeInput>? inputs,
        IReadOnlyCollection<ActivityAttendee> existing, CancellationToken cancellationToken)
    {
        inputs ??= [];
        if (inputs.Count > 100)
            throw new BusinessRuleException("An activity can have at most 100 attendees.");
        var userIds = inputs.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).Distinct().ToList();
        var contactIds = inputs.Where(x => x.ClientContactId.HasValue).Select(x => x.ClientContactId!.Value).Distinct().ToList();
        if (userIds.Count > 0 && await dbContext.Users.CountAsync(x => x.TenantId == tenantId && x.IsActive && userIds.Contains(x.Id), cancellationToken) != userIds.Count)
            throw new BusinessRuleException("Select valid staff attendees.");
        if (contactIds.Count > 0 && await dbContext.ClientPortalUsers.CountAsync(x => x.TenantId == tenantId && contactIds.Contains(x.Id), cancellationToken) != contactIds.Count)
            throw new BusinessRuleException("Select valid client contacts.");

        var result = new List<ActivityAttendee>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in inputs)
        {
            var externalName = Clean(input.ExternalName, 200, "An attendee's name");
            var externalEmail = Clean(input.ExternalEmail, 256, "An attendee's email")?.ToLowerInvariant();
            var kinds = (input.UserId.HasValue ? 1 : 0) + (input.ClientContactId.HasValue ? 1 : 0)
                + (externalName is not null || externalEmail is not null ? 1 : 0);
            if (kinds != 1)
                throw new BusinessRuleException("Each attendee is one person: a user, a client contact or an outside name and email.");
            if (externalEmail is not null && (!externalEmail.Contains('@') || externalEmail.Contains(' ')))
                throw new BusinessRuleException($"{externalEmail} is not a valid email address.");
            if (input.UserId == activity.OrganizerUserId)
                continue;
            var identity = input.UserId is { } u ? $"u:{u}" : input.ClientContactId is { } k ? $"k:{k}" : $"e:{externalEmail}|{externalName}";
            if (!seen.Add(identity))
                continue;

            var match = existing.FirstOrDefault(x => Identity(x) == identity);
            result.Add(match ?? new ActivityAttendee
            {
                TenantId = tenantId,
                ActivityId = activity.Id,
                UserId = input.UserId,
                ClientContactId = input.ClientContactId,
                ExternalName = input.UserId.HasValue || input.ClientContactId.HasValue ? null : externalName,
                ExternalEmail = input.UserId.HasValue || input.ClientContactId.HasValue ? null : externalEmail,
            });
        }
        return result;

        static string Identity(ActivityAttendee x) =>
            x.UserId is { } u ? $"u:{u}" : x.ClientContactId is { } k ? $"k:{k}" : $"e:{x.ExternalEmail}|{x.ExternalName}";
    }

    private async Task ApplyActionItemAsync(ActionItem item, SaveActionItemCommand command, CancellationToken cancellationToken)
    {
        item.Title = Clean(command.Title, 300, "The title") ?? throw new BusinessRuleException("Describe the action item.");
        if (command.AssigneeUserId.HasValue && command.AssigneeUserId != item.AssigneeUserId
            && !await dbContext.Users.AnyAsync(x => x.TenantId == item.TenantId && x.Id == command.AssigneeUserId && x.IsActive, cancellationToken))
            throw new BusinessRuleException("Select a valid assignee.");
        item.AssigneeUserId = command.AssigneeUserId;
        item.AssigneeExternalName = command.AssigneeUserId.HasValue ? null : Clean(command.AssigneeExternalName, 200, "The owner's name");
        item.DueDate = command.DueDate;
        if (!string.IsNullOrWhiteSpace(command.Status))
        {
            var status = ActionItemStatuses.All.FirstOrDefault(x => string.Equals(x, command.Status.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException("Status is Open, Done or Cancelled.");
            if (status != item.Status)
                item.CompletedAt = status == ActionItemStatuses.Done ? DateTime.UtcNow : null;
            item.Status = status;
        }
    }

    private static string? Clean(string? value, int maxLength, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new BusinessRuleException($"{what} can be at most {maxLength} characters.");
        return trimmed;
    }

    /// <summary>The minutes as a PDF: details, attendance, agenda, minutes, addenda and the action items.</summary>
    private async Task<byte[]> RenderPdfAsync(Activity activity, CancellationToken cancellationToken)
    {
        var names = await notifier.AttendeeNamesAsync(activity, cancellationToken);
        var organizer = await dbContext.Users.Where(x => x.Id == activity.OrganizerUserId).Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken);
        var linked = await notifier.LinkedLabelAsync(activity, cancellationToken);
        var when = await notifier.WhenAsync(activity, cancellationToken);
        var items = await dbContext.ActionItems.Where(x => x.TenantId == activity.TenantId && x.ActivityId == activity.Id)
            .OrderBy(x => x.DueDate == null).ThenBy(x => x.DueDate)
            .Select(x => new { x.Title, Owner = x.AssigneeUser != null ? x.AssigneeUser.FullName : x.AssigneeExternalName, x.DueDate, x.Status })
            .ToListAsync(cancellationToken);
        var addenda = activity.Addenda.OrderBy(x => x.CreatedAt).ToList();
        var authors = await dbContext.Users.Where(x => addenda.Select(a => a.AuthorUserId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        var sections = new List<string>
        {
            $"{ActivityNotifier.Humanize(activity.Type)} · {when}",
            $"Where: {activity.Location ?? "Not set"}",
            $"Organiser: {organizer ?? "Unknown"}",
        };
        if (linked is not null) sections.Add($"About: {linked}");
        var attendance = new StringBuilder("Attendees:");
        foreach (var attendee in activity.Attendees)
            attendance.Append($"\n  {names.GetValueOrDefault(attendee.Id, "Attendee")} — {(attendee.Attended ? "attended" : "absent")}");
        if (activity.Attendees.Count == 0) attendance.Append(" none listed");
        sections.Add(attendance.ToString());
        if (activity.Agenda is not null) sections.Add($"Agenda\n{activity.Agenda}");
        sections.Add($"Minutes\n{activity.Minutes}");
        foreach (var addendum in addenda)
            sections.Add($"Addendum, {addendum.CreatedAt:dd MMM yyyy} by {authors.GetValueOrDefault(addendum.AuthorUserId, "Unknown")}\n{addendum.Text}");
        if (items.Count > 0)
            sections.Add("Action items" + string.Concat(items.Select(x =>
                $"\n  • {x.Title} — {x.Owner ?? "Unassigned"}{(x.DueDate.HasValue ? $", due {x.DueDate:dd MMM yyyy}" : "")}{(x.Status == ActionItemStatuses.Open ? "" : $" ({x.Status.ToLowerInvariant()})")}")));
        if (activity.MinutesPublishedAt.HasValue)
            sections.Add($"Published {activity.MinutesPublishedAt:dd MMM yyyy HH:mm} UTC.");
        return pdfRenderer.RenderWorkOrderReportPdf($"Minutes: {activity.Title}", sections);
    }
}

/// <summary>
/// Builds and sends activity and action-item notifications through <see cref="INotificationDispatcher"/>:
/// the title and body for the bell and push, and the template values for the email. Shared by the
/// activity service and the reminder runner. The person who made the change is never told about it.
/// </summary>
internal sealed class ActivityNotifier(
    AppDbContext dbContext,
    INotificationDispatcher dispatcher,
    ILinkedRecordService linkedRecords,
    ITenantTimeZoneService tenantTimeZoneService)
{
    public async Task ActivityAsync(string key, Activity activity, IReadOnlyCollection<Guid> userIds, Guid? actorUserId, CancellationToken cancellationToken,
        string? reason = null, IReadOnlyCollection<EmailAttachment>? attachments = null)
    {
        var recipients = userIds.Where(x => x != actorUserId).Distinct().ToList();
        if (recipients.Count == 0)
            return;
        var when = await WhenAsync(activity, cancellationToken);
        var organizer = await dbContext.Users.Where(x => x.Id == activity.OrganizerUserId).Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken);
        var linked = await LinkedLabelAsync(activity, cancellationToken);
        var type = Humanize(activity.Type);
        var where = activity.Location ?? "Not set";
        var (title, body) = key switch
        {
            ActivityNotificationKeys.Scheduled => ($"{type}: {activity.Title}", $"{when} · {where}. Organised by {organizer}."),
            ActivityNotificationKeys.Updated => ($"Changed: {activity.Title}", $"Now {when} · {where}."),
            ActivityNotificationKeys.Cancelled => ($"Cancelled: {activity.Title}", $"{when}. {reason}".Trim()),
            ActivityNotificationKeys.Reminder => ($"Starting soon: {activity.Title}", $"{when} · {where}."),
            ActivityNotificationKeys.MinutesPublished => ($"Minutes: {activity.Title}", $"The minutes of {when} are published."),
            _ => (activity.Title, when),
        };
        await dispatcher.NotifyAsync(new NotificationRequest(
            activity.TenantId, key, recipients, title, body, $"/activities/{activity.Id}", nameof(Activity), activity.Id,
            new Dictionary<string, string?>
            {
                ["ActivityTitle"] = activity.Title,
                ["ActivityType"] = type,
                ["When"] = when,
                ["Location"] = where,
                ["OrganizerName"] = organizer,
                ["LinkedRecord"] = linked ?? "—",
                ["Reason"] = reason ?? "",
            },
            Attachments: attachments), cancellationToken);
    }

    public async Task ActionItemAssignedAsync(ActionItem item, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (item.AssigneeUserId is not Guid assignee || assignee == actorUserId || item.Status != ActionItemStatuses.Open)
            return;
        var assignedBy = await dbContext.Users.Where(x => x.Id == actorUserId).Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken);
        await ActionItemAsync(ActivityNotificationKeys.ActionItemAssigned, item, assignee, $"Action item: {item.Title}",
            item.DueDate.HasValue ? $"Due {item.DueDate:dd MMM}. From {assignedBy}." : $"From {assignedBy}.", assignedBy, cancellationToken);
    }

    public Task ActionItemDueAsync(ActionItem item, CancellationToken cancellationToken) =>
        item.AssigneeUserId is not Guid assignee
            ? Task.CompletedTask
            : ActionItemAsync(ActivityNotificationKeys.ActionItemDue, item, assignee, $"Due {item.DueDate:dd MMM}: {item.Title}",
                "Your action item is due tomorrow.", null, cancellationToken);

    private async Task ActionItemAsync(string key, ActionItem item, Guid assignee, string title, string body, string? assignedBy, CancellationToken cancellationToken)
    {
        var activityTitle = item.ActivityId.HasValue
            ? await dbContext.Activities.Where(x => x.Id == item.ActivityId).Select(x => x.Title).SingleOrDefaultAsync(cancellationToken)
            : null;
        string? linked = null;
        if (item.EntityType is not null && item.EntityId.HasValue)
            linked = (await linkedRecords.DescribeAsync(item.TenantId, [(item.EntityType, item.EntityId)], cancellationToken)).Values.FirstOrDefault()?.Label;
        await dispatcher.NotifyAsync(new NotificationRequest(
            item.TenantId, key, [assignee], title, body,
            item.ActivityId.HasValue ? $"/activities/{item.ActivityId}" : "/action-items", nameof(ActionItem), item.Id,
            new Dictionary<string, string?>
            {
                ["ActionItemTitle"] = item.Title,
                ["DueDate"] = item.DueDate?.ToString("dd MMM yyyy") ?? "Not set",
                ["ActivityTitle"] = activityTitle ?? "—",
                ["LinkedRecord"] = linked ?? "—",
                ["AssignedBy"] = assignedBy ?? "Ecosys",
            }), cancellationToken);
    }

    /// <summary>"Thu 22 Oct 2026, 10:00–11:00" in the tenant's time zone, or the date(s) for an all-day activity.</summary>
    public async Task<string> WhenAsync(Activity activity, CancellationToken cancellationToken)
    {
        if (activity.AllDay)
            return activity.EndsAt.Date > activity.StartsAt.Date
                ? $"{activity.StartsAt:ddd d MMM} – {activity.EndsAt:ddd d MMM yyyy} (all day)"
                : $"{activity.StartsAt:ddd d MMM yyyy} (all day)";
        var zone = await tenantTimeZoneService.GetTimeZoneAsync(activity.TenantId, cancellationToken);
        var start = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(activity.StartsAt, DateTimeKind.Utc), zone);
        var end = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(activity.EndsAt, DateTimeKind.Utc), zone);
        return end.Date == start.Date
            ? $"{start:ddd d MMM yyyy, HH:mm}–{end:HH:mm}"
            : $"{start:ddd d MMM yyyy, HH:mm} – {end:ddd d MMM yyyy, HH:mm}";
    }

    public async Task<string?> LinkedLabelAsync(Activity activity, CancellationToken cancellationToken) =>
        activity.EntityType is null || !activity.EntityId.HasValue
            ? null
            : (await linkedRecords.DescribeAsync(activity.TenantId, [(activity.EntityType, activity.EntityId)], cancellationToken)).Values.FirstOrDefault()?.Label;

    /// <summary>A display name for each attendee row, whatever kind of attendee it is.</summary>
    public async Task<IReadOnlyDictionary<Guid, string>> AttendeeNamesAsync(Activity activity, CancellationToken cancellationToken)
    {
        var userIds = activity.Attendees.Where(x => x.UserId.HasValue).Select(x => x.UserId!.Value).ToList();
        var contactIds = activity.Attendees.Where(x => x.ClientContactId.HasValue).Select(x => x.ClientContactId!.Value).ToList();
        var users = await dbContext.Users.Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        var contacts = await dbContext.ClientPortalUsers.Where(x => contactIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        return activity.Attendees.ToDictionary(x => x.Id, x =>
            x.UserId is { } u ? users.GetValueOrDefault(u, "Staff member")
            : x.ClientContactId is { } k ? contacts.GetValueOrDefault(k, "Client contact")
            : x.ExternalName ?? x.ExternalEmail ?? "Guest");
    }

    public static string Humanize(string type) => type switch
    {
        ActivityTypes.SiteVisit => "Site visit",
        _ => type,
    };
}
