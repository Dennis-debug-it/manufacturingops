using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>Running checks: shift and daily checklists on assets, done by scanning or walking a route.</summary>
[ApiController]
[Authorize]
[Route("api/factory/checks")]
public sealed class RunningChecksController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IRunningCheckService runningCheckService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyCollection<CheckTemplateResponse>>> Templates(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        await RunningCheckTemplates.InstallAsync(dbContext, TenantId, cancellationToken);
        var templates = await dbContext.PmTemplates
            .Include(x => x.AssetCategory)
            .Where(x => x.TenantId == TenantId && x.IsActive && x.DeletedAt == null && (x.WorkType == RunningCheckTemplates.WorkType || x.WorkType == "Inspection"))
            .OrderBy(x => x.Name)
            .Select(x => new CheckTemplateResponse(x.Id, x.Name, x.WorkType, x.AssetCategoryId, x.AssetCategory != null ? x.AssetCategory.Name : null, x.Version,
                x.Questions.Count(q => q.IsActive), null))
            .ToListAsync(cancellationToken);
        return Ok(templates.Select(x => x with { SuggestedFrequency = RunningCheckTemplates.SuggestedFrequency(x.Name) }).ToList());
    }

    [HttpGet("schedules")]
    public async Task<ActionResult<IReadOnlyCollection<CheckScheduleResponse>>> Schedules([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        var schedules = await LoadSchedules().Where(x => x.TenantId == TenantId && (includeInactive || x.IsActive)).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return Ok(schedules.Where(x => scope.Allows(x.OrgNodeId)).Select(Map).ToList());
    }

    /// <summary>
    /// Sets up a running check. Name the assets (in route order), or leave them out to take every
    /// active asset of the template's class under the chosen factory or section.
    /// </summary>
    [HttpPost("schedules")]
    public async Task<ActionResult<CheckScheduleResponse>> CreateSchedule([FromBody] UpsertCheckScheduleRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var schedule = new CheckSchedule { TenantId = TenantId };
        await ApplyAsync(schedule, request, cancellationToken);
        dbContext.CheckSchedules.Add(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(await LoadSchedules().SingleAsync(x => x.Id == schedule.Id, cancellationToken)));
    }

    [HttpPut("schedules/{id:guid}")]
    public async Task<ActionResult<CheckScheduleResponse>> UpdateSchedule(Guid id, [FromBody] UpsertCheckScheduleRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var schedule = await LoadSchedules().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Running check was not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, schedule.OrgNodeId, cancellationToken);
        dbContext.CheckScheduleAssets.RemoveRange(schedule.Assets);
        schedule.Assets.Clear();
        await ApplyAsync(schedule, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(await LoadSchedules().SingleAsync(x => x.Id == schedule.Id, cancellationToken)));
    }

    /// <summary>
    /// The checks due now (this shift, or today) in the user's part of the factory, with the ones
    /// already done and those missed today, so the list shows progress. Filter by section or by a
    /// scanned asset.
    /// </summary>
    [HttpGet("due")]
    public async Task<ActionResult<IReadOnlyCollection<CheckRunSummary>>> Due([FromQuery] Guid? orgNodeId, [FromQuery] Guid? assetId, CancellationToken cancellationToken)
    {
        EnsureCanDoChecks();
        var now = DateTime.UtcNow;
        await runningCheckService.EnsureCurrentRunsAsync(TenantId, now, cancellationToken);
        var since = now.AddHours(-24);
        var query = dbContext.CheckRuns.Where(x => x.TenantId == TenantId
            && ((x.DueFrom <= now && x.DueBy > now) || (x.Status == CheckRunStatuses.Missed && x.DueBy >= since)));
        query = await ScopedAsync(query, orgNodeId, cancellationToken);
        if (assetId.HasValue) query = query.Where(x => x.AssetId == assetId);
        var runs = await query
            .OrderBy(x => x.Status == CheckRunStatuses.Due ? 0 : x.Status == CheckRunStatuses.Missed ? 1 : 2)
            .ThenBy(x => x.CheckSchedule!.Name).ThenBy(x => x.Sequence)
            .Select(SummaryProjection)
            .ToListAsync(cancellationToken);
        return Ok(runs);
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult<CheckRunDetail>> Run(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanDoChecks();
        var run = await LoadRunAsync(id, cancellationToken);
        var summary = await dbContext.CheckRuns.Where(x => x.Id == id).Select(SummaryProjection).SingleAsync(cancellationToken);
        if (run.Status == CheckRunStatuses.Completed)
        {
            var answers = await dbContext.CheckRunAnswers.Where(x => x.CheckRunId == id).OrderBy(x => x.DisplayOrder)
                .Select(x => new CheckQuestionResponse(x.QuestionId ?? x.Id, x.SectionName, x.Prompt, x.ResponseType, x.Unit, x.MinValue, x.MaxValue, false, false, false, x.FailAction,
                    x.Value, x.Passed, x.Note, x.RaisedWorkOrderId))
                .ToListAsync(cancellationToken);
            return Ok(new CheckRunDetail(summary, answers, run.Notes, run.SignatureDataUrl is not null));
        }

        var questions = await dbContext.PmTemplateQuestions
            .Where(x => x.PmTemplateId == run.PmTemplateId && x.IsActive)
            .OrderBy(x => x.Section != null ? x.Section.DisplayOrder : int.MaxValue).ThenBy(x => x.DisplayOrder)
            .Select(x => new CheckQuestionResponse(x.Id, x.Section != null ? x.Section.SectionName : x.SectionName, x.Prompt, x.ResponseType, x.Unit, x.MinValue, x.MaxValue,
                x.IsRequired, x.EvidenceRequired, x.RequiresNoteOnFail, x.FailAction ?? CheckFailActions.None, null, null, null, null))
            .ToListAsync(cancellationToken);
        return Ok(new CheckRunDetail(summary, questions, null, false));
    }

    /// <summary>Records the check. Offline phones send it later with the time it was done.</summary>
    [HttpPost("runs/{id:guid}/submit")]
    public async Task<ActionResult<CheckSubmissionResult>> Submit(Guid id, [FromBody] SubmitCheckRequest request, CancellationToken cancellationToken)
    {
        EnsureCanDoChecks();
        var run = await LoadRunAsync(id, cancellationToken);
        var result = await runningCheckService.SubmitAsync(run, new FactoryActor(UserId, UserDisplayName), new CheckSubmission(
            (request.Answers ?? []).Select(x => new CheckAnswerInput(x.QuestionId, x.Value, x.Note, x.AttachmentId)).ToList(),
            request.CompletedAt, request.Notes, request.SignatureDataUrl), cancellationToken);
        return Ok(result);
    }

    /// <summary>Completion by schedule and shift over the last few days, for supervisors and the dashboard.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<IReadOnlyCollection<CheckShiftSummary>>> Summary([FromQuery] Guid? orgNodeId, [FromQuery] int? days, CancellationToken cancellationToken)
    {
        EnsureCanDoChecks();
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days ?? 1, 1, 31));
        var query = await ScopedAsync(dbContext.CheckRuns.Where(x => x.TenantId == TenantId && x.DueFrom >= since), orgNodeId, cancellationToken);
        var rows = await query
            .GroupBy(x => new { x.CheckScheduleId, Schedule = x.CheckSchedule!.Name, x.PeriodKey, x.ShiftName, x.DueFrom })
            .Select(g => new CheckShiftSummary(
                g.Key.CheckScheduleId, g.Key.Schedule, g.Key.PeriodKey, g.Key.ShiftName, g.Key.DueFrom,
                g.Count(), g.Count(x => x.Status == CheckRunStatuses.Completed), g.Count(x => x.Status == CheckRunStatuses.Missed), g.Sum(x => x.FailedCount)))
            .ToListAsync(cancellationToken);
        return Ok(rows.OrderByDescending(x => x.DueFrom).ThenBy(x => x.ScheduleName).ToList());
    }

    private static readonly System.Linq.Expressions.Expression<Func<CheckRun, CheckRunSummary>> SummaryProjection = x => new CheckRunSummary(
        x.Id, x.CheckScheduleId, x.CheckSchedule!.Name, x.CheckSchedule.IsRoute, x.Sequence,
        x.AssetId, x.Asset!.AssetCode, x.Asset.AssetName, x.OrgNodeId,
        x.PeriodKey, x.ShiftName, x.DueFrom, x.DueBy, x.Status, x.CompletedAt, x.CompletedByName, x.FailedCount);

    private void EnsureCanManage() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders);

    /// <summary>Operators and artisans do checks: anyone who can raise or work jobs.</summary>
    private void EnsureCanDoChecks() =>
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanCompleteWorkOrders, PermissionNames.CanCreateWorkOrders, PermissionNames.CanAssignWorkOrders);

    private IQueryable<CheckSchedule> LoadSchedules() => dbContext.CheckSchedules
        .Include(x => x.PmTemplate)
        .Include(x => x.OrgNode)
        .Include(x => x.Assets).ThenInclude(x => x.Asset);

    private async Task<IQueryable<CheckRun>> ScopedAsync(IQueryable<CheckRun> query, Guid? orgNodeId, CancellationToken cancellationToken)
    {
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (scope.Nodes is not null)
        {
            var nodes = scope.Nodes.ToList();
            query = query.Where(x => nodes.Contains(x.OrgNodeId));
        }

        if (orgNodeId.HasValue)
        {
            var subtree = (await orgScopeService.GetSubtreeAsync(TenantId, orgNodeId.Value, cancellationToken)).ToList();
            query = query.Where(x => subtree.Contains(x.OrgNodeId));
        }

        return query;
    }

    private async Task<CheckRun> LoadRunAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await dbContext.CheckRuns.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Check was not found.");
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        return scope.Allows(run.OrgNodeId) ? run : throw new NotFoundException("Check was not found.");
    }

    private async Task ApplyAsync(CheckSchedule schedule, UpsertCheckScheduleRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Name the running check.");
        var template = await dbContext.PmTemplates.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.TemplateId && x.IsActive && x.DeletedAt == null, cancellationToken)
            ?? throw new BusinessRuleException("Checklist template was not found.");
        var frequency = CheckFrequencies.All.FirstOrDefault(x => string.Equals(x, request.Frequency, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Frequency must be PerShift or Daily.");
        var node = await dbContext.OrgNodes.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.OrgNodeId && x.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Choose the factory or section.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, node.Id, cancellationToken);

        var subtree = (await orgScopeService.GetSubtreeAsync(TenantId, node.Id, cancellationToken)).ToList();
        List<Guid> assetIds;
        if (request.AssetIds is { Count: > 0 })
        {
            assetIds = request.AssetIds.Distinct().ToList();
            var found = await dbContext.Assets.Where(x => x.TenantId == TenantId && assetIds.Contains(x.Id) && x.Status != "Inactive"
                && x.OrgNodeId.HasValue && subtree.Contains(x.OrgNodeId.Value)).Select(x => x.Id).ToListAsync(cancellationToken);
            if (found.Count != assetIds.Count) throw new BusinessRuleException("Every asset must be active and inside the chosen factory or section.");
        }
        else
        {
            if (template.AssetCategoryId is null) throw new BusinessRuleException("This template has no asset class, so choose the assets.");
            assetIds = await dbContext.Assets
                .Where(x => x.TenantId == TenantId && x.AssetCategoryId == template.AssetCategoryId && x.Status != "Inactive" && x.OrgNodeId.HasValue && subtree.Contains(x.OrgNodeId.Value))
                .OrderBy(x => x.AssetCode).Select(x => x.Id).ToListAsync(cancellationToken);
            if (assetIds.Count == 0) throw new BusinessRuleException("No assets of the template's class are in that factory or section.");
        }

        schedule.Name = request.Name.Trim();
        schedule.PmTemplateId = template.Id;
        schedule.Frequency = frequency;
        schedule.OrgNodeId = node.Id;
        schedule.IsRoute = request.IsRoute;
        schedule.IsActive = request.IsActive ?? true;
        foreach (var (assetId, index) in assetIds.Select((a, i) => (a, i)))
        {
            var item = new CheckScheduleAsset { CheckScheduleId = schedule.Id, AssetId = assetId, Sequence = index + 1 };
            schedule.Assets.Add(item);
            dbContext.Entry(item).State = EntityState.Added;
        }
    }

    private static CheckScheduleResponse Map(CheckSchedule x) => new(
        x.Id, x.Name, x.PmTemplateId, x.PmTemplate?.Name ?? string.Empty, x.Frequency, x.OrgNodeId, x.OrgNode?.Name, x.IsRoute, x.IsActive,
        x.Assets.OrderBy(a => a.Sequence).Select(a => new CheckScheduleAssetDto(a.AssetId, a.Asset?.AssetCode ?? string.Empty, a.Asset?.AssetName ?? string.Empty)).ToList());
}

