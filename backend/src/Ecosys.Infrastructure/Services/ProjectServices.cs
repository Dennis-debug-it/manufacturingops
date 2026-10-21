using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public static class ProjectVisibility
{
    public const string AllProjects = "AllProjects";
    public const string MembersOnly = "MembersOnly";
}

/// <summary>Tenant settings for the Projects module, stored as a <see cref="TenantModuleSetting"/> JSON blob.</summary>
public sealed record ProjectSettingsModel
{
    public bool Milestones { get; init; } = true;
    public bool Tasks { get; init; } = true;
    public bool Variations { get; init; } = true;
    public bool Materials { get; init; } = true;
    public bool Documents { get; init; } = true;
    public string Visibility { get; init; } = ProjectVisibility.AllProjects;
    public bool ClientPortal { get; init; } = true;
    /// <summary>When true, a milestone counts towards progress only once it is Verified.</summary>
    public bool ProgressRequiresVerification { get; init; }
    public bool AllowManualProgressOverride { get; init; } = true;
    public IReadOnlyList<string> Trades { get; init; } = [];
    public bool MusterRoll { get; init; } = true;
    public bool SiteDiary { get; init; } = true;
    public bool Valuations { get; init; } = true;
    public bool Financials { get; init; } = true;
    /// <summary>Days after the work date that the supervisor can still correct marks (0 = same day only).</summary>
    public int MusterLockDays { get; init; } = 1;
    /// <summary>Hour of the working day (tenant time) after which a missing muster roll is flagged.</summary>
    public int MusterReminderHour { get; init; } = 11;
    /// <summary>How many days ahead milestone, task and end-date reminders are sent.</summary>
    public int ReminderLeadDays { get; init; } = 3;
    /// <summary>
    /// Closing is blocked while more than this share of the revised value is still uninvoiced.
    /// Null turns the check off, for tenants that bill projects outside Ecosys.
    /// </summary>
    public decimal? ClosureUnbilledTolerancePercent { get; init; }
}

public interface IProjectSettingsService
{
    Task<ProjectSettingsModel> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ProjectSettingsModel> SaveAsync(Guid tenantId, ProjectSettingsModel settings, CancellationToken cancellationToken = default);
}

internal sealed class ProjectSettingsService(AppDbContext dbContext) : IProjectSettingsService
{
    public const string ModuleKey = "projects";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProjectSettingsModel> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.TenantModuleSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ModuleKey == ModuleKey, cancellationToken);
        if (row is null || string.IsNullOrWhiteSpace(row.ValueJson))
            return new ProjectSettingsModel();
        try
        {
            return JsonSerializer.Deserialize<ProjectSettingsModel>(row.ValueJson, JsonOptions) ?? new ProjectSettingsModel();
        }
        catch (JsonException)
        {
            return new ProjectSettingsModel();
        }
    }

    public async Task<ProjectSettingsModel> SaveAsync(Guid tenantId, ProjectSettingsModel settings, CancellationToken cancellationToken = default)
    {
        if (settings.Visibility is not (ProjectVisibility.AllProjects or ProjectVisibility.MembersOnly))
            throw new BusinessRuleException("Visibility must be AllProjects or MembersOnly.");
        if (settings.MusterLockDays is < 0 or > 31)
            throw new BusinessRuleException("The muster lock must be between 0 and 31 days.");
        if (settings.MusterReminderHour is < 0 or > 23)
            throw new BusinessRuleException("The muster reminder hour must be between 0 and 23.");
        if (settings.ReminderLeadDays is < 0 or > 60)
            throw new BusinessRuleException("Reminder lead time must be between 0 and 60 days.");
        if (settings.ClosureUnbilledTolerancePercent is < 0 or > 100)
            throw new BusinessRuleException("The unbilled tolerance must be between 0 and 100%.");

        var normalized = settings with
        {
            Trades = settings.Trades
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };

        var row = await dbContext.TenantModuleSettings
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ModuleKey == ModuleKey, cancellationToken);
        if (row is null)
        {
            row = new TenantModuleSetting { TenantId = tenantId, ModuleKey = ModuleKey };
            dbContext.TenantModuleSettings.Add(row);
        }

        row.ValueJson = JsonSerializer.Serialize(normalized, JsonOptions);
        await dbContext.SaveChangesAsync(cancellationToken);
        return normalized;
    }
}

