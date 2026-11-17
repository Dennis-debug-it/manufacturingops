using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>Platform invoices raised from quotes and for renewals, and paying them.</summary>
public sealed partial class AuthEmailSafetyTests
{
    [Fact]
    public async Task APilotInvoice_PaidByMarkingItPaid_ExtendsTheLicenceByThePilot_AndIsTakenOffYearOne()
    {
        await using var db = CreateDbContext();
        db.EmailSettings.Add(new EmailSetting { TenantId = PlatformConstants.RootTenantId, IsEnabled = true, Provider = EmailDeliveryMode.Smtp.ToString(), Host = "smtp.ecosys.test",
            Port = 587, SenderName = "Ecosys", SenderAddress = "billing@ecosys.test" });
        await db.SaveChangesAsync();
        var (tenantId, license) = await TenantWithLicenseAsync(db, ServiceOpsPlans.Professional, "Trial");
        var pricing = PricingWithInvoicing(db);
        var finance = FinanceWithInvoicing(db);

        var terms = new QuoteTermsRequest(ServiceOpsPlans.Professional, Visitors: true, Security: true, Factories: 1, IncludePilot: true, Hardware: [new HardwareRequest("nfc-tag", 10)]);
        var quote = PricingOk(await pricing.CreateQuote(new SaveQuoteRequest("Chelal Tea", null, "jane@chelal.test", tenantId, null, null, terms), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => pricing.InvoiceQuote(quote.Quote.Id, new InvoiceQuoteRequest("Pilot"), default));
        PricingOk(await pricing.SetStatus(quote.Quote.Id, new QuoteStatusRequest("Accepted"), default));

        var withPilot = PricingOk(await pricing.InvoiceQuote(quote.Quote.Id, new InvoiceQuoteRequest("Pilot"), default));
        var pilotRow = Assert.Single(withPilot.Invoices);
        Assert.Equal(("Pilot", "Draft", 60_000m * 1.16m), (pilotRow.Purpose, pilotRow.Status, pilotRow.Total));
        // Asking again gives the same invoice.
        Assert.Equal(pilotRow.Id, Assert.Single(PricingOk(await pricing.InvoiceQuote(quote.Quote.Id, new InvoiceQuoteRequest("pilot"), default)).Invoices).Id);
        Assert.Equal(0, await db.EmailOutboxMessages.CountAsync(x => x.TemplateKey == "platform-invoice-issued")); // drafts are not emailed

        Assert.IsType<OkObjectResult>((await finance.SendInvoice(pilotRow.Id, default)).Result);
        Assert.IsType<OkObjectResult>((await finance.SendInvoice(pilotRow.Id, default)).Result);
        Assert.Equal(1, await db.EmailOutboxMessages.CountAsync(x => x.TemplateKey == "platform-invoice-issued"));

        var start = DateTime.UtcNow;
        Assert.IsType<OkObjectResult>((await finance.MarkInvoicePaid(pilotRow.Id, new MarkInvoicePaidRequest(null, "M-Pesa", "QAB12CD"), default)).Result);
        var pilot = await db.PlatformInvoices.SingleAsync(x => x.Id == pilotRow.Id);
        Assert.Equal(("Paid", 0m), (pilot.Status, pilot.Balance));
        var receipt = await db.PlatformPayments.SingleAsync(x => x.InvoiceId == pilot.Id);
        Assert.Equal(("M-Pesa", "QAB12CD", pilot.Total), (receipt.Method, receipt.Reference, receipt.Amount));
        await db.Entry(license).ReloadAsync();
        Assert.Equal(LicenseStatuses.Active, license.Status);
        // Three months of pilot, not a year of the licence's billing cycle.
        Assert.InRange((license.ExpiresAt!.Value - start).TotalDays, 85, 95);

        var both = PricingOk(await pricing.InvoiceQuote(quote.Quote.Id, new InvoiceQuoteRequest("YearOne"), default));
        var yearOneId = both.Invoices.Single(i => i.Purpose == "YearOne").Id;
        var yearOne = await db.PlatformInvoices.Include(x => x.Lines).SingleAsync(x => x.Id == yearOneId);
        Assert.Contains(yearOne.Lines, x => x.Description.StartsWith("Pilot fee credited") && x.LineTotal == -60_000m);
        Assert.Contains(yearOne.Lines, x => x.Description.StartsWith("NFC tag") && x.Quantity == 10);
        var result = quote.Result!;
        Assert.Equal(result.YearOneTotal - 60_000m, yearOne.Subtotal);
        Assert.InRange((yearOne.CoversTo!.Value - yearOne.CoversFrom!.Value).TotalDays, 365, 366);
    }

    [Fact]
    public async Task RenewalInvoices_AreRaisedOnceAhead_ForPerFactoryAndPerSeatLicences()
    {
        await using var db = CreateDbContext();
        var (factoryTenant, perFactory) = await TenantWithLicenseAsync(db, ServiceOpsPlans.Enterprise, "Active");
        perFactory.PricingBasis = PricingBases.PerFactory;
        perFactory.LicensedFactories = 3;
        perFactory.OperationsAddOns = "security";
        perFactory.BillingCycle = BillingCycles.Annual;
        perFactory.NextBillingDate = DateTime.UtcNow.Date.AddDays(10);
        perFactory.ExpiresAt = perFactory.NextBillingDate;
        var (seatTenant, perSeat) = await TenantWithLicenseAsync(db, ServiceOpsPlans.Essentials, "Active");
        perSeat.BillingCycle = BillingCycles.Monthly;
        perSeat.OfficeSeats = 4;
        perSeat.FieldSeats = 6;
        perSeat.OfficeSeatPriceOverride = 2_000m;
        perSeat.FieldSeatPriceOverride = 1_000m;
        perSeat.NextBillingDate = DateTime.UtcNow.Date.AddDays(3);
        var (_, later) = await TenantWithLicenseAsync(db, ServiceOpsPlans.Essentials, "Active");
        later.NextBillingDate = DateTime.UtcNow.AddDays(60);
        later.OfficeSeatPriceOverride = 2_000m;
        await db.SaveChangesAsync();
        var invoicing = Invoicing(db);

        Assert.Equal(2, await invoicing.RaiseDueRenewalsAsync(DateTime.UtcNow));
        Assert.Equal(0, await invoicing.RaiseDueRenewalsAsync(DateTime.UtcNow));

        var annual = await db.PlatformInvoices.Include(x => x.Lines).SingleAsync(x => x.TenantId == factoryTenant);
        Assert.Equal((PlatformInvoicePurposes.Renewal, "Draft"), (annual.Purpose, annual.Status));
        Assert.Equal((perFactory.NextBillingDate, perFactory.NextBillingDate!.Value.AddYears(1)), (annual.CoversFrom, annual.CoversTo));
        Assert.Equal(3 * (300_000m + 55_000m), annual.Subtotal); // under five factories: no discount
        Assert.Equal(annual.Subtotal * 1.16m, annual.Total);
        Assert.Equal(perFactory.NextBillingDate, annual.DueDate);

        var monthly = await db.PlatformInvoices.Include(x => x.Lines).SingleAsync(x => x.TenantId == seatTenant);
        Assert.Equal(4 * 2_000m + 6 * 1_000m, monthly.Subtotal);
        Assert.Equal(2, monthly.Lines.Count);

        // Paying a year's renewal moves the licence on a year from where it was.
        await invoicing.RecordPaymentAsync(annual, annual.Total, "Bank Transfer", null, null, null);
        await db.Entry(perFactory).ReloadAsync();
        Assert.Equal(perFactory.NextBillingDate!.Value.Date, annual.CoversTo!.Value.Date);

        // A renewal draft for a period that has since been paid is voided, not left to be sent.
        var stale = await invoicing.RaiseRenewalAsync(perFactory);
        var yearAhead = await db.PlatformInvoices.SingleAsync(x => x.Id == stale.Id);
        var prepaid = new PlatformInvoice { InvoiceNumber = "INV-PRE", TenantId = factoryTenant, CustomerName = "Tea", Total = 10m, Balance = 10m, Status = "Sent",
            Purpose = PlatformInvoicePurposes.YearOne, CoversFrom = DateTime.UtcNow.Date, CoversTo = DateTime.UtcNow.Date.AddYears(1) };
        db.PlatformInvoices.Add(prepaid);
        await db.SaveChangesAsync();
        await invoicing.RecordPaymentAsync(prepaid, 10m, "Cash", null, null, null);
        await db.Entry(yearAhead).ReloadAsync();
        Assert.Equal(("Void", 0m), (yearAhead.Status, yearAhead.Balance));
        Assert.Contains("INV-PRE", yearAhead.Notes);
        await db.Entry(perFactory).ReloadAsync();

        // The pricing endpoint shows the next one and can raise it early.
        var pricing = PricingWithInvoicing(db);
        var terms = PricingOk(await pricing.TenantTerms(factoryTenant, default));
        Assert.Equal(perFactory.NextBillingDate!.Value.Date, terms.Renewal!.CoversFrom);
        Assert.Null(terms.Renewal.ExistingInvoiceId);
        var raised = PricingOk(await pricing.RaiseRenewal(factoryTenant, default));
        Assert.NotNull(raised.Renewal!.ExistingInvoiceNumber);
        Assert.Equal(raised.Renewal.ExistingInvoiceId, PricingOk(await pricing.RaiseRenewal(factoryTenant, default)).Renewal!.ExistingInvoiceId);
    }

    private static async Task<(Guid TenantId, TenantLicense License)> TenantWithLicenseAsync(AppDbContext db, string edition, string status)
    {
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, $"Tea {tenantId:N}"[..12], $"ops-{tenantId:N}@tea.test");
        var plan = await db.LicensePlans.SingleAsync(x => x.PlanCode == edition);
        var license = new TenantLicense { TenantId = tenantId, LicensePlanId = plan.Id, LicensePlan = plan, Status = status, OfficeSeats = 5, FieldSeats = 5, BillingCycle = BillingCycles.Annual };
        db.TenantLicenses.Add(license);
        await db.SaveChangesAsync();
        return (tenantId, license);
    }

    private static PlatformBillingLifecycleService Lifecycle(AppDbContext db) =>
        new(db, new FakeEmailTemplateService(), new FakeEmailSubjectRuleService(), new EmailOutboxService(db, new EmailDeliveryLogService(db)));

    private static PlatformInvoicingService Invoicing(AppDbContext db) => new(db, Lifecycle(db));

    private static PlatformPricingController PricingWithInvoicing(AppDbContext db) =>
        new(db, new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true), new NoOpAuditLogService(), new IncludingLicenseGuard(db), Invoicing(db));

    private static PlatformFinanceController FinanceWithInvoicing(AppDbContext db) =>
        new(db, new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true), new NoOpAuditLogService(), Lifecycle(db), Invoicing(db));
}
