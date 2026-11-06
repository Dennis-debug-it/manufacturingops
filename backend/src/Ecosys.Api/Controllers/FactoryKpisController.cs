using System.ComponentModel.DataAnnotations;
using Ecosys.Api.Common;
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

/// <summary>Shared by the cost and KPI controllers: dates, scope and factory lookups.</summary>
public abstract class FactoryReportingControllerBase(AppDbContext dbContext, ITenantContext tenantContext, IOrgScopeService orgScopeService) : TenantAwareControllerBase(tenantContext)
{
    protected AppDbContext Db => dbContext;

    /// <summary>The period [from, to) in UTC; by default the current month to date.</summary>
    protected static (DateTime From, DateTime To) Period(DateTime? from, DateTime? to)
    {
        var now = DateTime.UtcNow;
        var start = DateTime.SpecifyKind((from ?? new DateTime(now.Year, now.Month, 1)).Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind((to ?? now).Date.AddDays(1), DateTimeKind.Utc);
        if (end <= start) throw new BusinessRuleException("The end of the period must be after its start.");
        if ((end - start).TotalDays > 3 * 366) throw new BusinessRuleException("Choose a period of at most three years.");
        return (start, end);
    }

    /// <summary>The nodes reported on (the chosen node's subtree within the user's scope; null for everything) and the name of the scope.</summary>
    protected async Task<(List<Guid>? Nodes, string Name)> ScopeAsync(Guid? orgNodeId, CancellationToken cancellationToken)
    {
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (orgNodeId is { } id)
        {
            var node = await dbContext.OrgNodes.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Location was not found.");
            var subtree = await orgScopeService.GetSubtreeAsync(TenantId, id, cancellationToken);
            var allowed = subtree.Where(x => scope.Allows(x)).ToList();
            if (allowed.Count == 0) throw new NotFoundException("Location was not found.");
            return (allowed, node.Name);
        }

        return (scope.Nodes?.ToList(), scope.Nodes is null ? "All factories" : "My factories");
    }

    protected async Task<List<OrgNode>> FactoriesInScopeAsync(CancellationToken cancellationToken)
    {
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        var nodes = await dbContext.OrgNodes.AsNoTracking().Where(x => x.TenantId == TenantId).ToListAsync(cancellationToken);
        var byId = nodes.ToDictionary(x => x.Id);
        bool Visible(OrgNode factory)
        {
            if (scope.Nodes is null || scope.Nodes.Contains(factory.Id)) return true;
            // A user limited to a section still sees their factory.
            return scope.Nodes.Any(n => { for (Guid? c = n; c is { } id && byId.TryGetValue(id, out var x); c = x.ParentId) if (id == factory.Id) return true; return false; });
        }

        return nodes.Where(x => x.Level == OrgNodeLevels.Factory && x.IsActive && Visible(x)).OrderBy(x => x.Name).ToList();
    }

    protected async Task<OrgNode> FactoryAsync(Guid factoryId, CancellationToken cancellationToken) =>
        (await FactoriesInScopeAsync(cancellationToken)).FirstOrDefault(x => x.Id == factoryId) ?? throw new NotFoundException("Factory was not found.");

    protected async Task<string> CompanyAsync(CancellationToken cancellationToken) =>
        await dbContext.Tenants.AsNoTracking().Where(x => x.Id == TenantId).Select(x => x.CompanyName).SingleOrDefaultAsync(cancellationToken) ?? string.Empty;

    protected static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Maintenance costs (labour, parts, contractor, downtime) by job, asset, section, factory and cost centre; cost centres and annual budgets.</summary>
[ApiController]
[Authorize]
[Route("api/factory/costs")]
public sealed class FactoryCostsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IFactoryKpiService kpiService,
    IFactorySettingsService settingsService) : FactoryReportingControllerBase(dbContext, tenantContext, orgScopeService)
{
    /// <summary>Costs over a period, grouped by factory, section, cost centre, asset, job, month or category.</summary>
    [HttpGet]
    public async Task<ActionResult<CostSummary>> Summary([FromQuery] Guid? orgNodeId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? groupBy, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var (start, end) = Period(from, to);
        var (nodes, name) = await ScopeAsync(orgNodeId, cancellationToken);
        var lines = await kpiService.CostLinesAsync(TenantId, nodes, start, end, cancellationToken);
        var settings = await settingsService.GetOrCreateAsync(TenantId, cancellationToken);
        var currency = await Db.Tenants.Where(x => x.Id == TenantId).Select(x => x.Currency).SingleAsync(cancellationToken);
        var key = (groupBy ?? "section").ToLowerInvariant();
        Func<CostLine, (Guid? Id, string Name)> by = key switch
        {
            "factory" => x => (x.FactoryId, x.FactoryName ?? "No factory"),
            "section" => x => (x.SectionId, x.SectionName ?? "Factory-wide"),
            "costcentre" => x => (x.CostCentreId, x.CostCentreName ?? "No cost centre"),
            "asset" => x => (x.AssetId, x.AssetId is null ? "No asset" : $"{x.AssetCode} {x.AssetName}"),
            "job" => x => (x.WorkOrderId, $"{x.WorkOrderNumber} {x.Title}"),
            "month" => x => (null, x.At.ToString("yyyy-MM")),
            _ => throw new BusinessRuleException("Group by factory, section, costcentre, asset, job or month."),
        };
        var rows = lines.GroupBy(by).Select(g =>
        {
            decimal Sum(string category) => Math.Round(g.Where(x => x.Category == category).Sum(x => x.Amount), 2);
            var lost = g.Sum(x => x.ProductionLost);
            return new CostSummaryRow(g.Key.Id, g.Key.Name, Sum(CostCategories.Labour), Sum(CostCategories.Parts), Sum(CostCategories.Contractor),
                Math.Round(g.Where(x => x.Category != CostCategories.Downtime).Sum(x => x.Amount), 2), Math.Round(g.Sum(x => x.DowntimeHours), 1), Math.Round(lost, 0),
                settings.ProductionUnitValue is { } v ? Math.Round(lost * v, 2) : null);
        });
        var ordered = key == "month" ? rows.OrderBy(x => x.Name).ToList() : rows.OrderByDescending(x => x.Total).ThenByDescending(x => x.DowntimeHours).ToList();
        return Ok(new CostSummary(name, start, end, currency, settings.ProductionUnit, key, ordered,
            new CostSummaryRow(null, "Total", ordered.Sum(x => x.Labour), ordered.Sum(x => x.Parts), ordered.Sum(x => x.Contractor), ordered.Sum(x => x.Total),
                ordered.Sum(x => x.DowntimeHours), ordered.Sum(x => x.ProductionLost), settings.ProductionUnitValue is null ? null : ordered.Sum(x => x.DowntimeValue ?? 0))));
    }

    /// <summary>One job's costs, every line.</summary>
    [HttpGet("/api/factory/work-orders/{workOrderId:guid}/costs")]
    public async Task<ActionResult<JobCosts>> Job(Guid workOrderId, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var job = await Db.WorkOrders.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == workOrderId && x.ClientId == null, cancellationToken)
            ?? throw new NotFoundException("Job was not found.");
        var (nodes, _) = await ScopeAsync(job.OrgNodeId, cancellationToken);
        var lines = (await kpiService.CostLinesAsync(TenantId, nodes, DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), DateTime.UtcNow.AddDays(1), cancellationToken))
            .Where(x => x.WorkOrderId == workOrderId).ToList();
        var settings = await settingsService.GetOrCreateAsync(TenantId, cancellationToken);
        decimal Sum(string category) => Math.Round(lines.Where(x => x.Category == category).Sum(x => x.Amount), 2);
        var lost = lines.Sum(x => x.ProductionLost);
        return Ok(new JobCosts(workOrderId, Sum(CostCategories.Labour), Sum(CostCategories.Parts), Sum(CostCategories.Contractor),
            Math.Round(lines.Where(x => x.Category != CostCategories.Downtime).Sum(x => x.Amount), 2), Math.Round(lines.Sum(x => x.DowntimeHours), 1), Math.Round(lost, 0),
            settings.ProductionUnitValue is { } v ? Math.Round(lost * v, 2) : null, settings.ProductionUnit, lines.FirstOrDefault()?.CostCentreName));
    }

    [HttpGet("settings")]
    public async Task<ActionResult<CostSettings>> GetSettings(CancellationToken cancellationToken)
    {
        EnsureCanView();
        var s = await settingsService.GetOrCreateAsync(TenantId, cancellationToken);
        return Ok(Map(s, await Db.Tenants.Where(x => x.Id == TenantId).Select(x => x.Currency).SingleAsync(cancellationToken)));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<CostSettings>> SaveSettings([FromBody] SaveCostSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        if (request.ProductionUnitValue is < 0) throw new BusinessRuleException("The value can't be negative.");
        if (request.VatRatePercent is < 0 or > 50) throw new BusinessRuleException("VAT must be between 0 and 50%.");
        if (request.PersonalDataRetentionYears is < 1 or > 50) throw new BusinessRuleException("Keep personal data for 1 to 50 years.");
        var months = PeakSeason.Months(request.PeakSeasonMonths);
        var s = await settingsService.GetOrCreateAsync(TenantId, cancellationToken);
        var tracked = await Db.FactorySettings.SingleAsync(x => x.Id == s.Id, cancellationToken);
        tracked.ProductionUnitValue = request.ProductionUnitValue;
        tracked.VatRatePercent = request.VatRatePercent;
        tracked.PeakSeasonMonths = string.Join(",", months.OrderBy(x => x));
        tracked.PersonalDataRetentionYears = request.PersonalDataRetentionYears;
        tracked.DataResidency = Clean(request.DataResidency);
        await Db.SaveChangesAsync(cancellationToken);
        return Ok(Map(tracked, await Db.Tenants.Where(x => x.Id == TenantId).Select(x => x.Currency).SingleAsync(cancellationToken)));
    }

    /* ── Cost centres ────────────────────────────────────────────────────── */

    [HttpGet("cost-centres")]
    public async Task<ActionResult<IReadOnlyCollection<CostCentreDto>>> CostCentres(CancellationToken cancellationToken)
    {
        EnsureCanView();
        var factories = (await FactoriesInScopeAsync(cancellationToken)).Select(x => x.Id).ToList();
        return Ok(await Db.CostCentres.AsNoTracking().Where(x => x.TenantId == TenantId && factories.Contains(x.FactoryId)).OrderBy(x => x.Factory!.Name).ThenBy(x => x.Code)
            .Select(x => new CostCentreDto(x.Id, x.FactoryId, x.Factory!.Name, x.Code, x.Name, x.OrgNodeId, x.OrgNode != null ? x.OrgNode.Name : null, x.IsActive, x.ExternalId, x.ExternalSystem))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("cost-centres")]
    public async Task<ActionResult<IReadOnlyCollection<CostCentreDto>>> CreateCostCentre([FromBody] SaveCostCentreRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var centre = new CostCentre { TenantId = TenantId };
        await ApplyAsync(centre, request, cancellationToken);
        Db.CostCentres.Add(centre);
        await Db.SaveChangesAsync(cancellationToken);
        return await CostCentres(cancellationToken);
    }

    [HttpPut("cost-centres/{id:guid}")]
    public async Task<ActionResult<IReadOnlyCollection<CostCentreDto>>> UpdateCostCentre(Guid id, [FromBody] SaveCostCentreRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var centre = await Db.CostCentres.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Cost centre was not found.");
        await FactoryAsync(centre.FactoryId, cancellationToken);
        await ApplyAsync(centre, request, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);
        return await CostCentres(cancellationToken);
    }

    /* ── Budgets ─────────────────────────────────────────────────────────── */

    [HttpGet("budgets")]
    public async Task<ActionResult<IReadOnlyCollection<BudgetDto>>> Budgets([FromQuery] int? year, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var factories = (await FactoriesInScopeAsync(cancellationToken)).Select(x => x.Id).ToList();
        var y = year ?? DateTime.UtcNow.Year;
        return Ok(await Db.MaintenanceBudgets.AsNoTracking().Where(x => x.TenantId == TenantId && x.Year == y && factories.Contains(x.FactoryId))
            .OrderBy(x => x.Factory!.Name).ThenBy(x => x.CostCentre != null ? x.CostCentre.Code : "").ThenBy(x => x.Category)
            .Select(x => new BudgetDto(x.Id, x.FactoryId, x.Factory!.Name, x.CostCentreId, x.CostCentre != null ? x.CostCentre.Name : null, x.Year, x.Category, x.Amount, x.Notes))
            .ToListAsync(cancellationToken));
    }

    /// <summary>Sets a year's budget lines for a factory: each (cost centre, category) line is created, changed, or removed when its amount is cleared.</summary>
    [HttpPut("budgets")]
    public async Task<ActionResult<IReadOnlyCollection<BudgetDto>>> SaveBudgets([FromBody] SaveBudgetsRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        await FactoryAsync(request.FactoryId, cancellationToken);
        if (request.Year is < 2000 or > 2100) throw new BusinessRuleException("Choose a year.");
        var centres = await Db.CostCentres.Where(x => x.TenantId == TenantId && x.FactoryId == request.FactoryId).Select(x => x.Id).ToListAsync(cancellationToken);
        var existing = await Db.MaintenanceBudgets.Where(x => x.TenantId == TenantId && x.FactoryId == request.FactoryId && x.Year == request.Year).ToListAsync(cancellationToken);
        foreach (var line in request.Lines)
        {
            var category = CostCategories.Find(line.Category) ?? throw new BusinessRuleException("Category must be Labour, Parts or Contractor.");
            if (line.CostCentreId is { } c && !centres.Contains(c)) throw new BusinessRuleException("Cost centre is not in this factory.");
            if (line.Amount is < 0) throw new BusinessRuleException("A budget can't be negative.");
            var row = existing.FirstOrDefault(x => x.CostCentreId == line.CostCentreId && x.Category == category);
            if (line.Amount is null or 0)
            {
                if (row is not null) Db.MaintenanceBudgets.Remove(row);
                continue;
            }

            if (row is null)
            {
                row = new MaintenanceBudget { TenantId = TenantId, FactoryId = request.FactoryId, CostCentreId = line.CostCentreId, Year = request.Year, Category = category };
                Db.MaintenanceBudgets.Add(row);
                existing.Add(row);
            }

            row.Amount = line.Amount.Value;
            row.Notes = Clean(line.Notes);
        }

        await Db.SaveChangesAsync(cancellationToken);
        return await Budgets(request.Year, cancellationToken);
    }

    private async Task ApplyAsync(CostCentre centre, SaveCostCentreRequest request, CancellationToken cancellationToken)
    {
        await FactoryAsync(request.FactoryId, cancellationToken);
        var code = string.IsNullOrWhiteSpace(request.Code) ? throw new BusinessRuleException("Code is required.") : request.Code.Trim().ToUpperInvariant();
        if (await Db.CostCentres.AnyAsync(x => x.TenantId == TenantId && x.Code == code && x.Id != centre.Id, cancellationToken)) throw new ConflictException($"Cost centre {code} already exists.");
        if (request.OrgNodeId is { } node)
        {
            var subtree = await orgScopeService.GetSubtreeAsync(TenantId, request.FactoryId, cancellationToken);
            if (!subtree.Contains(node)) throw new BusinessRuleException("The location must be in the cost centre's factory.");
            if (await Db.CostCentres.AnyAsync(x => x.TenantId == TenantId && x.OrgNodeId == node && x.Id != centre.Id && x.IsActive, cancellationToken))
                throw new ConflictException("Another cost centre already carries that location's jobs.");
        }

        centre.FactoryId = request.FactoryId;
        centre.Code = code;
        centre.Name = string.IsNullOrWhiteSpace(request.Name) ? throw new BusinessRuleException("Name is required.") : request.Name.Trim();
        centre.OrgNodeId = request.OrgNodeId;
        centre.IsActive = request.IsActive ?? true;
        centre.ExternalId = Clean(request.ExternalId);
        centre.ExternalSystem = Clean(request.ExternalSystem);
    }

    private static CostSettings Map(FactorySettings s, string currency) =>
        new(currency, s.ProductionUnit, s.ProductionUnitValue, s.VatRatePercent, PeakSeason.Months(s.PeakSeasonMonths).OrderBy(x => x).ToList(), s.PersonalDataRetentionYears, s.DataResidency);

    private void EnsureCanView() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewReports, PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders);

    private void EnsureCanManage() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings);
}