public interface IProjectSetupService
{
    /// <summary>Seeds a default project type and one stage per category the first time a tenant uses Projects.</summary>
    Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

internal sealed class ProjectSetupService(AppDbContext dbContext) : IProjectSetupService
{
    private static readonly (string Name, string Category)[] DefaultStages =
    [
        ("Awarded", ProjectStageCategories.Planning),
        ("In Progress", ProjectStageCategories.Active),
        ("On Hold", ProjectStageCategories.OnHold),
        ("Testing & Handover", ProjectStageCategories.Handover),
        ("Completed", ProjectStageCategories.Completed),
        ("Closed", ProjectStageCategories.Closed),
        ("Cancelled", ProjectStageCategories.Cancelled),
    ];

    public async Task EnsureDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var changed = false;
        if (!await dbContext.ProjectStages.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
        {
            for (var i = 0; i < DefaultStages.Length; i++)
            {
                dbContext.ProjectStages.Add(new ProjectStage
                {
                    TenantId = tenantId, Name = DefaultStages[i].Name, Category = DefaultStages[i].Category, SortOrder = (i + 1) * 10,
                });
            }
            changed = true;
        }

        if (!await dbContext.ProjectTypes.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
        {
            dbContext.ProjectTypes.Add(new ProjectType { TenantId = tenantId, Name = "General Project", SortOrder = 10 });
            changed = true;
        }

        if (!changed)
            return;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent first request seeded the same defaults; theirs win.
            foreach (var entry in dbContext.ChangeTracker.Entries().Where(x => x.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
        }
    }
}

/// <summary>Pure calculations shared by the API, the approval workflow and tests.</summary>
public static class ProjectCommercials
{
    /// <summary>
    /// Recomputes a project's approved addition and omission totals from its variation rows (never
    /// by incrementing), after a decision has been applied to a tracked variation. Saving is left to
    /// the caller so the totals commit in the same transaction as the decision. The project's
    /// <see cref="Project.ValueVersion"/> concurrency token makes a concurrent decision fail rather
    /// than overwrite these totals.
    /// </summary>
    public static async Task RecomputeApprovedTotalsAsync(AppDbContext dbContext, Guid tenantId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects.SingleAsync(x => x.TenantId == tenantId && x.Id == projectId, cancellationToken);
        // Tracked instances keep their unsaved status changes, so the decision just made is included.
        var variations = await dbContext.ProjectVariations
            .Where(x => x.TenantId == tenantId && x.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        ApplyApprovedTotals(project, variations);
    }

    public static void ApplyApprovedTotals(Project project, IEnumerable<ProjectVariation> variations)
    {
        var approved = variations.Where(x => x.Status == ProjectVariationStatuses.Approved).ToList();
        var additions = approved.Where(x => x.Kind == ProjectVariationKinds.Addition).Sum(x => x.Amount);
        var omissions = approved.Where(x => x.Kind == ProjectVariationKinds.Omission).Sum(x => x.Amount);

        if (project.OriginalValue + additions - omissions < 0)
            throw new BusinessRuleException("Approved omissions cannot take the project value below zero.");

        if (additions == project.ApprovedAdditions && omissions == project.ApprovedOmissions)
            return;

        project.ApprovedAdditions = additions;
        project.ApprovedOmissions = omissions;
        project.ValueVersion++;
    }

    /// <summary>Weighted-milestone progress: completed weight over total weight, 0–100.</summary>
    public static decimal ComputeProgress(IEnumerable<ProjectMilestone> milestones, bool requireVerification)
    {
        var list = milestones.ToList();
        var total = list.Sum(x => x.Weight);
        if (total <= 0)
            return 0;
        var done = list.Where(x => requireVerification
                ? x.Status == ProjectMilestoneStatuses.Verified
                : x.Status is ProjectMilestoneStatuses.Completed or ProjectMilestoneStatuses.Verified)
            .Sum(x => x.Weight);
        return Math.Round(done / total * 100m, 2);
    }

    /// <summary>Share of milestone weight whose planned date has passed: where the project should be by now.</summary>
    public static decimal ComputePlannedProgress(IEnumerable<ProjectMilestone> milestones, DateOnly today)
    {
        var list = milestones.ToList();
        var total = list.Sum(x => x.Weight);
        if (total <= 0)
            return 0;
        var due = list.Where(x => x.PlannedDate.HasValue && x.PlannedDate.Value <= today).Sum(x => x.Weight);
        return Math.Round(due / total * 100m, 2);
    }

    public static bool IsBehindSchedule(decimal actualProgress, decimal plannedProgress) => actualProgress < plannedProgress;
}

public interface IProjectAccessService
{
    bool Can(string permission);
    void Ensure(string permission);
    void EnsureAny(params string[] permissions);

    /// <summary>Projects the current user may see, honouring the tenant's members-only setting.</summary>
    Task<IQueryable<Project>> VisibleProjectsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Project> LoadVisibleAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Whether the caller is on the project team in one of <paramref name="roles"/> (directly or through their technician profile).</summary>
    Task<bool> HasProjectRoleAsync(Guid tenantId, Guid projectId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    /// <summary>
    /// Site work (muster roll, roster, diary) needs projects.manage, or projects.muster.mark together
    /// with a Supervisor or Project Manager place on this project's team. Permissions are tenant-wide,
    /// so the second check is what scopes a supervisor to their own sites.
    /// </summary>
    Task<Project> LoadForSiteWorkAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Site records can be read by anyone who can see the project, or who can run its site.</summary>
    Task<Project> LoadForSiteReadAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken = default);
}

internal sealed class ProjectAccessService(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IProjectSettingsService projectSettingsService) : IProjectAccessService
{
    public bool Can(string permission) => tenantContext.IsAdmin || tenantContext.HasPermission(permission);

    public void Ensure(string permission)
    {
        if (!Can(permission))
            throw new ForbiddenException("You do not have permission to perform this action.");
    }

    public void EnsureAny(params string[] permissions)
    {
        if (!permissions.Any(Can))
            throw new ForbiddenException("You do not have permission to perform this action.");
    }

    public async Task<IQueryable<Project>> VisibleProjectsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionNames.ProjectsView);
        var query = dbContext.Projects.Where(x => x.TenantId == tenantId);
        if (Can(PermissionNames.ProjectsManage))
            return query;

        var settings = await projectSettingsService.GetAsync(tenantId, cancellationToken);
        if (settings.Visibility != ProjectVisibility.MembersOnly)
            return query;

        var userId = tenantContext.GetRequiredUserId();
        return query.Where(x => x.ProjectManagerUserId == userId || x.Members.Any(m => m.UserId == userId));
    }

    public async Task<Project> LoadVisibleAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var query = await VisibleProjectsAsync(tenantId, cancellationToken);
        return await query.SingleOrDefaultAsync(x => x.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");
    }


    public async Task<bool> HasProjectRoleAsync(Guid tenantId, Guid projectId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        var userId = tenantContext.GetRequiredUserId();
        return await dbContext.ProjectMembers.AnyAsync(x => x.TenantId == tenantId && x.ProjectId == projectId && roles.Contains(x.Role)
            && (x.UserId == userId || (x.Technician != null && x.Technician.UserId == userId)), cancellationToken);
    }

    public async Task<Project> LoadForSiteWorkAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await dbContext.Projects.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project not found.");
        if (Can(PermissionNames.ProjectsManage))
            return project;
        if (Can(PermissionNames.ProjectsMusterMark) && await HasProjectRoleAsync(tenantId, projectId, ProjectMemberRoles.SiteRoles, cancellationToken))
            return project;
        // Don't reveal projects the caller has nothing to do with.
        if (!Can(PermissionNames.ProjectsView))
            throw new NotFoundException("Project not found.");
        throw new ForbiddenException("Only this project's supervisors and managers can record site work.");
    }

