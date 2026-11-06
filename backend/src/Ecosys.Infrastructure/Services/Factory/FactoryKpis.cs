using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services.Factory;

/// <summary>One cost on a job, in the tenant currency, with where it belongs.</summary>
public sealed record CostLine(
    Guid WorkOrderId, string WorkOrderNumber, string Title, Guid? AssetId, string? AssetCode, string? AssetName,
    Guid? OrgNodeId, Guid? FactoryId, string? FactoryName, Guid? SectionId, string? SectionName, Guid? CostCentreId, string? CostCentreName,
    DateTime At, string Category, decimal Amount, decimal DowntimeHours = 0, decimal ProductionLost = 0);

public sealed record NamedAmount(Guid? Id, string Name, decimal Amount);
public sealed record CostBreakdown(Guid? Id, string Name, decimal Labour, decimal Parts, decimal Contractor, decimal Total, decimal DowntimeHours, decimal ProductionLost, decimal DowntimeValue);

public sealed record AssetKpis(int Total, int Operational, int Standby, int Down, IReadOnlyDictionary<string, int> ByCriticality);
public sealed record FailingAsset(Guid AssetId, string AssetCode, string AssetName, int Breakdowns, decimal DowntimeHours);
public sealed record ReliabilityKpis(int Breakdowns, decimal? MtbfHours, decimal? MttrHours, int RepeatFailures, IReadOnlyCollection<FailingAsset> TopFailing);
public sealed record PlanningKpis(decimal? PmCompliancePercent, int PmDue, int PmOnTime, int OverdueJobs, decimal BacklogHours, decimal? BacklogWeeks, int PlannedJobs, int ReactiveJobs, decimal? PlannedPercent);
public sealed record WindowKpis(int Windows, int JobsPlanned, int JobsDone, decimal? CompletionPercent, int CarriedOver, int OldestCarryover, int BreakdownsOnDeferredAssets);
public sealed record OverhaulKpis(int Items, int Done, decimal? ScopeCompletePercent, decimal? Budget, decimal ActualCost, int Recommissioned, int AwaitingRecommissioning);
public sealed record DowntimeKpis(decimal Hours, decimal ProductionLost, decimal? ValueLost, decimal PeakHours, decimal LowHours, decimal PeakProductionLost, decimal LowProductionLost);
public sealed record BudgetKpis(decimal AnnualBudget, decimal BudgetForPeriod, decimal Actual, decimal? VariancePercent, IReadOnlyCollection<BudgetLineKpi> Lines);
public sealed record BudgetLineKpi(string Factory, string? CostCentre, string Category, decimal BudgetForPeriod, decimal Actual);
public sealed record CostKpis(decimal Labour, decimal Parts, decimal Contractor, decimal Total, decimal? DowntimeValue, decimal Production, decimal? CostPerUnit,
    IReadOnlyCollection<CostBreakdown> ByFactory, IReadOnlyCollection<CostBreakdown> BySection, IReadOnlyCollection<CostBreakdown> ByCostCentre, IReadOnlyCollection<CostBreakdown> TopAssets,
    IReadOnlyCollection<NamedAmount> ByMonth, BudgetKpis Budget);
public sealed record SparesKpis(decimal ConsumptionValue, int IssueLines, int CriticalStockOuts, int CriticalBelowMinimum, decimal StockValue);
public sealed record PeopleKpis(decimal LoggedHours, decimal OvertimeHours, decimal? OvertimePercent, decimal? UtilisationPercent, int JobsCompleted, int CertificatesExpiring, int CertificatesExpired);
public sealed record EnergyKpis(decimal EnergyKwh, decimal Fuel, string? FuelUnit, decimal Production, decimal? KwhPerUnit, decimal? FuelPerUnit);
public sealed record ComplianceKpis(int StatutoryDueSoon, int StatutoryOverdue, int CalibrationsOverdue, int NonConformitiesOpen, int NonConformitiesOverdue, DateTime? NextAuditOn, string? NextAudit);

public sealed record FactoryKpiReport(
    string ScopeName, DateTime From, DateTime To, string ProductionUnit, string Currency,
    AssetKpis Assets, ReliabilityKpis Reliability, PlanningKpis Planning, WindowKpis Window, OverhaulKpis Overhaul,
    DowntimeKpis Downtime, CostKpis Costs, SparesKpis Spares, PeopleKpis People, EnergyKpis Energy, ComplianceKpis Compliance);

public sealed record FactoryComparisonRow(Guid FactoryId, string FactoryName, decimal DowntimeHours, decimal ProductionLost, int Breakdowns, decimal? MttrHours, decimal? PmCompliancePercent,
    decimal CostTotal, decimal Production, decimal? CostPerUnit, decimal? KwhPerUnit, decimal? FuelPerUnit, int CriticalStockOuts, int StatutoryOverdue, int NonConformitiesOpen);

