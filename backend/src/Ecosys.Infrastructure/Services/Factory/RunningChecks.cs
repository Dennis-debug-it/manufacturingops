using System.Globalization;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record CheckAnswerInput(Guid QuestionId, string? Value, string? Note = null, Guid? AttachmentId = null);

public sealed record CheckSubmission(
    IReadOnlyCollection<CheckAnswerInput> Answers,
    DateTime? CompletedAt = null,
    string? Notes = null,
    string? SignatureDataUrl = null);

public sealed record CheckSubmissionResult(int Failed, IReadOnlyCollection<string> RaisedWorkOrders, IReadOnlyCollection<string> Warnings);

public interface IRunningCheckService
{
    /// <summary>Creates this shift's (or day's) checks for every active schedule that doesn't have them yet. Safe to call repeatedly.</summary>
    Task<int> EnsureCurrentRunsAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>Marks checks still due after their shift ended as missed, and returns them.</summary>
    Task<IReadOnlyList<CheckRun>> MarkMissedAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a completed check: scores each answer, records readings on the asset's meters,
    /// and raises a job (or a fix-in-window defect) for failed items as their questions say.
    /// </summary>
    Task<CheckSubmissionResult> SubmitAsync(CheckRun run, FactoryActor actor, CheckSubmission submission, CancellationToken cancellationToken = default);
}

