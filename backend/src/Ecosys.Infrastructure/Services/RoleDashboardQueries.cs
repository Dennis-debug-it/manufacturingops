using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>Who a widget query runs for: their tenant, permissions and branch scope.</summary>
public sealed record DashboardQueryViewer(Guid TenantId, Guid UserId, bool IsAdmin, Func<string, bool> HasPermission, BranchQueryScope Scope)
{
    public bool Can(string permission) => IsAdmin || HasPermission(permission);
}

/// <summary>
/// One query per catalogue widget (<see cref="DashboardWidgetCatalog"/>). Every figure carries the link
/// to the office list behind it, filtered (through that list's URL) to exactly the rows it counts. Lists are short (at most
/// <see cref="ListLimit"/> rows) and every money figure is per currency.
/// </summary>
internal sealed class RoleDashboardQueries(
    AppDbContext dbContext,
    ITenantTimeZoneService tenantTimeZoneService)
{
    public const int ListLimit = 6;

    /// <summary>Statuses that are no longer open work, as the existing dashboard and the list's <c>state=open</c> filter count them.</summary>
    public static readonly string[] NonOpenWorkOrderStatuses = WorkOrderListStates.NonOpenStatuses;

    /// <summary>Same window the work-order list's <c>slaStatus=at-risk</c> filter uses.</summary>
    private const int SlaAtRiskWindowMinutes = 120;

    private sealed record Clock(DateTime UtcNow, DateOnly Today, DateTime TodayStartUtc, DateTime TodayEndUtc, DateTime MonthStartUtc, DateOnly MonthStart);

    public async Task<DashboardWidgetData> RunAsync(DashboardWidgetDefinition widget, DashboardQueryViewer viewer, CancellationToken cancellationToken)
    {
        var clock = await ClockAsync(viewer.TenantId, cancellationToken);
        var (stats, items, note) = widget.Key switch
        {
            DashboardWidgetCatalog.WorkOrdersOpenOverdue => await OpenOverdueAsync(viewer, clock, cancellationToken),
            DashboardWidgetCatalog.WorkOrdersSlaAtRisk => await SlaAtRiskAsync(viewer, clock, cancellationToken),
            DashboardWidgetCatalog.WorkOrdersUnassigned => await UnassignedAsync(viewer, cancellationToken),
            DashboardWidgetCatalog.DispatchTodaysVisits => await TodaysVisitsAsync(viewer, clock, cancellationToken),
            DashboardWidgetCatalog.TechniciansWorkload => await WorkloadAsync(viewer, cancellationToken),
            DashboardWidgetCatalog.TechniciansOnSite => await OnSiteAsync(viewer, cancellationToken),
            DashboardWidgetCatalog.MaintenancePmCompliance => await PmComplianceAsync(viewer, clock, cancellationToken),
            DashboardWidgetCatalog.ContractsExpiring => await ContractsExpiringAsync(viewer, clock, cancellationToken),
            DashboardWidgetCatalog.MaterialsLowStock => await LowStockAsync(viewer, cancellationToken),
            DashboardWidgetCatalog.MaterialsPendingIssues => await PendingIssuesAsync(viewer, cancellationToken),
            DashboardWidgetCatalog.PurchaseOrdersOpen => await OpenPurchaseOrdersAsync(viewer, clock, cancellationToken),
            DashboardWidgetCatalog.FinanceReceivablesOverdue => await ReceivablesOverdueAsync(viewer, clock, cancellationToken),
            DashboardWidgetCatalog.FinanceReceivablesAgeing => await ReceivablesAgeingAsync(viewer, clock, cancellationToken),
            DashboardWidgetCatalog.FinanceCashReceived => await CashReceivedAsync(viewer, clock, cancellationToken),
            DashboardWidgetCatalog.FinanceTaxWithheld => await TaxWithheldAsync(viewer, clock, cancellationToken),
            DashboardWidgetCatalog.FinanceInvoicesToSend => await InvoicesToSendAsync(viewer, cancellationToken),
            DashboardWidgetCatalog.ActionItemsDue => await ActionItemsDueAsync(viewer, clock, cancellationToken),
            _ => throw new InvalidOperationException($"No query for dashboard widget '{widget.Key}'."),
        };
        return new DashboardWidgetData(widget.Key, widget.Title, stats, items, note, clock.UtcNow);
    }

    private async Task<Clock> ClockAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var zone = await tenantTimeZoneService.GetTimeZoneAsync(tenantId, cancellationToken);
        var utcNow = DateTime.UtcNow;
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone).Date;
        var monthStart = new DateTime(localToday.Year, localToday.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localToday, DateTimeKind.Unspecified), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localToday.AddDays(1), DateTimeKind.Unspecified), zone);
        return new Clock(utcNow, DateOnly.FromDateTime(localToday), start, end,
            TimeZoneInfo.ConvertTimeToUtc(monthStart, zone), DateOnly.FromDateTime(monthStart));
    }

    private sealed record Result(IReadOnlyList<DashboardStat> Stats, IReadOnlyList<DashboardListItem> Items, string? Note = null)
    {
        public void Deconstruct(out IReadOnlyList<DashboardStat> stats, out IReadOnlyList<DashboardListItem> items, out string? note)
        {
            stats = Stats;
            items = Items;
            note = Note;
        }
    }

    private static DashboardStat Count(string label, int value, string link, string? tone = null) =>
        new(label, value, "number", null, link, value > 0 ? tone : null);

    private static string InvoicesLink(string age, string? currency = null) =>
        currency is null ? $"/finance/invoices?age={age}" : $"/finance/invoices?age={age}&currency={Uri.EscapeDataString(currency)}";

    // ── Operations ───────────────────────────────────────────────────────────

    private IQueryable<WorkOrder> WorkOrders(DashboardQueryViewer viewer) =>
        dbContext.WorkOrders.AsNoTracking().Where(x => x.TenantId == viewer.TenantId).WhereAccessible(viewer.Scope, x => x.BranchId);

    private IQueryable<WorkOrder> OpenWorkOrders(DashboardQueryViewer viewer) =>
        WorkOrders(viewer).Where(x => !NonOpenWorkOrderStatuses.Contains(x.Status));

    private IQueryable<WorkOrderTechnicianAssignment> OpenAssignments(DashboardQueryViewer viewer)
    {
        var open = OpenWorkOrders(viewer).Select(x => x.Id);
        return dbContext.WorkOrderTechnicianAssignments.AsNoTracking()
            .Where(x => x.TenantId == viewer.TenantId && open.Contains(x.WorkOrderId));
    }

    private async Task<Result> OpenOverdueAsync(DashboardQueryViewer viewer, Clock clock, CancellationToken ct)
    {
        var open = await OpenWorkOrders(viewer).CountAsync(ct);
        var overdue = await OpenWorkOrders(viewer).CountAsync(x => x.DueDate != null && x.DueDate < clock.UtcNow, ct);
        var oldest = await OpenWorkOrders(viewer).Where(x => x.DueDate != null && x.DueDate < clock.UtcNow)
            .OrderBy(x => x.DueDate).Take(ListLimit)
            .Select(x => new { x.Id, x.WorkOrderNumber, x.Title, x.DueDate })
            .ToListAsync(ct);
        return new Result(
            [Count("Open", open, "/work-orders?state=open"), Count("Overdue", overdue, "/work-orders?state=overdue", "danger")],
            [.. oldest.Select(x => new DashboardListItem($"{x.WorkOrderNumber} {x.Title}", $"Due {x.DueDate:dd MMM}", null, null, null, $"/work-orders/{x.Id}", "danger"))]);
    }

    private async Task<Result> SlaAtRiskAsync(DashboardQueryViewer viewer, Clock clock, CancellationToken ct)
    {
        var threshold = clock.UtcNow.AddMinutes(SlaAtRiskWindowMinutes);
        var atRiskQuery = OpenWorkOrders(viewer).Where(x => !x.SlaResponseBreached && !x.SlaResolutionBreached
            && x.SlaResolutionDeadline != null && x.SlaResolutionDeadline <= threshold);
        var atRisk = await atRiskQuery.CountAsync(ct);
        var breached = await OpenWorkOrders(viewer).CountAsync(x => x.SlaResponseBreached || x.SlaResolutionBreached, ct);
        var soonest = await atRiskQuery.OrderBy(x => x.SlaResolutionDeadline).Take(ListLimit)
            .Select(x => new { x.Id, x.WorkOrderNumber, x.Title, x.SlaResolutionDeadline })
            .ToListAsync(ct);
        return new Result(
            [Count("At risk", atRisk, "/work-orders?state=open&slaStatus=at-risk", "warning"), Count("Breached", breached, "/work-orders?state=open&slaStatus=breached", "danger")],
            [.. soonest.Select(x => new DashboardListItem($"{x.WorkOrderNumber} {x.Title}", $"Resolve by {x.SlaResolutionDeadline:dd MMM HH:mm} UTC", null, null, null, $"/work-orders/{x.Id}", "warning"))]);
    }

    private async Task<Result> UnassignedAsync(DashboardQueryViewer viewer, CancellationToken ct)
    {
        var query = OpenWorkOrders(viewer).Where(x => x.AssignmentGroupId == null && !x.TechnicianAssignments.Any());
        var count = await query.CountAsync(ct);
        var oldest = await query.OrderBy(x => x.CreatedAt).Take(ListLimit)
            .Select(x => new { x.Id, x.WorkOrderNumber, x.Title, x.Priority })
            .ToListAsync(ct);
        return new Result(
            [Count("Unassigned", count, "/work-orders?state=open&unassigned=1", "warning")],
            [.. oldest.Select(x => new DashboardListItem($"{x.WorkOrderNumber} {x.Title}", x.Priority, null, null, null, $"/work-orders/{x.Id}"))]);
    }

    private async Task<Result> TodaysVisitsAsync(DashboardQueryViewer viewer, Clock clock, CancellationToken ct)
    {
        var today = WorkOrders(viewer)
            .Where(x => x.ScheduledStartAt >= clock.TodayStartUtc && x.ScheduledStartAt < clock.TodayEndUtc && x.Status != WorkOrderStatuses.Cancelled)
            .Select(x => x.Id);
        var visits = await today.CountAsync(ct);
        var rows = await dbContext.WorkOrderTechnicianAssignments.AsNoTracking()
            .Where(x => x.TenantId == viewer.TenantId && today.Contains(x.WorkOrderId))
            .Select(x => new { x.TechnicianId, Name = x.Technician != null ? x.Technician.FullName : "Technician", Arrived = x.ArrivalAt != null })
            .ToListAsync(ct);
        var byTechnician = rows.GroupBy(x => new { x.TechnicianId, x.Name })
            .Select(g => new { g.Key.TechnicianId, g.Key.Name, Visits = g.Count(), Arrived = g.Count(x => x.Arrived) })
            .OrderByDescending(x => x.Visits).ThenBy(x => x.Name)
            .ToList();
        return new Result(
            [Count("Visits today", visits, "/dispatch/board"), Count("Technicians out", byTechnician.Count, "/dispatch/board")],
            [.. byTechnician.Take(ListLimit).Select(x => new DashboardListItem(x.Name, $"{x.Arrived} of {x.Visits} arrived", x.Visits, "number", null, $"/dispatch/route/{x.TechnicianId}"))]);
    }

    private async Task<Result> WorkloadAsync(DashboardQueryViewer viewer, CancellationToken ct)
    {
        var rows = await OpenAssignments(viewer)
            .Select(x => new { x.TechnicianId, Name = x.Technician != null ? x.Technician.FullName : "Technician", x.Status, OnSite = x.ArrivalAt != null && x.DepartureAt == null })
            .ToListAsync(ct);
        var byTechnician = rows.GroupBy(x => new { x.TechnicianId, x.Name })
            .Select(g => new { g.Key.TechnicianId, g.Key.Name, Active = g.Count(), Pending = g.Count(x => x.Status == "Pending"), OnSite = g.Count(x => x.OnSite) })
            .OrderByDescending(x => x.Active).ThenBy(x => x.Name)
            .ToList();
        return new Result(
            [Count("Assigned jobs", rows.Count, "/work-orders?state=open"), Count("Awaiting response", rows.Count(x => x.Status == "Pending"), "/dispatch/board", "warning")],
            [.. byTechnician.Take(ListLimit).Select(x => new DashboardListItem(x.Name,
                x.Pending > 0 ? $"{x.Pending} awaiting response" : x.OnSite > 0 ? "On site" : null,
                x.Active, "number", null, $"/work-orders?state=open&technicianId={x.TechnicianId}"))]);
    }

    private async Task<Result> OnSiteAsync(DashboardQueryViewer viewer, CancellationToken ct)
    {
        var rows = await OpenAssignments(viewer).Where(x => x.ArrivalAt != null && x.DepartureAt == null)
            .OrderBy(x => x.ArrivalAt)
            .Select(x => new { x.WorkOrderId, x.TechnicianId, Name = x.Technician != null ? x.Technician.FullName : "Technician", Number = x.WorkOrder!.WorkOrderNumber })
            .ToListAsync(ct);
        return new Result(
            [Count("On site", rows.Select(x => x.TechnicianId).Distinct().Count(), "/dispatch/map")],
            [.. rows.Take(ListLimit).Select(x => new DashboardListItem(x.Name, x.Number, null, null, null, $"/work-orders/{x.WorkOrderId}"))]);
    }

    private async Task<Result> PmComplianceAsync(DashboardQueryViewer viewer, Clock clock, CancellationToken ct)
    {
        var plans = dbContext.PreventiveMaintenancePlans.AsNoTracking()
            .Where(x => x.TenantId == viewer.TenantId && x.Status == "Active")
            .WhereAccessible(viewer.Scope, x => x.BranchId);
        var active = await plans.CountAsync(ct);
        var overdue = await plans.CountAsync(x => x.NextPmDate != null && x.NextPmDate < clock.UtcNow, ct);
        var dueSoon = await plans.CountAsync(x => x.NextPmDate != null && x.NextPmDate >= clock.UtcNow && x.NextPmDate < clock.UtcNow.AddDays(7), ct);

        var since = clock.UtcNow.AddDays(-30);
        var pm = WorkOrders(viewer).Where(x => x.IsPreventiveMaintenance && x.DueDate != null && x.DueDate >= since && x.DueDate <= clock.UtcNow
            && x.Status != WorkOrderStatuses.Cancelled);
        var due = await pm.CountAsync(ct);
        var onTime = await pm.CountAsync(x => x.CompletedAt != null && x.CompletedAt <= x.DueDate, ct);
        var rate = due == 0 ? 100m : Math.Round(onTime * 100m / due, 1);
        return new Result(
            [
                new DashboardStat("On time (30 days)", rate, "percent", null, "/maintenance/compliance", rate < 80 ? "warning" : null),
                Count("Plans overdue", overdue, "/maintenance/compliance", "danger"),
                Count("Due in 7 days", dueSoon, "/maintenance/calendar"),
                Count("Active plans", active, "/maintenance/schedules"),
            ],
            [],
            due == 0 ? "No preventive maintenance fell due in the last 30 days." : null);
    }

    private async Task<Result> ContractsExpiringAsync(DashboardQueryViewer viewer, Clock clock, CancellationToken ct)
    {
        var contracts = dbContext.Contracts.AsNoTracking()
            .Where(x => x.TenantId == viewer.TenantId && !x.IsArchived && x.EndDate >= clock.TodayStartUtc
                && x.Status != ContractStatus.Expired && x.Status != ContractStatus.Terminated && x.Status != ContractStatus.Cancelled);
        var in30 = await contracts.CountAsync(x => x.EndDate < clock.TodayStartUtc.AddDays(31), ct);
        var in90 = await contracts.CountAsync(x => x.EndDate < clock.TodayStartUtc.AddDays(91), ct);
        var soonest = await contracts.Where(x => x.EndDate < clock.TodayStartUtc.AddDays(91)).OrderBy(x => x.EndDate).Take(ListLimit)
            .Select(x => new { x.Id, x.ContractNumber, x.Name, x.EndDate })
            .ToListAsync(ct);
        return new Result(
            [Count("Within 30 days", in30, "/contracts?expiring=30", "warning"), Count("Within 90 days", in90, "/contracts?expiring=90")],
            [.. soonest.Select(x => new DashboardListItem($"{x.ContractNumber} {x.Name}", $"Ends {x.EndDate:dd MMM yyyy}", null, null, null, $"/contracts/{x.Id}"))]);
    }

    private async Task<Result> LowStockAsync(DashboardQueryViewer viewer, CancellationToken ct)
    {
        // Same rule as the existing dashboard: per-branch stock once branches exist, else the item totals.
        var rows = viewer.Scope.HasConfiguredBranches
            ? await dbContext.BranchMaterialStocks.AsNoTracking()
                .Where(x => x.TenantId == viewer.TenantId && x.QuantityOnHand <= x.ReorderLevel && (x.QuantityOnHand != 0 || x.ReorderLevel > 0))
                .WhereAccessible(viewer.Scope, x => (Guid?)x.BranchId)
                .OrderBy(x => x.QuantityOnHand - x.ReorderLevel)
                .Select(x => new { x.MaterialId, Name = x.Material!.ItemName, x.QuantityOnHand, x.ReorderLevel, Where = (string?)x.Branch!.Name })
                .ToListAsync(ct)
            : await dbContext.MaterialItems.AsNoTracking()
                .Where(x => x.TenantId == viewer.TenantId && x.IsActive && x.QuantityOnHand <= x.ReorderLevel && (x.QuantityOnHand != 0 || x.ReorderLevel > 0))
                .OrderBy(x => x.QuantityOnHand - x.ReorderLevel)
                .Select(x => new { MaterialId = x.Id, Name = x.ItemName, x.QuantityOnHand, x.ReorderLevel, Where = (string?)null })
                .ToListAsync(ct);
        return new Result(
            [Count("At or below reorder level", rows.Count, "/inventory/alerts", "warning")],
            [.. rows.Take(ListLimit).Select(x => new DashboardListItem(x.Name,
                $"{x.QuantityOnHand:0.##} on hand, reorder at {x.ReorderLevel:0.##}{(x.Where is null ? "" : $" · {x.Where}")}",
                x.QuantityOnHand, "number", null, $"/inventory/parts/{x.MaterialId}", "warning"))]);
    }

    private async Task<Result> PendingIssuesAsync(DashboardQueryViewer viewer, CancellationToken ct)
    {
        var issues = dbContext.MaterialIssues.AsNoTracking()
            .Where(x => x.TenantId == viewer.TenantId)
            .WhereAccessible(viewer.Scope, x => x.BranchId);
        var awaitingApproval = await issues.CountAsync(x => x.IssueStatus == "pending_approval", ct);
        var awaitingIssue = await issues.CountAsync(x => x.IssueStatus == "approved", ct);
        var oldest = await issues.Where(x => x.IssueStatus == "pending_approval" || x.IssueStatus == "approved")
            .OrderBy(x => x.CreatedAt).Take(ListLimit)
            .Select(x => new { x.IssueNumber, x.IssueStatus, x.TotalEstimatedCost })
            .ToListAsync(ct);
        return new Result(
            [Count("Awaiting approval", awaitingApproval, "/inventory/requests?stage=Requested", "warning"), Count("Approved, not issued", awaitingIssue, "/inventory/requests?stage=Approved")],
            [.. oldest.Select(x => new DashboardListItem(x.IssueNumber, x.IssueStatus == "approved" ? "Approved, not issued" : "Awaiting approval", null, null, null, $"/inventory/requests?stage={(x.IssueStatus == "approved" ? "Approved" : "Requested")}&q={x.IssueNumber}"))]);
    }

    private async Task<Result> OpenPurchaseOrdersAsync(DashboardQueryViewer viewer, Clock clock, CancellationToken ct)
    {
        var all = dbContext.PurchaseOrders.AsNoTracking().Where(x => x.TenantId == viewer.TenantId);
        var orders = PurchaseOrderListStates.Apply(all, PurchaseOrderListStates.Open, clock.TodayStartUtc);
        var open = await orders.CountAsync(ct);
        var late = await PurchaseOrderListStates.Apply(all, PurchaseOrderListStates.Late, clock.TodayStartUtc).CountAsync(ct);
        var list = await orders.OrderBy(x => x.ExpectedAt == null).ThenBy(x => x.ExpectedAt).ThenBy(x => x.OrderedAt).Take(ListLimit)
            .Select(x => new { x.Id, x.PoNumber, Supplier = x.Supplier!.SupplierName, x.Status, x.ExpectedAt, x.TotalAmount, x.Currency })
            .ToListAsync(ct);
        return new Result(
            [Count("Open", open, "/inventory/purchase-orders?state=open"), Count("Past expected date", late, "/inventory/purchase-orders?state=late", "danger")],
            [.. list.Select(x => new DashboardListItem($"{x.PoNumber} {x.Supplier}",
                x.ExpectedAt is null ? x.Status : $"{x.Status} · expected {x.ExpectedAt:dd MMM}", x.TotalAmount, "money", x.Currency,
                $"/inventory/purchase-orders/{x.Id}", x.Status != "Draft" && x.ExpectedAt < clock.TodayStartUtc ? "danger" : null))]);
    }

    // ── Finance ──────────────────────────────────────────────────────────────

    /// <summary>The tenant's invoices; <see cref="ReceivableAges"/> narrows them as the invoice list's <c>age</c> filter does.</summary>
    private IQueryable<TenantInvoice> Invoices(DashboardQueryViewer viewer) =>
        dbContext.TenantInvoices.AsNoTracking().Where(x => x.TenantId == viewer.TenantId);

    private async Task<Result> ReceivablesOverdueAsync(DashboardQueryViewer viewer, Clock clock, CancellationToken ct)
    {
        var overdue = await ReceivableAges.Apply(Invoices(viewer), ReceivableAges.Overdue, clock.TodayStartUtc)
            .Select(x => new { x.Id, x.InvoiceNumber, Client = x.Client!.ClientName, x.Currency, x.Balance, x.DueDate })
            .ToListAsync(ct);
        var stats = new List<DashboardStat> { Count("Overdue invoices", overdue.Count, InvoicesLink(ReceivableAges.Overdue), "danger") };
        stats.AddRange(overdue.GroupBy(x => x.Currency).OrderByDescending(g => g.Count())
            .Select(g => new DashboardStat("Overdue balance", g.Sum(x => x.Balance), "money", g.Key, InvoicesLink(ReceivableAges.Overdue, g.Key), "danger")));
        return new Result(stats,
            [.. overdue.OrderBy(x => x.DueDate).Take(ListLimit).Select(x => new DashboardListItem($"{x.InvoiceNumber} {x.Client}",
                $"{ReceivableAges.DaysOverdue(x.DueDate, clock.TodayStartUtc)} days overdue", x.Balance, "money", x.Currency, $"/finance/invoices/{x.Id}", "danger"))]);
    }

    private async Task<Result> ReceivablesAgeingAsync(DashboardQueryViewer viewer, Clock clock, CancellationToken ct)
    {
        var rows = await ReceivableAges.Receivables(Invoices(viewer)).Select(x => new { x.Currency, x.Balance, x.DueDate }).ToListAsync(ct);
        var stats = new List<DashboardStat>();
        var currencies = rows.GroupBy(x => x.Currency).OrderByDescending(g => g.Sum(x => x.Balance)).ToList();
        foreach (var currency in currencies)
        {
            foreach (var bucket in ReceivableAges.Buckets)
            {
                var amount = currency.Where(x => ReceivableAges.BucketOf(x.DueDate, clock.TodayStartUtc) == bucket.Key).Sum(x => x.Balance);
                stats.Add(new DashboardStat(bucket.Label, amount, "money", currency.Key, InvoicesLink(bucket.Key, currency.Key), amount > 0 ? bucket.Tone : null));
            }
        }
        return new Result(stats, [], rows.Count == 0 ? "Nothing is owed." : null);
    }

    private async Task<Result> CashReceivedAsync(DashboardQueryViewer viewer, Clock clock, CancellationToken ct)
    {
        var payments = await dbContext.TenantPayments.AsNoTracking()
            .Where(x => x.TenantId == viewer.TenantId && x.Status == "Confirmed" && x.PaymentDate >= clock.MonthStartUtc && x.PaymentDate < clock.TodayEndUtc)
            .Select(x => new { Currency = x.Invoice != null ? x.Invoice.Currency : null, x.Amount })
            .ToListAsync(ct);
        var link = $"/finance/receipts?status=Confirmed&from={clock.MonthStart:yyyy-MM-dd}";
        var stats = new List<DashboardStat> { Count("Receipts", payments.Count, link) };
        stats.AddRange(payments.GroupBy(x => x.Currency ?? "KES").OrderByDescending(g => g.Count())
            .Select(g => new DashboardStat("Received", g.Sum(x => x.Amount), "money", g.Key, link)));
        return new Result(stats, []);
    }

    private async Task<Result> TaxWithheldAsync(DashboardQueryViewer viewer, Clock clock, CancellationToken ct)
    {
        var rows = await dbContext.WithholdingCertificates.AsNoTracking()
            .Where(x => x.TenantId == viewer.TenantId && x.Status == "Confirmed" && x.CertificateDate >= clock.MonthStart && x.CertificateDate <= clock.Today)
            .Select(x => new { x.TaxType, x.Amount, Currency = x.Invoice!.Currency })
            .ToListAsync(ct);
        var stats = new List<DashboardStat> { Count("Certificates", rows.Count, "/finance/receipts") };
        stats.AddRange(rows.GroupBy(x => new { x.TaxType, x.Currency }).OrderBy(g => g.Key.TaxType)
            .Select(g => new DashboardStat(g.Key.TaxType == WithholdingTaxTypes.Vat ? "VAT withheld" : "Income tax withheld", g.Sum(x => x.Amount), "money", g.Key.Currency, "/finance/receipts")));
        return new Result(stats, []);
    }

    private async Task<Result> InvoicesToSendAsync(DashboardQueryViewer viewer, CancellationToken ct)
    {
        var drafts = dbContext.TenantInvoices.AsNoTracking()
            .Where(x => x.TenantId == viewer.TenantId && x.DocumentType == "Invoice" && x.Status == "Draft");
        var count = await drafts.CountAsync(ct);
        var oldest = await drafts.OrderBy(x => x.InvoiceDate).Take(ListLimit)
            .Select(x => new { x.Id, x.InvoiceNumber, Client = x.Client!.ClientName, x.TotalAmount, x.Currency })
            .ToListAsync(ct);
        return new Result(
            [Count("Drafts", count, "/finance/invoices?status=Draft", "warning")],
            [.. oldest.Select(x => new DashboardListItem($"{x.InvoiceNumber} {x.Client}", null, x.TotalAmount, "money", x.Currency, $"/finance/invoices/{x.Id}"))]);
    }

    // ── Activities ───────────────────────────────────────────────────────────

    /// <summary>Days ahead that count as "due this week", today included (the action items page's <c>?due=week</c>).</summary>
    private const int ActionItemsDueSoonDays = 7;

    private async Task<Result> ActionItemsDueAsync(DashboardQueryViewer viewer, Clock clock, CancellationToken ct)
    {
        var lastSoon = clock.Today.AddDays(ActionItemsDueSoonDays - 1);
        var rows = await dbContext.ActionItems.AsNoTracking()
            .Where(x => x.TenantId == viewer.TenantId && x.AssigneeUserId == viewer.UserId && x.Status == ActionItemStatuses.Open)
            .Select(x => new { x.Title, x.DueDate })
            .ToListAsync(ct);
        var overdue = rows.Where(x => x.DueDate < clock.Today).OrderBy(x => x.DueDate).ToList();
        var soon = rows.Where(x => x.DueDate >= clock.Today && x.DueDate <= lastSoon).OrderBy(x => x.DueDate).ToList();
        return new Result(
            [
                Count("Overdue", overdue.Count, "/action-items?due=overdue", "danger"),
                Count("Due this week", soon.Count, "/action-items?due=week", "warning"),
                Count("Open", rows.Count, "/action-items"),
            ],
            [.. overdue.Concat(soon).Take(ListLimit).Select(x => new DashboardListItem(x.Title,
                x.DueDate < clock.Today ? $"Overdue since {x.DueDate:dd MMM}" : $"Due {x.DueDate:dd MMM}",
                null, null, null, x.DueDate < clock.Today ? "/action-items?due=overdue" : "/action-items?due=week",
                x.DueDate < clock.Today ? "danger" : null))]);
    }
}