public sealed record PilotMeasure(string Measure, string Unit, decimal? Baseline, decimal? Current, decimal? ChangePercent, bool LowerIsBetter);
public sealed record PilotReport(string ScopeName, DateTime BaselineFrom, DateTime BaselineTo, DateTime From, DateTime To, IReadOnlyCollection<PilotMeasure> Measures, FactoryKpiReport Baseline, FactoryKpiReport Current);

public interface IFactoryKpiService
{
    /// <summary>Every cost on jobs in the nodes (null for all) over the period: labour, parts, contractor (net of VAT) and downtime.</summary>
    Task<IReadOnlyList<CostLine>> CostLinesAsync(Guid tenantId, IReadOnlyCollection<Guid>? nodes, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    Task<FactoryKpiReport> KpisAsync(Guid tenantId, IReadOnlyCollection<Guid>? nodes, string scopeName, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>The headline figures for each factory side by side (group level).</summary>
    Task<IReadOnlyList<FactoryComparisonRow>> CompareAsync(Guid tenantId, IReadOnlyCollection<Guid> factoryIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Before and after: the pilot results for the go-to-market case.</summary>
    Task<PilotReport> PilotAsync(Guid tenantId, IReadOnlyCollection<Guid>? nodes, string scopeName, DateTime baselineFrom, DateTime baselineTo, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>Consumption by meter type over the period: the sum of increments on counters, or of readings on gauges logged as quantities.</summary>
    Task<IReadOnlyDictionary<string, (decimal Quantity, string? Unit)>> MeterUsageAsync(Guid tenantId, IReadOnlyCollection<Guid>? nodes, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);
}

internal sealed class FactoryKpiService(
    AppDbContext dbContext,
    ISparesService sparesService,
    IFactoryPmService pmService,
    IPeopleService peopleService,
    IFactorySettingsService settingsService) : IFactoryKpiService
{
    private static readonly string[] Finished = [WorkOrderStatuses.Completed, WorkOrderStatuses.Verified, WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];
    private static readonly string[] Dropped = [WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected];

    private sealed record Node(Guid Id, Guid? ParentId, string Level, string Name);

    public async Task<IReadOnlyList<CostLine>> CostLinesAsync(Guid tenantId, IReadOnlyCollection<Guid>? nodes, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var tree = await TreeAsync(tenantId, cancellationToken);
        var centres = await dbContext.CostCentres.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => new { x.Id, x.Name, x.FactoryId, x.OrgNodeId }).ToListAsync(cancellationToken);
        var centreByNode = centres.Where(x => x.OrgNodeId != null).GroupBy(x => x.OrgNodeId!.Value).ToDictionary(g => g.Key, g => g.First());

        var jobs = dbContext.WorkOrders.AsNoTracking().Where(x => x.TenantId == tenantId && x.ClientId == null && x.OrgNodeId != null);
        if (nodes is not null) jobs = jobs.Where(x => nodes.Contains(x.OrgNodeId!.Value));
        var jobIds = jobs.Select(x => x.Id);

        var labour = await dbContext.WorkOrderTimeLogs.AsNoTracking()
            .Where(x => jobIds.Contains(x.WorkOrderId) && x.StartedAt >= fromUtc && x.StartedAt < toUtc && x.LabourCost != null)
            .Select(x => new { x.WorkOrderId, x.StartedAt, Amount = x.LabourCost!.Value }).ToListAsync(cancellationToken);
        var parts = await dbContext.WorkOrderMaterialUsages.AsNoTracking()
            .Where(x => jobIds.Contains(x.WorkOrderId) && x.UsedAt >= fromUtc && x.UsedAt < toUtc && x.QuantityUsed > 0)
            .Select(x => new { x.WorkOrderId, x.UsedAt, Amount = x.QuantityUsed * (x.UnitCost ?? 0) }).ToListAsync(cancellationToken);
        // Contractor cost is recognised once the invoice matches the work (or is approved), net of VAT.
        var contractor = await dbContext.ContractorInvoiceLines.AsNoTracking()
            .Where(x => x.WorkOrderId != null && jobIds.Contains(x.WorkOrderId.Value)
                && (x.Invoice!.Status == ContractorInvoiceStatuses.Approved || x.Invoice.Status == ContractorInvoiceStatuses.Matched)
                && x.Invoice.InvoiceDate >= fromUtc && x.Invoice.InvoiceDate < toUtc)
            .Select(x => new { WorkOrderId = x.WorkOrderId!.Value, x.Invoice!.InvoiceDate, LineAmount = x.Amount, InvoiceAmount = x.Invoice.Amount, x.Invoice.VatAmount, x.Invoice.Currency })
            .ToListAsync(cancellationToken);
        var downtime = await jobs.Where(x => x.IsBreakdown && x.DowntimeStartedAt != null && x.DowntimeStartedAt < toUtc && (x.DowntimeEndedAt == null || x.DowntimeEndedAt > fromUtc))
            .Select(x => new { x.Id, Start = x.DowntimeStartedAt!.Value, x.DowntimeEndedAt, Rate = x.Asset != null ? x.Asset.LostProductionPerHour : null }).ToListAsync(cancellationToken);

        var ids = labour.Select(x => x.WorkOrderId).Concat(parts.Select(x => x.WorkOrderId)).Concat(contractor.Select(x => x.WorkOrderId)).Concat(downtime.Select(x => x.Id)).Distinct().ToList();
        var info = await dbContext.WorkOrders.AsNoTracking().Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.WorkOrderNumber, x.Title, x.AssetId, AssetCode = x.Asset != null ? x.Asset.AssetCode : null, AssetName = x.Asset != null ? x.Asset.AssetName : null, x.OrgNodeId })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        CostLine Line(Guid jobId, DateTime at, string category, decimal amount, decimal hours = 0, decimal lost = 0)
        {
            var job = info[jobId];
            var factory = Up(tree, job.OrgNodeId, OrgNodeLevels.Factory);
            var section = Up(tree, job.OrgNodeId, OrgNodeLevels.Section);
            // The cost centre of the job's node or its nearest parent; else the factory's own.
            Guid? centreId = null;
            string? centreName = null;
            for (Guid? n = job.OrgNodeId; n is { } current; n = tree.TryGetValue(current, out var node) ? node.ParentId : null)
            {
                if (centreByNode.TryGetValue(current, out var centre)) { centreId = centre.Id; centreName = centre.Name; break; }
            }

            return new CostLine(jobId, job.WorkOrderNumber, job.Title, job.AssetId, job.AssetCode, job.AssetName, job.OrgNodeId,
                factory?.Id, factory?.Name, section?.Id, section?.Name, centreId, centreName, at, category, Math.Round(amount, 2), hours, lost);
        }

        var lines = new List<CostLine>();
        lines.AddRange(labour.Select(x => Line(x.WorkOrderId, x.StartedAt, CostCategories.Labour, x.Amount)));
        lines.AddRange(parts.Select(x => Line(x.WorkOrderId, x.UsedAt, CostCategories.Parts, x.Amount)));
        foreach (var x in contractor)
        {
            var net = x.InvoiceAmount > 0 ? x.LineAmount * (1 - x.VatAmount / x.InvoiceAmount) : x.LineAmount;
            lines.Add(Line(x.WorkOrderId, x.InvoiceDate, CostCategories.Contractor, await sparesService.ToTenantCurrencyAsync(tenantId, net, x.Currency, cancellationToken)));
        }

        foreach (var x in downtime)
        {
            var start = x.Start < fromUtc ? fromUtc : x.Start;
            var end = x.DowntimeEndedAt is { } e && e < toUtc ? e : (toUtc < DateTime.UtcNow ? toUtc : DateTime.UtcNow);
            if (end <= start) continue;
            var hours = (decimal)(end - start).TotalHours;
            lines.Add(Line(x.Id, start, CostCategories.Downtime, 0, Math.Round(hours, 2), Math.Round(hours * (x.Rate ?? 0), 2)));
        }

        return lines;
    }

    public async Task<IReadOnlyDictionary<string, (decimal Quantity, string? Unit)>> MeterUsageAsync(Guid tenantId, IReadOnlyCollection<Guid>? nodes, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var meters = dbContext.Meters.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive
            && (x.MeterType == MeterTypes.Energy || x.MeterType == MeterTypes.Fuel || x.MeterType == MeterTypes.Production) && x.Asset!.OrgNodeId != null);
        if (nodes is not null) meters = meters.Where(x => nodes.Contains(x.Asset!.OrgNodeId!.Value));
        var info = await meters.Select(x => new { x.Id, x.MeterType, x.Unit, x.IsCumulative }).ToListAsync(cancellationToken);
        var ids = info.Select(x => x.Id).ToList();
        var readings = await dbContext.MeterReadings.AsNoTracking()
            .Where(x => ids.Contains(x.MeterId) && x.ReadAt >= fromUtc && x.ReadAt < toUtc && !x.IsReset)
            .Select(x => new { x.MeterId, x.Value, x.Delta }).ToListAsync(cancellationToken);
        return info.GroupBy(x => x.MeterType).ToDictionary(g => g.Key, g =>
        {
            var quantity = g.Sum(m => readings.Where(r => r.MeterId == m.Id).Sum(r => m.IsCumulative ? Math.Max(r.Delta ?? 0, 0) : r.Value));
            var units = g.Select(m => m.Unit).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
            return (quantity, units.Count == 1 ? units[0] : units.Count == 0 ? null : string.Join("/", units));
        });
    }

