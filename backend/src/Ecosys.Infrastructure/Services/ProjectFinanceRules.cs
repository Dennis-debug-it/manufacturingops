using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Converts amounts into one currency with the tenant's exchange rates (Finance → Exchange Rates). A rate
/// row "USD → KES 129" converts USD to KES by multiplying and KES to USD by dividing. The rate used is the
/// latest one effective on the amount's date; an amount dated before the first rate uses the first rate.
/// </summary>
public sealed class CurrencyConverter
{
    private readonly string target;
    private readonly IReadOnlyList<ExchangeRate> rates;

    private CurrencyConverter(string target, IReadOnlyList<ExchangeRate> rates)
    {
        this.target = Normalize(target);
        this.rates = rates;
    }

    public static async Task<CurrencyConverter> LoadAsync(AppDbContext dbContext, Guid tenantId, string target, CancellationToken cancellationToken = default)
    {
        var code = Normalize(target);
        var rates = await dbContext.ExchangeRates.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Rate > 0 && (x.BaseCurrency == code || x.QuoteCurrency == code))
            .OrderBy(x => x.EffectiveFrom)
            .ToListAsync(cancellationToken);
        return new CurrencyConverter(code, rates);
    }

    public string Target => target;

    public bool CanConvert(string? from) => Factor(from, DateTime.UtcNow).HasValue;

    /// <summary>The amount in the target currency, or null when no rate links the two currencies.</summary>
    public decimal? Convert(decimal amount, string? from, DateTime on) =>
        Factor(from, on) is { } factor ? Math.Round(amount * factor, 2) : null;

    private decimal? Factor(string? from, DateTime on)
    {
        var source = Normalize(from);
        if (source == target)
            return 1m;

        var pair = rates.Where(x => (x.BaseCurrency == source && x.QuoteCurrency == target) || (x.BaseCurrency == target && x.QuoteCurrency == source)).ToList();
        var rate = pair.LastOrDefault(x => x.EffectiveFrom <= on) ?? pair.FirstOrDefault();
        if (rate is null)
            return null;
        return rate.BaseCurrency == source ? rate.Rate : 1m / rate.Rate;
    }

    public static string Normalize(string? currency) => (currency ?? string.Empty).Trim().ToUpperInvariant();
}

/// <summary>
/// The rules that keep a project and its Finance documents in step: invoices Projects raises keep the
/// project's client, currency and amount; a project's net billing counts credit notes; and costs in
/// another currency can only join a project the exchange rates can convert them into.
/// </summary>
public static class ProjectFinanceRules
{
    /// <summary>
    /// Refuses an edit that changes the client, currency or amount (before tax) of an invoice Projects
    /// raised: a valuation, the advance or a retention release. Those come from the project; to change
    /// them, void the invoice and raise it again from the project. Other invoices pass untouched.
    /// </summary>
    public static async Task EnsureProjectInvoiceEditAllowedAsync(
        AppDbContext dbContext, Guid tenantId, TenantInvoice invoice, Guid clientId, string? currency, decimal subtotal,
        CancellationToken cancellationToken = default)
    {
        var source = await RaisedByProjectAsync(dbContext, tenantId, invoice, cancellationToken);
        if (source is null)
            return;

        if (clientId != invoice.ClientId || CurrencyConverter.Normalize(currency) != CurrencyConverter.Normalize(invoice.Currency) || subtotal != invoice.Subtotal)
            throw new BusinessRuleException(
                $"This invoice was raised from {source}. Its client, currency and amount come from the project and can't be changed here; void it and raise it again from the project.");
    }

    /// <summary>What raised this invoice in Projects ("project PRJ-1 (valuation VAL-2)"), or null if Projects didn't.</summary>
    public static async Task<string?> RaisedByProjectAsync(AppDbContext dbContext, Guid tenantId, TenantInvoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice.ProjectId is not { } projectId)
            return null;

