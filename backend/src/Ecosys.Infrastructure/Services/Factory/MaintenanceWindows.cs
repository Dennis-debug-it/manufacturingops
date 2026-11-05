using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

public sealed record WindowHandbackResult(int Done, int CarriedOver, int ToOverhaul, Guid? NextWindowId);

public interface IMaintenanceWindowService
{
    /// <summary>
    /// Makes sure every factory with a window has its next two windows, and queues open line-stop
    /// jobs into the earliest open one. Safe to call repeatedly.
    /// </summary>
    Task EnsureUpcomingAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task AddJobAsync(MaintenanceWindow window, Guid workOrderId, CancellationToken cancellationToken = default);
    Task RemoveJobAsync(MaintenanceWindow window, Guid workOrderId, CancellationToken cancellationToken = default);
    Task MoveAsync(MaintenanceWindow window, DateTime newDate, TimeOnly? start, TimeOnly? end, CancellationToken cancellationToken = default);
    Task SkipAsync(MaintenanceWindow window, FactoryActor actor, string reason, CancellationToken cancellationToken = default);
    Task PackAsync(MaintenanceWindow window, FactoryActor actor, CancellationToken cancellationToken = default);
    Task StartAsync(MaintenanceWindow window, FactoryActor actor, CancellationToken cancellationToken = default);
    Task SetHandbackItemAsync(MaintenanceWindow window, Guid itemId, bool done, string? note, FactoryActor actor, CancellationToken cancellationToken = default);

    /// <summary>Hands the line back: finished jobs are closed off in the window, unfinished ones carry over (and escalate, or go to the overhaul list).</summary>
    Task<WindowHandbackResult> HandBackAsync(MaintenanceWindow window, FactoryActor actor, string? notes, string? signatureDataUrl, CancellationToken cancellationToken = default);

    /// <summary>Technician hours the window has: its own figure, or its length × active technicians.</summary>
    Task<decimal> CapacityAsync(MaintenanceWindow window, CancellationToken cancellationToken = default);

    /// <summary>The factory an org node belongs to (itself when it is a factory); null above factory level.</summary>
    Task<Guid?> FactoryOfAsync(Guid tenantId, Guid orgNodeId, CancellationToken cancellationToken = default);
}