    public async Task<FactoryKpiReport> KpisAsync(Guid tenantId, IReadOnlyCollection<Guid>? nodes, string scopeName, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetOrCreateAsync(tenantId, cancellationToken);
        var currency = await dbContext.Tenants.Where(x => x.Id == tenantId).Select(x => x.Currency).SingleOrDefaultAsync(cancellationToken) ?? "KES";
        var tree = await TreeAsync(tenantId, cancellationToken);
        var now = DateTime.UtcNow;
        var periodEnd = toUtc < now ? toUtc : now;
        var periodHours = Math.Max((decimal)(periodEnd - fromUtc).TotalHours, 1);

        var jobs = dbContext.WorkOrders.AsNoTracking().Where(x => x.TenantId == tenantId && x.ClientId == null && x.OrgNodeId != null);
        var assets = dbContext.Assets.AsNoTracking().Where(x => x.TenantId == tenantId && x.Status != "Inactive" && x.OrgNodeId != null);
        if (nodes is not null)
        {
            jobs = jobs.Where(x => nodes.Contains(x.OrgNodeId!.Value));
            assets = assets.Where(x => nodes.Contains(x.OrgNodeId!.Value));
        }

        // Assets.
        var assetRows = await assets.Select(x => new { x.OperationalStatus, x.Criticality }).ToListAsync(cancellationToken);
        var assetKpis = new AssetKpis(assetRows.Count, assetRows.Count(x => x.OperationalStatus == AssetOperationalStatuses.Operational),
            assetRows.Count(x => x.OperationalStatus == AssetOperationalStatuses.Standby), assetRows.Count(x => x.OperationalStatus == AssetOperationalStatuses.Down),
            assetRows.GroupBy(x => string.IsNullOrWhiteSpace(x.Criticality) ? "Unrated" : x.Criticality!).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()));

