using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>Labour from the muster roll: man-days, and cost at the office-set rate in effect on each day (D6, D8).</summary>
public static class ProjectLabour
{
    public static decimal? RateOn(IEnumerable<ProjectWorkerRate> rates, DateOnly workDate) =>
        rates.Where(x => x.EffectiveFrom <= workDate).OrderByDescending(x => x.EffectiveFrom).Select(x => (decimal?)x.DailyRate).FirstOrDefault();

    public sealed record WorkerLabour(Guid WorkerId, decimal ManDays, decimal UncostedDays, decimal Cost);

    public static IReadOnlyList<WorkerLabour> Summarize(IEnumerable<ProjectMusterEntry> entries, IEnumerable<ProjectWorkerRate> rates)
    {
        var ratesByWorker = rates.GroupBy(x => x.ProjectWorkerId).ToDictionary(g => g.Key, g => g.ToList());
        return entries
            .Where(x => x.DayFraction > 0)
            .GroupBy(x => x.ProjectWorkerId)
            .Select(g =>
            {
                var workerRates = ratesByWorker.GetValueOrDefault(g.Key) ?? [];
                decimal cost = 0, uncosted = 0;
                foreach (var entry in g)
                {
                    var rate = RateOn(workerRates, entry.WorkDate);
                    if (rate is null) uncosted += entry.DayFraction;
                    else cost += entry.DayFraction * rate.Value;
                }
                return new WorkerLabour(g.Key, g.Sum(x => x.DayFraction), uncosted, Math.Round(cost, 2));
            })
            .ToList();
    }
}

/// <summary>Interim valuation arithmetic (D9). Inputs are totals; the QS's BQ workings stay outside Ecosys.</summary>
public static class ProjectValuationCalculator
{
    public sealed record Result(decimal GrossValueToDate, decimal RetentionToDate, decimal AdvanceRecoveredToDate, decimal PreviouslyCertified, decimal AmountDue);

    /// <param name="previous">The latest earlier valuation that was certified (or invoiced), if any.</param>
    /// <param name="certifiedBefore">Sum of the certified amounts of every earlier certified valuation.</param>
    public static Result Compute(Project project, decimal workDoneToDate, decimal materialsOnSite, ProjectValuation? previous, decimal certifiedBefore)
    {
        if (workDoneToDate < 0 || materialsOnSite < 0)
            throw new BusinessRuleException("Work done and materials on site cannot be negative.");

        var gross = workDoneToDate + materialsOnSite;

        var retention = 0m;
        if (project.RetentionPercent is > 0)
        {
            retention = Math.Round(gross * project.RetentionPercent.Value / 100m, 2);
            if (project.RetentionCapAmount.HasValue)
                retention = Math.Min(retention, project.RetentionCapAmount.Value);
        }

        // Advance recovery applies to work done in the period, not to materials on site, and stops
        // once the whole advance is recovered.
        var recoveredBefore = previous?.AdvanceRecoveredToDate ?? 0m;
        var recovered = recoveredBefore;
        if (project.AdvancePaymentAmount is > 0 && project.AdvanceRecoveryPercent is > 0)
        {
            var workThisPeriod = Math.Max(0m, workDoneToDate - (previous?.WorkDoneToDate ?? 0m));
            var outstanding = Math.Max(0m, project.AdvancePaymentAmount.Value - recoveredBefore);
            recovered += Math.Min(Math.Round(workThisPeriod * project.AdvanceRecoveryPercent.Value / 100m, 2), outstanding);
        }

        var due = gross - retention - recovered - certifiedBefore;
        return new Result(gross, retention, recovered, certifiedBefore, due);
    }

    /// <summary>Retention currently held: the latest certified retention less what has been released.</summary>
    public static decimal RetentionHeld(IEnumerable<ProjectValuation> valuations, IEnumerable<ProjectRetentionRelease> releases)
    {
        var latest = valuations
            .Where(x => x.Status is ProjectValuationStatuses.Certified or ProjectValuationStatuses.Invoiced)
            .OrderByDescending(x => x.PeriodEnd).ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        return Math.Max(0m, (latest?.RetentionToDate ?? 0m) - releases.Sum(x => x.Amount));
    }
}

