using System.Globalization;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure.Services;

public static class PlatformInvoicePayments
{
    /// <summary>Adds a payment to an invoice's paid amount, balance and status.</summary>
    public static void Apply(PlatformInvoice invoice, decimal amount)
    {
        invoice.AmountPaid += amount;
        if (invoice.AmountPaid >= invoice.Total)
        {
            invoice.AmountPaid = invoice.Total;
            invoice.Balance = 0;
            invoice.Status = "Paid";
        }
        else
        {
            invoice.Balance = invoice.Total - invoice.AmountPaid;
            invoice.Status = "PartiallyPaid";
        }
    }
}

public sealed record InvoiceLineDraft(string Description, decimal Quantity, decimal UnitPrice, bool Taxable = true);

/// <summary>What the next renewal invoice for a licence would hold, before it is raised.</summary>
public sealed record RenewalPreview(DateTime CoversFrom, DateTime CoversTo, int Months, string Currency, IReadOnlyList<InvoiceLineDraft> Lines, decimal Subtotal, decimal TaxRate,
    decimal Tax, decimal Total, Guid? ExistingInvoiceId, string? ExistingInvoiceNumber);

/// <summary>
/// The platform's own invoices to its tenants, raised from an accepted price-book quote (year one, or the
/// pilot) and for subscription renewals, plus recording a payment against one. Raised invoices start as
/// drafts: nothing reaches the customer until someone sends it from Platform finance.
/// </summary>
public interface IPlatformInvoicingService
{
    Task<string> NextNumberAsync(string documentType, string defaultPrefix, CancellationToken cancellationToken = default);
    Task<PlatformInvoice> FromQuoteAsync(PriceQuote quote, string purpose, CancellationToken cancellationToken = default);
    Task<RenewalPreview?> PreviewRenewalAsync(TenantLicense license, CancellationToken cancellationToken = default);
    Task<PlatformInvoice> RaiseRenewalAsync(TenantLicense license, CancellationToken cancellationToken = default);
    Task<int> RaiseDueRenewalsAsync(DateTime now, CancellationToken cancellationToken = default);
    Task<PlatformPayment> RecordPaymentAsync(PlatformInvoice invoice, decimal amount, string method, string? reference, DateTime? paidAt, Guid? userId, CancellationToken cancellationToken = default);
}

internal sealed class PlatformInvoicingService(AppDbContext dbContext, IPlatformBillingLifecycleService billingLifecycle) : IPlatformInvoicingService
{
    /// <summary>Renewal invoices are raised this many days before the period they pay for starts.</summary>
    public const int RenewalLeadDays = 14;
    private const int PaymentTermsDays = 14;

    public async Task<string> NextNumberAsync(string documentType, string defaultPrefix, CancellationToken cancellationToken = default)
    {
        var setting = await dbContext.NumberingSettings.SingleOrDefaultAsync(
            x => x.TenantId == PlatformConstants.RootTenantId && x.BranchId == null && x.DocumentType == documentType, cancellationToken);
        if (setting is null)
        {
            setting = new NumberingSetting
            {
                TenantId = PlatformConstants.RootTenantId, DocumentType = documentType, Prefix = defaultPrefix, NextNumber = 1, PaddingLength = 6,
                ResetFrequency = "Never", IncludeYear = true, IsActive = true,
            };
            dbContext.NumberingSettings.Add(setting);
        }

        var number = setting.NextNumber;
        setting.NextNumber += 1;
        return $"{setting.Prefix.ToUpperInvariant()}-{DateTime.UtcNow:yyyy}-{number.ToString(CultureInfo.InvariantCulture).PadLeft(Math.Max(3, setting.PaddingLength), '0')}";
    }