        // Costs and downtime (one ledger).
        var lines = await CostLinesAsync(tenantId, nodes, fromUtc, toUtc, cancellationToken);
        var peak = PeakMonths(settings.PeakSeasonMonths);
        var downtimeLines = lines.Where(x => x.Category == CostCategories.Downtime).ToList();
        var downtimeHours = downtimeLines.Sum(x => x.DowntimeHours);
        var lost = downtimeLines.Sum(x => x.ProductionLost);
        var downtime = new DowntimeKpis(Math.Round(downtimeHours, 1), Math.Round(lost, 0), settings.ProductionUnitValue is { } v ? Math.Round(lost * v, 2) : null,
            Math.Round(downtimeLines.Where(x => peak.Contains(x.At.Month)).Sum(x => x.DowntimeHours), 1), Math.Round(downtimeLines.Where(x => !peak.Contains(x.At.Month)).Sum(x => x.DowntimeHours), 1),
            Math.Round(downtimeLines.Where(x => peak.Contains(x.At.Month)).Sum(x => x.ProductionLost), 0), Math.Round(downtimeLines.Where(x => !peak.Contains(x.At.Month)).Sum(x => x.ProductionLost), 0));

        // Reliability: breakdowns starting in the period.
        var breakdowns = await jobs.Where(x => x.IsBreakdown && x.CreatedAt >= fromUtc && x.CreatedAt < toUtc && !Dropped.Contains(x.Status))
            .Select(x => new { x.AssetId, AssetCode = x.Asset != null ? x.Asset.AssetCode : null, AssetName = x.Asset != null ? x.Asset.AssetName : null,
                x.DowntimeStartedAt, x.DowntimeEndedAt, Rework = x.ReworkOfWorkOrderId != null }).ToListAsync(cancellationToken);
        var repaired = breakdowns.Where(x => x.DowntimeStartedAt != null && x.DowntimeEndedAt != null).ToList();
        var failingAssets = breakdowns.Where(x => x.AssetId != null).Select(x => x.AssetId).Distinct().Count();
        var reliability = new ReliabilityKpis(
            breakdowns.Count,
            breakdowns.Count == 0 ? null : Math.Round((periodHours * failingAssets - downtimeHours) / breakdowns.Count, 1),
            repaired.Count == 0 ? null : Math.Round((decimal)repaired.Average(x => (x.DowntimeEndedAt!.Value - x.DowntimeStartedAt!.Value).TotalHours), 1),
            breakdowns.Count(x => x.Rework),
            breakdowns.Where(x => x.AssetId != null).GroupBy(x => new { x.AssetId, x.AssetCode, x.AssetName })
                .Select(g => new FailingAsset(g.Key.AssetId!.Value, g.Key.AssetCode ?? "", g.Key.AssetName ?? "", g.Count(),
                    Math.Round(downtimeLines.Where(d => d.AssetId == g.Key.AssetId).Sum(d => d.DowntimeHours), 1)))
                .OrderByDescending(x => x.Breakdowns).ThenByDescending(x => x.DowntimeHours).Take(5).ToList());