public sealed record CheckTemplateResponse(Guid Id, string Name, string Kind, Guid? AssetCategoryId, string? AssetCategoryName, int Version, int QuestionCount, string? SuggestedFrequency);

public sealed record CheckScheduleAssetDto(Guid AssetId, string AssetCode, string AssetName);

public sealed record CheckScheduleResponse(
    Guid Id, string Name, Guid TemplateId, string TemplateName, string Frequency, Guid OrgNodeId, string? OrgNodeName, bool IsRoute, bool IsActive,
    IReadOnlyCollection<CheckScheduleAssetDto> Assets);

public sealed record UpsertCheckScheduleRequest(
    [StringLength(200)] string Name, Guid TemplateId, string Frequency, Guid OrgNodeId, bool IsRoute = false,
    IReadOnlyCollection<Guid>? AssetIds = null, bool? IsActive = null);

public sealed record CheckRunSummary(
    Guid Id, Guid ScheduleId, string ScheduleName, bool IsRoute, int Sequence,
    Guid AssetId, string AssetCode, string AssetName, Guid OrgNodeId,
    string PeriodKey, string? ShiftName, DateTime DueFrom, DateTime DueBy, string Status, DateTime? CompletedAt, string? CompletedByName, int FailedCount);

public sealed record CheckQuestionResponse(
    Guid Id, string? SectionName, string Prompt, string Type, string? Unit, decimal? MinValue, decimal? MaxValue,
    bool Required, bool PhotoRequired, bool NoteRequiredOnFail, string FailAction,
    string? Value, bool? Passed, string? Note, Guid? RaisedWorkOrderId);

public sealed record CheckRunDetail(CheckRunSummary Run, IReadOnlyCollection<CheckQuestionResponse> Questions, string? Notes, bool Signed);

public sealed record SubmitCheckAnswer(Guid QuestionId, [StringLength(1000)] string? Value, [StringLength(1000)] string? Note = null, Guid? AttachmentId = null);

public sealed record SubmitCheckRequest(
    IReadOnlyCollection<SubmitCheckAnswer>? Answers, DateTime? CompletedAt = null, [StringLength(2000)] string? Notes = null, string? SignatureDataUrl = null);

public sealed record CheckShiftSummary(
    Guid ScheduleId, string ScheduleName, string PeriodKey, string? ShiftName, DateTime DueFrom, int Total, int Completed, int Missed, int FailedItems);