/// <summary>Dashboards at group, factory and section level, exports, the factory comparison and the pilot results report.</summary>
[ApiController]
[Authorize]
[Route("api/factory/kpis")]
public sealed class FactoryKpisController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IFactoryKpiService kpiService,
    IScheduledReportService scheduledReportService) : FactoryReportingControllerBase(dbContext, tenantContext, orgScopeService)
{
    [HttpGet]
    public async Task<ActionResult<FactoryKpiReport>> Get([FromQuery] Guid? orgNodeId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var (start, end) = Period(from, to);
        var (nodes, name) = await ScopeAsync(orgNodeId, cancellationToken);
        return Ok(await kpiService.KpisAsync(TenantId, nodes, name, start, end, cancellationToken));
    }

    /// <summary>The group view: each factory's headline figures side by side.</summary>
    [HttpGet("compare")]
    public async Task<ActionResult<IReadOnlyList<FactoryComparisonRow>>> Compare([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var (start, end) = Period(from, to);
        var factories = (await FactoriesInScopeAsync(cancellationToken)).Select(x => x.Id).ToList();
        return Ok(await kpiService.CompareAsync(TenantId, factories, start, end, cancellationToken));
    }

    /// <summary>The KPI report as an Excel workbook (KPIs, costs by section and asset, cost lines) or a PDF board report.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] Guid? orgNodeId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var (start, end) = Period(from, to);
        var (nodes, name) = await ScopeAsync(orgNodeId, cancellationToken);
        var k = await kpiService.KpisAsync(TenantId, nodes, name, start, end, cancellationToken);
        var stem = $"maintenance-{Slug(name)}-{start:yyyyMMdd}-{end.AddDays(-1):yyyyMMdd}";
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            return File(FactoryPdfs.KpiReport(await CompanyAsync(cancellationToken), k), "application/pdf", $"{stem}.pdf");
        }

        var lines = await kpiService.CostLinesAsync(TenantId, nodes, start, end, cancellationToken);
        static string? D(decimal? v) => v?.ToString("0.##");
        var kpiRows = new List<IReadOnlyList<string?>>
        {
            new[] { "Assets", "Total", k.Assets.Total.ToString() }, new[] { "Assets", "Down", k.Assets.Down.ToString() },
            new[] { "Reliability", "Breakdowns", k.Reliability.Breakdowns.ToString() }, new[] { "Reliability", "MTBF (h)", D(k.Reliability.MtbfHours) },
            new[] { "Reliability", "MTTR (h)", D(k.Reliability.MttrHours) }, new[] { "Reliability", "Repeat failures", k.Reliability.RepeatFailures.ToString() },
            new[] { "Planning", "PM compliance %", D(k.Planning.PmCompliancePercent) }, new[] { "Planning", "Overdue jobs", k.Planning.OverdueJobs.ToString() },
            new[] { "Planning", "Backlog (h)", D(k.Planning.BacklogHours) }, new[] { "Planning", "Backlog (weeks)", D(k.Planning.BacklogWeeks) },
            new[] { "Planning", "Planned share %", D(k.Planning.PlannedPercent) },
            new[] { "Weekly window", "Plan completion %", D(k.Window.CompletionPercent) }, new[] { "Weekly window", "Carried over", k.Window.CarriedOver.ToString() },
            new[] { "Overhaul", "Scope done %", D(k.Overhaul.ScopeCompletePercent) }, new[] { "Overhaul", "Actual cost", D(k.Overhaul.ActualCost) }, new[] { "Overhaul", "Budget", D(k.Overhaul.Budget) },
            new[] { "Downtime", "Hours", D(k.Downtime.Hours) }, new[] { "Downtime", $"{k.ProductionUnit} lost", D(k.Downtime.ProductionLost) }, new[] { "Downtime", "Value lost", D(k.Downtime.ValueLost) },
            new[] { "Downtime", "Peak season hours", D(k.Downtime.PeakHours) }, new[] { "Downtime", "Low season hours", D(k.Downtime.LowHours) },
            new[] { "Costs", "Labour", D(k.Costs.Labour) }, new[] { "Costs", "Parts", D(k.Costs.Parts) }, new[] { "Costs", "Contractor", D(k.Costs.Contractor) },
            new[] { "Costs", "Total", D(k.Costs.Total) }, new[] { "Costs", $"Cost per {k.ProductionUnit}", D(k.Costs.CostPerUnit) },
            new[] { "Costs", "Budget for period", D(k.Costs.Budget.BudgetForPeriod) }, new[] { "Costs", "Against budget %", D(k.Costs.Budget.VariancePercent) },
            new[] { "Spares", "Issued value", D(k.Spares.ConsumptionValue) }, new[] { "Spares", "Critical stock-outs", k.Spares.CriticalStockOuts.ToString() },
            new[] { "Spares", "Critical below minimum", k.Spares.CriticalBelowMinimum.ToString() }, new[] { "Spares", "Stock value", D(k.Spares.StockValue) },
            new[] { "People", "Hours logged", D(k.People.LoggedHours) }, new[] { "People", "Overtime %", D(k.People.OvertimePercent) }, new[] { "People", "Utilisation %", D(k.People.UtilisationPercent) },
            new[] { "People", "Certificates expiring", k.People.CertificatesExpiring.ToString() },
            new[] { "Energy", "kWh", D(k.Energy.EnergyKwh) }, new[] { "Energy", $"kWh per {k.ProductionUnit}", D(k.Energy.KwhPerUnit) },
            new[] { "Energy", $"Fuel ({k.Energy.FuelUnit})", D(k.Energy.Fuel) }, new[] { "Energy", $"Fuel per {k.ProductionUnit}", D(k.Energy.FuelPerUnit) },
            new[] { "Compliance", "Statutory overdue", k.Compliance.StatutoryOverdue.ToString() }, new[] { "Compliance", "Statutory due in 30 days", k.Compliance.StatutoryDueSoon.ToString() },
            new[] { "Compliance", "Open corrective actions", k.Compliance.NonConformitiesOpen.ToString() },
        };
        IReadOnlyList<IReadOnlyList<string?>> Breakdown(IEnumerable<CostBreakdown> rows) =>
            rows.Select(x => (IReadOnlyList<string?>)new[] { x.Name, D(x.Labour), D(x.Parts), D(x.Contractor), D(x.Total), D(x.DowntimeHours), D(x.ProductionLost) }).ToList();
        string[] breakdownHeaders = ["Name", "Labour", "Parts", "Contractor", "Total", "Downtime h", $"{k.ProductionUnit} lost"];
        var bytes = TabularFiles.WriteXlsx(
        [
            new SheetSpec("KPIs", ["Category", "Measure", "Value"], kpiRows),
            new SheetSpec("By factory", breakdownHeaders, Breakdown(k.Costs.ByFactory)),
            new SheetSpec("By section", breakdownHeaders, Breakdown(k.Costs.BySection)),
            new SheetSpec("By cost centre", breakdownHeaders, Breakdown(k.Costs.ByCostCentre)),
            new SheetSpec("Cost lines", ["Date", "Job", "Title", "Asset", "Factory", "Section", "Cost centre", "Category", "Amount", "Downtime h", $"{k.ProductionUnit} lost"],
                lines.OrderBy(x => x.At).Select(x => (IReadOnlyList<string?>)new[] { x.At.ToString("yyyy-MM-dd"), x.WorkOrderNumber, x.Title, x.AssetCode, x.FactoryName, x.SectionName, x.CostCentreName, x.Category, D(x.Amount), D(x.DowntimeHours), D(x.ProductionLost) }).ToList()),
        ]);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{stem}.xlsx");
    }

    /// <summary>Pilot results: the baseline period against the pilot period, per 30 days where it matters.</summary>
    [HttpGet("pilot")]
    public async Task<IActionResult> Pilot([FromQuery] Guid? orgNodeId, [FromQuery] DateTime baselineFrom, [FromQuery] DateTime baselineTo, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? format, CancellationToken cancellationToken)
    {
        EnsureCanView();
        var (bStart, bEnd) = Period(baselineFrom, baselineTo);
        var (start, end) = Period(from, to);
        if (bEnd > start) throw new BusinessRuleException("The baseline must end before the pilot period starts.");
        var (nodes, name) = await ScopeAsync(orgNodeId, cancellationToken);
        var report = await kpiService.PilotAsync(TenantId, nodes, name, bStart, bEnd, start, end, cancellationToken);
        return string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase)
            ? File(FactoryPdfs.Pilot(await CompanyAsync(cancellationToken), report), "application/pdf", $"pilot-results-{Slug(name)}.pdf")
            : Ok(report);
    }

    /* ── Scheduled board reports ─────────────────────────────────────────── */

    [HttpGet("schedules")]
    public async Task<ActionResult<IReadOnlyCollection<KpiScheduleDto>>> Schedules(CancellationToken cancellationToken)
    {
        EnsureCanView();
        var reports = await scheduledReportService.ListAsync(TenantId, cancellationToken);
        var names = await Db.OrgNodes.AsNoTracking().Where(x => x.TenantId == TenantId).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return Ok(reports.Where(x => x.ReportType == ScheduledReportService.FactoryKpiReportType)
            .Select(x => new KpiScheduleDto(x.Id, x.Name, x.OrgNodeId, x.OrgNodeId is { } n ? names.GetValueOrDefault(n) : null, x.Frequency, x.DayOfMonth, x.DayOfWeek, x.TimeOfDay, x.DateRangePreset,
                x.Recipients, x.IsActive, x.LastRunAt, x.NextRunAt, x.LastRunStatus)).ToList());
    }

    [HttpPost("schedules")]
    public async Task<ActionResult<IReadOnlyCollection<KpiScheduleDto>>> Schedule([FromBody] SaveKpiScheduleRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings);
        if (request.OrgNodeId is { } node) await ScopeAsync(node, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Recipients)) throw new BusinessRuleException("Add at least one email address.");
        await scheduledReportService.CreateAsync(TenantId, new CreateScheduledReportRequest(
            string.IsNullOrWhiteSpace(request.Name) ? "Maintenance board report" : request.Name.Trim(), ScheduledReportService.FactoryKpiReportType,
            request.Frequency ?? "Monthly", request.DayOfMonth ?? (request.Frequency is null or "Monthly" or "Quarterly" ? 1 : null), request.DayOfWeek, request.TimeOfDay ?? "07:00",
            request.DateRangePreset ?? "LastMonth", null, null, request.Recipients, request.OrgNodeId), cancellationToken);
        return await Schedules(cancellationToken);
    }

    [HttpDelete("schedules/{id:guid}")]
    public async Task<IActionResult> Unschedule(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings);
        var report = await Db.ScheduledReports.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.ReportType == ScheduledReportService.FactoryKpiReportType, cancellationToken)
            ?? throw new NotFoundException("Schedule was not found.");
        Db.ScheduledReports.Remove(report);
        await Db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string Slug(string value) => new(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    private void EnsureCanView() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewReports, PermissionNames.CanViewWorkOrders, PermissionNames.CanManageSettings);
}