internal sealed class MaintenanceWindowService(
    AppDbContext dbContext,
    IFactorySettingsService factorySettingsService,
    INotificationDispatcher? notificationDispatcher = null) : IMaintenanceWindowService
{
    public const string CarryoverKey = "factory-window.carryover";

    /// <summary>The start-up checks every window ends with before the line goes back to production.</summary>
    internal static readonly IReadOnlyList<string> StandardHandback =
    [
        "All isolations removed; locks and tags returned (LOTO cleared)",
        "Guards and covers refitted on every machine worked on",
        "Tools, rags and loose parts cleared from machines and conveyors",
        "Work areas cleaned; oil and spills wiped up",
        "Trial run of the line without leaf: no abnormal noise, vibration or heat",
        "Dryer and boiler brought up to temperature and pressure normally",
        "Production supervisor told the line is ready and accepts it",
    ];

    private static readonly string[] Finished =
        [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    private static readonly string[] OpenWindow =
        [MaintenanceWindowStatuses.Planned, MaintenanceWindowStatuses.Packed, MaintenanceWindowStatuses.InProgress];

    public async Task EnsureUpcomingAsync(Guid tenantId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var settings = await factorySettingsService.GetOrCreateAsync(tenantId, cancellationToken);
        var zone = await ZoneAsync(tenantId, cancellationToken);
        var factories = await dbContext.OrgNodes
            .Where(x => x.TenantId == tenantId && x.IsActive && x.Level == OrgNodeLevels.Factory)
            .ToListAsync(cancellationToken);

        foreach (var factory in factories)
        {
            if (!(factory.WindowEnabled ?? settings.WeeklyWindowEnabled))
            {
                continue;
            }

            var day = factory.WindowDay ?? settings.WeeklyWindowDay;
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone);
            // The factory's local date, stored as a UTC-kind date (PostgreSQL only takes UTC).
            var first = DateTime.SpecifyKind(localNow.Date.AddDays(((int)day - (int)localNow.DayOfWeek + 7) % 7), DateTimeKind.Utc);
            if (first == localNow.Date && localNow.TimeOfDay >= settings.WeeklyWindowEnd.ToTimeSpan() && settings.WeeklyWindowEnd > settings.WeeklyWindowStart)
            {
                first = first.AddDays(7);
            }

            var existing = await dbContext.MaintenanceWindows
                .Where(x => x.TenantId == tenantId && x.FactoryId == factory.Id && x.WindowDate >= first.AddDays(-7))
                .Select(x => new { x.WindowDate, x.MovedFromDate, x.Status })
                .ToListAsync(cancellationToken);
            var added = new List<DateTime>();
            foreach (var date in new[] { first, first.AddDays(7) })
            {
                if (existing.Any(x => x.WindowDate == date || x.MovedFromDate == date))
                {
                    continue;
                }

                dbContext.MaintenanceWindows.Add(NewWindow(tenantId, factory.Id, date, settings.WeeklyWindowStart, settings.WeeklyWindowEnd, zone));
                added.Add(date);
            }

            // Always one open window ahead, so carried-over and skipped work has somewhere to go
            // even when the next ones were already run early or skipped.
            if (added.Count == 0 && !existing.Any(x => x.WindowDate >= first && MaintenanceWindowStatuses.IsOpen(x.Status)))
            {
                var latest = existing.Where(x => x.WindowDate >= first).Select(x => x.MovedFromDate is { } moved && moved > x.WindowDate ? moved : x.WindowDate).DefaultIfEmpty(first).Max();
                var date = first;
                while (date <= latest || existing.Any(x => x.WindowDate == date || x.MovedFromDate == date))
                {
                    date = date.AddDays(7);
                }

                dbContext.MaintenanceWindows.Add(NewWindow(tenantId, factory.Id, date, settings.WeeklyWindowStart, settings.WeeklyWindowEnd, zone));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await AutoQueueAsync(tenantId, cancellationToken);
    }

    private MaintenanceWindow NewWindow(Guid tenantId, Guid factoryId, DateTime date, TimeOnly start, TimeOnly end, TimeZoneInfo zone)
    {
        var (startsAt, endsAt) = Span(date, start, end, zone);
        var window = new MaintenanceWindow { TenantId = tenantId, FactoryId = factoryId, WindowDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc), StartsAt = startsAt, EndsAt = endsAt };
        foreach (var (prompt, index) in StandardHandback.Select((p, i) => (p, i)))
        {
            window.HandbackItems.Add(new MaintenanceWindowHandbackItem { TenantId = tenantId, WindowId = window.Id, Prompt = prompt, Sequence = index });
        }

        return window;
    }

    private static (DateTime Start, DateTime End) Span(DateTime localDate, TimeOnly start, TimeOnly end, TimeZoneInfo zone)
    {
        var localStart = DateTime.SpecifyKind(localDate.Date + start.ToTimeSpan(), DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(localDate.Date + end.ToTimeSpan(), DateTimeKind.Unspecified);
        if (localEnd <= localStart) localEnd = localEnd.AddDays(1);
        return (TimeZoneInfo.ConvertTimeToUtc(localStart, zone), TimeZoneInfo.ConvertTimeToUtc(localEnd, zone));
    }

    private async Task<TimeZoneInfo> ZoneAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var id = await dbContext.Tenants.Where(x => x.Id == tenantId).Select(x => x.TimeZoneId).SingleOrDefaultAsync(cancellationToken);
        try
        {
            return string.IsNullOrWhiteSpace(id) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>Open line-stop jobs not yet in an open window, and not on the overhaul list, go into their factory's earliest open window.</summary>
    private async Task AutoQueueAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var windows = await dbContext.MaintenanceWindows
            .Where(x => x.TenantId == tenantId && (x.Status == MaintenanceWindowStatuses.Planned || x.Status == MaintenanceWindowStatuses.Packed))
            .OrderBy(x => x.WindowDate)
            .ToListAsync(cancellationToken);
        if (windows.Count == 0)
        {
            return;
        }

        var queued = await dbContext.MaintenanceWindowJobs
            .Where(x => x.TenantId == tenantId && OpenWindow.Contains(x.Window!.Status))
            .Select(x => x.WorkOrderId).ToListAsync(cancellationToken);
        var onOverhaul = await dbContext.OverhaulItems
            .Where(x => x.TenantId == tenantId && x.WorkOrderId != null && (x.Status == OverhaulItemStatuses.Listed || x.Status == OverhaulItemStatuses.Scoped || x.Status == OverhaulItemStatuses.InProgress))
            .Select(x => x.WorkOrderId!.Value).ToListAsync(cancellationToken);
        var exclude = queued.Concat(onOverhaul).ToHashSet();

        var candidates = await dbContext.WorkOrders
            .Where(x => x.TenantId == tenantId && x.ClientId == null && x.NeedsLineStop && x.OrgNodeId != null
                && x.Status != WorkOrderStatuses.Requested && !Finished.Contains(x.Status))
            .Select(x => new { x.Id, OrgNodeId = x.OrgNodeId!.Value })
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return;
        }

        var factoryOf = await FactoryMapAsync(tenantId, cancellationToken);
        var next = windows.GroupBy(x => x.FactoryId).ToDictionary(g => g.Key, g => g.First());
        var counts = await dbContext.MaintenanceWindowJobs.Where(x => windows.Select(w => w.Id).Contains(x.WindowId))
            .GroupBy(x => x.WindowId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        foreach (var job in candidates.Where(x => !exclude.Contains(x.Id)))
        {
            if (!factoryOf.TryGetValue(job.OrgNodeId, out var factoryId) || !next.TryGetValue(factoryId, out var window))
            {
                continue;
            }

            var sequence = counts.GetValueOrDefault(window.Id);
            counts[window.Id] = sequence + 1;
            dbContext.MaintenanceWindowJobs.Add(new MaintenanceWindowJob { TenantId = tenantId, WindowId = window.Id, WorkOrderId = job.Id, AddedAutomatically = true, Sequence = sequence });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, Guid>> FactoryMapAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var nodes = await dbContext.OrgNodes.AsNoTracking().Where(x => x.TenantId == tenantId)
            .Select(x => new { x.Id, x.ParentId, x.Level }).ToDictionaryAsync(x => x.Id, cancellationToken);
        var map = new Dictionary<Guid, Guid>();
        foreach (var node in nodes.Values)
        {
            var current = node;
            for (var guard = 0; current is not null && guard < 20; guard++)
            {
                if (current.Level == OrgNodeLevels.Factory)
                {
                    map[node.Id] = current.Id;
                    break;
                }

                current = current.ParentId is { } parent ? nodes.GetValueOrDefault(parent) : null;
            }
        }

        return map;
    }

    public async Task<Guid?> FactoryOfAsync(Guid tenantId, Guid orgNodeId, CancellationToken cancellationToken = default) =>
        (await FactoryMapAsync(tenantId, cancellationToken)).TryGetValue(orgNodeId, out var factory) ? factory : null;

    public async Task AddJobAsync(MaintenanceWindow window, Guid workOrderId, CancellationToken cancellationToken = default)
    {
        EnsureEditable(window);
        var job = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.TenantId == window.TenantId && x.Id == workOrderId && x.ClientId == null, cancellationToken)
            ?? throw new BusinessRuleException("The job was not found.");
        if (Finished.Contains(job.Status))
        {
            throw new BusinessRuleException("The job is already finished.");
        }

        if (job.OrgNodeId is not { } node || await FactoryOfAsync(window.TenantId, node, cancellationToken) != window.FactoryId)
        {
            throw new BusinessRuleException("The job is in another factory.");
        }

        var elsewhere = await dbContext.MaintenanceWindowJobs
            .Where(x => x.WorkOrderId == workOrderId && OpenWindow.Contains(x.Window!.Status))
            .ToListAsync(cancellationToken);
        if (elsewhere.Any(x => x.WindowId == window.Id))
        {
            return;
        }

        // A job sits in one open window at a time: adding it here takes it out of the other one.
        dbContext.MaintenanceWindowJobs.RemoveRange(elsewhere);
        var sequence = await dbContext.MaintenanceWindowJobs.CountAsync(x => x.WindowId == window.Id, cancellationToken);
        dbContext.MaintenanceWindowJobs.Add(new MaintenanceWindowJob { TenantId = window.TenantId, WindowId = window.Id, WorkOrderId = workOrderId, Sequence = sequence });
        job.NeedsLineStop = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveJobAsync(MaintenanceWindow window, Guid workOrderId, CancellationToken cancellationToken = default)
    {
        EnsureEditable(window);
        var entry = await dbContext.MaintenanceWindowJobs.SingleOrDefaultAsync(x => x.WindowId == window.Id && x.WorkOrderId == workOrderId, cancellationToken)
            ?? throw new NotFoundException("The job is not in this window.");
        dbContext.MaintenanceWindowJobs.Remove(entry);
        // Taken out on purpose: it no longer needs a line stop, or it would only be queued again.
        var job = await dbContext.WorkOrders.SingleAsync(x => x.Id == workOrderId, cancellationToken);
        job.NeedsLineStop = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveAsync(MaintenanceWindow window, DateTime newDate, TimeOnly? start, TimeOnly? end, CancellationToken cancellationToken = default)
    {
        EnsureEditable(window);
        var zone = await ZoneAsync(window.TenantId, cancellationToken);
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
        var date = DateTime.SpecifyKind(newDate.Date, DateTimeKind.Utc);
        if (date < localToday)
        {
            throw new BusinessRuleException("A window can't be moved into the past.");
        }

        if (date != window.WindowDate && await dbContext.MaintenanceWindows.AnyAsync(x => x.TenantId == window.TenantId && x.FactoryId == window.FactoryId && x.WindowDate == date && x.Id != window.Id, cancellationToken))
        {
            throw new BusinessRuleException("This factory already has a window that day.");
        }

        if (date != window.WindowDate)
        {
            window.MovedFromDate ??= window.WindowDate;
            window.WindowDate = date;
        }

        var localStart = TimeOnly.FromTimeSpan(TimeZoneInfo.ConvertTimeFromUtc(window.StartsAt, zone).TimeOfDay);
        var localEnd = TimeOnly.FromTimeSpan(TimeZoneInfo.ConvertTimeFromUtc(window.EndsAt, zone).TimeOfDay);
        (window.StartsAt, window.EndsAt) = Span(date, start ?? localStart, end ?? localEnd, zone);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SkipAsync(MaintenanceWindow window, FactoryActor actor, string reason, CancellationToken cancellationToken = default)
    {
        EnsureEditable(window);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessRuleException("Say why there is no window this week.");
        }

        window.Status = MaintenanceWindowStatuses.Skipped;
        window.SkipReason = reason.Trim();
        // Its jobs go to the next window; a skipped week is not counted against them as a carryover.
        dbContext.MaintenanceWindowJobs.RemoveRange(await dbContext.MaintenanceWindowJobs.Where(x => x.WindowId == window.Id).ToListAsync(cancellationToken));
        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureUpcomingAsync(window.TenantId, DateTime.UtcNow, cancellationToken);
    }

    public async Task PackAsync(MaintenanceWindow window, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (window.Status != MaintenanceWindowStatuses.Planned && window.Status != MaintenanceWindowStatuses.Packed)
        {
            throw new BusinessRuleException("Only a planned window can be packed.");
        }

        window.Status = MaintenanceWindowStatuses.Packed;
        window.PackedAt = OccurredAt.UtcNow;
        window.PackedByName = actor.Name;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task StartAsync(MaintenanceWindow window, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (window.Status is not (MaintenanceWindowStatuses.Planned or MaintenanceWindowStatuses.Packed))
        {
            throw new BusinessRuleException("This window can't be started.");
        }

        window.Status = MaintenanceWindowStatuses.InProgress;
        window.StartedAt = OccurredAt.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetHandbackItemAsync(MaintenanceWindow window, Guid itemId, bool done, string? note, FactoryActor actor, CancellationToken cancellationToken = default)
    {
        if (window.Status is not (MaintenanceWindowStatuses.InProgress or MaintenanceWindowStatuses.Packed or MaintenanceWindowStatuses.Planned))
        {
            throw new BusinessRuleException("This window is closed.");
        }

        var item = await dbContext.MaintenanceWindowHandbackItems.SingleOrDefaultAsync(x => x.WindowId == window.Id && x.Id == itemId, cancellationToken)
            ?? throw new NotFoundException("The check was not found.");
        item.Done = done;
        item.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        item.DoneByName = done ? actor.Name : null;
        item.DoneAt = done ? OccurredAt.UtcNow : null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WindowHandbackResult> HandBackAsync(MaintenanceWindow window, FactoryActor actor, string? notes, string? signatureDataUrl, CancellationToken cancellationToken = default)
    {
        if (window.Status != MaintenanceWindowStatuses.InProgress)
        {
            throw new BusinessRuleException("Start the window before handing it back.");
        }

        var left = await dbContext.MaintenanceWindowHandbackItems.CountAsync(x => x.WindowId == window.Id && !x.Done, cancellationToken);
        if (left > 0)
        {
            throw new BusinessRuleException($"Finish the start-up checks first ({left} left).");
        }

        if (signatureDataUrl is not null && (!signatureDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || signatureDataUrl.Length > 500_000))
        {
            throw new BusinessRuleException("The signature could not be read. Sign again.");
        }

        var settings = await factorySettingsService.GetOrCreateAsync(window.TenantId, cancellationToken);
        var entries = await dbContext.MaintenanceWindowJobs.Include(x => x.WorkOrder).ThenInclude(x => x!.Asset)
            .Where(x => x.WindowId == window.Id).ToListAsync(cancellationToken);
        var now = OccurredAt.UtcNow;
        window.Status = MaintenanceWindowStatuses.HandedBack;
        window.HandedBackAt = now;
        window.HandedBackByName = actor.Name;
        window.HandbackNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        window.HandbackSignatureDataUrl = signatureDataUrl;

        var escalate = new List<string>();
        var toOverhaul = 0;
        var carried = new List<WorkOrder>();
        foreach (var entry in entries)
        {
            var job = entry.WorkOrder!;
            if (job.Status is WorkOrderStatuses.Cancelled or WorkOrderStatuses.Rejected)
            {
                entry.Outcome = WindowJobOutcomes.Cancelled;
                continue;
            }

            if (Finished.Contains(job.Status))
            {
                entry.Outcome = WindowJobOutcomes.Done;
                continue;
            }

            entry.Outcome = WindowJobOutcomes.CarriedOver;
            job.WindowCarryoverCount++;
            dbContext.WorkOrderEvents.Add(new WorkOrderEvent
            {
                TenantId = job.TenantId, WorkOrderId = job.Id, ActorUserId = actor.UserId, EventType = "WindowCarryover", Status = job.Status,
                Message = $"Not finished in the {window.WindowDate:dd MMM} window; carried over ({job.WindowCarryoverCount}×).", OccurredAt = now
            });

            if (job.WindowCarryoverCount >= settings.WindowCarryoverToOverhaulAt)
            {
                toOverhaul++;
                escalate.Add($"{job.WorkOrderNumber} {job.Title}: carried over {job.WindowCarryoverCount}×, moved to the overhaul list");
                if (!await dbContext.OverhaulItems.AnyAsync(x => x.WorkOrderId == job.Id && (x.Status == OverhaulItemStatuses.Listed || x.Status == OverhaulItemStatuses.Scoped || x.Status == OverhaulItemStatuses.InProgress), cancellationToken))
                {
                    dbContext.OverhaulItems.Add(new OverhaulItem
                    {
                        TenantId = job.TenantId,
                        FactoryId = window.FactoryId,
                        AssetId = job.AssetId,
                        WorkOrderId = job.Id,
                        Title = job.Title,
                        Description = $"Carried over {job.WindowCarryoverCount} weekly windows without being finished.",
                        Source = OverhaulItemSources.Carryover,
                        Priority = job.Priority,
                        EstimatedHours = job.EstimatedLabourHours,
                        AddedByName = actor.Name,
                    });
                }
            }
            else
            {
                if (job.WindowCarryoverCount >= settings.WindowCarryoverEscalateAt)
                {
                    escalate.Add($"{job.WorkOrderNumber} {job.Title}: carried over {job.WindowCarryoverCount}×");
                }

                carried.Add(job);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureUpcomingAsync(window.TenantId, now, cancellationToken);
        var nextWindow = await dbContext.MaintenanceWindows
            .Where(x => x.TenantId == window.TenantId && x.FactoryId == window.FactoryId && x.Id != window.Id
                && (x.Status == MaintenanceWindowStatuses.Planned || x.Status == MaintenanceWindowStatuses.Packed))
            .OrderBy(x => x.WindowDate).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);

        await NotifyAsync(window, escalate, cancellationToken);
        return new WindowHandbackResult(entries.Count(x => x.Outcome == WindowJobOutcomes.Done), carried.Count, toOverhaul, nextWindow);
    }

    private async Task NotifyAsync(MaintenanceWindow window, IReadOnlyList<string> lines, CancellationToken cancellationToken)
    {
        if (notificationDispatcher is null || lines.Count == 0)
        {
            return;
        }

        var recipients = await FactoryRecipients.SupervisorsAsync(dbContext, window.TenantId, window.FactoryId, cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        var place = await dbContext.OrgNodes.Where(x => x.Id == window.FactoryId).Select(x => x.Name).SingleAsync(cancellationToken);
        await notificationDispatcher.NotifyAsync(new NotificationRequest(
            window.TenantId,
            CarryoverKey,
            recipients,
            $"Window jobs carried over in {place}",
            string.Join("; ", lines.Take(5)) + (lines.Count > 5 ? $" and {lines.Count - 5} more" : string.Empty),
            $"/maintenance/windows/{window.Id}",
            nameof(MaintenanceWindow),
            window.Id,
            new Dictionary<string, string?> { ["Place"] = place, ["Date"] = window.WindowDate.ToString("dd MMM yyyy"), ["Jobs"] = string.Join("\n", lines) },
            NotificationSeverities.Warning), cancellationToken);
    }

    public async Task<decimal> CapacityAsync(MaintenanceWindow window, CancellationToken cancellationToken = default)
    {
        if (window.CapacityHours is { } set)
        {
            return set;
        }

        var technicians = await dbContext.Technicians.CountAsync(x => x.TenantId == window.TenantId && x.IsActive, cancellationToken);
        return Math.Round((decimal)(window.EndsAt - window.StartsAt).TotalHours * technicians, 1);
    }

    private static void EnsureEditable(MaintenanceWindow window)
    {
        if (window.Status is not (MaintenanceWindowStatuses.Planned or MaintenanceWindowStatuses.Packed))
        {
            throw new BusinessRuleException("Only a window that hasn't started can be changed.");
        }
    }
}