        // Planning.
        var pm = await pmService.ComplianceAsync(tenantId, nodes, fromUtc, toUtc, cancellationToken);
        var created = jobs.Where(x => x.CreatedAt >= fromUtc && x.CreatedAt < toUtc && !Dropped.Contains(x.Status));
        var reactive = await created.CountAsync(x => x.WorkType == FactoryWorkTypes.Corrective || x.IsBreakdown, cancellationToken);
        var plannedJobs = await created.CountAsync(x => x.WorkType != FactoryWorkTypes.Corrective && !x.IsBreakdown, cancellationToken);
        var open = jobs.Where(x => !Finished.Contains(x.Status));
        var backlog = await open.Where(x => x.Status != WorkOrderStatuses.Requested).SumAsync(x => x.EstimatedLabourHours ?? 0, cancellationToken);
        var technicians = await dbContext.Technicians.CountAsync(x => x.TenantId == tenantId && x.IsActive && (nodes == null || x.HomeOrgNodeId == null || nodes.Contains(x.HomeOrgNodeId.Value)), cancellationToken);
        var weeklyCapacity = technicians * settings.PmHoursPerTechnicianWeek;
        var planning = new PlanningKpis(pm.Percent, pm.Due, pm.OnTime, await open.CountAsync(x => x.DueDate != null && x.DueDate < now, cancellationToken),
            Math.Round(backlog, 1), weeklyCapacity > 0 ? Math.Round(backlog / weeklyCapacity, 1) : null, plannedJobs, reactive,
            plannedJobs + reactive == 0 ? null : Math.Round(100m * plannedJobs / (plannedJobs + reactive), 1));

        // Weekly window.
        var factoryIds = FactoriesOf(tree, nodes);
        var windows = await dbContext.MaintenanceWindows.AsNoTracking()
            .Where(x => x.TenantId == tenantId && factoryIds.Contains(x.FactoryId) && x.WindowDate >= fromUtc && x.WindowDate < toUtc && x.Status != MaintenanceWindowStatuses.Skipped)
            .Select(x => new { x.Id, x.Status, Jobs = x.Jobs.Select(j => new { j.Outcome, j.WorkOrder!.WindowCarryoverCount, j.WorkOrder.AssetId }).ToList() })
            .ToListAsync(cancellationToken);
        var handedBack = windows.Where(x => x.Status == MaintenanceWindowStatuses.HandedBack).ToList();
        var windowJobs = handedBack.SelectMany(x => x.Jobs).ToList();
        var deferredAssets = windows.SelectMany(x => x.Jobs).Where(j => j.Outcome == WindowJobOutcomes.CarriedOver && j.AssetId != null).Select(j => j.AssetId!.Value).ToHashSet();
        var window = new WindowKpis(windows.Count, windowJobs.Count, windowJobs.Count(j => j.Outcome == WindowJobOutcomes.Done),
            windowJobs.Count == 0 ? null : Math.Round(100m * windowJobs.Count(j => j.Outcome == WindowJobOutcomes.Done) / windowJobs.Count, 1),
            windowJobs.Count(j => j.Outcome == WindowJobOutcomes.CarriedOver),
            await open.Where(x => x.WindowCarryoverCount > 0).Select(x => (int?)x.WindowCarryoverCount).MaxAsync(cancellationToken) ?? 0,
            breakdowns.Count(x => x.AssetId != null && deferredAssets.Contains(x.AssetId.Value)));

        // Overhaul: plans running or ending in the period.
        var plans = await dbContext.OverhaulPlans.AsNoTracking()
            .Where(x => x.TenantId == tenantId && factoryIds.Contains(x.FactoryId) && x.PlannedStart < toUtc && x.PlannedEnd >= fromUtc)
            .Select(x => new { x.Budget, Items = x.Items.Select(i => new { i.Status, i.ActualCost, i.NeedsRecommissioning, i.RecommissionedAt }).ToList() })
            .ToListAsync(cancellationToken);
        var items = plans.SelectMany(x => x.Items).Where(i => i.Status != OverhaulItemStatuses.Dropped).ToList();
        var overhaul = new OverhaulKpis(items.Count, items.Count(i => i.Status == OverhaulItemStatuses.Done),
            items.Count == 0 ? null : Math.Round(100m * items.Count(i => i.Status == OverhaulItemStatuses.Done) / items.Count, 1),
            plans.Any(p => p.Budget != null) ? plans.Sum(p => p.Budget ?? 0) : null, items.Sum(i => i.ActualCost ?? 0),
            items.Count(i => i.RecommissionedAt != null), items.Count(i => i.Status == OverhaulItemStatuses.Done && i.NeedsRecommissioning && i.RecommissionedAt == null));

        // Energy and production from meters.
        var usage = await MeterUsageAsync(tenantId, nodes, fromUtc, toUtc, cancellationToken);
        var production = usage.TryGetValue(MeterTypes.Production, out var p) ? p.Quantity : 0;
        var kwh = usage.TryGetValue(MeterTypes.Energy, out var e) ? e.Quantity : 0;
        var fuel = usage.TryGetValue(MeterTypes.Fuel, out var f) ? f : (0, null);
        var energy = new EnergyKpis(Math.Round(kwh, 1), Math.Round(fuel.Item1, 2), fuel.Item2, Math.Round(production, 0),
            production > 0 ? Math.Round(kwh / production, 3) : null, production > 0 ? Math.Round(fuel.Item1 / production, 4) : null);