public sealed record ProjectFinancials(
    string Currency,
    decimal OriginalValue,
    decimal RevisedValue,
    decimal PendingVariations,
    decimal BudgetTotal,
    IReadOnlyList<ProjectFinancialsCategory> Categories,
    decimal ManDays,
    decimal UncostedDays,
    decimal ActualCost,
    decimal CommittedCost,
    decimal Invoiced,
    decimal CashReceived,
    decimal TaxWithheld,
    decimal Outstanding,
    decimal Overdue,
    decimal CertifiedToDate,
    decimal RetentionHeld,
    decimal RetentionDueNow,
    decimal AdvanceOutstanding,
    decimal UnbilledValue,
    decimal? IndicativeMarginPercent,
    decimal? ForecastMarginPercent);

public sealed record ProjectFinancialsCategory(string Category, decimal Budget, decimal Actual, string Source);

public interface IProjectFinancialsService
{
    /// <summary>The section 7 breakdown. Every figure is derived from the records that hold it.</summary>
    Task<ProjectFinancials> ComputeAsync(Guid tenantId, Project project, CancellationToken cancellationToken = default);

    /// <summary>Retention that can be released now at each stage, given handover and the defects liability period.</summary>
    Task<(decimal Held, decimal CompletionDue, decimal FinalDue)> RetentionPositionAsync(Guid tenantId, Project project, CancellationToken cancellationToken = default);
}