    public async Task<Project> LoadForSiteReadAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken = default)
    {
        if (Can(PermissionNames.ProjectsView))
            return await LoadVisibleAsync(tenantId, projectId, cancellationToken);
        return await LoadForSiteWorkAsync(tenantId, projectId, cancellationToken);
    }
}

public sealed record CreateProjectCommand(
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
    Guid? TemplateId,
    bool VisibleInClientPortal = true);

public sealed record ProjectTemplateItem(string Name, decimal Weight = 1, int? OffsetDays = null, IReadOnlyList<ProjectTemplateTaskItem>? Tasks = null);

public sealed record ProjectTemplateTaskItem(string Title, int? OffsetDays = null);

public sealed record CreateVariationCommand(string Kind, decimal Amount, string Title, string? Description, string? ClientReference);

public interface IProjectService
{
    Task<Project> CreateAsync(Guid tenantId, Guid userId, CreateProjectCommand command, CancellationToken cancellationToken = default);
    Task ChangeStageAsync(Guid tenantId, Guid userId, Project project, Guid stageId, string? note, CancellationToken cancellationToken = default);
    Task RecalculateProgressAsync(Guid tenantId, Project project, CancellationToken cancellationToken = default);

    Task<ProjectVariation> CreateVariationAsync(Guid tenantId, Guid userId, Project project, CreateVariationCommand command, CancellationToken cancellationToken = default);
    Task SubmitVariationAsync(Guid tenantId, Guid userId, Project project, ProjectVariation variation, CancellationToken cancellationToken = default);
    Task DecideVariationAsync(Guid tenantId, Guid userId, Project project, ProjectVariation variation, bool approve, string? note, CancellationToken cancellationToken = default);
    Task VoidVariationAsync(Guid tenantId, Guid userId, Project project, ProjectVariation variation, string? reason, CancellationToken cancellationToken = default);
}