        // Costs.
        decimal Sum(IEnumerable<CostLine> xs, string category) => Math.Round(xs.Where(x => x.Category == category).Sum(x => x.Amount), 2);
        CostBreakdown Breakdown(Guid? id, string name, IReadOnlyCollection<CostLine> xs)
        {
            var hours = xs.Sum(x => x.DowntimeHours);
            var lostHere = xs.Sum(x => x.ProductionLost);
            return new CostBreakdown(id, name, Sum(xs, CostCategories.Labour), Sum(xs, CostCategories.Parts), Sum(xs, CostCategories.Contractor),
                Math.Round(xs.Where(x => x.Category != CostCategories.Downtime).Sum(x => x.Amount), 2), Math.Round(hours, 1), Math.Round(lostHere, 0),
                Math.Round(lostHere * (settings.ProductionUnitValue ?? 0), 2));
        }

        IReadOnlyCollection<CostBreakdown> By(Func<CostLine, Guid?> key, Func<CostLine, string?> name) =>
            lines.GroupBy(x => key(x)).Select(g => Breakdown(g.Key, name(g.First()) ?? "Unassigned", g.ToList())).OrderByDescending(x => x.Total).ThenByDescending(x => x.DowntimeHours).ToList();

        var total = Math.Round(lines.Where(x => x.Category != CostCategories.Downtime).Sum(x => x.Amount), 2);
        var budget = await BudgetAsync(tenantId, factoryIds, tree, lines, fromUtc, toUtc, cancellationToken);
        var costs = new CostKpis(Sum(lines, CostCategories.Labour), Sum(lines, CostCategories.Parts), Sum(lines, CostCategories.Contractor), total, downtime.ValueLost,
            Math.Round(production, 0), production > 0 ? Math.Round(total / production, 4) : null,
            By(x => x.FactoryId, x => x.FactoryName), By(x => x.SectionId, x => x.SectionName), By(x => x.CostCentreId, x => x.CostCentreName),
            By(x => x.AssetId, x => x.AssetId is null ? "No asset" : $"{x.AssetCode} {x.AssetName}").Where(x => x.Id != null).Take(10).ToList(),
            lines.Where(x => x.Category != CostCategories.Downtime).GroupBy(x => new DateTime(x.At.Year, x.At.Month, 1, 0, 0, 0, DateTimeKind.Utc)).OrderBy(g => g.Key)
                .Select(g => new NamedAmount(null, g.Key.ToString("yyyy-MM"), Math.Round(g.Sum(x => x.Amount), 2))).ToList(),
            budget);