internal sealed class ProjectFinancialsService(AppDbContext dbContext) : IProjectFinancialsService
{
    public async Task<ProjectFinancials> ComputeAsync(Guid tenantId, Project project, CancellationToken cancellationToken = default)
    {
        var id = project.Id;

        var pending = await dbContext.ProjectVariations
            .Where(x => x.TenantId == tenantId && x.ProjectId == id && x.Status == ProjectVariationStatuses.Submitted)
            .Select(x => new { x.Kind, x.Amount }).ToListAsync(cancellationToken);
        var budget = await dbContext.ProjectBudgetLines.Where(x => x.TenantId == tenantId && x.ProjectId == id)
            .Select(x => new { x.Category, x.Amount }).ToListAsync(cancellationToken);

        // Materials: issued cost, less the value of anything returned from those issues.
        var issues = await dbContext.MaterialIssues
            .Where(x => x.TenantId == tenantId && x.ProjectId == id && (x.IssueStatus == "issued" || x.IssueStatus == "returned"))
            .Select(x => new { x.TotalIssuedCost, Returned = x.Lines.Sum(l => l.QuantityReturned * (l.UnitCost ?? 0)) })
            .ToListAsync(cancellationToken);
        var materials = issues.Sum(x => x.TotalIssuedCost - x.Returned);

        var muster = await dbContext.ProjectMusterEntries.Where(x => x.TenantId == tenantId && x.ProjectId == id).ToListAsync(cancellationToken);
        var workerIds = muster.Select(x => x.ProjectWorkerId).Distinct().ToList();
        var rates = await dbContext.ProjectWorkerRates.Where(x => x.TenantId == tenantId && workerIds.Contains(x.ProjectWorkerId)).ToListAsync(cancellationToken);
        var labour = ProjectLabour.Summarize(muster, rates);

        var bills = await dbContext.SupplierBills.Where(x => x.TenantId == tenantId && x.ProjectId == id).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var expenses = await dbContext.Expenses.Where(x => x.TenantId == tenantId && x.ProjectId == id && (x.Status == "Approved" || x.Status == "Reimbursed"))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var costEntries = await dbContext.ProjectCostEntries.Where(x => x.TenantId == tenantId && x.ProjectId == id)
            .Select(x => new { x.Category, x.Amount }).ToListAsync(cancellationToken);

        // Committed: open purchase-order value for the project that hasn't been billed yet.
        var openPurchaseOrders = await dbContext.PurchaseOrders
            .Where(x => x.TenantId == tenantId && x.ProjectId == id && x.Status != "Cancelled" && x.Status != "Received")
            .Select(x => x.Lines.Sum(l => l.QuantityOrdered * l.UnitCost))
            .ToListAsync(cancellationToken);
        var committed = Math.Max(0m, openPurchaseOrders.Sum() - 0m);

        var invoices = await dbContext.TenantInvoices
            .Where(x => x.TenantId == tenantId && x.ProjectId == id && x.Status != "Void" && x.DocumentType == "Invoice")
            .Select(x => new { x.Id, x.TotalAmount, x.TaxAmount, x.Balance, x.DueDate, x.Status })
            .ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(x => x.Id).ToList();
        var allocations = await dbContext.PaymentAllocations
            .Where(x => x.TenantId == tenantId && invoiceIds.Contains(x.InvoiceId) && x.Status == "Active")
            .Select(x => new { x.SourceType, x.Amount }).ToListAsync(cancellationToken);

        var valuations = await dbContext.ProjectValuations.Where(x => x.TenantId == tenantId && x.ProjectId == id).ToListAsync(cancellationToken);
        var releases = await dbContext.ProjectRetentionReleases.Where(x => x.TenantId == tenantId && x.ProjectId == id).ToListAsync(cancellationToken);
        var latestCertified = valuations.Where(x => x.Status is ProjectValuationStatuses.Certified or ProjectValuationStatuses.Invoiced)
            .OrderByDescending(x => x.PeriodEnd).ThenByDescending(x => x.CreatedAt).FirstOrDefault();
        var (held, completionDue, finalDue) = RetentionPosition(project, valuations, releases);

        var labourCost = labour.Sum(x => x.Cost);
        var otherCostByCategory = costEntries.GroupBy(x => x.Category).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        decimal Budget(string category) => budget.Where(x => x.Category == category).Sum(x => x.Amount);
        decimal Other(string category) => otherCostByCategory.GetValueOrDefault(category);

        var categories = new List<ProjectFinancialsCategory>
        {
            new(ProjectCostCategories.Materials, Budget(ProjectCostCategories.Materials), materials + Other(ProjectCostCategories.Materials), "Material issues net of returns, plus material cost entries"),
            new(ProjectCostCategories.Labour, Budget(ProjectCostCategories.Labour), labourCost + Other(ProjectCostCategories.Labour), "Muster roll at office daily rates, plus labour cost entries"),
            new(ProjectCostCategories.Subcontractor, Budget(ProjectCostCategories.Subcontractor), bills + Other(ProjectCostCategories.Subcontractor), "Supplier bills on the project, plus subcontractor cost entries"),
            new(ProjectCostCategories.Equipment, Budget(ProjectCostCategories.Equipment), Other(ProjectCostCategories.Equipment), "Equipment cost entries"),
            new(ProjectCostCategories.Other, Budget(ProjectCostCategories.Other), expenses + Other(ProjectCostCategories.Other), "Approved expense claims, plus other cost entries"),
        };
        var actual = categories.Sum(x => x.Actual);
        var revised = project.RevisedValue;
        decimal? Margin(decimal cost) => revised > 0 ? Math.Round((revised - cost) / revised * 100m, 2) : null;
        var today = DateTime.UtcNow.Date;
        var certifiedToDate = latestCertified?.GrossValueToDate ?? 0m;

        return new ProjectFinancials(
            project.Currency,
            project.OriginalValue,
            revised,
            pending.Sum(x => x.Kind == ProjectVariationKinds.Omission ? -x.Amount : x.Amount),
            budget.Sum(x => x.Amount),
            categories,
            labour.Sum(x => x.ManDays),
            labour.Sum(x => x.UncostedDays),
            actual,
            committed,
            invoices.Sum(x => x.TotalAmount - x.TaxAmount),
            allocations.Where(x => x.SourceType == AllocationSourceTypes.Payment).Sum(x => x.Amount),
            allocations.Where(x => x.SourceType == AllocationSourceTypes.Withholding).Sum(x => x.Amount),
            invoices.Sum(x => x.Balance),
            invoices.Where(x => x.Balance > 0 && x.DueDate.Date < today).Sum(x => x.Balance),
            certifiedToDate,
            held,
            completionDue + finalDue,
            Math.Max(0m, (project.AdvancePaymentAmount ?? 0m) - (latestCertified?.AdvanceRecoveredToDate ?? 0m)),
            Math.Max(0m, revised - certifiedToDate),
            Margin(actual),
            Margin(actual + committed));
    }

    public async Task<(decimal Held, decimal CompletionDue, decimal FinalDue)> RetentionPositionAsync(Guid tenantId, Project project, CancellationToken cancellationToken = default)
    {
        var valuations = await dbContext.ProjectValuations.Where(x => x.TenantId == tenantId && x.ProjectId == project.Id).ToListAsync(cancellationToken);
        var releases = await dbContext.ProjectRetentionReleases.Where(x => x.TenantId == tenantId && x.ProjectId == project.Id).ToListAsync(cancellationToken);
        return RetentionPosition(project, valuations, releases);
    }

