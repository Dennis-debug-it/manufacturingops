using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Follow-ups with an owner and a due date, from a meeting or on a record (R3). Listed for the caller
/// ("My action items"), for a linked record (a project's Tasks tab, a client or contract page) or for one meeting.
/// </summary>
[ApiController]
[Authorize]
[Route("api/action-items")]
public sealed class ActionItemsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAccessViewerFactory viewerFactory,
    IActivityService activityService,
    ILinkedRecordService linkedRecords) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ActionItemResponse>>> List(
        [FromQuery] bool mine, [FromQuery] string? entityType, [FromQuery] Guid? entityId, [FromQuery] Guid? activityId, [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        var items = await activityService.ListActionItemsAsync(viewer, new ActionItemListQuery(mine, entityType, entityId, activityId, status), cancellationToken);
        return Ok(await MapAsync(dbContext, linkedRecords, viewer, items, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ActionItemResponse>> Create([FromBody] SaveActionItemRequest request, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        var item = await activityService.CreateActionItemAsync(viewer, request.ToCommand(), cancellationToken);
        return Ok((await MapAsync(dbContext, linkedRecords, viewer, [item], cancellationToken)).Single());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ActionItemResponse>> Update(Guid id, [FromBody] SaveActionItemRequest request, CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        var item = await activityService.UpdateActionItemAsync(viewer, id, request.ToCommand(), cancellationToken);
        return Ok((await MapAsync(dbContext, linkedRecords, viewer, [item], cancellationToken)).Single());
    }

    private Task<AccessViewer> ViewerAsync(CancellationToken cancellationToken)
    {
        EnsureTenantOperationalAccess();
        return viewerFactory.CurrentAsync(cancellationToken);
    }

    internal static async Task<IReadOnlyList<ActionItemResponse>> MapAsync(AppDbContext dbContext, ILinkedRecordService linkedRecords, AccessViewer viewer,
        IReadOnlyCollection<ActionItem> items, CancellationToken cancellationToken)
    {
        var userIds = items.Where(x => x.AssigneeUserId.HasValue).Select(x => x.AssigneeUserId!.Value).Distinct().ToList();
        var names = await dbContext.Users.Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        var activityIds = items.Where(x => x.ActivityId.HasValue).Select(x => x.ActivityId!.Value).Distinct().ToList();
        var activities = await dbContext.Activities.Where(x => activityIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new { x.Title, x.OrganizerUserId }, cancellationToken);
        var linked = await linkedRecords.DescribeAsync(viewer.TenantId, items.Select(x => (x.EntityType, x.EntityId)), cancellationToken);
        var manage = viewer.Can(PermissionNames.ActivitiesManage);
        return items.Select(x =>
        {
            var activity = x.ActivityId is { } activityId ? activities.GetValueOrDefault(activityId) : null;
            var canEdit = manage || x.AssigneeUserId == viewer.UserId || x.CreatedByUserId == viewer.UserId || activity?.OrganizerUserId == viewer.UserId;
            return new ActionItemResponse(
                x.Id, x.Title, x.AssigneeUserId, x.AssigneeUserId is { } a ? names.GetValueOrDefault(a) : null, x.AssigneeExternalName,
                x.DueDate, x.Status, x.ActivityId, activity?.Title, ActivitiesController.Linked(linked, x.EntityType, x.EntityId),
                x.CreatedAt, x.CompletedAt, canEdit);
        }).ToList();
    }
}

public sealed record ActionItemResponse(
    Guid Id, string Title, Guid? AssigneeUserId, string? AssigneeName, string? AssigneeExternalName, DateOnly? DueDate, string Status,
    Guid? ActivityId, string? ActivityTitle, LinkedRecordResponse? LinkedRecord, DateTime CreatedAt, DateTime? CompletedAt, bool CanEdit);

public sealed record SaveActionItemRequest(
    string Title, Guid? AssigneeUserId, string? AssigneeExternalName, DateOnly? DueDate, string? Status, Guid? ActivityId, string? EntityType, Guid? EntityId)
{
    public SaveActionItemCommand ToCommand() => new(Title, AssigneeUserId, AssigneeExternalName, DueDate, Status, ActivityId, EntityType, EntityId);
}