    public async Task<PlatformInvoice> FromQuoteAsync(PriceQuote quote, string purpose, CancellationToken cancellationToken = default)
    {
        if (quote.Status != QuoteStatuses.Accepted) throw new BusinessRuleException("Mark the quote accepted before invoicing it.");
        var result = FactoryPricing.Parse<PricingResult>(quote.ResultJson) ?? throw new BusinessRuleException("Save the quote again before invoicing it.");
        var existingId = purpose == PlatformInvoicePurposes.Pilot ? quote.PilotInvoiceId : quote.InvoiceId;
        if (existingId is { } id && await dbContext.PlatformInvoices.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id && x.Status != "Void", cancellationToken) is { } existing)
            return existing;

        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        var today = DateTime.UtcNow.Date;
        var lines = new List<InvoiceLineDraft>();
        DateTime coversTo;
        if (purpose == PlatformInvoicePurposes.Pilot)
        {
            var pilot = result.Pilot ?? throw new BusinessRuleException("This quote does not offer a pilot.");
            lines.Add(new InvoiceLineDraft($"Pilot: one factory for {pilot.Months} months ({quote.QuoteNumber})", 1, pilot.Fee));
            coversTo = today.AddMonths(pilot.Months);
        }
        else if (purpose == PlatformInvoicePurposes.YearOne)
        {
            lines.AddRange(result.Lines.Select(x => new InvoiceLineDraft(
                x.Kind == PriceLineKinds.Discount ? x.Description : $"{x.Description}{(x.Kind == PriceLineKinds.Subscription ? $", {today:dd MMM yyyy} to {today.AddYears(1).AddDays(-1):dd MMM yyyy}" : string.Empty)}",
                x.Kind == PriceLineKinds.Discount ? 1 : x.Quantity, x.Kind == PriceLineKinds.Discount ? x.Amount : x.UnitPrice)));
            // A paid pilot is taken off year one when the price book says so.
            if (result.Pilot is { CreditedToYearOne: true } && quote.PilotInvoiceId is { } pilotId
                && await dbContext.PlatformInvoices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pilotId && x.Status == "Paid", cancellationToken) is { } paidPilot)
            {
                lines.Add(new InvoiceLineDraft($"Pilot fee credited ({paidPilot.InvoiceNumber})", 1, -(paidPilot.Subtotal - paidPilot.DiscountAmount)));
            }