public sealed record CostSummaryRow(Guid? Id, string Name, decimal Labour, decimal Parts, decimal Contractor, decimal Total, decimal DowntimeHours, decimal ProductionLost, decimal? DowntimeValue);
public sealed record CostSummary(string ScopeName, DateTime From, DateTime To, string Currency, string ProductionUnit, string GroupBy, IReadOnlyCollection<CostSummaryRow> Rows, CostSummaryRow Totals);
public sealed record JobCosts(Guid WorkOrderId, decimal Labour, decimal Parts, decimal Contractor, decimal Total, decimal DowntimeHours, decimal ProductionLost, decimal? DowntimeValue, string ProductionUnit, string? CostCentre);
public sealed record CostSettings(string Currency, string ProductionUnit, decimal? ProductionUnitValue, decimal VatRatePercent, IReadOnlyCollection<int> PeakSeasonMonths, int PersonalDataRetentionYears, string? DataResidency);
public sealed record SaveCostSettingsRequest(decimal? ProductionUnitValue, decimal VatRatePercent, string? PeakSeasonMonths, int PersonalDataRetentionYears, [StringLength(200)] string? DataResidency = null);
public sealed record CostCentreDto(Guid Id, Guid FactoryId, string FactoryName, string Code, string Name, Guid? OrgNodeId, string? OrgNodeName, bool IsActive, string? ExternalId, string? ExternalSystem);
public sealed record SaveCostCentreRequest(Guid FactoryId, [StringLength(30)] string Code, [StringLength(150)] string Name, Guid? OrgNodeId = null, bool? IsActive = null,
    [StringLength(100)] string? ExternalId = null, [StringLength(50)] string? ExternalSystem = null);
public sealed record BudgetDto(Guid Id, Guid FactoryId, string FactoryName, Guid? CostCentreId, string? CostCentreName, int Year, string Category, decimal Amount, string? Notes);
public sealed record SaveBudgetLine(Guid? CostCentreId, string Category, decimal? Amount, [StringLength(500)] string? Notes = null);
public sealed record SaveBudgetsRequest(Guid FactoryId, int Year, IReadOnlyCollection<SaveBudgetLine> Lines);
public sealed record KpiScheduleDto(Guid Id, string Name, Guid? OrgNodeId, string? OrgNodeName, string Frequency, int? DayOfMonth, string? DayOfWeek, string TimeOfDay, string DateRangePreset,
    string Recipients, bool IsActive, DateTime? LastRunAt, DateTime? NextRunAt, string? LastRunStatus);
public sealed record SaveKpiScheduleRequest([StringLength(200)] string? Name, string Recipients, Guid? OrgNodeId = null, string? Frequency = null, int? DayOfMonth = null, string? DayOfWeek = null,
    string? TimeOfDay = null, string? DateRangePreset = null);