    /// <summary>
    /// Two-part release: the completion share once practical completion (handover) is reached, and
    /// the balance once the defects liability period has also run. Each stage releases once.
    /// </summary>
    internal static (decimal Held, decimal CompletionDue, decimal FinalDue) RetentionPosition(Project project, IReadOnlyCollection<ProjectValuation> valuations, IReadOnlyCollection<ProjectRetentionRelease> releases)
    {
        var held = ProjectValuationCalculator.RetentionHeld(valuations, releases);
        if (held <= 0 || !project.HandedOverAt.HasValue)
            return (held, 0, 0);

        var completionReleased = releases.Any(x => x.Stage == ProjectRetentionStages.PracticalCompletion);
        var completionDue = 0m;
        if (!completionReleased && project.RetentionReleaseAtCompletionPercent > 0)
            completionDue = Math.Min(held, Math.Round((held + releases.Sum(x => x.Amount)) * project.RetentionReleaseAtCompletionPercent / 100m, 2));

        var liabilityEnds = project.HandedOverAt.Value.AddMonths(project.DefectsLiabilityMonths ?? 0);
        var finalDue = DateTime.UtcNow >= liabilityEnds && !releases.Any(x => x.Stage == ProjectRetentionStages.EndOfDefectsLiability)
            ? held - completionDue
            : 0m;
        return (held, completionDue, Math.Max(0, finalDue));
    }
}

public interface IProjectInvoiceService
{
    /// <summary>
    /// Raises a Draft tax invoice for a project amount (valuation, advance or retention release),
    /// with the tenant's VAT and the client's credit terms. Finance reviews and sends it through the
    /// existing invoice flow. The invoice is added to the context; the caller saves.
    /// </summary>
    Task<TenantInvoice> CreateDraftInvoiceAsync(Guid tenantId, Project project, string description, decimal amount, Guid? valuationId, CancellationToken cancellationToken = default);
}

internal sealed class ProjectInvoiceService(AppDbContext dbContext, IDocumentNumberingService documentNumberingService) : IProjectInvoiceService
{
    public async Task<TenantInvoice> CreateDraftInvoiceAsync(Guid tenantId, Project project, string description, decimal amount, Guid? valuationId, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            throw new BusinessRuleException("There is nothing to invoice: the amount is zero or less.");

        var client = await dbContext.Clients.Include(x => x.DefaultCreditTerm)
            .SingleAsync(x => x.TenantId == tenantId && x.Id == project.ClientId, cancellationToken);
        var defaults = await dbContext.TenantFinanceDefaults.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        var taxRate = defaults is null ? 16m : defaults.EnableVat ? defaults.DefaultVatRate : 0m;
        var invoiceDate = DateTime.UtcNow.Date;
        var creditTerm = client.DefaultCreditTerm is { IsActive: true } ? client.DefaultCreditTerm : null;
        var dueDays = creditTerm?.NetDays ?? defaults?.DefaultInvoiceDueDays ?? 30;

        var lineTotal = Math.Round(amount, 2);
        var taxAmount = Math.Round(lineTotal * taxRate / 100m, 2);
        var invoice = new TenantInvoice
        {
            TenantId = tenantId,
            ClientId = project.ClientId,
            ProjectId = project.Id,
            ProjectValuationId = valuationId,
            CreditTermId = creditTerm?.Id,
            InvoiceNumber = await documentNumberingService.GenerateAsync(tenantId, project.BranchId, DocumentTypes.Invoice, cancellationToken),
            DocumentType = DocumentTypes.Invoice,
            Status = "Draft",
            InvoiceDate = DateTime.SpecifyKind(invoiceDate, DateTimeKind.Utc),
            DueDate = DateTime.SpecifyKind(invoiceDate.AddDays(dueDays), DateTimeKind.Utc),
            Currency = project.Currency,
            Notes = $"{project.ProjectNumber} {project.Name}",
            Subtotal = lineTotal,
            TaxAmount = taxAmount,
            TotalAmount = lineTotal + taxAmount,
            Balance = lineTotal + taxAmount,
        };
        invoice.Lines.Add(new TenantInvoiceLine
        {
            Description = description,
            ItemType = "Service",
            Unit = "lot",
            Quantity = 1,
            UnitRate = lineTotal,
            LineTotal = lineTotal,
            TaxRate = taxRate,
            TaxAmount = taxAmount,
            GrandTotal = lineTotal + taxAmount,
        });
        dbContext.TenantInvoices.Add(invoice);
        await InvoiceBalanceCalculator.RecalcClientBalanceAsync(dbContext, client, cancellationToken);
        return invoice;
    }
}