internal sealed class ProjectService(
    AppDbContext dbContext,
    IDocumentNumberingService documentNumberingService,
    IApprovalWorkflowService approvalWorkflowService,
    IProjectSetupService projectSetupService,
    IProjectSettingsService projectSettingsService,
    IProjectAccessService projectAccessService,
    IAuditLogService auditLogService) : IProjectService
{
    private static readonly JsonSerializerOptions TemplateJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Project> CreateAsync(Guid tenantId, Guid userId, CreateProjectCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new BusinessRuleException("Project name is required.");
        if (command.OriginalValue < 0)
            throw new BusinessRuleException("Project value cannot be negative.");
        if (command.PlannedStartDate.HasValue && command.PlannedEndDate.HasValue && command.PlannedEndDate < command.PlannedStartDate)
            throw new BusinessRuleException("Planned end date cannot be before the planned start date.");

        await projectSetupService.EnsureDefaultsAsync(tenantId, cancellationToken);

        if (!await dbContext.Clients.AnyAsync(x => x.TenantId == tenantId && x.Id == command.ClientId, cancellationToken))
            throw new BusinessRuleException("Select a valid client.");
        if (command.SiteId.HasValue && !await dbContext.Sites.AnyAsync(x => x.TenantId == tenantId && x.Id == command.SiteId && x.ClientId == command.ClientId, cancellationToken))
            throw new BusinessRuleException("The site must belong to the selected client.");
        if (command.ProjectManagerUserId.HasValue && !await dbContext.Users.AnyAsync(x => x.TenantId == tenantId && x.Id == command.ProjectManagerUserId && x.IsActive, cancellationToken))
            throw new BusinessRuleException("Select a valid project manager.");
        if (command.BranchId.HasValue && !await dbContext.Branches.AnyAsync(x => x.TenantId == tenantId && x.Id == command.BranchId, cancellationToken))
            throw new BusinessRuleException("Select a valid branch.");

        var type = command.ProjectTypeId.HasValue
            ? await dbContext.ProjectTypes.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == command.ProjectTypeId && x.IsActive, cancellationToken)
                ?? throw new BusinessRuleException("Select a valid project type.")
            : await dbContext.ProjectTypes.Where(x => x.TenantId == tenantId && x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).FirstAsync(cancellationToken);

        var stage = command.StageId.HasValue
            ? await dbContext.ProjectStages.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == command.StageId && x.IsActive, cancellationToken)
                ?? throw new BusinessRuleException("Select a valid stage.")
            : await dbContext.ProjectStages.Where(x => x.TenantId == tenantId && x.IsActive && x.Category == ProjectStageCategories.Planning)
                .OrderBy(x => x.SortOrder).FirstOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleException("Configure a Planning stage before creating projects.");
        if (stage.Category is ProjectStageCategories.Closed or ProjectStageCategories.Cancelled)
            throw new BusinessRuleException("A new project cannot start closed or cancelled.");

        var templateId = command.TemplateId ?? type.DefaultTemplateId;
        var template = templateId.HasValue
            ? await dbContext.ProjectTemplates.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == templateId && x.IsActive, cancellationToken)
            : null;
        if (command.TemplateId.HasValue && template is null)
            throw new BusinessRuleException("Select a valid project template.");

        var project = new Project
        {
            TenantId = tenantId,
            BranchId = command.BranchId,
            ProjectNumber = await documentNumberingService.GenerateAsync(tenantId, command.BranchId, DocumentTypes.Project, cancellationToken),
            Name = command.Name.Trim(),
            Description = command.Description?.Trim(),
            ClientId = command.ClientId,
            SiteId = command.SiteId,
            ProjectTypeId = type.Id,
            StageId = stage.Id,
            ProjectManagerUserId = command.ProjectManagerUserId,
            OriginalValue = command.OriginalValue,
            Currency = string.IsNullOrWhiteSpace(command.Currency) ? "KES" : command.Currency.Trim().ToUpperInvariant(),
            PlannedStartDate = command.PlannedStartDate,
            PlannedEndDate = command.PlannedEndDate,
            VisibleInClientPortal = command.VisibleInClientPortal,
        };
        if (stage.Category == ProjectStageCategories.Active)
            project.ActualStartDate = DateOnly.FromDateTime(DateTime.UtcNow);
        dbContext.Projects.Add(project);

        if (command.ProjectManagerUserId.HasValue)
        {
            dbContext.ProjectMembers.Add(new ProjectMember
            {
                TenantId = tenantId, ProjectId = project.Id, UserId = command.ProjectManagerUserId, Role = ProjectMemberRoles.ProjectManager,
            });
        }

        if (template is not null)
            CopyTemplate(tenantId, project, template);

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(tenantId, userId, "projects.created", nameof(Project), project.Id.ToString(),
            $"Project {project.ProjectNumber} '{project.Name}' created ({project.Currency} {project.OriginalValue:N2}).", cancellationToken);
        return project;
    }

    private void CopyTemplate(Guid tenantId, Project project, ProjectTemplate template)
    {
        List<ProjectTemplateItem> items;
        try
        {
            items = JsonSerializer.Deserialize<List<ProjectTemplateItem>>(template.ItemsJson, TemplateJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            throw new BusinessRuleException($"Template '{template.Name}' is not valid. Fix it in Project settings.");
        }

        var order = 0;
        foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            order += 10;
            var milestone = new ProjectMilestone
            {
                TenantId = tenantId,
                ProjectId = project.Id,
                Name = item.Name.Trim(),
                Weight = item.Weight < 0 ? 0 : item.Weight,
                SortOrder = order,
                PlannedDate = Offset(project.PlannedStartDate, item.OffsetDays),
            };
            dbContext.ProjectMilestones.Add(milestone);

            var taskOrder = 0;
            foreach (var task in (item.Tasks ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Title)))
            {
                taskOrder += 10;
                dbContext.ProjectTasks.Add(new ProjectTask
                {
                    TenantId = tenantId,
                    ProjectId = project.Id,
                    MilestoneId = milestone.Id,
                    Title = task.Title.Trim(),
                    SortOrder = taskOrder,
                    DueDate = Offset(project.PlannedStartDate, task.OffsetDays),
                });
            }
        }
    }

    private static DateOnly? Offset(DateOnly? start, int? days) =>
        start.HasValue && days.HasValue ? start.Value.AddDays(days.Value) : null;

    public async Task ChangeStageAsync(Guid tenantId, Guid userId, Project project, Guid stageId, string? note, CancellationToken cancellationToken = default)
    {
        var current = await dbContext.ProjectStages.SingleAsync(x => x.TenantId == tenantId && x.Id == project.StageId, cancellationToken);
        var target = await dbContext.ProjectStages.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == stageId && x.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Select a valid stage.");
        if (target.Id == current.Id)
            return;

        var leavingFinal = current.Category is ProjectStageCategories.Closed or ProjectStageCategories.Cancelled;
        if (leavingFinal)
        {
            projectAccessService.Ensure(PermissionNames.ProjectsClose);
            if (string.IsNullOrWhiteSpace(note))
                throw new BusinessRuleException("Give a reason for reopening the project.");
        }

        if (target.Category is ProjectStageCategories.Closed or ProjectStageCategories.Cancelled)
            projectAccessService.Ensure(PermissionNames.ProjectsClose);

        if (target.Category == ProjectStageCategories.Closed)
            await EnsureCanCloseAsync(tenantId, project, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        switch (target.Category)
        {
            case ProjectStageCategories.Active:
                project.ActualStartDate ??= today;
                break;
            case ProjectStageCategories.Completed:
                project.ActualStartDate ??= today;
                project.ActualEndDate ??= today;
                project.HandedOverAt ??= DateTime.UtcNow;
                break;
            case ProjectStageCategories.Closed:
                project.ActualEndDate ??= today;
                project.ClosedAt = DateTime.UtcNow;
                project.ClosedByUserId = userId;
                project.ClosureNotes = note?.Trim();
                break;
        }

        if (leavingFinal && target.Category is not (ProjectStageCategories.Closed or ProjectStageCategories.Cancelled))
        {
            project.ClosedAt = null;
            project.ClosedByUserId = null;
        }

        project.StageId = target.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(tenantId, userId, leavingFinal ? "projects.reopened" : "projects.stage-changed", nameof(Project), project.Id.ToString(),
            $"Project {project.ProjectNumber} moved from '{current.Name}' to '{target.Name}'." + (string.IsNullOrWhiteSpace(note) ? "" : $" Note: {note.Trim()}"),
            cancellationToken);
    }

    private async Task EnsureCanCloseAsync(Guid tenantId, Project project, CancellationToken cancellationToken)
    {
        var blockers = new List<string>();
        if (await dbContext.ProjectVariations.AnyAsync(x => x.TenantId == tenantId && x.ProjectId == project.Id && x.Status == ProjectVariationStatuses.Submitted, cancellationToken))
            blockers.Add("variations are still awaiting a decision");
        // Draft, awaiting approval or approved-but-not-issued issues are still open; issued,
        // returned, rejected and cancelled ones are settled.
        if (await dbContext.MaterialIssues.AnyAsync(x => x.TenantId == tenantId && x.ProjectId == project.Id
                && (x.IssueStatus == "draft" || x.IssueStatus == "pending_approval" || x.IssueStatus == "approved"), cancellationToken))
            blockers.Add("material issues are still open");
        var valuationStatuses = await dbContext.ProjectValuations
            .Where(x => x.TenantId == tenantId && x.ProjectId == project.Id && x.Status != ProjectValuationStatuses.Void && x.Status != ProjectValuationStatuses.Invoiced)
            .Select(x => x.Status).ToListAsync(cancellationToken);
        if (valuationStatuses.Any(x => x is ProjectValuationStatuses.Draft or ProjectValuationStatuses.Submitted))
            blockers.Add("a valuation is still in progress");
        if (valuationStatuses.Contains(ProjectValuationStatuses.Certified))
            blockers.Add("a certified valuation hasn't been invoiced");

        var tolerance = (await projectSettingsService.GetAsync(tenantId, cancellationToken)).ClosureUnbilledTolerancePercent;
        if (tolerance.HasValue && project.RevisedValue > 0)
        {
            var invoiced = await dbContext.TenantInvoices
                .Where(x => x.TenantId == tenantId && x.ProjectId == project.Id && x.Status != "Void" && x.DocumentType == "Invoice")
                .SumAsync(x => (decimal?)(x.TotalAmount - x.TaxAmount), cancellationToken) ?? 0m;
            var unbilled = project.RevisedValue - invoiced;
            if (unbilled > project.RevisedValue * tolerance.Value / 100m)
                blockers.Add($"{project.Currency} {unbilled:N2} of the revised value is still uninvoiced");
        }

        if (blockers.Count > 0)
            throw new BusinessRuleException($"This project can't be closed yet: {string.Join("; ", blockers)}.");
    }

    public async Task RecalculateProgressAsync(Guid tenantId, Project project, CancellationToken cancellationToken = default)
    {
        var settings = await projectSettingsService.GetAsync(tenantId, cancellationToken);
        var milestones = await dbContext.ProjectMilestones.Where(x => x.TenantId == tenantId && x.ProjectId == project.Id).ToListAsync(cancellationToken);
        project.ProgressPercent = ProjectCommercials.ComputeProgress(milestones, settings.ProgressRequiresVerification);
    }

    public async Task<ProjectVariation> CreateVariationAsync(Guid tenantId, Guid userId, Project project, CreateVariationCommand command, CancellationToken cancellationToken = default)
    {
        EnsureOpen(project, await StageCategoryAsync(tenantId, project, cancellationToken));
        var kind = NormalizeKind(command.Kind);
        if (command.Amount <= 0)
            throw new BusinessRuleException("Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(command.Title))
            throw new BusinessRuleException("Variation title is required.");

        var variation = new ProjectVariation
        {
            TenantId = tenantId,
            ProjectId = project.Id,
            VariationNumber = await documentNumberingService.GenerateAsync(tenantId, project.BranchId, DocumentTypes.ProjectVariation, cancellationToken),
            Kind = kind,
            Amount = command.Amount,
            Title = command.Title.Trim(),
            Description = command.Description?.Trim(),
            ClientReference = command.ClientReference?.Trim(),
        };
        dbContext.ProjectVariations.Add(variation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(tenantId, userId, "projects.variation.created", nameof(ProjectVariation), variation.Id.ToString(),
            $"Variation {variation.VariationNumber} ({kind} {project.Currency} {variation.Amount:N2}) drafted on {project.ProjectNumber}.", cancellationToken);
        return variation;
    }

    public async Task SubmitVariationAsync(Guid tenantId, Guid userId, Project project, ProjectVariation variation, CancellationToken cancellationToken = default)
    {
        EnsureOpen(project, await StageCategoryAsync(tenantId, project, cancellationToken));
        if (variation.Status != ProjectVariationStatuses.Draft)
            throw new BusinessRuleException("Only draft variations can be submitted.");
        if (variation.Kind == ProjectVariationKinds.Omission && project.RevisedValue - variation.Amount < 0)
            throw new BusinessRuleException("This omission would take the project value below zero.");

        variation.Status = ProjectVariationStatuses.Submitted;
        variation.SubmittedAt = DateTime.UtcNow;
        variation.SubmittedByUserId = userId;
        await dbContext.SaveChangesAsync(cancellationToken);

        // An approval matrix for ProjectVariation, when configured, takes over the decision.
        // Without one, a holder of projects.variations.approve decides directly.
        await approvalWorkflowService.StartAsync(tenantId, ApprovalDocumentTypes.ProjectVariation, variation.Id, variation.VariationNumber,
            variation.Amount, project.Currency, userId, cancellationToken);
        await auditLogService.LogAsync(tenantId, userId, "projects.variation.submitted", nameof(ProjectVariation), variation.Id.ToString(),
            $"Variation {variation.VariationNumber} submitted on {project.ProjectNumber}.", cancellationToken);
    }

    public async Task DecideVariationAsync(Guid tenantId, Guid userId, Project project, ProjectVariation variation, bool approve, string? note, CancellationToken cancellationToken = default)
    {
        if (variation.Status != ProjectVariationStatuses.Submitted)
            throw new BusinessRuleException("Only submitted variations can be approved or rejected.");

        var activeRequest = await approvalWorkflowService.GetActiveForDocumentAsync(tenantId, ApprovalDocumentTypes.ProjectVariation, variation.Id, cancellationToken);
        if (activeRequest is not null)
        {
            // The matrix enforces who may decide at each level and applies the outcome on the final one.
            await approvalWorkflowService.DecideAsync(tenantId, activeRequest.Id, approve, note, cancellationToken);
            return;
        }

        projectAccessService.Ensure(PermissionNames.ProjectsVariationsApprove);
        variation.Status = approve ? ProjectVariationStatuses.Approved : ProjectVariationStatuses.Rejected;
        variation.DecidedAt = DateTime.UtcNow;
        variation.DecidedByUserId = userId;
        variation.DecisionNote = note?.Trim();
        await ProjectCommercials.RecomputeApprovedTotalsAsync(dbContext, tenantId, project.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(tenantId, userId, "projects.variation.decided", nameof(ProjectVariation), variation.Id.ToString(),
            $"Variation {variation.VariationNumber} {variation.Status.ToLowerInvariant()} on {project.ProjectNumber}.", cancellationToken);
    }

    public async Task VoidVariationAsync(Guid tenantId, Guid userId, Project project, ProjectVariation variation, string? reason, CancellationToken cancellationToken = default)
    {
        switch (variation.Status)
        {
            case ProjectVariationStatuses.Void:
                throw new BusinessRuleException("This variation is already void.");
            case ProjectVariationStatuses.Submitted:
                throw new BusinessRuleException("Approve or reject this variation before voiding it.");
            case ProjectVariationStatuses.Approved:
                projectAccessService.Ensure(PermissionNames.ProjectsVariationsApprove);
                if (string.IsNullOrWhiteSpace(reason))
                    throw new BusinessRuleException("Give a reason for voiding an approved variation.");
                break;
        }

        var wasApproved = variation.Status == ProjectVariationStatuses.Approved;
        variation.Status = ProjectVariationStatuses.Void;
        variation.DecisionNote = string.IsNullOrWhiteSpace(reason) ? variation.DecisionNote : reason.Trim();
        if (wasApproved)
            await ProjectCommercials.RecomputeApprovedTotalsAsync(dbContext, tenantId, project.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(tenantId, userId, "projects.variation.voided", nameof(ProjectVariation), variation.Id.ToString(),
            $"Variation {variation.VariationNumber} voided on {project.ProjectNumber}." + (string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}"),
            cancellationToken);
    }

    private async Task<string> StageCategoryAsync(Guid tenantId, Project project, CancellationToken cancellationToken) =>
        await dbContext.ProjectStages.Where(x => x.TenantId == tenantId && x.Id == project.StageId).Select(x => x.Category).SingleAsync(cancellationToken);

    private static void EnsureOpen(Project project, string stageCategory)
    {
        if (stageCategory is ProjectStageCategories.Closed or ProjectStageCategories.Cancelled)
            throw new BusinessRuleException($"Project {project.ProjectNumber} is {stageCategory.ToLowerInvariant()}. Reopen it to make changes.");
    }

    private static string NormalizeKind(string? kind) =>
        ProjectVariationKinds.All.FirstOrDefault(x => string.Equals(x, kind?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new BusinessRuleException("Variation kind must be Addition or Omission.");
}