        // Spares.
        var stores = dbContext.Stores.AsNoTracking().Where(x => x.TenantId == tenantId && factoryIds.Contains(x.FactoryId)).Select(x => x.Id);
        var issues = await dbContext.StockMovements.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.StoreId != null && stores.Contains(x.StoreId.Value) && x.MovementType == StoreMovementTypes.Issue && x.CreatedAt >= fromUtc && x.CreatedAt < toUtc)
            .Select(x => new { x.Quantity, x.UnitCost }).ToListAsync(cancellationToken);
        var stock = await dbContext.StoreStocks.AsNoTracking().Where(x => stores.Contains(x.StoreId))
            .Select(x => new { x.OnHand, x.Reserved, x.MinLevel, x.AverageCost, x.MaterialItem!.IsCriticalSpare }).ToListAsync(cancellationToken);
        var spares = new SparesKpis(Math.Round(issues.Sum(x => x.Quantity * (x.UnitCost ?? 0)), 2), issues.Count,
            stock.Count(x => x.IsCriticalSpare && x.OnHand - x.Reserved <= 0), stock.Count(x => x.IsCriticalSpare && x.OnHand - x.Reserved < x.MinLevel),
            Math.Round(stock.Sum(x => x.OnHand * x.AverageCost), 2));

        // People.
        var workload = await peopleService.WorkloadAsync(tenantId, nodes, DateOnly.FromDateTime(fromUtc), DateOnly.FromDateTime(periodEnd.AddTicks(-1)), cancellationToken);
        var logged = workload.Sum(x => x.LoggedHours);
        var rostered = workload.Sum(x => x.RosteredHours);
        var certificates = await dbContext.TechnicianCertificates.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ExpiresOn != null && x.Technician!.IsActive && (nodes == null || x.Technician.HomeOrgNodeId == null || nodes.Contains(x.Technician.HomeOrgNodeId.Value)))
            .Select(x => x.ExpiresOn!.Value).ToListAsync(cancellationToken);
        var people = new PeopleKpis(logged, workload.Sum(x => x.OvertimeHours), logged > 0 ? Math.Round(100 * workload.Sum(x => x.OvertimeHours) / logged, 1) : null,
            rostered > 0 ? Math.Round(100 * logged / rostered, 1) : null, workload.Sum(x => x.JobsCompleted),
            certificates.Count(x => x.Date >= now.Date && x.Date <= now.Date.AddDays(30)), certificates.Count(x => x.Date < now.Date));

        // Compliance.
        var statutory = dbContext.ComplianceItems.AsNoTracking().Where(x => x.TenantId == tenantId && x.IsActive && x.DueDate != null);
        if (nodes is not null) statutory = statutory.Where(x => nodes.Contains(x.OrgNodeId));
        var statutoryRows = await statutory.Select(x => new { x.Kind, Due = x.DueDate!.Value }).ToListAsync(cancellationToken);
        var ncs = await dbContext.NonConformities.AsNoTracking().Where(x => x.TenantId == tenantId && factoryIds.Contains(x.FactoryId)
                && (x.Status == NonConformityStatuses.Open || x.Status == NonConformityStatuses.InProgress || x.Status == NonConformityStatuses.Resolved))
            .Select(x => x.DueDate).ToListAsync(cancellationToken);
        var nextAudit = await dbContext.AuditEvents.AsNoTracking().Where(x => x.TenantId == tenantId && factoryIds.Contains(x.FactoryId) && x.Status == "Planned" && x.ScheduledFor >= now.Date)
            .OrderBy(x => x.ScheduledFor).Select(x => new { x.ScheduledFor, x.Kind, Standard = x.Standard != null ? x.Standard.Name : null }).FirstOrDefaultAsync(cancellationToken);
        var compliance = new ComplianceKpis(
            statutoryRows.Count(x => x.Due.Date >= now.Date && x.Due.Date <= now.Date.AddDays(30)), statutoryRows.Count(x => x.Due.Date < now.Date),
            statutoryRows.Count(x => x.Kind == ComplianceKinds.Calibration && x.Due.Date < now.Date),
            ncs.Count, ncs.Count(x => x != null && x.Value.Date < now.Date),
            nextAudit?.ScheduledFor, nextAudit is null ? null : $"{nextAudit.Kind}{(nextAudit.Standard is null ? "" : $" · {nextAudit.Standard}")}");

        return new FactoryKpiReport(scopeName, fromUtc, toUtc, settings.ProductionUnit, currency, assetKpis, reliability, planning, window, overhaul, downtime, costs, spares, people, energy, compliance);
    }

    public async Task<IReadOnlyList<FactoryComparisonRow>> CompareAsync(Guid tenantId, IReadOnlyCollection<Guid> factoryIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var tree = await TreeAsync(tenantId, cancellationToken);
        var rows = new List<FactoryComparisonRow>();
        foreach (var factoryId in factoryIds)
        {
            if (!tree.TryGetValue(factoryId, out var factory)) continue;
            var k = await KpisAsync(tenantId, Subtree(tree, factoryId), factory.Name, fromUtc, toUtc, cancellationToken);
            rows.Add(new FactoryComparisonRow(factoryId, factory.Name, k.Downtime.Hours, k.Downtime.ProductionLost, k.Reliability.Breakdowns, k.Reliability.MttrHours,
                k.Planning.PmCompliancePercent, k.Costs.Total, k.Costs.Production, k.Costs.CostPerUnit, k.Energy.KwhPerUnit, k.Energy.FuelPerUnit,
                k.Spares.CriticalStockOuts, k.Compliance.StatutoryOverdue, k.Compliance.NonConformitiesOpen));
        }

        return rows.OrderBy(x => x.FactoryName).ToList();
    }

    public async Task<PilotReport> PilotAsync(Guid tenantId, IReadOnlyCollection<Guid>? nodes, string scopeName, DateTime baselineFrom, DateTime baselineTo, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var before = await KpisAsync(tenantId, nodes, scopeName, baselineFrom, baselineTo, cancellationToken);
        var after = await KpisAsync(tenantId, nodes, scopeName, fromUtc, toUtc, cancellationToken);
        // Per 30 days, so a baseline and pilot of different lengths compare fairly.
        decimal Days(DateTime a, DateTime b) => Math.Max((decimal)(b - a).TotalDays, 1);
        decimal? Per30(decimal? value, DateTime a, DateTime b) => value is { } x ? Math.Round(x * 30 / Days(a, b), 2) : null;
        PilotMeasure M(string name, string unit, decimal? b, decimal? c, bool lower) =>
            new(name, unit, b, c, b is { } x && x != 0 && c is { } y ? Math.Round(100 * (y - x) / x, 1) : null, lower);
        var unit = after.ProductionUnit;
        var measures = new List<PilotMeasure>
        {
            M("Downtime per 30 days", "h", Per30(before.Downtime.Hours, baselineFrom, baselineTo), Per30(after.Downtime.Hours, fromUtc, toUtc), true),
            M($"Production lost per 30 days", unit, Per30(before.Downtime.ProductionLost, baselineFrom, baselineTo), Per30(after.Downtime.ProductionLost, fromUtc, toUtc), true),
            M("Breakdowns per 30 days", "", Per30(before.Reliability.Breakdowns, baselineFrom, baselineTo), Per30(after.Reliability.Breakdowns, fromUtc, toUtc), true),
            M("Mean time to repair", "h", before.Reliability.MttrHours, after.Reliability.MttrHours, true),
            M("PM compliance", "%", before.Planning.PmCompliancePercent, after.Planning.PmCompliancePercent, false),
            M("Planned work share", "%", before.Planning.PlannedPercent, after.Planning.PlannedPercent, false),
            M($"Maintenance cost per {unit}", after.Currency, before.Costs.CostPerUnit, after.Costs.CostPerUnit, true),
            M($"Energy per {unit}", "kWh", before.Energy.KwhPerUnit, after.Energy.KwhPerUnit, true),
        };
        return new PilotReport(scopeName, baselineFrom, baselineTo, fromUtc, toUtc, measures, before, after);
    }

    private async Task<BudgetKpis> BudgetAsync(Guid tenantId, IReadOnlyCollection<Guid> factoryIds, IReadOnlyDictionary<Guid, Node> tree, IReadOnlyList<CostLine> lines, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var years = Enumerable.Range(fromUtc.Year, toUtc.AddTicks(-1).Year - fromUtc.Year + 1).ToList();
        var budgets = await dbContext.MaintenanceBudgets.AsNoTracking()
            .Where(x => x.TenantId == tenantId && factoryIds.Contains(x.FactoryId) && years.Contains(x.Year))
            .Select(x => new { x.FactoryId, x.CostCentreId, CostCentre = x.CostCentre != null ? x.CostCentre.Name : null, x.Year, x.Category, x.Amount }).ToListAsync(cancellationToken);
        // A year's budget counts in proportion to how much of that year the period covers.
        decimal Share(int year)
        {
            var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddYears(1);
            var overlap = (decimal)((toUtc < end ? toUtc : end) - (fromUtc > start ? fromUtc : start)).TotalDays;
            return Math.Max(overlap, 0) / (decimal)(end - start).TotalDays;
        }

        var result = budgets.Select(b =>
        {
            var actual = lines.Where(x => x.FactoryId == b.FactoryId && x.Category == b.Category && x.At.Year == b.Year && (b.CostCentreId == null || x.CostCentreId == b.CostCentreId)).Sum(x => x.Amount);
            return new BudgetLineKpi(tree.TryGetValue(b.FactoryId, out var f) ? f.Name : "", b.CostCentre, b.Category, Math.Round(b.Amount * Share(b.Year), 2), Math.Round(actual, 2));
        }).OrderBy(x => x.Factory).ThenBy(x => x.CostCentre).ThenBy(x => x.Category).ToList();

        // A factory budgeted by cost centre has no factory-wide line, so the totals don't double count.
        var forPeriod = result.Sum(x => x.BudgetForPeriod);
        var actualTotal = Math.Round(lines.Where(x => x.Category != CostCategories.Downtime && x.FactoryId is { } fid && budgets.Any(b => b.FactoryId == fid)).Sum(x => x.Amount), 2);
        return new BudgetKpis(budgets.Where(b => years.Contains(b.Year)).Sum(b => b.Amount), Math.Round(forPeriod, 2), actualTotal,
            forPeriod > 0 ? Math.Round(100 * (actualTotal - forPeriod) / forPeriod, 1) : null, result);
    }

    private async Task<Dictionary<Guid, Node>> TreeAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.OrgNodes.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => new Node(x.Id, x.ParentId, x.Level, x.Name)).ToDictionaryAsync(x => x.Id, cancellationToken);

    private static Node? Up(IReadOnlyDictionary<Guid, Node> tree, Guid? start, string level)
    {
        for (var (current, depth) = (start, 0); current is { } id && depth < 10 && tree.TryGetValue(id, out var node); current = node.ParentId, depth++)
        {
            if (node.Level == level) return node;
        }

        return null;
    }

    private static HashSet<Guid> FactoriesOf(IReadOnlyDictionary<Guid, Node> tree, IReadOnlyCollection<Guid>? nodes)
    {
        if (nodes is null) return tree.Values.Where(x => x.Level == OrgNodeLevels.Factory).Select(x => x.Id).ToHashSet();
        var result = new HashSet<Guid>();
        foreach (var id in nodes)
        {
            if (Up(tree, id, OrgNodeLevels.Factory) is { } factory) result.Add(factory.Id);
        }

        return result;
    }

    private static List<Guid> Subtree(IReadOnlyDictionary<Guid, Node> tree, Guid root)
    {
        var result = new List<Guid> { root };
        for (var i = 0; i < result.Count; i++)
        {
            result.AddRange(tree.Values.Where(x => x.ParentId == result[i]).Select(x => x.Id));
        }

        return result;
    }

    private static HashSet<int> PeakMonths(string? csv) => PeakSeason.Months(csv);
}

public static class PeakSeason
{
    /// <summary>Month numbers from a comma-separated list, ignoring anything that isn't 1–12.</summary>
    public static HashSet<int> Months(string? csv) =>
        (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var m) ? m : 0).Where(m => m is >= 1 and <= 12).ToHashSet();
}