        var project = await dbContext.Projects.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == projectId)
            .Select(x => new { x.ProjectNumber, x.AdvanceInvoiceId })
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null)
            return null;

        if (invoice.ProjectValuationId is { } valuationId)
        {
            var number = await dbContext.ProjectValuations.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == valuationId).Select(x => x.ValuationNumber).SingleOrDefaultAsync(cancellationToken);
            return $"project {project.ProjectNumber} (valuation {number})";
        }

        if (project.AdvanceInvoiceId == invoice.Id)
            return $"project {project.ProjectNumber} (advance payment)";

        return await dbContext.ProjectRetentionReleases.AnyAsync(x => x.TenantId == tenantId && x.InvoiceId == invoice.Id, cancellationToken)
            ? $"project {project.ProjectNumber} (retention release)"
            : null;
    }

    /// <summary>
    /// What the project has billed, before tax: its live invoices less the credit notes issued against them.
    /// Draft and void credit notes don't count.
    /// </summary>
    public static async Task<decimal> NetInvoicedAsync(AppDbContext dbContext, Guid tenantId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var invoices = await dbContext.TenantInvoices
            .Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.Status != "Void" && x.DocumentType == DocumentTypes.Invoice)
            .Select(x => new { x.Id, x.Subtotal })
            .ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(x => x.Id).ToList();
        var credited = await dbContext.CreditNotes
            .Where(x => x.TenantId == tenantId && x.InvoiceId.HasValue && invoiceIds.Contains(x.InvoiceId.Value) && x.Status != "Draft" && x.Status != "Void")
            .SumAsync(x => (decimal?)x.Subtotal, cancellationToken) ?? 0m;
        return invoices.Sum(x => x.Subtotal) - credited;
    }

    /// <summary>
    /// Billing Projects believes is done but Finance has voided: an invoiced valuation or a retention release
    /// whose invoice is void (or gone). Each must be raised again before the project can close.
    /// </summary>
    public static async Task<IReadOnlyList<string>> VoidedBillingAsync(AppDbContext dbContext, Guid tenantId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var liveInvoiceIds = dbContext.TenantInvoices.Where(x => x.TenantId == tenantId && x.Status != "Void").Select(x => x.Id);

        var valuations = await dbContext.ProjectValuations
            .Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.Status == ProjectValuationStatuses.Invoiced
                && (x.InvoiceId == null || !liveInvoiceIds.Contains(x.InvoiceId.Value)))
            .Select(x => x.ValuationNumber)
            .ToListAsync(cancellationToken);
        var releases = await dbContext.ProjectRetentionReleases
            .Where(x => x.TenantId == tenantId && x.ProjectId == projectId && x.InvoiceId.HasValue && !liveInvoiceIds.Contains(x.InvoiceId.Value))
            .CountAsync(cancellationToken);

        var blockers = valuations.Select(number => $"the invoice for valuation {number} was voided and hasn't been raised again").ToList();
        if (releases > 0)
            blockers.Add("a retention release invoice was voided and hasn't been raised again");
        return blockers;
    }

    /// <summary>
    /// Refuses to put a cost in <paramref name="costCurrency"/> on a project kept in another currency unless an
    /// exchange rate between the two exists, so the project's Financials can always convert it.
    /// </summary>
    public static async Task EnsureCostCurrencyAsync(AppDbContext dbContext, Guid tenantId, Guid? projectId, string? costCurrency, CancellationToken cancellationToken = default)
    {
        if (projectId is not { } id)
            return;

        var project = await dbContext.Projects.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => new { x.ProjectNumber, x.Currency })
            .SingleAsync(cancellationToken);
        var converter = await CurrencyConverter.LoadAsync(dbContext, tenantId, project.Currency, cancellationToken);
        if (!converter.CanConvert(costCurrency))
            throw new BusinessRuleException(
                $"Project {project.ProjectNumber} is kept in {converter.Target} and this cost is in {CurrencyConverter.Normalize(costCurrency)}. Add an exchange rate between them in Finance → Exchange Rates first.");
    }

    /// <summary>A project's invoices are in the project's currency, so its billing can be added up.</summary>
    public static async Task EnsureProjectCurrencyAsync(AppDbContext dbContext, Guid tenantId, Guid? projectId, string? currency, CancellationToken cancellationToken = default)
    {
        if (projectId is not { } id)
            return;

        var project = await dbContext.Projects.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => new { x.ProjectNumber, x.Currency })
            .SingleAsync(cancellationToken);
        if (CurrencyConverter.Normalize(currency) != CurrencyConverter.Normalize(project.Currency))
            throw new BusinessRuleException($"Invoices for project {project.ProjectNumber} are in the project's currency, {project.Currency}.");
    }

    /// <summary>The currency stock is valued in: the tenant's currency.</summary>
    public static async Task<string> StockCurrencyAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken = default) =>
        await dbContext.Tenants.AsNoTracking().Where(x => x.Id == tenantId).Select(x => x.Currency).SingleOrDefaultAsync(cancellationToken) ?? "KES";
}
