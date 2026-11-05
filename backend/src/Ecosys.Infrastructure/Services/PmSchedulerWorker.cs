using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public sealed class PmSchedulerWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PmSchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PM scheduler worker started.");

        // Wait until 06:00 UTC for first run, then every 24h
        await WaitUntilNextRunTimeAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private static async Task WaitUntilNextRunTimeAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var nextRun = now.Date.AddHours(6);
        if (nextRun <= now)
            nextRun = nextRun.AddDays(1);

        var delay = nextRun - now;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;

            await RunPlansAsync(db, scope.ServiceProvider.GetService<INotificationDispatcher>(), now, ct, scope.ServiceProvider.GetService<IContractOperationalService>());

            // Send overdue-by-DueDate notifications for open work orders that have passed their due date
            await RunOverdueNotificationsAsync(db, scope.ServiceProvider, now, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "PM scheduler cycle failed.");
        }
    }

    /// <summary>
    /// Raises the PM work orders now due. A schedule whose asset's contract has lapsed is put on hold
    /// instead (see <see cref="PmContractGuard"/>), and a held one resumes once the asset is covered again.
    /// </summary>
    internal async Task<(int Generated, int Held, int Resumed)> RunPlansAsync(AppDbContext db, INotificationDispatcher? dispatcher, DateTime now, CancellationToken ct, IContractOperationalService? contractOperationalService = null)
    {
        var activePlans = await db.PreventiveMaintenancePlans
            .Include(x => x.Asset)
            .Include(x => x.PmTemplate)
                .ThenInclude(t => t!.Questions)
                .ThenInclude(q => q.Section)
            .Where(x => (x.Status == "Active" || x.Status == PmContractGuard.HeldStatus) && x.NextPmDate.HasValue)
            .ToListAsync(ct);
        var held = 0;
        var resumed = 0;

        var generated = 0;

        foreach (var plan in activePlans)
        {
            try
            {
                // A schedule held because its contract lapsed resumes once a contract covers
                // the asset again; until then it raises nothing.
                if (plan.Status == PmContractGuard.HeldStatus)
                {
                    var asOf = plan.NextPmDate!.Value.Date > now.Date ? plan.NextPmDate.Value : now;
                    var recheck = await PmContractGuard.CheckAsync(db, plan, asOf, ct);
                    if (!recheck.MayGenerate)
                        continue;
                    PmContractGuard.Resume(plan, now);
                    resumed++;
                    logger.LogInformation("PM plan {PlanId} resumed: the asset is under contract again.", plan.Id);
                }

                // Auto-schedule off means "manual trigger only": nothing is raised by the scheduler.
                if (!plan.AutoSchedule || !ShouldGenerate(plan, now))
                    continue;

                // Work orders still require a client until the factory work order model (Phase 2)
                // lands; factory assets have none, so their plans wait rather than fail the save.
                if (plan.Asset is { ClientId: null })
                    continue;

                var check = await PmContractGuard.CheckAsync(db, plan, plan.NextPmDate!.Value, ct);
                if (!check.MayGenerate)
                {
                    if (PmContractGuard.Hold(plan, check.Contract!, plan.NextPmDate.Value, now))
                    {
                        held++;
                        await db.SaveChangesAsync(ct);
                        await PmContractGuard.NotifyHeldAsync(dispatcher, db, plan, check.Contract!, ct);
                        logger.LogInformation("PM plan {PlanId} on hold: contract {ContractNumber} lapsed.", plan.Id, check.Contract!.ContractNumber);
                    }
                    continue;
                }

                var hasOpenWo = await db.WorkOrders.AnyAsync(
                    x => x.PreventiveMaintenancePlanId == plan.Id &&
                         x.Status != "Completed" && x.Status != "Cancelled",
                    ct);

                if (hasOpenWo)
                    continue;

                var wo = await CreateWorkOrderAsync(db, plan, now, ct);
                if (check.Outcome == PmContractOutcome.ContinuingWithoutContract)
                {
                    // Kept going without a contract: the client pays for the visit.
                    wo.Title = $"PM (chargeable) – {plan.Asset?.AssetName ?? "Asset"}";
                    wo.Description = $"Not under contract (contract {check.Contract!.ContractNumber} has lapsed): chargeable at the client's standard rates.";
                    if (contractOperationalService is not null)
                        await contractOperationalService.ApplyOperationalSnapshotAsync(wo, ct);
                }
                plan.LastGeneratedAt = now;
                generated++;

                logger.LogInformation(
                    "Generated PM work order {WoNumber} for plan {PlanId} on asset {AssetId}.",
                    wo.WorkOrderNumber, plan.Id, plan.AssetId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate PM work order for plan {PlanId}.", plan.Id);
            }
        }

        if (generated > 0 || held > 0 || resumed > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("PM scheduler cycle complete. Generated {Count} work orders; held {Held} and resumed {Resumed} schedules.", generated, held, resumed);
        }

        return (generated, held, resumed);
    }

    /// <summary>
    /// The event key (outbox, subject prefix, people's notification settings) and the template it
    /// renders. They differ: the built-in overdue template has always been registered under the
    /// hyphenated key, which is also the key a tenant's edited copy is saved under, and rendering by
    /// the event key threw "template not found" for every work order, so no overdue email ever went.
    /// </summary>
    internal const string OverdueEventKey = "work-order.overdue";
    internal const string OverdueTemplateKey = "work-order-overdue";

    /// <summary>Emails each tenant's admins about open work orders past their due date, once a day per work order.</summary>
    internal async Task<int> RunOverdueNotificationsAsync(AppDbContext db, IServiceProvider services, DateTime now, CancellationToken ct)
    {
        var notified = 0;
        try
        {
            var emailOutbox = services.GetService<IEmailOutboxService>();
            var emailTemplate = services.GetService<IEmailTemplateService>();
            var emailSubjectRule = services.GetService<IEmailSubjectRuleService>();

            if (emailOutbox is null || emailTemplate is null || emailSubjectRule is null)
                return 0;

            // Find open WOs that are past due date.
            // Deduplication: skip any WO that already has an "OverdueNotified" event today.
            var todayUtc = now.Date;
            var alreadyNotifiedToday = await db.WorkOrderEvents
                .Where(x => x.EventType == "OverdueNotified" && x.OccurredAt.Date >= todayUtc)
                .Select(x => x.WorkOrderId)
                .Distinct()
                .ToListAsync(ct);

            var overdueWOs = await db.WorkOrders
                .Where(x => x.DueDate.HasValue
                    && x.DueDate.Value.Date < now.Date
                    && x.Status != "Completed" && x.Status != "Closed"
                    && x.Status != "Cancelled" && x.Status != "Acknowledged"
                    && !alreadyNotifiedToday.Contains(x.Id))
                .Include(x => x.AssignedTechnician)
                .Include(x => x.Asset)
                .ToListAsync(ct);

            foreach (var wo in overdueWOs)
            {
                try
                {
                    var optedOut = await db.UserNotificationPreferences
                        .Where(x => x.TenantId == wo.TenantId && x.NotificationKey == OverdueEventKey && !x.EmailEnabled)
                        .Select(x => x.UserId)
                        .ToListAsync(ct);
                    var admins = await db.Users
                        .Where(x => x.TenantId == wo.TenantId && x.IsActive
                            && x.Role == AppRoles.Admin && x.Email != null && x.HasEmail
                            && !optedOut.Contains(x.Id))
                        .Select(x => new { x.Email, x.FullName })
                        .ToListAsync(ct);

                    if (admins.Count == 0)
                        continue;

                    var template = await emailTemplate.RenderTenantTemplateAsync(
                        wo.TenantId,
                        OverdueTemplateKey,
                        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["WorkOrderNumber"] = wo.WorkOrderNumber,
                            ["AssignedTo"] = wo.AssignedTechnician?.FullName ?? "Unassigned",
                            ["Priority"] = wo.Priority ?? "Normal",
                            ["DueDate"] = wo.DueDate?.ToString("yyyy-MM-dd") ?? "Not set",
                            ["AssetName"] = wo.Asset?.AssetName ?? "N/A"
                        },
                        ct);

                    if (!template.Enabled)
                        continue;

                    var subject = await emailSubjectRule.BuildFinalSubjectAsync(
                        wo.TenantId, OverdueEventKey, template.Subject, cancellationToken: ct);

                    foreach (var admin in admins)
                    {
                        await emailOutbox.QueueEmailAsync(
                            new QueueEmailRequest(
                                wo.TenantId, OverdueEventKey, OverdueTemplateKey,
                                admin.Email!, admin.FullName,
                                template.SenderNameOverride, null, null,
                                subject, template.HtmlBody, template.TextBody, null),
                            ct);
                    }

                    // Record a WorkOrderEvent so we don't re-send within the same day
                    db.WorkOrderEvents.Add(new WorkOrderEvent
                    {
                        TenantId = wo.TenantId,
                        WorkOrderId = wo.Id,
                        EventType = "OverdueNotified",
                        Status = wo.Status,
                        Message = "Overdue notification email queued.",
                        OccurredAt = now
                    });
                    notified++;
                    logger.LogInformation("Queued overdue notification for work order {WoNumber}.", wo.WorkOrderNumber);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to queue overdue notification for work order {WorkOrderId}.", wo.Id);
                }
            }

            if (notified > 0)
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Overdue notification sweep complete. Notified for {Count} work orders.", notified);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Overdue notification sweep failed.");
        }

        return notified;
    }

    private static bool ShouldGenerate(PreventiveMaintenancePlan plan, DateTime now)
    {
        if (!plan.NextPmDate.HasValue)
            return false;

        var generateByDate = plan.NextPmDate.Value.AddDays(-plan.DaysBeforeDue);
        return now.Date >= generateByDate.Date;
    }

    private static async Task<WorkOrder> CreateWorkOrderAsync(
        AppDbContext db,
        PreventiveMaintenancePlan plan,
        DateTime now,
        CancellationToken ct)
    {
        var woNumber = await GenerateWoNumberAsync(db, plan.TenantId, ct);

        var wo = new WorkOrder
        {
            TenantId = plan.TenantId,
            BranchId = plan.BranchId,
            ClientId = plan.Asset?.ClientId ?? Guid.Empty,
            SiteId = plan.SiteId,
            AssetId = plan.AssetId,
            AssignmentGroupId = plan.DefaultAssignmentGroupId,
            WorkOrderNumber = woNumber,
            Title = $"PM – {plan.Asset?.AssetName ?? "Asset"}",
            Priority = "Medium",
            Status = "Open",
            IsPreventiveMaintenance = true,
            PreventiveMaintenancePlanId = plan.Id,
            PmTemplateId = plan.PmTemplateId,
            PmTemplateNameSnapshot = plan.PmTemplate?.Name,
            PmTemplateVersionSnapshot = plan.PmTemplate?.Version,
            DueDate = plan.NextPmDate
        };
        db.WorkOrders.Add(wo);

        if (plan.PmTemplate?.Questions is { Count: > 0 } questions)
        {
            foreach (var q in questions.OrderBy(x => x.Section?.DisplayOrder ?? int.MaxValue).ThenBy(x => x.DisplayOrder))
            {
                db.WorkOrderChecklistItems.Add(new WorkOrderChecklistItem
                {
                    TenantId = plan.TenantId,
                    WorkOrderId = wo.Id,
                    PmTemplateQuestionId = q.Id,
                    SectionName = q.SectionName,
                    SectionOrder = q.Section?.DisplayOrder ?? 0,
                    QuestionText = q.Prompt,
                    HelperText = q.HelperText,
                    InputType = q.ResponseType,
                    QuestionType = q.ResponseType,
                    Unit = q.Unit,
                    DefaultValue = q.DefaultValue,
                    MinValue = q.MinValue,
                    MaxValue = q.MaxValue,
                    IsRequired = q.IsRequired,
                    EvidenceRequired = q.EvidenceRequired,
                    TechnicianRemarksAllowed = q.TechnicianRemarksAllowed,
                    RequiresNoteOnFail = q.RequiresNoteOnFail,
                    SortOrder = q.DisplayOrder,
                    ResponseValue = q.DefaultValue,
                    OptionsJson = q.OptionsJson
                });
            }
        }

        return wo;
    }

    private static async Task<string> GenerateWoNumberAsync(AppDbContext db, Guid tenantId, CancellationToken ct)
    {
        var setting = await db.NumberingSettings
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.BranchId == null && x.DocumentType == "WorkOrder", ct);

        var prefix = setting?.Prefix ?? "WO";
        var next = setting?.NextNumber ?? 1;
        var pad = setting?.PaddingLength ?? 6;

        if (setting != null)
            setting.NextNumber += 1;

        return $"{prefix}-{next.ToString().PadLeft(pad, '0')}";
    }
}