internal sealed class RunningCheckService(
    AppDbContext dbContext,
    IFactorySettingsService factorySettingsService,
    ITenantTimeZoneService timeZoneService,
    IMeterReadingService meterReadingService,
    IFactoryWorkOrderService workOrderService,
    IConditionMonitor? conditionMonitor = null) : IRunningCheckService
{
    /// <summary>Readings this submission put on meters, judged once the run is saved.</summary>
    private readonly List<(Meter Meter, MeterReading Reading)> fedReadings = [];

    private static readonly string[] PassValues = ["pass", "yes", "true", "ok", "good"];
    private static readonly string[] FailValues = ["fail", "no", "false", "poor", "bad", "failed", "not ok"];

    public async Task<int> EnsureCurrentRunsAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var schedules = await dbContext.CheckSchedules
            .Include(x => x.Assets).ThenInclude(x => x.Asset)
            .Include(x => x.PmTemplate)
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync(cancellationToken);
        if (schedules.Count == 0)
        {
            return 0;
        }

        var settings = await factorySettingsService.GetOrCreateAsync(tenantId, cancellationToken);
        var shifts = ShiftCalendar.Parse(settings.ShiftsJson);
        var zone = await timeZoneService.GetTimeZoneAsync(tenantId, cancellationToken);

        var created = new List<CheckRun>();
        foreach (var schedule in schedules)
        {
            var period = ShiftCalendar.Current(schedule.Frequency, shifts, zone, utcNow);
            if (period is null)
            {
                continue;
            }

            var existing = await dbContext.CheckRuns
                .Where(x => x.CheckScheduleId == schedule.Id && x.PeriodKey == period.Key)
                .Select(x => x.AssetId)
                .ToListAsync(cancellationToken);
            foreach (var item in schedule.Assets.Where(x => x.Asset is { Status: not "Inactive" } && !existing.Contains(x.AssetId)))
            {
                var run = new CheckRun
                {
                    TenantId = tenantId,
                    CheckScheduleId = schedule.Id,
                    AssetId = item.AssetId,
                    OrgNodeId = item.Asset!.OrgNodeId ?? schedule.OrgNodeId,
                    PmTemplateId = schedule.PmTemplateId,
                    TemplateVersion = schedule.PmTemplate?.Version ?? 1,
                    PeriodKey = period.Key,
                    ShiftName = period.ShiftName,
                    DueFrom = period.StartUtc,
                    DueBy = period.EndUtc,
                    Sequence = item.Sequence
                };
                dbContext.CheckRuns.Add(run);
                created.Add(run);
            }
        }

        if (created.Count == 0)
        {
            return 0;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request generated the same checks (unique schedule/asset/shift); theirs stand.
            created.ForEach(x => dbContext.Entry(x).State = EntityState.Detached);
            return 0;
        }

        return created.Count;
    }

    public async Task<IReadOnlyList<CheckRun>> MarkMissedAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var overdue = await dbContext.CheckRuns
            .Include(x => x.Asset)
            .Include(x => x.CheckSchedule)
            .Where(x => x.TenantId == tenantId && x.Status == CheckRunStatuses.Due && x.DueBy <= utcNow)
            .ToListAsync(cancellationToken);
        overdue.ForEach(x => x.Status = CheckRunStatuses.Missed);
        if (overdue.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return overdue;
    }

    public async Task<CheckSubmissionResult> SubmitAsync(CheckRun run, FactoryActor actor, CheckSubmission submission, CancellationToken cancellationToken = default)
    {
        if (run.Status == CheckRunStatuses.Completed)
        {
            throw new BusinessRuleException("This check has already been done.");
        }

        var completedAt = FactoryTime.EffectiveAt(submission.CompletedAt);
        if (completedAt > DateTime.UtcNow.AddMinutes(10) || completedAt < run.DueFrom.AddHours(-1))
        {
            throw new BusinessRuleException("The time the check was done is outside its shift.");
        }

        if (submission.SignatureDataUrl is { } signature && (!signature.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || signature.Length > 500_000))
        {
            throw new BusinessRuleException("The signature could not be read. Sign again.");
        }

        var questions = await dbContext.PmTemplateQuestions
            .Include(x => x.Section)
            .Where(x => x.PmTemplateId == run.PmTemplateId && x.IsActive)
            .OrderBy(x => x.Section != null ? x.Section.DisplayOrder : int.MaxValue).ThenBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);
        var given = submission.Answers.GroupBy(x => x.QuestionId).ToDictionary(x => x.Key, x => x.Last());
        var asset = await dbContext.Assets.SingleAsync(x => x.Id == run.AssetId, cancellationToken);

        var answers = new List<CheckRunAnswer>();
        var warnings = new List<string>();
        foreach (var (question, index) in questions.Select((q, i) => (q, i)))
        {
            given.TryGetValue(question.Id, out var input);
            var value = string.IsNullOrWhiteSpace(input?.Value) ? null : input!.Value!.Trim();
            var type = question.ResponseType.ToLowerInvariant();
            var hasPhoto = input?.AttachmentId is not null;
            if (question.IsRequired && value is null && !(type == "photo" && hasPhoto))
            {
                throw new BusinessRuleException($"Answer \"{question.Prompt}\".");
            }

            if (question.EvidenceRequired && !hasPhoto)
            {
                throw new BusinessRuleException($"Add a photo for \"{question.Prompt}\".");
            }

            var answer = new CheckRunAnswer
            {
                CheckRunId = run.Id,
                QuestionId = question.Id,
                SectionName = question.Section?.SectionName ?? question.SectionName,
                Prompt = question.Prompt,
                ResponseType = question.ResponseType,
                Unit = question.Unit,
                MinValue = question.MinValue,
                MaxValue = question.MaxValue,
                Value = value,
                Note = string.IsNullOrWhiteSpace(input?.Note) ? null : input!.Note!.Trim(),
                AttachmentId = input?.AttachmentId,
                FailAction = question.FailAction ?? CheckFailActions.None,
                DisplayOrder = index
            };
            Score(answer, value, type);
            if (answer.Passed == false && question.RequiresNoteOnFail && answer.Note is null)
            {
                throw new BusinessRuleException($"Say what is wrong for \"{question.Prompt}\".");
            }

            if (question.FeedsMeterType is { } meterType && answer.NumberValue is { } reading)
            {
                var warning = await FeedMeterAsync(run, asset, meterType, reading, completedAt, actor, cancellationToken);
                if (warning is not null)
                {
                    warnings.Add(warning);
                    answer.Note = answer.Note is null ? warning : $"{answer.Note} ({warning})";
                }
            }

            answers.Add(answer);
        }

        dbContext.CheckRunAnswers.AddRange(answers);
        run.Status = CheckRunStatuses.Completed;
        run.CompletedAt = completedAt;
        run.CompletedByUserId = actor.UserId;
        run.CompletedByName = actor.Name;
        run.Notes = string.IsNullOrWhiteSpace(submission.Notes) ? null : submission.Notes.Trim();
        run.SignatureDataUrl = submission.SignatureDataUrl;
        run.FailedCount = answers.Count(x => x.Passed == false);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (conditionMonitor is not null)
        {
            foreach (var (meter, reading) in fedReadings)
            {
                await conditionMonitor.EvaluateAsync(meter, reading, actor, cancellationToken);
            }
        }

        var raised = new List<string>();
        foreach (var group in answers.Where(x => x.Passed == false && x.FailAction != CheckFailActions.None).GroupBy(x => x.FailAction))
        {
            var items = group.ToList();
            var window = group.Key == CheckFailActions.WindowDefect;
            var summary = items.Count == 1 ? items[0].Prompt : $"{items[0].Prompt} (+{items.Count - 1} more)";
            var job = await workOrderService.CreateAsync(run.TenantId, actor, new CreateFactoryWorkOrder(
                $"{(window ? "Fix in window" : "Check failed")}: {Trim(summary, 200)}",
                FactoryWorkTypes.Corrective,
                window ? "Medium" : asset.Criticality == AssetCriticality.A ? "High" : "Medium",
                AssetId: asset.Id,
                Description: string.Join("\n", items.Select(x => $"- {x.Prompt}: {x.Value}{(x.Unit is null ? string.Empty : $" {x.Unit}")}{(x.Note is null ? string.Empty : $" ({x.Note})")}")),
                NeedsLineStop: window,
                Source: FactoryRequestSources.RunningCheck,
                RequestedByName: actor.Name), cancellationToken);
            items.ForEach(x => x.RaisedWorkOrderId = job.Id);
            raised.Add(job.WorkOrderNumber);
        }

        if (raised.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new CheckSubmissionResult(run.FailedCount, raised, warnings);
    }

    /// <summary>Pass/fail for yes/no style answers, or a reading against its limits; null where pass/fail has no meaning.</summary>
    internal static void Score(CheckRunAnswer answer, string? value, string type)
    {
        if (value is null)
        {
            answer.Passed = null;
            return;
        }

        switch (type)
        {
            case "passfail" or "yesno" or "boolean" or "checkbox" or "condition":
                var lowered = value.ToLowerInvariant();
                answer.Passed = PassValues.Contains(lowered) ? true : FailValues.Contains(lowered) ? false : null;
                if (answer.Passed is null && type is "passfail" or "yesno" or "boolean")
                {
                    throw new BusinessRuleException($"\"{answer.Prompt}\" must be answered pass or fail.");
                }

                break;
            case "number" or "reading":
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                {
                    throw new BusinessRuleException($"\"{answer.Prompt}\" must be a number.");
                }

                answer.NumberValue = number;
                answer.Passed = answer.MinValue is null && answer.MaxValue is null
                    ? null
                    : (answer.MinValue is null || number >= answer.MinValue) && (answer.MaxValue is null || number <= answer.MaxValue);
                break;
            default:
                answer.Passed = null;
                break;
        }
    }

    /// <summary>Records a check reading on the asset's meter of that type. A reading the meter refuses is kept on the check with a warning.</summary>
    private async Task<string?> FeedMeterAsync(CheckRun run, Asset asset, string meterType, decimal reading, DateTime at, FactoryActor actor, CancellationToken cancellationToken)
    {
        var meter = await dbContext.Meters
            .Where(x => x.TenantId == run.TenantId && x.AssetId == asset.Id && x.IsActive && x.MeterType == meterType)
            .OrderByDescending(x => x.IsPrimary)
            .FirstOrDefaultAsync(cancellationToken);
        if (meter is null)
        {
            return $"{asset.AssetCode} has no {meterType} meter, so the reading was kept on the check only";
        }

        try
        {
            var recorded = await meterReadingService.RecordAsync(meter, new NewMeterReading(reading, at, MeterReadingSources.Manual, Notes: "Running check", UserId: actor.UserId, UserName: actor.Name), cancellationToken);
            fedReadings.Add((meter, recorded));
            return null;
        }
        catch (BusinessRuleException ex)
        {
            return $"not recorded on the meter: {ex.Message}";
        }
    }

    private static string Trim(string value, int max) => value.Length <= max ? value : $"{value[..(max - 3)]}...";
}
