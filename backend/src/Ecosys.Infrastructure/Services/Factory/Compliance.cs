using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services.Factory;

public static class ComplianceStatuses
{
    public const string Ok = "Ok";
    public const string DueSoon = "DueSoon";
    public const string Overdue = "Overdue";
    /// <summary>No due date yet: the last inspection date needs entering.</summary>
    public const string NoDate = "NoDate";

    public const int DueSoonDays = 30;

    public static string Of(DateTime? dueDate, DateTime today) => dueDate is not { } due
        ? NoDate
        : due.Date < today.Date ? Overdue : (due.Date - today.Date).TotalDays <= DueSoonDays ? DueSoon : Ok;
}

public sealed record ComplianceTemplate(string Key, string Name, string Kind, string Regulation, int IntervalMonths, IReadOnlyList<string>? AssetClassCodes, int? RaiseJobDaysBefore = 30);

/// <summary>
/// The statutory items a Kenyan tea factory tracks (scope Phase 3). Intervals are the usual
/// legal or recommended ones; each factory checks them against its own certificates.
/// </summary>
public static class ComplianceTemplates
{
    public static readonly IReadOnlyList<ComplianceTemplate> TeaKenya =
    [
        new("boiler-exam", "Boiler examination by an approved person", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.67 (DOSH)", 14, ["TEA-BLR"]),
        new("boiler-hydro", "Boiler hydraulic test", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.67 (DOSH)", 24, ["TEA-BLR"]),
        new("air-receiver", "Air receiver (pressure vessel) examination", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.69–70 (DOSH)", 24, ["TEA-CMP"]),
        new("lifting-appliances", "Lifting appliances examination (hoists, chain blocks, cranes)", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.63 (DOSH)", 12, null),
        new("lifting-tackle", "Chains, ropes and lifting tackle examination", ComplianceKinds.StatutoryInspection, "OSHA 2007 s.64 (DOSH)", 6, null),
        new("weighbridge", "Weighbridge verification", ComplianceKinds.Calibration, "Weights and Measures Act (Cap 513)", 12, ["TEA-WB"]),
        new("scales", "Scale verification and calibration", ComplianceKinds.Calibration, "Weights and Measures Act (Cap 513)", 12, ["TEA-SCL"]),
        new("metal-detector", "Metal detector calibration and test", ComplianceKinds.Calibration, "ISO 22000 / customer requirement", 6, null),
        new("fire-extinguishers", "Fire extinguishers service", ComplianceKinds.FireSafety, "Fire Risk Reduction Rules 2007", 12, null),
        new("fire-audit", "Fire safety audit", ComplianceKinds.FireSafety, "Fire Risk Reduction Rules 2007", 12, null),
        new("osh-audit", "Occupational safety and health audit", ComplianceKinds.Certificate, "OSHA 2007 s.11", 12, null),
        new("workplace-registration", "Workplace registration certificate", ComplianceKinds.Certificate, "OSHA 2007 s.44", 12, null, null),
        new("medical-exam", "Medical examination of exposed workers", ComplianceKinds.Certificate, "Medical Examination Rules 2005", 12, null, null),
    ];
}

public sealed record ComplianceRecordInput(DateTime DoneAt, string Result, DateTime? ValidUntil, string? CertificateNumber, string? Examiner, string? Notes, Guid? CertificateAttachmentId);

public interface IComplianceService
{
    /// <summary>Records an inspection, calibration or renewal and moves the item's due date on (a fail makes it due now).</summary>
    Task<ComplianceRecord> RecordAsync(ComplianceItem item, ComplianceRecordInput input, FactoryActor actor, CancellationToken cancellationToken = default);

    /// <summary>Adds a factory's statutory items from the template (those it doesn't have). Returns how many.</summary>
    Task<int> InstallAsync(Guid tenantId, Guid factoryId, CancellationToken cancellationToken = default);

    /// <summary>Raises the jobs that fall due and sends due-soon and overdue alerts. Returns (jobs raised, alerts sent).</summary>
    Task<(int Jobs, int Alerts)> RunAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default);
}

internal sealed class ComplianceService(
    AppDbContext dbContext,
    IFactoryWorkOrderService workOrderService,
    INotificationDispatcher? notificationDispatcher = null) : IComplianceService
{
    public const string DueKey = "compliance.due";
    public const string OverdueKey = "compliance.overdue";

    private static readonly string[] Finished =
        [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    public async Task<ComplianceRecord> RecordAsync(ComplianceItem item, ComplianceRecordInput input, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        var result = ComplianceResults.All.FirstOrDefault(x => string.Equals(x, input.Result, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("The result must be pass, conditional or fail.");
        var doneAt = DateTime.SpecifyKind(input.DoneAt.Date, DateTimeKind.Utc);
        if (doneAt > DateTime.UtcNow.Date.AddDays(1))
        {
            throw new BusinessRuleException("The date can't be in the future.");
        }

        if (input.ValidUntil is { } until && until.Date <= doneAt)
        {
            throw new BusinessRuleException("The certificate must be valid after the date it was done.");
        }

        var record = new ComplianceRecord
        {
            TenantId = item.TenantId,
            ItemId = item.Id,
            DoneAt = doneAt,
            Result = result,
            CertificateNumber = Clean(input.CertificateNumber),
            Examiner = Clean(input.Examiner),
            Notes = Clean(input.Notes),
            CertificateAttachmentId = input.CertificateAttachmentId,
            RecordedByName = actor.Name,
        };

        // A failed examination leaves the equipment uncertified: due now, until it passes.
        var newest = !await dbContext.ComplianceRecords.AnyAsync(x => x.ItemId == item.Id && x.DoneAt > doneAt, cancellationToken);
        if (newest)
        {
            item.LastDoneAt = doneAt;
            record.ValidUntil = result == ComplianceResults.Fail
                ? null
                : DateTime.SpecifyKind((input.ValidUntil?.Date ?? doneAt.AddMonths(item.IntervalMonths)), DateTimeKind.Utc);
            item.DueDate = record.ValidUntil ?? doneAt;
            item.LastDueAlertDays = null;
            item.LastOverdueAlertAt = null;
        }
        else
        {
            record.ValidUntil = input.ValidUntil;
        }

        if (item.OpenWorkOrderId is { } jobId)
        {
            record.WorkOrderId = jobId;
            if (result != ComplianceResults.Fail)
            {
                item.OpenWorkOrderId = null;
            }
        }

        dbContext.ComplianceRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        return record;
    }

    public async Task<int> InstallAsync(Guid tenantId, Guid factoryId, CancellationToken cancellationToken = default)
    {
        var factory = await dbContext.OrgNodes.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == factoryId && x.Level == OrgNodeLevels.Factory, cancellationToken)
            ?? throw new NotFoundException("The factory was not found.");
        var nodes = await dbContext.OrgNodes.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => new { x.Id, x.ParentId }).ToListAsync(cancellationToken);
        var subtree = new HashSet<Guid> { factory.Id };
        for (var grew = true; grew;)
        {
            grew = false;
            foreach (var node in nodes.Where(x => x.ParentId is { } p && subtree.Contains(p) && !subtree.Contains(x.Id)))
            {
                subtree.Add(node.Id);
                grew = true;
            }
        }

        var assets = await dbContext.Assets.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ClientId == null && x.OrgNodeId != null && subtree.Contains(x.OrgNodeId.Value) && x.Status != "Inactive"
                && x.AssetCategory != null && x.AssetCategory.Code != null)
            .Select(x => new { x.Id, x.OrgNodeId, x.AssetCode, x.AssetName, Code = x.AssetCategory!.Code! })
            .ToListAsync(cancellationToken);
        var existing = await dbContext.ComplianceItems
            .Where(x => x.TenantId == tenantId && x.TemplateKey != null && subtree.Contains(x.OrgNodeId))
            .Select(x => new { x.TemplateKey, x.AssetId }).ToListAsync(cancellationToken);

        var added = 0;
        foreach (var template in ComplianceTemplates.TeaKenya)
        {
            if (template.AssetClassCodes is null)
            {
                if (existing.Any(x => x.TemplateKey == template.Key && x.AssetId == null)) continue;
                dbContext.ComplianceItems.Add(New(tenantId, factory.Id, null, template.Name, template));
                added++;
                continue;
            }

            foreach (var asset in assets.Where(x => template.AssetClassCodes.Contains(x.Code)))
            {
                if (existing.Any(x => x.TemplateKey == template.Key && x.AssetId == asset.Id)) continue;
                dbContext.ComplianceItems.Add(New(tenantId, asset.OrgNodeId!.Value, asset.Id, $"{template.Name} – {asset.AssetCode}", template));
                added++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return added;
    }

    private static ComplianceItem New(Guid tenantId, Guid orgNodeId, Guid? assetId, string name, ComplianceTemplate template) => new()
    {
        TenantId = tenantId, OrgNodeId = orgNodeId, AssetId = assetId, Name = name, Kind = template.Kind, Regulation = template.Regulation,
        IntervalMonths = template.IntervalMonths, RaiseJobDaysBefore = template.RaiseJobDaysBefore, TemplateKey = template.Key,
    };

    public async Task<(int Jobs, int Alerts)> RunAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var today = utcNow.Date;
        var items = await dbContext.ComplianceItems.Include(x => x.OpenWorkOrder)
            .Where(x => x.TenantId == tenantId && x.IsActive && x.DueDate != null)
            .ToListAsync(cancellationToken);

        var jobs = 0;
        foreach (var item in items.Where(x => x.RaiseJobDaysBefore is { } lead && (x.DueDate!.Value.Date - today).TotalDays <= lead))
        {
            if (item.OpenWorkOrder is { } open && !Finished.Contains(open.Status))
            {
                continue;
            }

            var job = await workOrderService.CreateAsync(tenantId, new FactoryActor(null, "Compliance"), new CreateFactoryWorkOrder(
                $"Statutory: {item.Name}", FactoryWorkTypes.Statutory, item.DueDate!.Value.Date < today ? "High" : "Medium",
                AssetId: item.AssetId, OrgNodeId: item.AssetId is null ? item.OrgNodeId : null,
                Description: $"Due {item.DueDate:dd MMM yyyy}. {item.Regulation}{(item.Provider is null ? string.Empty : $" Provider: {item.Provider}.")} Record the result and certificate on the compliance item.",
                DueDate: item.DueDate, RequestedByName: "Compliance"), cancellationToken);
            if (job.Status == WorkOrderStatuses.Requested)
            {
                await workOrderService.ApplyAsync(job, new FactoryActor(null, "Compliance"), new FactoryTransition(FactoryWorkOrderActions.Approve, "Statutory requirement."), cancellationToken);
            }

            item.OpenWorkOrderId = job.Id;
            jobs++;
        }

        var due = new List<ComplianceItem>();
        var overdue = new List<ComplianceItem>();
        foreach (var item in items)
        {
            var daysLeft = (int)(item.DueDate!.Value.Date - today).TotalDays;
            if (daysLeft < 0)
            {
                if (item.LastOverdueAlertAt is null || item.LastOverdueAlertAt <= utcNow.AddDays(-7))
                {
                    item.LastOverdueAlertAt = utcNow;
                    overdue.Add(item);
                }

                continue;
            }

            int? step = daysLeft <= 7 ? 7 : daysLeft <= 14 ? 14 : daysLeft <= 30 ? 30 : null;
            if (step is { } s && (item.LastDueAlertDays is null || s < item.LastDueAlertDays))
            {
                item.LastDueAlertDays = s;
                due.Add(item);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var alerts = await NotifyAsync(tenantId, DueKey, due, today, cancellationToken) + await NotifyAsync(tenantId, OverdueKey, overdue, today, cancellationToken);
        return (jobs, alerts);
    }

    private async Task<int> NotifyAsync(Guid tenantId, string key, IReadOnlyList<ComplianceItem> items, DateTime today, CancellationToken cancellationToken)
    {
        if (notificationDispatcher is null || items.Count == 0)
        {
            return 0;
        }

        var sent = 0;
        foreach (var group in items.GroupBy(x => x.OrgNodeId))
        {
            var recipients = (await FactoryRecipients.SupervisorsAsync(dbContext, tenantId, group.Key, cancellationToken)).ToHashSet();
            foreach (var owner in group.Where(x => x.OwnerUserId != null).Select(x => x.OwnerUserId!.Value))
            {
                recipients.Add(owner);
            }

            if (recipients.Count == 0)
            {
                continue;
            }

            var lines = group.OrderBy(x => x.DueDate).Select(x => key == OverdueKey
                ? $"{x.Name}: due {x.DueDate:dd MMM yyyy} ({(today - x.DueDate!.Value.Date).TotalDays:0} days overdue)"
                : $"{x.Name}: due {x.DueDate:dd MMM yyyy}").ToList();
            await notificationDispatcher.NotifyAsync(new NotificationRequest(
                tenantId,
                key,
                recipients.ToList(),
                key == OverdueKey ? $"{lines.Count} statutory item{(lines.Count == 1 ? " is" : "s are")} overdue" : $"{lines.Count} statutory item{(lines.Count == 1 ? "" : "s")} due soon",
                string.Join("; ", lines.Take(5)) + (lines.Count > 5 ? $" and {lines.Count - 5} more" : string.Empty),
                key == OverdueKey ? "/maintenance/statutory?status=Overdue" : "/maintenance/statutory?status=DueSoon",
                nameof(ComplianceItem),
                group.Count() == 1 ? group.First().Id : null,
                new Dictionary<string, string?> { ["Count"] = lines.Count.ToString(), ["Items"] = string.Join("\n", lines) },
                key == OverdueKey ? NotificationSeverities.Critical : NotificationSeverities.Warning), cancellationToken);
            sent++;
        }

        return sent;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Raises statutory jobs and sends compliance alerts every 6 hours.</summary>
public sealed class ComplianceWorker(IServiceScopeFactory scopeFactory, ILogger<ComplianceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<IComplianceService>();
                var tenants = await db.ComplianceItems.AsNoTracking().Where(x => x.IsActive && x.Tenant!.IsActive).Select(x => x.TenantId).Distinct().ToListAsync(stoppingToken);
                foreach (var tenantId in tenants)
                {
                    try
                    {
                        var (jobs, alerts) = await service.RunAsync(tenantId, DateTime.UtcNow, stoppingToken);
                        if (jobs + alerts > 0)
                        {
                            logger.LogInformation("Compliance for tenant {TenantId}: {Jobs} job(s) raised, {Alerts} alert(s) sent.", tenantId, jobs, alerts);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Compliance run for tenant {TenantId} failed.", tenantId);
                        db.ChangeTracker.Clear();
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Compliance cycle failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
