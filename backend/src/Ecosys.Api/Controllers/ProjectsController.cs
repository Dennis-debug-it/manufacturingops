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
[Route("api/projects")]
public sealed class ProjectsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IProjectAccessService projectAccessService,
    IProjectService projectService,
    IProjectSettingsService projectSettingsService,
    IProjectNotificationService projectNotificationService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    // ── Register ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ProjectListItemResponse>>> List(
        [FromQuery] string? stageCategory,
        [FromQuery] Guid? projectTypeId,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? projectManagerUserId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var projects = await LoadRegisterAsync(cancellationToken);
        var items = projects
            .Where(x => string.IsNullOrWhiteSpace(stageCategory)
                || string.Equals(stageCategory, "Open", StringComparison.OrdinalIgnoreCase) && ProjectStageCategories.Open.Contains(x.Stage!.Category)
                || string.Equals(x.Stage!.Category, stageCategory, StringComparison.OrdinalIgnoreCase))
            .Where(x => !projectTypeId.HasValue || x.ProjectTypeId == projectTypeId)
            .Where(x => !clientId.HasValue || x.ClientId == clientId)
            .Where(x => !projectManagerUserId.HasValue || x.ProjectManagerUserId == projectManagerUserId)
            .Where(x => string.IsNullOrWhiteSpace(search)
                || x.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                || x.ProjectNumber.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                || (x.Client?.ClientName ?? "").Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(MapListItem)
            .ToList();
        return Ok(items);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ProjectRegisterSummaryResponse>> Summary(CancellationToken cancellationToken)
    {
        var projects = await LoadRegisterAsync(cancellationToken);
        var open = projects.Where(x => ProjectStageCategories.Open.Contains(x.Stage!.Category)).ToList();
        var openIds = open.Select(x => x.Id).ToList();
        var pending = await dbContext.ProjectVariations
            .Where(x => x.TenantId == TenantId && openIds.Contains(x.ProjectId) && x.Status == ProjectVariationStatuses.Submitted)
            .Select(x => new { x.Kind, x.Amount })
            .ToListAsync(cancellationToken);

        return Ok(new ProjectRegisterSummaryResponse(
            ActiveProjects: open.Count(x => x.Stage!.Category is ProjectStageCategories.Active or ProjectStageCategories.Handover),
            OpenProjects: open.Count,
            TotalRevisedValue: open.Sum(x => x.RevisedValue),
            BehindSchedule: open.Count(x => x.Stage!.Category != ProjectStageCategories.OnHold
                && ProjectCommercials.IsBehindSchedule(x.EffectiveProgressPercent, ProjectCommercials.ComputePlannedProgress(x.Milestones, Today))),
            PendingVariationsCount: pending.Count,
            PendingVariationsValue: pending.Sum(x => x.Kind == ProjectVariationKinds.Omission ? -x.Amount : x.Amount)));
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDetailResponse>> Create([FromBody] UpsertProjectRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await projectService.CreateAsync(TenantId, UserId, new CreateProjectCommand(
            request.Name, request.ClientId, request.SiteId, request.ProjectTypeId, request.StageId, request.ProjectManagerUserId,
            request.BranchId, request.OriginalValue, request.Currency, request.PlannedStartDate, request.PlannedEndDate,
            request.Description, request.TemplateId, request.VisibleInClientPortal ?? true), cancellationToken);
        await projectService.RecalculateProgressAsync(TenantId, project, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (project.ProjectManagerUserId.HasValue)
            await projectNotificationService.MemberAddedAsync(TenantId, project, project.ProjectManagerUserId.Value, ProjectMemberRoles.ProjectManager, cancellationToken);
        return Ok(await BuildDetailAsync(project.Id, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDetailResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        return Ok(await BuildDetailAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDetailResponse>> Update(Guid id, [FromBody] UpsertProjectRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Project name is required.");
        if (request.OriginalValue < 0)
            throw new BusinessRuleException("Project value cannot be negative.");
        if (request.OriginalValue + project.ApprovedAdditions - project.ApprovedOmissions < 0)
            throw new BusinessRuleException("The project value cannot be lower than its approved omissions.");
        if (request.PlannedStartDate.HasValue && request.PlannedEndDate.HasValue && request.PlannedEndDate < request.PlannedStartDate)
            throw new BusinessRuleException("Planned end date cannot be before the planned start date.");
        if (request.ClientId != project.ClientId)
            throw new BusinessRuleException("A project's client cannot be changed. Cancel it and create a new project instead.");
        if (request.SiteId.HasValue && !await dbContext.Sites.AnyAsync(x => x.TenantId == TenantId && x.Id == request.SiteId && x.ClientId == project.ClientId, cancellationToken))
            throw new BusinessRuleException("The site must belong to the project's client.");
        if (request.ProjectTypeId.HasValue && !await dbContext.ProjectTypes.AnyAsync(x => x.TenantId == TenantId && x.Id == request.ProjectTypeId, cancellationToken))
            throw new BusinessRuleException("Select a valid project type.");
        if (request.ProjectManagerUserId.HasValue && !await dbContext.Users.AnyAsync(x => x.TenantId == TenantId && x.Id == request.ProjectManagerUserId && x.IsActive, cancellationToken))
            throw new BusinessRuleException("Select a valid project manager.");
        if (request.BranchId.HasValue && !await dbContext.Branches.AnyAsync(x => x.TenantId == TenantId && x.Id == request.BranchId, cancellationToken))
            throw new BusinessRuleException("Select a valid branch.");

        var previousManager = project.ProjectManagerUserId;
        var previousValue = project.OriginalValue;
        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        project.SiteId = request.SiteId;
        project.ProjectTypeId = request.ProjectTypeId ?? project.ProjectTypeId;
        project.ProjectManagerUserId = request.ProjectManagerUserId;
        project.BranchId = request.BranchId;
        project.OriginalValue = request.OriginalValue;
        project.Currency = string.IsNullOrWhiteSpace(request.Currency) ? project.Currency : request.Currency.Trim().ToUpperInvariant();
        project.PlannedStartDate = request.PlannedStartDate;
        project.PlannedEndDate = request.PlannedEndDate;
        project.VisibleInClientPortal = request.VisibleInClientPortal ?? project.VisibleInClientPortal;

        var newManager = project.ProjectManagerUserId;
        var managerChanged = newManager.HasValue && newManager != previousManager;
        if (managerChanged && !await dbContext.ProjectMembers.AnyAsync(x => x.TenantId == TenantId && x.ProjectId == id && x.UserId == newManager, cancellationToken))
        {
            dbContext.ProjectMembers.Add(new ProjectMember { TenantId = TenantId, ProjectId = id, UserId = newManager, Role = ProjectMemberRoles.ProjectManager });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (previousValue != project.OriginalValue)
        {
            await auditLogService.LogAsync(TenantId, UserId, "projects.value-changed", nameof(Project), project.Id.ToString(),
                $"Project {project.ProjectNumber} original value changed from {previousValue:N2} to {project.OriginalValue:N2}.", cancellationToken);
        }
        if (managerChanged)
            await projectNotificationService.MemberAddedAsync(TenantId, project, newManager!.Value, ProjectMemberRoles.ProjectManager, cancellationToken);
        return Ok(await BuildDetailAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/stage")]
    public async Task<ActionResult<ProjectDetailResponse>> ChangeStage(Guid id, [FromBody] ChangeProjectStageRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        await projectService.ChangeStageAsync(TenantId, UserId, project, request.StageId, request.Note, cancellationToken);
        return Ok(await BuildDetailAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/progress-override")]
    public async Task<ActionResult<ProjectDetailResponse>> OverrideProgress(Guid id, [FromBody] ProgressOverrideRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var settings = await projectSettingsService.GetAsync(TenantId, cancellationToken);
        if (!settings.AllowManualProgressOverride)
            throw new BusinessRuleException("Manual progress overrides are turned off in Project settings.");
        var project = await LoadEditableAsync(id, cancellationToken);

        if (request.Percent.HasValue)
        {
            if (request.Percent < 0 || request.Percent > 100)
                throw new BusinessRuleException("Progress must be between 0 and 100.");
            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new BusinessRuleException("Give a reason for overriding the calculated progress.");
            project.ProgressOverridePercent = Math.Round(request.Percent.Value, 2);
            project.ProgressOverrideReason = request.Reason.Trim();
            project.ProgressOverrideByUserId = UserId;
        }
        else
        {
            project.ProgressOverridePercent = null;
            project.ProgressOverrideReason = null;
            project.ProgressOverrideByUserId = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.progress-override", nameof(Project), project.Id.ToString(),
            request.Percent.HasValue
                ? $"Progress on {project.ProjectNumber} overridden to {project.ProgressOverridePercent:N2}% (calculated {project.ProgressPercent:N2}%). Reason: {project.ProgressOverrideReason}"
                : $"Progress override on {project.ProjectNumber} removed.",
            cancellationToken);
        return Ok(await BuildDetailAsync(id, cancellationToken));
    }

    // ── Team ───────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectMemberResponse>>> ListMembers(Guid id, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        return Ok(await LoadMembersAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<ProjectMemberResponse>> AddMember(Guid id, [FromBody] UpsertProjectMemberRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        if (!request.UserId.HasValue && !request.TechnicianId.HasValue)
            throw new BusinessRuleException("Select a user or a technician.");
        var role = NormalizeMemberRole(request.Role);

        var technician = request.TechnicianId.HasValue
            ? await dbContext.Technicians.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.TechnicianId && x.IsActive, cancellationToken)
                ?? throw new BusinessRuleException("Select a valid technician.")
            : null;
        var userId = request.UserId ?? technician?.UserId;
        if (userId.HasValue && !await dbContext.Users.AnyAsync(x => x.TenantId == TenantId && x.Id == userId && x.IsActive, cancellationToken))
            throw new BusinessRuleException("Select a valid user.");

        var duplicate = await dbContext.ProjectMembers.AnyAsync(x => x.TenantId == TenantId && x.ProjectId == id
            && ((userId.HasValue && x.UserId == userId) || (technician != null && x.TechnicianId == technician.Id)), cancellationToken);
        if (duplicate)
            throw new BusinessRuleException("This person is already on the project team.");

        var member = new ProjectMember
        {
            TenantId = TenantId,
            ProjectId = id,
            UserId = userId,
            TechnicianId = technician?.Id,
            Role = role,
            Trade = request.Trade?.Trim(),
            FromDate = request.FromDate,
            ToDate = request.ToDate,
        };
        dbContext.ProjectMembers.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (userId.HasValue)
            await projectNotificationService.MemberAddedAsync(TenantId, project, userId.Value, role, cancellationToken);
        return Ok((await LoadMembersAsync(id, cancellationToken)).Single(x => x.Id == member.Id));
    }

    [HttpPut("{id:guid}/members/{memberId:guid}")]
    public async Task<ActionResult<ProjectMemberResponse>> UpdateMember(Guid id, Guid memberId, [FromBody] UpsertProjectMemberRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        await LoadEditableAsync(id, cancellationToken);
        var member = await dbContext.ProjectMembers.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == id && x.Id == memberId, cancellationToken)
            ?? throw new NotFoundException("Team member not found.");
        member.Role = NormalizeMemberRole(request.Role);
        member.Trade = request.Trade?.Trim();
        member.FromDate = request.FromDate;
        member.ToDate = request.ToDate;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok((await LoadMembersAsync(id, cancellationToken)).Single(x => x.Id == member.Id));
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        var member = await dbContext.ProjectMembers.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == id && x.Id == memberId, cancellationToken)
            ?? throw new NotFoundException("Team member not found.");
        if (member.UserId.HasValue && member.UserId == project.ProjectManagerUserId)
            throw new BusinessRuleException("Change the project manager before removing them from the team.");
        dbContext.ProjectMembers.Remove(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Consultants on the project ────────────────────────────────────────────

    [HttpGet("{id:guid}/consultants")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectConsultantResponse>>> ListConsultants(Guid id, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        return Ok(await LoadConsultantsAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/consultants")]
    public async Task<ActionResult<ProjectConsultantResponse>> LinkConsultant(Guid id, [FromBody] LinkProjectConsultantRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        await LoadEditableAsync(id, cancellationToken);
        if (!await dbContext.Consultants.AnyAsync(x => x.TenantId == TenantId && x.Id == request.ConsultantId && x.IsActive, cancellationToken))
            throw new BusinessRuleException("Select an active consultant from the directory.");
        if (await dbContext.ProjectConsultants.AnyAsync(x => x.TenantId == TenantId && x.ProjectId == id && x.ConsultantId == request.ConsultantId, cancellationToken))
            throw new BusinessRuleException("This consultant is already on the project.");

        var link = new ProjectConsultant { TenantId = TenantId, ProjectId = id, ConsultantId = request.ConsultantId, RoleOnProject = request.RoleOnProject?.Trim() };
        dbContext.ProjectConsultants.Add(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok((await LoadConsultantsAsync(id, cancellationToken)).Single(x => x.Id == link.Id));
    }

    [HttpDelete("{id:guid}/consultants/{projectConsultantId:guid}")]
    public async Task<IActionResult> UnlinkConsultant(Guid id, Guid projectConsultantId, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        await LoadEditableAsync(id, cancellationToken);
        var link = await dbContext.ProjectConsultants.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == id && x.Id == projectConsultantId, cancellationToken)
            ?? throw new NotFoundException("Consultant link not found.");
        dbContext.ProjectConsultants.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Milestones ─────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/milestones")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectMilestoneResponse>>> ListMilestones(Guid id, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        return Ok(await LoadMilestonesAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/milestones")]
    public async Task<ActionResult<ProjectMilestoneResponse>> CreateMilestone(Guid id, [FromBody] UpsertProjectMilestoneRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        ValidateMilestone(request);
        var nextOrder = (await dbContext.ProjectMilestones.Where(x => x.TenantId == TenantId && x.ProjectId == id).MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0) + 10;
        var milestone = new ProjectMilestone
        {
            TenantId = TenantId,
            ProjectId = id,
            Name = request.Name.Trim(),
            Weight = request.Weight ?? 1,
            PlannedDate = request.PlannedDate,
            ForecastDate = request.ForecastDate,
            SortOrder = request.SortOrder ?? nextOrder,
        };
        dbContext.ProjectMilestones.Add(milestone);
        await dbContext.SaveChangesAsync(cancellationToken);
        await projectService.RecalculateProgressAsync(TenantId, project, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapMilestone(milestone));
    }

    [HttpPut("{id:guid}/milestones/{milestoneId:guid}")]
    public async Task<ActionResult<ProjectMilestoneResponse>> UpdateMilestone(Guid id, Guid milestoneId, [FromBody] UpsertProjectMilestoneRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        var milestone = await LoadMilestoneAsync(id, milestoneId, cancellationToken);
        ValidateMilestone(request);
        milestone.Name = request.Name.Trim();
        milestone.Weight = request.Weight ?? milestone.Weight;
        milestone.PlannedDate = request.PlannedDate;
        milestone.ForecastDate = request.ForecastDate;
        milestone.SortOrder = request.SortOrder ?? milestone.SortOrder;
        await projectService.RecalculateProgressAsync(TenantId, project, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapMilestone(milestone));
    }

    [HttpPost("{id:guid}/milestones/{milestoneId:guid}/status")]
    public async Task<ActionResult<ProjectMilestoneResponse>> SetMilestoneStatus(Guid id, Guid milestoneId, [FromBody] SetMilestoneStatusRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        var milestone = await LoadMilestoneAsync(id, milestoneId, cancellationToken);
        var status = ProjectMilestoneStatuses.All.FirstOrDefault(x => string.Equals(x, request.Status?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Status must be NotStarted, InProgress, Completed or Verified.");

        switch (status)
        {
            case ProjectMilestoneStatuses.NotStarted or ProjectMilestoneStatuses.InProgress:
                milestone.CompletedAt = null;
                milestone.CompletedByUserId = null;
                milestone.VerifiedAt = null;
                milestone.VerifiedByUserId = null;
                break;
            case ProjectMilestoneStatuses.Completed:
                milestone.CompletedAt ??= DateTime.UtcNow;
                milestone.CompletedByUserId ??= UserId;
                milestone.VerifiedAt = null;
                milestone.VerifiedByUserId = null;
                break;
            case ProjectMilestoneStatuses.Verified:
                milestone.CompletedAt ??= DateTime.UtcNow;
                milestone.CompletedByUserId ??= UserId;
                milestone.VerifiedAt = DateTime.UtcNow;
                milestone.VerifiedByUserId = UserId;
                break;
        }

        milestone.Status = status;
        await projectService.RecalculateProgressAsync(TenantId, project, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.milestone.status", nameof(ProjectMilestone), milestone.Id.ToString(),
            $"Milestone '{milestone.Name}' on {project.ProjectNumber} set to {status}. Progress now {project.ProgressPercent:N2}%.", cancellationToken);
        return Ok(MapMilestone(milestone));
    }

    [HttpDelete("{id:guid}/milestones/{milestoneId:guid}")]
    public async Task<IActionResult> DeleteMilestone(Guid id, Guid milestoneId, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        var milestone = await LoadMilestoneAsync(id, milestoneId, cancellationToken);
        var tasks = await dbContext.ProjectTasks.Where(x => x.TenantId == TenantId && x.MilestoneId == milestoneId).ToListAsync(cancellationToken);
        foreach (var task in tasks)
            task.MilestoneId = null;
        dbContext.ProjectMilestones.Remove(milestone);
        await dbContext.SaveChangesAsync(cancellationToken);
        await projectService.RecalculateProgressAsync(TenantId, project, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Tasks ──────────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/tasks")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectTaskResponse>>> ListTasks(Guid id, [FromQuery] Guid? milestoneId, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        var tasks = await LoadTasksAsync(id, cancellationToken);
        return Ok(tasks
            .Where(x => !milestoneId.HasValue || x.MilestoneId == milestoneId)
            .Where(x => string.IsNullOrWhiteSpace(status) || string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase))
            .ToList());
    }

    [HttpPost("{id:guid}/tasks")]
    public async Task<ActionResult<ProjectTaskResponse>> CreateTask(Guid id, [FromBody] UpsertProjectTaskRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        await ValidateTaskAsync(id, request, cancellationToken);
        var nextOrder = (await dbContext.ProjectTasks.Where(x => x.TenantId == TenantId && x.ProjectId == id).MaxAsync(x => (int?)x.SortOrder, cancellationToken) ?? 0) + 10;
        var task = new ProjectTask
        {
            TenantId = TenantId,
            ProjectId = id,
            MilestoneId = request.MilestoneId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            AssigneeUserId = request.AssigneeUserId,
            DueDate = request.DueDate,
            Priority = NormalizePriority(request.Priority),
            SortOrder = nextOrder,
        };
        ApplyTaskStatus(task, request.Status);
        dbContext.ProjectTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (task.AssigneeUserId.HasValue && task.AssigneeUserId != UserId)
            await projectNotificationService.TaskAssignedAsync(TenantId, project, task, cancellationToken);
        return Ok((await LoadTasksAsync(id, cancellationToken)).Single(x => x.Id == task.Id));
    }

    [HttpPut("{id:guid}/tasks/{taskId:guid}")]
    public async Task<ActionResult<ProjectTaskResponse>> UpdateTask(Guid id, Guid taskId, [FromBody] UpsertProjectTaskRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        var task = await dbContext.ProjectTasks.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == id && x.Id == taskId, cancellationToken)
            ?? throw new NotFoundException("Task not found.");
        await ValidateTaskAsync(id, request, cancellationToken);
        var previousAssignee = task.AssigneeUserId;
        task.MilestoneId = request.MilestoneId;
        task.Title = request.Title.Trim();
        task.Description = request.Description?.Trim();
        task.AssigneeUserId = request.AssigneeUserId;
        task.DueDate = request.DueDate;
        task.Priority = NormalizePriority(request.Priority);
        ApplyTaskStatus(task, request.Status);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (task.AssigneeUserId.HasValue && task.AssigneeUserId != previousAssignee && task.AssigneeUserId != UserId)
            await projectNotificationService.TaskAssignedAsync(TenantId, project, task, cancellationToken);
        return Ok((await LoadTasksAsync(id, cancellationToken)).Single(x => x.Id == task.Id));
    }

    [HttpDelete("{id:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id, Guid taskId, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        await LoadEditableAsync(id, cancellationToken);
        var task = await dbContext.ProjectTasks.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == id && x.Id == taskId, cancellationToken)
            ?? throw new NotFoundException("Task not found.");
        dbContext.ProjectTasks.Remove(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ── Variations ─────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/variations")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectVariationResponse>>> ListVariations(Guid id, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        var variations = await dbContext.ProjectVariations
            .Where(x => x.TenantId == TenantId && x.ProjectId == id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(variations.Select(MapVariation).ToList());
    }

    [HttpPost("{id:guid}/variations")]
    public async Task<ActionResult<ProjectVariationResponse>> CreateVariation(Guid id, [FromBody] UpsertProjectVariationRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.EnsureAny(PermissionNames.ProjectsVariationsSubmit, PermissionNames.ProjectsManage);
        var project = await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        var variation = await projectService.CreateVariationAsync(TenantId, UserId, project,
            new CreateVariationCommand(request.Kind, request.Amount, request.Title, request.Description, request.ClientReference), cancellationToken);
        return Ok(MapVariation(variation));
    }

    [HttpPut("{id:guid}/variations/{variationId:guid}")]
    public async Task<ActionResult<ProjectVariationResponse>> UpdateVariation(Guid id, Guid variationId, [FromBody] UpsertProjectVariationRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.EnsureAny(PermissionNames.ProjectsVariationsSubmit, PermissionNames.ProjectsManage);
        await LoadEditableAsync(id, cancellationToken);
        var variation = await LoadVariationAsync(id, variationId, cancellationToken);
        if (variation.Status != ProjectVariationStatuses.Draft)
            throw new BusinessRuleException("Only draft variations can be edited. Void an approved variation and raise a new one.");
        if (request.Amount <= 0)
            throw new BusinessRuleException("Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new BusinessRuleException("Variation title is required.");
        variation.Kind = ProjectVariationKinds.All.FirstOrDefault(x => string.Equals(x, request.Kind?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Variation kind must be Addition or Omission.");
        variation.Amount = request.Amount;
        variation.Title = request.Title.Trim();
        variation.Description = request.Description?.Trim();
        variation.ClientReference = request.ClientReference?.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapVariation(variation));
    }

    [HttpPost("{id:guid}/variations/{variationId:guid}/submit")]
    public async Task<ActionResult<ProjectVariationResponse>> SubmitVariation(Guid id, Guid variationId, CancellationToken cancellationToken)
    {
        projectAccessService.EnsureAny(PermissionNames.ProjectsVariationsSubmit, PermissionNames.ProjectsManage);
        var project = await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        var variation = await LoadVariationAsync(id, variationId, cancellationToken);
        await projectService.SubmitVariationAsync(TenantId, UserId, project, variation, cancellationToken);
        await projectNotificationService.VariationSubmittedAsync(TenantId, project, variation, cancellationToken);
        return Ok(MapVariation(variation));
    }

    [HttpPost("{id:guid}/variations/{variationId:guid}/decision")]
    public async Task<ActionResult<ProjectVariationResponse>> DecideVariation(Guid id, Guid variationId, [FromBody] VariationDecisionRequest request, CancellationToken cancellationToken)
    {
        var project = await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        var variation = await LoadVariationAsync(id, variationId, cancellationToken);
        await projectService.DecideVariationAsync(TenantId, UserId, project, variation, request.Approve, request.Note, cancellationToken);
        var refreshed = await dbContext.ProjectVariations.AsNoTracking().SingleAsync(x => x.Id == variationId, cancellationToken);
        if (refreshed.Status is ProjectVariationStatuses.Approved or ProjectVariationStatuses.Rejected)
            await projectNotificationService.VariationDecidedAsync(TenantId, project, refreshed, cancellationToken);
        return Ok(MapVariation(refreshed));
    }

    [HttpPost("{id:guid}/variations/{variationId:guid}/void")]
    public async Task<ActionResult<ProjectVariationResponse>> VoidVariation(Guid id, Guid variationId, [FromBody] VoidVariationRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.EnsureAny(PermissionNames.ProjectsVariationsSubmit, PermissionNames.ProjectsManage, PermissionNames.ProjectsVariationsApprove);
        var project = await LoadEditableAsync(id, cancellationToken);
        var variation = await LoadVariationAsync(id, variationId, cancellationToken);
        await projectService.VoidVariationAsync(TenantId, UserId, project, variation, request.Reason, cancellationToken);
        return Ok(MapVariation(variation));
    }

    // ── Linked work orders and material issues ────────────────────────────────

    [HttpGet("{id:guid}/work-orders")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectWorkOrderResponse>>> ListWorkOrders(Guid id, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        var workOrders = await dbContext.WorkOrders
            .Where(x => x.TenantId == TenantId && x.ProjectId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProjectWorkOrderResponse(x.Id, x.WorkOrderNumber, x.Title, x.Status, x.Priority, x.DueDate,
                x.AssignedTechnician != null ? x.AssignedTechnician.FullName : null))
            .ToListAsync(cancellationToken);
        return Ok(workOrders);
    }

    [HttpPost("{id:guid}/work-orders")]
    public async Task<IActionResult> LinkWorkOrder(Guid id, [FromBody] LinkRecordRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.RecordId, cancellationToken)
            ?? throw new NotFoundException("Work order not found.");
        if (workOrder.ClientId != project.ClientId)
            throw new BusinessRuleException("The work order belongs to a different client.");
        if (workOrder.ProjectId.HasValue && workOrder.ProjectId != id)
            throw new BusinessRuleException("The work order is already linked to another project.");
        workOrder.ProjectId = id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.work-order.linked", nameof(Project), id.ToString(),
            $"Work order {workOrder.WorkOrderNumber} linked to {project.ProjectNumber}.", cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/work-orders/{workOrderId:guid}")]
    public async Task<IActionResult> UnlinkWorkOrder(Guid id, Guid workOrderId, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == workOrderId && x.ProjectId == id, cancellationToken)
            ?? throw new NotFoundException("Work order not found on this project.");
        workOrder.ProjectId = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.work-order.unlinked", nameof(Project), id.ToString(),
            $"Work order {workOrder.WorkOrderNumber} unlinked from {project.ProjectNumber}.", cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/material-issues")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectMaterialIssueResponse>>> ListMaterialIssues(Guid id, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        var issues = await dbContext.MaterialIssues
            .Where(x => x.TenantId == TenantId && x.ProjectId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProjectMaterialIssueResponse(x.Id, x.IssueNumber, x.IssueStatus, x.IssuedAt, x.TotalIssuedCost, x.Lines.Count))
            .ToListAsync(cancellationToken);
        return Ok(issues);
    }

    [HttpPost("{id:guid}/material-issues")]
    public async Task<IActionResult> LinkMaterialIssue(Guid id, [FromBody] LinkRecordRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        var issue = await dbContext.MaterialIssues.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.RecordId, cancellationToken)
            ?? throw new NotFoundException("Material issue not found.");
        if (issue.ClientId.HasValue && issue.ClientId != project.ClientId)
            throw new BusinessRuleException("The material issue belongs to a different client.");
        if (issue.ProjectId.HasValue && issue.ProjectId != id)
            throw new BusinessRuleException("The material issue is already linked to another project.");
        issue.ProjectId = id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.material-issue.linked", nameof(Project), id.ToString(),
            $"Material issue {issue.IssueNumber} linked to {project.ProjectNumber}.", cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/material-issues/{materialIssueId:guid}")]
    public async Task<IActionResult> UnlinkMaterialIssue(Guid id, Guid materialIssueId, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await LoadEditableAsync(id, cancellationToken);
        var issue = await dbContext.MaterialIssues.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == materialIssueId && x.ProjectId == id, cancellationToken)
            ?? throw new NotFoundException("Material issue not found on this project.");
        issue.ProjectId = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.material-issue.unlinked", nameof(Project), id.ToString(),
            $"Material issue {issue.IssueNumber} unlinked from {project.ProjectNumber}.", cancellationToken);
        return NoContent();
    }

    // ── Documents (uploaded through /api/attachments with entity type "Project") ─

    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectDocumentResponse>>> ListDocuments(Guid id, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        var documents = await dbContext.Attachments
            .Where(x => x.TenantId == TenantId && x.EntityType == "Project" && x.EntityId == id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProjectDocumentResponse(x.Id, x.FileName, x.FileSize, x.MimeType, $"/api/attachments/{x.Id}/download",
                x.SharedWithClient, x.UploadedByUser != null ? x.UploadedByUser.FullName : null, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return Ok(documents);
    }

    [HttpPut("{id:guid}/documents/{attachmentId:guid}/sharing")]
    public async Task<IActionResult> SetDocumentSharing(Guid id, Guid attachmentId, [FromBody] DocumentSharingRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var project = await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        var attachment = await dbContext.Attachments.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == attachmentId && x.EntityType == "Project" && x.EntityId == id, cancellationToken)
            ?? throw new NotFoundException("Document not found on this project.");
        attachment.SharedWithClient = request.SharedWithClient;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.document.sharing", nameof(Attachment), attachment.Id.ToString(),
            $"Document '{attachment.FileName}' on {project.ProjectNumber} {(request.SharedWithClient ? "shared with" : "hidden from")} the client.", cancellationToken);
        return NoContent();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<List<Project>> LoadRegisterAsync(CancellationToken cancellationToken)
    {
        var query = await projectAccessService.VisibleProjectsAsync(TenantId, cancellationToken);
        return await query
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.ProjectType)
            .Include(x => x.Stage)
            .Include(x => x.ProjectManagerUser)
            .Include(x => x.Milestones)
            .AsSplitQuery()
            .OrderBy(x => x.Stage!.SortOrder)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Loads a project the user can see and refuses changes once it is closed or cancelled.</summary>
    private async Task<Project> LoadEditableAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await projectAccessService.LoadVisibleAsync(TenantId, id, cancellationToken);
        var category = await dbContext.ProjectStages.Where(x => x.Id == project.StageId).Select(x => x.Category).SingleAsync(cancellationToken);
        if (category is ProjectStageCategories.Closed or ProjectStageCategories.Cancelled)
            throw new BusinessRuleException($"Project {project.ProjectNumber} is {category.ToLowerInvariant()}. Reopen it to make changes.");
        return project;
    }

    private async Task<ProjectDetailResponse> BuildDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .Include(x => x.Client)
            .Include(x => x.Site)
            .Include(x => x.ProjectType)
            .Include(x => x.Stage)
            .Include(x => x.ProjectManagerUser)
            .Include(x => x.Branch)
            .Include(x => x.Milestones)
            .AsSplitQuery()
            .AsNoTracking()
            .SingleAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);

        var pending = await dbContext.ProjectVariations
            .Where(x => x.TenantId == TenantId && x.ProjectId == id && x.Status == ProjectVariationStatuses.Submitted)
            .Select(x => new { x.Kind, x.Amount })
            .ToListAsync(cancellationToken);
        var openTasks = await dbContext.ProjectTasks.CountAsync(x => x.TenantId == TenantId && x.ProjectId == id
            && x.Status != ProjectTaskStatuses.Done && x.Status != ProjectTaskStatuses.Cancelled, cancellationToken);
        var workOrders = await dbContext.WorkOrders.CountAsync(x => x.TenantId == TenantId && x.ProjectId == id, cancellationToken);
        var materialIssues = await dbContext.MaterialIssues.CountAsync(x => x.TenantId == TenantId && x.ProjectId == id, cancellationToken);
        var settings = await projectSettingsService.GetAsync(TenantId, cancellationToken);
        var planned = ProjectCommercials.ComputePlannedProgress(project.Milestones, Today);

        return new ProjectDetailResponse(
            MapListItem(project),
            project.Description,
            project.BranchId,
            project.Branch?.Name,
            project.ApprovedAdditions,
            project.ApprovedOmissions,
            pending.Count,
            pending.Sum(x => x.Kind == ProjectVariationKinds.Omission ? -x.Amount : x.Amount),
            project.ProgressPercent,
            project.ProgressOverridePercent,
            project.ProgressOverrideReason,
            planned,
            project.ActualStartDate,
            project.ActualEndDate,
            project.HandedOverAt,
            project.ClosedAt,
            project.ClosureNotes,
            project.VisibleInClientPortal,
            openTasks,
            workOrders,
            materialIssues,
            await LoadMembersAsync(id, cancellationToken),
            await LoadConsultantsAsync(id, cancellationToken),
            new ProjectFeatureFlagsResponse(settings.Milestones, settings.Tasks, settings.Variations, settings.Materials, settings.Documents,
                settings.AllowManualProgressOverride),
            new ProjectAbilitiesResponse(
                CanManage: projectAccessService.Can(PermissionNames.ProjectsManage),
                CanSubmitVariations: projectAccessService.Can(PermissionNames.ProjectsVariationsSubmit) || projectAccessService.Can(PermissionNames.ProjectsManage),
                CanApproveVariations: projectAccessService.Can(PermissionNames.ProjectsVariationsApprove),
                CanClose: projectAccessService.Can(PermissionNames.ProjectsClose)));
    }

    private static ProjectListItemResponse MapListItem(Project x)
    {
        var planned = ProjectCommercials.ComputePlannedProgress(x.Milestones, Today);
        var open = x.Stage is not null && ProjectStageCategories.Open.Contains(x.Stage.Category);
        return new ProjectListItemResponse(
            x.Id,
            x.ProjectNumber,
            x.Name,
            x.ClientId,
            x.Client?.ClientName ?? "Unknown client",
            x.SiteId,
            x.Site?.SiteName,
            x.ProjectTypeId,
            x.ProjectType?.Name ?? "",
            x.StageId,
            x.Stage?.Name ?? "",
            x.Stage?.Category ?? "",
            x.ProjectManagerUserId,
            x.ProjectManagerUser?.FullName,
            x.Currency,
            x.OriginalValue,
            x.RevisedValue,
            x.EffectiveProgressPercent,
            x.ProgressOverridePercent.HasValue,
            planned,
            open && x.Stage!.Category != ProjectStageCategories.OnHold && ProjectCommercials.IsBehindSchedule(x.EffectiveProgressPercent, planned),
            x.PlannedStartDate,
            x.PlannedEndDate,
            open && x.PlannedEndDate.HasValue ? x.PlannedEndDate.Value.DayNumber - Today.DayNumber : null);
    }

    private async Task<IReadOnlyList<ProjectMemberResponse>> LoadMembersAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMembers
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId)
            .OrderBy(x => x.Role == ProjectMemberRoles.ProjectManager ? 0 : x.Role == ProjectMemberRoles.Supervisor ? 1 : 2)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new ProjectMemberResponse(
                x.Id, x.UserId, x.TechnicianId,
                x.User != null ? x.User.FullName : x.Technician != null ? x.Technician.FullName : "Unknown",
                x.User != null ? x.User.Email : x.Technician != null ? x.Technician.Email : null,
                x.Role, x.Trade, x.FromDate, x.ToDate))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<ProjectConsultantResponse>> LoadConsultantsAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectConsultants
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ProjectConsultantResponse(
                x.Id, x.ConsultantId, x.Consultant!.FirmName, x.Consultant.Discipline, x.RoleOnProject,
                x.Consultant.ContactPerson, x.Consultant.Phone, x.Consultant.Email))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<ProjectMilestoneResponse>> LoadMilestonesAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await dbContext.ProjectMilestones
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken))
        .Select(MapMilestone)
        .ToList();

    private async Task<IReadOnlyList<ProjectTaskResponse>> LoadTasksAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectTasks
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId)
            .OrderBy(x => x.Status == ProjectTaskStatuses.Done || x.Status == ProjectTaskStatuses.Cancelled ? 1 : 0)
            .ThenBy(x => x.DueDate == null)
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.SortOrder)
            .Select(x => new ProjectTaskResponse(
                x.Id, x.MilestoneId, x.Milestone != null ? x.Milestone.Name : null, x.Title, x.Description,
                x.AssigneeUserId, x.AssigneeUser != null ? x.AssigneeUser.FullName : null,
                x.DueDate, x.Priority, x.Status, x.CompletedAt))
            .ToListAsync(cancellationToken);

    private async Task<ProjectMilestone> LoadMilestoneAsync(Guid projectId, Guid milestoneId, CancellationToken cancellationToken) =>
        await dbContext.ProjectMilestones.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id == milestoneId, cancellationToken)
        ?? throw new NotFoundException("Milestone not found.");

    private async Task<ProjectVariation> LoadVariationAsync(Guid projectId, Guid variationId, CancellationToken cancellationToken) =>
        await dbContext.ProjectVariations.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id == variationId, cancellationToken)
        ?? throw new NotFoundException("Variation not found.");

    private static void ValidateMilestone(UpsertProjectMilestoneRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Milestone name is required.");
        if (request.Weight is < 0)
            throw new BusinessRuleException("Milestone weight cannot be negative.");
    }

    private async Task ValidateTaskAsync(Guid projectId, UpsertProjectTaskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new BusinessRuleException("Task title is required.");
        if (request.MilestoneId.HasValue && !await dbContext.ProjectMilestones.AnyAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id == request.MilestoneId, cancellationToken))
            throw new BusinessRuleException("Select a milestone on this project.");
        if (request.AssigneeUserId.HasValue && !await dbContext.Users.AnyAsync(x => x.TenantId == TenantId && x.Id == request.AssigneeUserId && x.IsActive, cancellationToken))
            throw new BusinessRuleException("Select a valid assignee.");
    }

    private static void ApplyTaskStatus(ProjectTask task, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return;
        var status = ProjectTaskStatuses.All.FirstOrDefault(x => string.Equals(x, requested.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Task status must be Open, InProgress, Done or Cancelled.");
        if (status == ProjectTaskStatuses.Done && task.Status != ProjectTaskStatuses.Done)
            task.CompletedAt = DateTime.UtcNow;
        else if (status != ProjectTaskStatuses.Done)
            task.CompletedAt = null;
        task.Status = status;
    }

    private static string NormalizePriority(string? priority) =>
        new[] { "Low", "Medium", "High" }.FirstOrDefault(x => string.Equals(x, priority?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "Medium";

    private static string NormalizeMemberRole(string? role) =>
        ProjectMemberRoles.All.FirstOrDefault(x => string.Equals(x, role?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new BusinessRuleException("Role must be ProjectManager, Supervisor, Member or Viewer.");

    private static ProjectMilestoneResponse MapMilestone(ProjectMilestone x) => new(
        x.Id, x.Name, x.SortOrder, x.Weight, x.PlannedDate, x.ForecastDate, x.Status, x.CompletedAt, x.VerifiedAt,
        x.Status is not (ProjectMilestoneStatuses.Completed or ProjectMilestoneStatuses.Verified) && x.PlannedDate.HasValue && x.PlannedDate.Value < Today);

    private static ProjectVariationResponse MapVariation(ProjectVariation x) => new(
        x.Id, x.VariationNumber, x.Kind, x.Amount, x.Title, x.Description, x.ClientReference, x.Status,
        x.SubmittedAt, x.DecidedAt, x.DecisionNote, x.CreatedAt);
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record ProjectListItemResponse(
    Guid Id, string ProjectNumber, string Name,
    Guid ClientId, string ClientName, Guid? SiteId, string? SiteName,
    Guid ProjectTypeId, string ProjectTypeName,
    Guid StageId, string StageName, string StageCategory,
    Guid? ProjectManagerUserId, string? ProjectManagerName,
    string Currency, decimal OriginalValue, decimal RevisedValue,
    decimal ProgressPercent, bool ProgressOverridden, decimal PlannedProgressPercent, bool IsBehindSchedule,
    DateOnly? PlannedStartDate, DateOnly? PlannedEndDate, int? DaysRemaining);

public sealed record ProjectRegisterSummaryResponse(
    int ActiveProjects, int OpenProjects, decimal TotalRevisedValue, int BehindSchedule,
    int PendingVariationsCount, decimal PendingVariationsValue);

public sealed record ProjectFeatureFlagsResponse(bool Milestones, bool Tasks, bool Variations, bool Materials, bool Documents, bool AllowManualProgressOverride);

public sealed record ProjectAbilitiesResponse(bool CanManage, bool CanSubmitVariations, bool CanApproveVariations, bool CanClose);

public sealed record ProjectDetailResponse(
    ProjectListItemResponse Summary,
    string? Description,
    Guid? BranchId, string? BranchName,
    decimal ApprovedAdditions, decimal ApprovedOmissions,
    int PendingVariationsCount, decimal PendingVariationsValue,
    decimal CalculatedProgressPercent, decimal? ProgressOverridePercent, string? ProgressOverrideReason,
    decimal PlannedProgressPercent,
    DateOnly? ActualStartDate, DateOnly? ActualEndDate, DateTime? HandedOverAt, DateTime? ClosedAt, string? ClosureNotes,
    bool VisibleInClientPortal,
    int OpenTasks, int WorkOrders, int MaterialIssues,
    IReadOnlyList<ProjectMemberResponse> Members,
    IReadOnlyList<ProjectConsultantResponse> Consultants,
    ProjectFeatureFlagsResponse Features,
    ProjectAbilitiesResponse Abilities);

public sealed record UpsertProjectRequest(
    string Name,
    Guid ClientId,
    Guid? SiteId,
    Guid? ProjectTypeId,
    Guid? StageId,
    Guid? ProjectManagerUserId,
    Guid? BranchId,
    decimal OriginalValue,
    string? Currency,
    DateOnly? PlannedStartDate,
    DateOnly? PlannedEndDate,
    string? Description,
    Guid? TemplateId = null,
    bool? VisibleInClientPortal = null);

public sealed record ChangeProjectStageRequest(Guid StageId, string? Note);

public sealed record ProgressOverrideRequest(decimal? Percent, string? Reason);

public sealed record ProjectMemberResponse(
    Guid Id, Guid? UserId, Guid? TechnicianId, string Name, string? Email, string Role, string? Trade, DateOnly? FromDate, DateOnly? ToDate);

public sealed record UpsertProjectMemberRequest(Guid? UserId, Guid? TechnicianId, string Role, string? Trade, DateOnly? FromDate, DateOnly? ToDate);

public sealed record ProjectConsultantResponse(
    Guid Id, Guid ConsultantId, string FirmName, string? Discipline, string? RoleOnProject,
    string? ContactPerson, string? Phone, string? Email);

public sealed record LinkProjectConsultantRequest(Guid ConsultantId, string? RoleOnProject);

public sealed record ProjectMilestoneResponse(
    Guid Id, string Name, int SortOrder, decimal Weight, DateOnly? PlannedDate, DateOnly? ForecastDate,
    string Status, DateTime? CompletedAt, DateTime? VerifiedAt, bool IsOverdue);

public sealed record UpsertProjectMilestoneRequest(string Name, decimal? Weight, DateOnly? PlannedDate, DateOnly? ForecastDate, int? SortOrder);

public sealed record SetMilestoneStatusRequest(string Status);

public sealed record ProjectTaskResponse(
    Guid Id, Guid? MilestoneId, string? MilestoneName, string Title, string? Description,
    Guid? AssigneeUserId, string? AssigneeName, DateOnly? DueDate, string Priority, string Status, DateTime? CompletedAt);

public sealed record UpsertProjectTaskRequest(
    string Title, string? Description, Guid? MilestoneId, Guid? AssigneeUserId, DateOnly? DueDate, string? Priority, string? Status);

public sealed record ProjectVariationResponse(
    Guid Id, string VariationNumber, string Kind, decimal Amount, string Title, string? Description, string? ClientReference,
    string Status, DateTime? SubmittedAt, DateTime? DecidedAt, string? DecisionNote, DateTime CreatedAt);

public sealed record UpsertProjectVariationRequest(string Kind, decimal Amount, string Title, string? Description, string? ClientReference);

public sealed record VariationDecisionRequest(bool Approve, string? Note);

public sealed record VoidVariationRequest(string? Reason);

public sealed record LinkRecordRequest(Guid RecordId);

public sealed record ProjectWorkOrderResponse(Guid Id, string WorkOrderNumber, string Title, string Status, string Priority, DateTime? DueDate, string? AssignedTechnicianName);

public sealed record ProjectMaterialIssueResponse(Guid Id, string IssueNumber, string Status, DateTime? IssuedAt, decimal TotalIssuedCost, int LineCount);

public sealed record ProjectDocumentResponse(
    Guid Id, string FileName, long FileSize, string MimeType, string DownloadUrl, bool SharedWithClient, string? UploadedByName, DateTime CreatedAt);

public sealed record DocumentSharingRequest(bool SharedWithClient);
