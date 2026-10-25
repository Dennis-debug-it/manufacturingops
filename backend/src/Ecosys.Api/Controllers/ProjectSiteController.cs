using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Site work on a project: the worker roster, the daily muster roll and the site diary (D8).
/// Supervisors record presence only; nothing here returns or accepts pay rates.
/// </summary>
[ApiController]
[Authorize]
[Route("api/project-site")]
public sealed class ProjectSiteController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IProjectAccessService projectAccessService,
    IProjectSettingsService projectSettingsService,
    ITenantTimeZoneService tenantTimeZoneService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    // ── My sites ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Open projects the caller is on (as a user or through their technician profile), for the
    /// technician app's site list and clock-in picker. Membership is the grant here.
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyCollection<MySiteResponse>>> Mine(CancellationToken cancellationToken)
    {
        var userId = UserId;
        var canMark = projectAccessService.Can(PermissionNames.ProjectsMusterMark) || projectAccessService.Can(PermissionNames.ProjectsManage);
        var settings = await projectSettingsService.GetAsync(TenantId, cancellationToken);
        var memberships = await dbContext.ProjectMembers
            .Where(x => x.TenantId == TenantId && (x.UserId == userId || (x.Technician != null && x.Technician.UserId == userId))
                && ProjectStageCategories.Open.Contains(x.Project!.Stage!.Category))
            .Select(x => new { x.ProjectId, x.Project!.ProjectNumber, x.Project.Name, ClientName = x.Project.Client!.ClientName, SiteName = x.Project.Site != null ? x.Project.Site.SiteName : null, x.Role })
            .ToListAsync(cancellationToken);

        return Ok(memberships
            .GroupBy(x => x.ProjectId)
            .Select(g =>
            {
                var first = g.First();
                var siteRole = g.Any(m => ProjectMemberRoles.SiteRoles.Contains(m.Role));
                return new MySiteResponse(g.Key, first.ProjectNumber, first.Name, first.ClientName, first.SiteName,
                    g.Select(m => m.Role).Distinct().ToList(), settings.MusterRoll && canMark && siteRole, settings.SiteDiary && siteRole);
            })
            .OrderBy(x => x.Name)
            .ToList());
    }

    // ── Worker roster ─────────────────────────────────────────────────────────

    [HttpGet("{projectId:guid}/workers")]
    public async Task<ActionResult<IReadOnlyCollection<ProjectWorkerResponse>>> ListWorkers(Guid projectId, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadForSiteReadAsync(TenantId, projectId, cancellationToken);
        var workers = await dbContext.ProjectWorkers
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId && (includeInactive || x.IsActive))
            .OrderBy(x => x.FullName)
            .Select(x => new ProjectWorkerResponse(x.Id, x.FullName, x.Phone, x.Trade, x.EngagementType, x.TechnicianId, x.IsActive))
            .ToListAsync(cancellationToken);
        return Ok(workers);
    }

    [HttpPost("{projectId:guid}/workers")]
    public async Task<ActionResult<ProjectWorkerResponse>> AddWorker(Guid projectId, [FromBody] UpsertProjectWorkerRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadOpenForSiteWorkAsync(projectId, cancellationToken);
        var worker = new ProjectWorker { TenantId = TenantId, ProjectId = projectId };
        await ApplyWorkerAsync(worker, request, cancellationToken);
        dbContext.ProjectWorkers.Add(worker);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.worker.added", nameof(ProjectWorker), worker.Id.ToString(),
            $"{worker.FullName} added to the {project.ProjectNumber} roster.", cancellationToken);
        return Ok(MapWorker(worker));
    }

    [HttpPut("{projectId:guid}/workers/{workerId:guid}")]
    public async Task<ActionResult<ProjectWorkerResponse>> UpdateWorker(Guid projectId, Guid workerId, [FromBody] UpsertProjectWorkerRequest request, CancellationToken cancellationToken)
    {
        await LoadOpenForSiteWorkAsync(projectId, cancellationToken);
        var worker = await LoadWorkerAsync(projectId, workerId, cancellationToken);
        await ApplyWorkerAsync(worker, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapWorker(worker));
    }

    // ── Muster roll ───────────────────────────────────────────────────────────

    /// <summary>
    /// The day sheet: every active worker with their mark for the date. Unmarked workers are
    /// suggested from yesterday's mark, or as present when their technician profile clocked in on
    /// this project that day. Suggestions are not saved until the supervisor submits.
    /// </summary>
    [HttpGet("{projectId:guid}/muster")]
    public async Task<ActionResult<MusterDaySheetResponse>> GetMuster(Guid projectId, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadForSiteReadAsync(TenantId, projectId, cancellationToken);
        var today = await TodayAsync(cancellationToken);
        var workDate = date ?? today;
        var settings = await projectSettingsService.GetAsync(TenantId, cancellationToken);

        var marks = await dbContext.ProjectMusterEntries.Where(x => x.TenantId == TenantId && x.ProjectId == projectId && x.WorkDate == workDate).ToListAsync(cancellationToken);
        var previousDate = await dbContext.ProjectMusterEntries
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId && x.WorkDate < workDate)
            .MaxAsync(x => (DateOnly?)x.WorkDate, cancellationToken);
        var previous = previousDate.HasValue
            ? await dbContext.ProjectMusterEntries.Where(x => x.TenantId == TenantId && x.ProjectId == projectId && x.WorkDate == previousDate.Value).ToListAsync(cancellationToken)
            : [];
        var markedIds = marks.Select(x => x.ProjectWorkerId).ToHashSet();
        var workers = await dbContext.ProjectWorkers
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId && (x.IsActive || markedIds.Contains(x.Id)))
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);
        var technicianIds = workers.Where(x => x.TechnicianId.HasValue).Select(x => x.TechnicianId!.Value).ToList();
        var clockedIn = await dbContext.AttendanceEntries
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId && x.WorkDate == workDate && technicianIds.Contains(x.TechnicianId))
            .Select(x => x.TechnicianId).Distinct().ToListAsync(cancellationToken);

        var rows = workers.Select(worker =>
        {
            var mark = marks.FirstOrDefault(x => x.ProjectWorkerId == worker.Id);
            if (mark is not null)
                return new MusterRowResponse(worker.Id, worker.FullName, worker.Trade, worker.EngagementType, mark.Status, false, mark.Note, worker.IsActive);
            var suggestion = worker.TechnicianId.HasValue && clockedIn.Contains(worker.TechnicianId.Value)
                ? ProjectMusterStatuses.Present
                : previous.FirstOrDefault(x => x.ProjectWorkerId == worker.Id)?.Status;
            return new MusterRowResponse(worker.Id, worker.FullName, worker.Trade, worker.EngagementType, suggestion, suggestion is not null, null, worker.IsActive);
        }).ToList();

        return Ok(new MusterDaySheetResponse(workDate, marks.Count > 0, IsLocked(workDate, today, settings), rows,
            rows.Where(x => !x.IsSuggestion).Sum(x => x.Status is null ? 0m : ProjectMusterStatuses.DayFraction(x.Status))));
    }

    /// <summary>
    /// Saves the marks for a day. Safe to send more than once (offline replays): each worker has at
    /// most one mark per day, which is updated in place. Past the tenant's lock point a correction
    /// needs projects.manage and is recorded in the audit log.
    /// </summary>
    [HttpPut("{projectId:guid}/muster")]
    public async Task<ActionResult<MusterDaySheetResponse>> SaveMuster(Guid projectId, [FromQuery] DateOnly date, [FromBody] SaveMusterRequest request, CancellationToken cancellationToken)
    {
        var project = await LoadOpenForSiteWorkAsync(projectId, cancellationToken);
        var settings = await projectSettingsService.GetAsync(TenantId, cancellationToken);
        if (!settings.MusterRoll)
            throw new BusinessRuleException("The muster roll is turned off in Project settings.");
        var today = await TodayAsync(cancellationToken);
        if (date > today)
            throw new BusinessRuleException("You can't mark a day that hasn't happened yet.");
        var locked = IsLocked(date, today, settings);
        if (locked && !projectAccessService.Can(PermissionNames.ProjectsManage))
            throw new BusinessRuleException($"Marks for {date:dd MMM yyyy} are locked. Ask a project manager to correct them.");

        var workerIds = request.Marks.Select(x => x.WorkerId).Distinct().ToList();
        if (workerIds.Count != request.Marks.Count)
            throw new BusinessRuleException("Each worker can be marked once per day.");
        var workers = await dbContext.ProjectWorkers.Where(x => x.TenantId == TenantId && x.ProjectId == projectId && workerIds.Contains(x.Id)).ToListAsync(cancellationToken);
        if (workers.Count != workerIds.Count)
            throw new BusinessRuleException("Some workers aren't on this project's roster.");
        var existing = await dbContext.ProjectMusterEntries.Where(x => x.TenantId == TenantId && x.ProjectId == projectId && x.WorkDate == date && workerIds.Contains(x.ProjectWorkerId)).ToListAsync(cancellationToken);

        var changes = 0;
        foreach (var mark in request.Marks)
        {
            var status = ProjectMusterStatuses.All.FirstOrDefault(x => string.Equals(x, mark.Status?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException("Mark each worker Present, HalfDay or Absent.");
            var entry = existing.FirstOrDefault(x => x.ProjectWorkerId == mark.WorkerId);
            if (entry is null)
            {
                entry = new ProjectMusterEntry { TenantId = TenantId, ProjectId = projectId, ProjectWorkerId = mark.WorkerId, WorkDate = date };
                dbContext.ProjectMusterEntries.Add(entry);
                changes++;
            }
            else if (entry.Status != status || entry.Note != mark.Note?.Trim())
            {
                changes++;
            }
            entry.Status = status;
            entry.DayFraction = ProjectMusterStatuses.DayFraction(status);
            entry.Note = string.IsNullOrWhiteSpace(mark.Note) ? null : mark.Note.Trim();
            entry.MarkedByUserId = UserId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (changes > 0)
        {
            await auditLogService.LogAsync(TenantId, UserId, locked ? "projects.muster.corrected" : "projects.muster.marked", nameof(Project), project.Id.ToString(),
                $"Muster roll for {project.ProjectNumber} on {date:yyyy-MM-dd}: {request.Marks.Count} worker(s) marked{(locked ? " after the lock point" : "")}.", cancellationToken);
        }
        return await GetMuster(projectId, date, cancellationToken);
    }

    // ── Site diary ────────────────────────────────────────────────────────────

    [HttpGet("{projectId:guid}/diary")]
    public async Task<ActionResult<IReadOnlyCollection<DiaryEntryResponse>>> ListDiary(Guid projectId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        await projectAccessService.LoadForSiteReadAsync(TenantId, projectId, cancellationToken);
        var query = dbContext.ProjectDiaryEntries.Where(x => x.TenantId == TenantId && x.ProjectId == projectId);
        if (from.HasValue) query = query.Where(x => x.EntryDate >= from);
        if (to.HasValue) query = query.Where(x => x.EntryDate <= to);
        var entries = await query.Include(x => x.AuthorUser).OrderByDescending(x => x.EntryDate).ThenByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        return Ok(await MapDiaryAsync(projectId, entries, cancellationToken));
    }

    [HttpPost("{projectId:guid}/diary")]
    public async Task<ActionResult<DiaryEntryResponse>> CreateDiary(Guid projectId, [FromBody] UpsertDiaryEntryRequest request, CancellationToken cancellationToken)
    {
        await LoadOpenForSiteWorkAsync(projectId, cancellationToken);
        if (!(await projectSettingsService.GetAsync(TenantId, cancellationToken)).SiteDiary)
            throw new BusinessRuleException("The site diary is turned off in Project settings.");
        if (request.EntryDate > await TodayAsync(cancellationToken))
            throw new BusinessRuleException("A diary entry can't be for a future date.");
        var entry = new ProjectDiaryEntry { TenantId = TenantId, ProjectId = projectId, EntryDate = request.EntryDate, AuthorUserId = UserId };
        ApplyDiary(entry, request);
        dbContext.ProjectDiaryEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok((await MapDiaryAsync(projectId, [entry], cancellationToken)).Single());
    }

    [HttpPut("{projectId:guid}/diary/{entryId:guid}")]
    public async Task<ActionResult<DiaryEntryResponse>> UpdateDiary(Guid projectId, Guid entryId, [FromBody] UpsertDiaryEntryRequest request, CancellationToken cancellationToken)
    {
        await LoadOpenForSiteWorkAsync(projectId, cancellationToken);
        var entry = await LoadDiaryAsync(projectId, entryId, cancellationToken);
        if (entry.SubmittedAt.HasValue)
            throw new BusinessRuleException("A submitted diary entry is locked. Add a new entry to correct it.");
        if (entry.AuthorUserId != UserId && !projectAccessService.Can(PermissionNames.ProjectsManage))
            throw new ForbiddenException("Only the author can edit a draft diary entry.");
        entry.EntryDate = request.EntryDate;
        ApplyDiary(entry, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok((await MapDiaryAsync(projectId, [entry], cancellationToken)).Single());
    }

    [HttpPost("{projectId:guid}/diary/{entryId:guid}/submit")]
    public async Task<ActionResult<DiaryEntryResponse>> SubmitDiary(Guid projectId, Guid entryId, CancellationToken cancellationToken)
    {
        var project = await LoadOpenForSiteWorkAsync(projectId, cancellationToken);
        var entry = await LoadDiaryAsync(projectId, entryId, cancellationToken);
        if (entry.SubmittedAt.HasValue)
            throw new BusinessRuleException("This entry is already submitted.");
        entry.SubmittedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.diary.submitted", nameof(ProjectDiaryEntry), entry.Id.ToString(),
            $"Site diary for {project.ProjectNumber} on {entry.EntryDate:yyyy-MM-dd} submitted.", cancellationToken);
        return Ok((await MapDiaryAsync(projectId, [entry], cancellationToken)).Single());
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<DateOnly> TodayAsync(CancellationToken cancellationToken) =>
        DateOnly.FromDateTime(await tenantTimeZoneService.TenantTodayAsync(TenantId, cancellationToken));

    private static bool IsLocked(DateOnly workDate, DateOnly today, ProjectSettingsModel settings) =>
        workDate < today.AddDays(-settings.MusterLockDays);

    private async Task<Project> LoadOpenForSiteWorkAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projectAccessService.LoadForSiteWorkAsync(TenantId, projectId, cancellationToken);
        var category = await dbContext.ProjectStages.Where(x => x.Id == project.StageId).Select(x => x.Category).SingleAsync(cancellationToken);
        if (category is ProjectStageCategories.Closed or ProjectStageCategories.Cancelled)
            throw new BusinessRuleException($"Project {project.ProjectNumber} is {category.ToLowerInvariant()}.");
        return project;
    }

    private async Task<ProjectWorker> LoadWorkerAsync(Guid projectId, Guid workerId, CancellationToken cancellationToken) =>
        await dbContext.ProjectWorkers.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id == workerId, cancellationToken)
        ?? throw new NotFoundException("Worker not found on this project.");

    private async Task<ProjectDiaryEntry> LoadDiaryAsync(Guid projectId, Guid entryId, CancellationToken cancellationToken) =>
        await dbContext.ProjectDiaryEntries.Include(x => x.AuthorUser).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.ProjectId == projectId && x.Id == entryId, cancellationToken)
        ?? throw new NotFoundException("Diary entry not found.");

    private async Task ApplyWorkerAsync(ProjectWorker worker, UpsertProjectWorkerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new BusinessRuleException("Worker name is required.");
        var engagement = ProjectWorkerEngagements.All.FirstOrDefault(x => string.Equals(x, request.EngagementType?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? ProjectWorkerEngagements.Casual;
        if (request.TechnicianId.HasValue && request.TechnicianId != worker.TechnicianId)
        {
            if (!await dbContext.Technicians.AnyAsync(x => x.TenantId == TenantId && x.Id == request.TechnicianId && x.IsActive, cancellationToken))
                throw new BusinessRuleException("Select a valid technician.");
            if (await dbContext.ProjectWorkers.AnyAsync(x => x.TenantId == TenantId && x.ProjectId == worker.ProjectId && x.TechnicianId == request.TechnicianId && x.Id != worker.Id, cancellationToken))
                throw new BusinessRuleException("That technician is already on this roster.");
        }
        worker.FullName = request.FullName.Trim();
        worker.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        worker.Trade = string.IsNullOrWhiteSpace(request.Trade) ? null : request.Trade.Trim();
        worker.EngagementType = engagement;
        worker.TechnicianId = request.TechnicianId;
        worker.IsActive = request.IsActive ?? worker.IsActive;
    }

    private static void ApplyDiary(ProjectDiaryEntry entry, UpsertDiaryEntryRequest request)
    {
        static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        entry.WorkCompleted = Clean(request.WorkCompleted);
        entry.IssuesAndDelays = Clean(request.IssuesAndDelays);
        entry.InstructionsReceived = Clean(request.InstructionsReceived);
        entry.Weather = Clean(request.Weather);
    }

    /// <summary>Headcount and materials for each diary date come from the muster roll and material issues, not re-entered text.</summary>
    private async Task<IReadOnlyList<DiaryEntryResponse>> MapDiaryAsync(Guid projectId, IReadOnlyCollection<ProjectDiaryEntry> entries, CancellationToken cancellationToken)
    {
        var dates = entries.Select(x => x.EntryDate).Distinct().ToList();
        var headcounts = await dbContext.ProjectMusterEntries
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId && dates.Contains(x.WorkDate) && x.DayFraction > 0)
            .GroupBy(x => x.WorkDate).Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, cancellationToken);
        var issues = await dbContext.MaterialIssues
            .Where(x => x.TenantId == TenantId && x.ProjectId == projectId && x.IssuedAt != null)
            .Select(x => new { x.IssueNumber, x.IssuedAt })
            .ToListAsync(cancellationToken);
        var entryIds = entries.Select(x => x.Id).ToList();
        var photos = await dbContext.Attachments
            .Where(x => x.TenantId == TenantId && x.EntityType == "ProjectDiaryEntry" && entryIds.Contains(x.EntityId))
            .GroupBy(x => x.EntityId).Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        return entries.Select(x => new DiaryEntryResponse(
            x.Id, x.EntryDate, x.AuthorUserId, x.AuthorUser?.FullName, x.WorkCompleted, x.IssuesAndDelays, x.InstructionsReceived, x.Weather,
            x.SubmittedAt, headcounts.GetValueOrDefault(x.EntryDate),
            issues.Where(i => DateOnly.FromDateTime(i.IssuedAt!.Value) == x.EntryDate).Select(i => i.IssueNumber).ToList(),
            photos.GetValueOrDefault(x.Id))).ToList();
    }

    private static ProjectWorkerResponse MapWorker(ProjectWorker x) =>
        new(x.Id, x.FullName, x.Phone, x.Trade, x.EngagementType, x.TechnicianId, x.IsActive);
}