            coversTo = today.AddYears(1);
        }
        else
        {
            throw new BusinessRuleException("Invoice year one or the pilot.");
        }

        var tenant = quote.TenantId is { } tenantId ? await dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken) : null;
        var invoice = await NewInvoiceAsync(tenant?.Id, string.IsNullOrWhiteSpace(tenant?.CompanyName) ? quote.CustomerName : tenant.CompanyName, quote.ContactEmail ?? tenant?.ContactEmail ?? tenant?.Email,
            result.Currency, book.VatPercent, lines, purpose, today, coversTo, cancellationToken);
        invoice.PriceQuoteId = quote.Id;
        invoice.Notes = $"From quote {quote.QuoteNumber}." + (purpose == PlatformInvoicePurposes.Pilot && result.Pilot!.CreditedToYearOne ? " Taken off year one if you continue." : string.Empty);
        if (purpose == PlatformInvoicePurposes.Pilot) quote.PilotInvoiceId = invoice.Id; else quote.InvoiceId = invoice.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    public async Task<RenewalPreview?> PreviewRenewalAsync(TenantLicense license, CancellationToken cancellationToken = default)
    {
        if (license.LicensePlan is null) await dbContext.Entry(license).Reference(x => x.LicensePlan).LoadAsync(cancellationToken);
        var plan = license.LicensePlan!;
        var months = license.BillingCycle switch { BillingCycles.Quarterly => 3, BillingCycles.SemiAnnual => 6, BillingCycles.Annual => 12, _ => 1 };
        var from = (license.NextBillingDate ?? license.ExpiresAt ?? license.TrialEndsAt ?? DateTime.UtcNow).Date;
        var to = from.AddMonths(months);
        var period = $"{from:dd MMM yyyy} to {to.AddDays(-1):dd MMM yyyy}";
        var book = await FactoryPricing.GetOrCreateAsync(dbContext, cancellationToken);
        var lines = new List<InvoiceLineDraft>();

        if (license.PricingBasis == PricingBases.PerFactory)
        {
            var result = FactoryPricing.Calculate(book, new Dictionary<string, decimal> { [plan.PlanCode] = plan.FactoryAnnualPrice }, FactoryPricing.InputFor(license));
            // Never invoice an edition at zero because its price is missing.
            if (result.Warnings.FirstOrDefault(x => x.Contains("no price per factory")) is { } missing) throw new BusinessRuleException(missing);
            foreach (var line in result.Lines.Where(x => x.Kind is PriceLineKinds.Subscription or PriceLineKinds.Discount))
            {
                var share = decimal.Round(line.Amount * months / 12m, 2);
                lines.Add(line.Kind == PriceLineKinds.Discount
                    ? new InvoiceLineDraft(line.Description, 1, share)
                    : new InvoiceLineDraft($"{line.Description.Replace(", per factory per year", string.Empty).Replace(", per user per year", string.Empty)}, {period}", line.Quantity,
                        decimal.Round(line.UnitPrice * months / 12m, 2)));
            }
        }
        else
        {
            if (license.OfficeSeats > 0) lines.Add(new InvoiceLineDraft($"{plan.DisplayName}: office users, {period}", license.OfficeSeats, LicensePricing.OfficeSeatPrice(license) * months));
            if (license.FieldSeats > 0) lines.Add(new InvoiceLineDraft($"{plan.DisplayName}: field users, {period}", license.FieldSeats, LicensePricing.FieldSeatPrice(license) * months));
        }

        if (license.WhatsAppAddOn is { } code && await dbContext.LicenseAddOns.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code, cancellationToken) is { } addOn)
        {
            var monthly = license.BillingCycle == BillingCycles.Annual && addOn.AnnualPrice > 0 ? addOn.AnnualPrice / 12m : addOn.MonthlyPrice;
            lines.Add(new InvoiceLineDraft($"{addOn.DisplayName}, {period}", 1, decimal.Round(monthly * months, 2)));
        }

        lines.RemoveAll(x => x.Quantity * x.UnitPrice == 0);
        var subtotal = lines.Sum(x => decimal.Round(x.Quantity * x.UnitPrice, 2));
        var tax = decimal.Round(Math.Max(0, subtotal) * book.VatPercent / 100m, 2);
        var existing = await ExistingRenewalAsync(license.TenantId, from, cancellationToken);
        return new RenewalPreview(from, to, months, book.Currency, lines, subtotal, book.VatPercent, tax, subtotal + tax, existing?.Id, existing?.InvoiceNumber);
    }

    public async Task<PlatformInvoice> RaiseRenewalAsync(TenantLicense license, CancellationToken cancellationToken = default)
    {
        if (license.IsPerpetual) throw new BusinessRuleException("This licence never expires, so it is not renewed.");
        var preview = await PreviewRenewalAsync(license, cancellationToken) ?? throw new BusinessRuleException("Nothing to renew.");
        if (preview.ExistingInvoiceId is { } existingId) return await dbContext.PlatformInvoices.Include(x => x.Lines).SingleAsync(x => x.Id == existingId, cancellationToken);
        if (preview.Subtotal <= 0) throw new BusinessRuleException("This licence has no price to invoice; set its pricing first.");
        var tenant = await dbContext.Tenants.AsNoTracking().SingleAsync(x => x.Id == license.TenantId, cancellationToken);
        var invoice = await NewInvoiceAsync(tenant.Id, string.IsNullOrWhiteSpace(tenant.CompanyName) ? tenant.Name : tenant.CompanyName, tenant.ContactEmail ?? tenant.Email, preview.Currency, preview.TaxRate, preview.Lines,
            PlatformInvoicePurposes.Renewal, preview.CoversFrom, preview.CoversTo, cancellationToken);
        invoice.DueDate = preview.CoversFrom > DateTime.UtcNow.Date ? preview.CoversFrom : DateTime.UtcNow.Date.AddDays(PaymentTermsDays);
        invoice.Notes = $"Subscription renewal, {preview.CoversFrom:dd MMM yyyy} to {preview.CoversTo.AddDays(-1):dd MMM yyyy}.";
        await dbContext.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    /// <summary>
    /// Raises a draft renewal invoice for every active, paid licence whose next billing date is within
    /// <see cref="RenewalLeadDays"/> days and has none yet. Returns how many were raised.
    /// </summary>
    public async Task<int> RaiseDueRenewalsAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var horizon = now.AddDays(RenewalLeadDays);
        var due = await dbContext.TenantLicenses.Include(x => x.LicensePlan)
            .Where(x => x.Status == LicenseStatuses.Active && !x.IsPerpetual && x.TenantId != PlatformConstants.RootTenantId
                && x.NextBillingDate != null && x.NextBillingDate <= horizon)
            .ToListAsync(cancellationToken);
        var raised = 0;
        foreach (var license in due)
        {
            var preview = await PreviewRenewalAsync(license, cancellationToken);
            if (preview is null || preview.ExistingInvoiceId is not null || preview.Subtotal <= 0) continue;
            await RaiseRenewalAsync(license, cancellationToken);
            raised++;
        }

        return raised;
    }

    public async Task<PlatformPayment> RecordPaymentAsync(PlatformInvoice invoice, decimal amount, string method, string? reference, DateTime? paidAt, Guid? userId,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0) throw new BusinessRuleException("Paid amount must be greater than zero.");
        if (invoice.Status is "Void" or "Paid") throw new BusinessRuleException($"This invoice is {invoice.Status.ToLowerInvariant()}.");
        amount = Math.Min(amount, invoice.Balance > 0 ? invoice.Balance : amount);
        var payment = new PlatformPayment
        {
            PaymentNumber = await NextNumberAsync("Receipt", "RCT", cancellationToken), InvoiceId = invoice.Id, TenantId = invoice.TenantId, Amount = amount,
            Currency = invoice.Currency, Method = method, Status = "Paid", Reference = reference, PaidAt = paidAt ?? DateTime.UtcNow,
        };
        dbContext.PlatformPayments.Add(payment);
        PlatformInvoicePayments.Apply(invoice, amount);
        await dbContext.SaveChangesAsync(cancellationToken);
        await billingLifecycle.ApplyPaymentReceivedAsync(payment, invoice, userId, cancellationToken);
        return payment;
    }

    private Task<PlatformInvoice?> ExistingRenewalAsync(Guid tenantId, DateTime from, CancellationToken cancellationToken) =>
        dbContext.PlatformInvoices.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Purpose == PlatformInvoicePurposes.Renewal && x.CoversFrom == from && x.Status != "Void")
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<PlatformInvoice> NewInvoiceAsync(Guid? tenantId, string customer, string? email, string currency, decimal taxRate, IReadOnlyList<InvoiceLineDraft> lines,
        string purpose, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var subtotal = lines.Sum(x => decimal.Round(x.Quantity * x.UnitPrice, 2));
        var taxable = Math.Max(0, lines.Where(x => x.Taxable).Sum(x => decimal.Round(x.Quantity * x.UnitPrice, 2)));
        var tax = decimal.Round(taxable * taxRate / 100m, 2);
        var invoice = new PlatformInvoice
        {
            InvoiceNumber = await NextNumberAsync("Invoice", "INV", cancellationToken),
            TenantId = tenantId, CustomerName = customer, CustomerEmail = email, Currency = currency, Subtotal = subtotal, TaxRate = taxRate, TaxAmount = tax,
            Total = subtotal + tax, Balance = subtotal + tax, Status = "Draft", IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.Date.AddDays(PaymentTermsDays),
            Purpose = purpose, CoversFrom = from, CoversTo = to,
            Lines = lines.Select(x => new PlatformInvoiceLine
            {
                Description = x.Description.Length > 500 ? x.Description[..500] : x.Description, Quantity = x.Quantity, UnitPrice = x.UnitPrice, Taxable = x.Taxable,
                LineTotal = decimal.Round(x.Quantity * x.UnitPrice, 2),
            }).ToList(),
        };
        dbContext.PlatformInvoices.Add(invoice);
        return invoice;
    }
}

/// <summary>Daily: raises draft renewal invoices ahead of each paid licence's next billing date.</summary>
public sealed class RenewalInvoiceWorker(IServiceScopeFactory scopeFactory, ILogger<RenewalInvoiceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var raised = await scope.ServiceProvider.GetRequiredService<IPlatformInvoicingService>().RaiseDueRenewalsAsync(DateTime.UtcNow, stoppingToken);
                if (raised > 0) logger.LogInformation("Raised {Count} draft renewal invoices.", raised);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Renewal invoicing failed.");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }
}