public sealed record MySiteResponse(Guid ProjectId, string ProjectNumber, string Name, string ClientName, string? SiteName, IReadOnlyList<string> Roles, bool CanMarkMuster, bool CanWriteDiary);

public sealed record ProjectWorkerResponse(Guid Id, string FullName, string? Phone, string? Trade, string EngagementType, Guid? TechnicianId, bool IsActive);

public sealed record UpsertProjectWorkerRequest(string FullName, string? Phone, string? Trade, string? EngagementType, Guid? TechnicianId, bool? IsActive = null);

public sealed record MusterRowResponse(Guid WorkerId, string FullName, string? Trade, string EngagementType, string? Status, bool IsSuggestion, string? Note, bool IsActive);

public sealed record MusterDaySheetResponse(DateOnly WorkDate, bool IsSubmitted, bool IsLocked, IReadOnlyList<MusterRowResponse> Rows, decimal ManDays);

public sealed record SaveMusterMark(Guid WorkerId, string Status, string? Note);

public sealed record SaveMusterRequest(IReadOnlyList<SaveMusterMark> Marks);

public sealed record DiaryEntryResponse(
    Guid Id, DateOnly EntryDate, Guid AuthorUserId, string? AuthorName, string? WorkCompleted, string? IssuesAndDelays, string? InstructionsReceived,
    string? Weather, DateTime? SubmittedAt, int Headcount, IReadOnlyList<string> MaterialIssues, int PhotoCount);

public sealed record UpsertDiaryEntryRequest(DateOnly EntryDate, string? WorkCompleted, string? IssuesAndDelays, string? InstructionsReceived, string? Weather);
