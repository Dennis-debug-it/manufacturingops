using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Tests.Factory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>Per-factory pricing: the calculator, the price book, quotes and their PDF, and per-factory licences.</summary>
public sealed partial class AuthEmailSafetyTests
{
    private static readonly IReadOnlyDictionary<string, decimal> Editions = FactoryPricing.DefaultEditionPrices;

    [Fact]
    public void AGroupOf65Factories_GetsTheTopDiscount_AndImplementationIsOneOff()
    {
        var result = FactoryPricing.Calculate(new PriceBook(), Editions, new PricingInput(ServiceOpsPlans.Professional, Visitors: true, Security: true, Factories: 65));

        // Professional 220,000 + the Visitors/Security bundle 85,000, per factory, less 20% for 20+ factories.
        Assert.Equal(20m, result.DiscountPercent);
        Assert.Equal(19_825_000m * 0.8m, result.AnnualSubscription);
        Assert.Equal(200_000m * 65, result.OneOffTotal);
        Assert.Equal(result.AnnualSubscription + result.OneOffTotal, result.YearOneTotal);
        Assert.Equal(decimal.Round(result.YearOneTotal * 0.16m, 0), result.YearOneVat);
        Assert.Equal(decimal.Round(15_860_000m / 12 / 65, 0), result.PerFactoryPerMonth);
        Assert.Equal((15 * 65, 30 * 65), (result.IncludedOfficeUsers, result.IncludedFieldUsers));
        Assert.Contains(result.Lines, x => x.Kind == PriceLineKinds.Discount && x.Description == "Group discount for 65 factories (20%)");
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void SecurityOnly_WithAPilot_OnPremises_ExtraUsers_AndHardware()
    {
        var book = new PriceBook();
        var result = FactoryPricing.Calculate(book, Editions, new PricingInput(null, Visitors: false, Security: true, Factories: 2, ExtraFieldUsers: 4,
            ImplementationFeePerFactory: 100_000m, OnPremises: true, IncludePilot: true, Hardware: [new HardwareRequest("nfc-tag", 20), new HardwareRequest("gone", 1)]));

        Assert.Equal(0m, result.DiscountPercent); // below 5 factories
        var security = Assert.Single(result.Lines, x => x.Description.StartsWith("Security Patrol"));
        Assert.Equal(55_000m * 1.5m, security.UnitPrice); // on-premises
        Assert.Equal(55_000m * 1.5m * 2 + 4 * 12_000m, result.AnnualSubscription);
        Assert.Equal(168m, Assert.Single(result.Lines, x => x.Kind == PriceLineKinds.Hardware).UnitPrice); // 120 + 40%
        Assert.Equal(200_000m + 500_000m + 20 * 168m, result.OneOffTotal);
        Assert.Equal((3, 60_000m, true), (result.Pilot!.Months, result.Pilot.Fee, result.Pilot.CreditedToYearOne));
        Assert.Contains(result.Warnings, x => x.Contains("outside the usual"));
        Assert.Contains(result.Warnings, x => x.Contains("'gone'"));

        var agreed = FactoryPricing.Calculate(book, Editions, new PricingInput(ServiceOpsPlans.Essentials, false, false, 30, FactoryAnnualPriceOverride: 120_000m));
        Assert.Equal((0m, 3_600_000m), (agreed.DiscountPercent, agreed.AnnualSubscription));

        Assert.Throws<BusinessRuleException>(() => FactoryPricing.Calculate(book, Editions, new PricingInput(null, false, false, 1)));
        Assert.Throws<BusinessRuleException>(() => FactoryPricing.Calculate(book, Editions, new PricingInput("gold", false, false, 1)));
        Assert.Throws<BusinessRuleException>(() => FactoryPricing.Calculate(book, Editions, new PricingInput(ServiceOpsPlans.Essentials, false, false, 0)));
    }

    [Fact]
    public async Task ThePriceBook_StartsWithTheSuggestedPrices_AndRefusesNonsense()
    {
        await using var db = CreateDbContext();
        var controller = PricingController(db);

        var book = PricingOk(await controller.PriceBook(default));
        Assert.Equal([150_000m, 220_000m, 300_000m], book.Editions.Select(x => x.FactoryAnnualPrice));
        Assert.Equal([5, 20], book.DiscountTiers.Select(x => x.MinFactories));
        Assert.Equal(5, book.Hardware.Count);

        var cheaper = Request(book) with { Editions = [new EditionPrice(ServiceOpsPlans.Professional, "", 200_000m)], DiscountTiers = [new DiscountTier(10, 15)] };
        var saved = PricingOk(await controller.SavePriceBook(cheaper, default));
        Assert.Equal(200_000m, saved.Editions.Single(x => x.Code == ServiceOpsPlans.Professional).FactoryAnnualPrice);
        Assert.Equal(15m, Assert.Single(saved.DiscountTiers).Percent);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.SavePriceBook(Request(book) with { DiscountTiers = [new DiscountTier(5, 20), new DiscountTier(20, 10)] }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.SavePriceBook(Request(book) with { ImplementationFeeDefault = 900_000m }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.SavePriceBook(Request(book) with { VisitorsFactoryAnnualPrice = -1 }, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.SavePriceBook(Request(book) with { Hardware = [book.Hardware[0], book.Hardware[0]] }, default));
    }

    [Fact]
    public async Task AQuote_IsNumbered_Printed_Accepted_AndAppliedToTheWorkspace()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Chelal Tea", "ops@chelal.test");
        var plan = await db.LicensePlans.SingleAsync(x => x.PlanCode == ServiceOpsPlans.Essentials);
        db.TenantLicenses.Add(new TenantLicense { TenantId = tenantId, LicensePlanId = plan.Id, LicensePlan = plan, Status = "Active", OfficeSeats = 5, FieldSeats = 5 });
        await db.SaveChangesAsync();
        var controller = PricingController(db);

        var terms = new QuoteTermsRequest(ServiceOpsPlans.Professional, Visitors: true, Security: true, Factories: 6, ExtraOfficeUsers: 2, IncludePilot: true,
            Hardware: [new HardwareRequest("qr-label", 300)]);
        var preview = PricingOk(await controller.Preview(terms, default));
        Assert.Equal(10m, preview.DiscountPercent);

        var quote = PricingOk(await controller.CreateQuote(new SaveQuoteRequest("Chelal Tea Factory", "Jane Koech", "jane@chelal.test", null, "Pilot at Chelal first.", null, terms), default));
        Assert.Equal($"Q-{DateTime.UtcNow:yyyy}-0001", quote.Quote.QuoteNumber);
        Assert.Equal((preview.AnnualSubscription, preview.YearOneTotal), (quote.Quote.AnnualSubscription, quote.Quote.YearOneTotal));
        Assert.Equal("tester@ecosys.local", quote.Quote.PreparedByName);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(30), quote.Quote.ValidUntil);
        Assert.Equal(300, Assert.Single(quote.Request.Terms!.Hardware!).Quantity);

        var file = Assert.IsType<FileContentResult>(await controller.QuotePdfFile(quote.Quote.Id, default));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(file.FileContents, 0, 4));
        Assert.StartsWith(quote.Quote.QuoteNumber, file.FileDownloadName);

        // A price change afterwards does not alter a saved quote until it is saved again.
        var book = PricingOk(await controller.PriceBook(default));
        PricingOk(await controller.SavePriceBook(Request(book) with { SecuritySuiteFactoryAnnualPrice = 100_000m }, default));
        Assert.Equal(preview.AnnualSubscription, PricingOk(await controller.Quote(quote.Quote.Id, default)).Quote.AnnualSubscription);

        var copy = PricingOk(await controller.CopyQuote(quote.Quote.Id, default));
        Assert.Equal($"Q-{DateTime.UtcNow:yyyy}-0002", copy.Quote.QuoteNumber);
        Assert.True(copy.Quote.AnnualSubscription > quote.Quote.AnnualSubscription);
        Assert.IsType<NoContentResult>(await controller.DeleteQuote(copy.Quote.Id, default));

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.ApplyQuote(quote.Quote.Id, default));
        var linked = PricingOk(await controller.UpdateQuote(quote.Quote.Id, quote.Request with { TenantId = tenantId }, default));
        Assert.Equal("Chelal Tea", linked.Quote.TenantName);
        PricingOk(await controller.SetStatus(quote.Quote.Id, new QuoteStatusRequest("accepted"), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.UpdateQuote(quote.Quote.Id, quote.Request, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.DeleteQuote(quote.Quote.Id, default));

        var applied = PricingOk(await controller.ApplyQuote(quote.Quote.Id, default));
        Assert.Equal((PricingBases.PerFactory, ServiceOpsPlans.Professional, true, true, 6), (applied.PricingBasis, applied.Edition, applied.Visitors, applied.Security, applied.LicensedFactories));
        Assert.Equal((15 * 6 + 2, 30 * 6), (applied.OfficeSeats, applied.FieldSeats));
        Assert.Equal(BillingCycles.Annual, applied.BillingCycle);
        // Billed at the quote's price: the same yearly figure, spread over twelve months.
        Assert.Equal(decimal.Round(linked.Quote.AnnualSubscription / 12m, 0), decimal.Round(applied.MonthlyValue, 0));
        Assert.Equal(decimal.Round(linked.Quote.AnnualSubscription / 12m, 0),
            decimal.Round(await LicensePricing.MonthlyRecurringRevenueAsync(db, x => x.TenantId == tenantId, default), 0));
    }

    [Fact]
    public async Task ATenant_CanBeBilledPerFactory_AndBackPerSeat()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        await SeedTenantAsync(db, tenantId, "Kapkoros", "ops@kapkoros.test");
        var plan = await db.LicensePlans.SingleAsync(x => x.PlanCode == ServiceOpsPlans.Enterprise);
        db.TenantLicenses.Add(new TenantLicense { TenantId = tenantId, LicensePlanId = plan.Id, LicensePlan = plan, Status = "Active", OfficeSeats = 12, FieldSeats = 4, OperationsAddOns = "security" });
        db.OrgNodes.Add(new OrgNode { TenantId = tenantId, Level = OrgNodeLevels.Factory, Name = "Kapkoros", Code = "KPK" });
        await db.SaveChangesAsync();
        var controller = PricingController(db);

        var before = PricingOk(await controller.TenantTerms(tenantId, default));
        Assert.Equal((PricingBases.PerSeat, 1, (int?)null), (before.PricingBasis, before.FactoriesInWorkspace, before.LicensedFactories));
        Assert.Equal((300_000m + 55_000m) / 1, before.PerFactoryPrice!.AnnualSubscription);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.SaveTenantTerms(tenantId, new TenantFactoryTermsRequest(PricingBases.PerFactory), default));
        var perFactory = PricingOk(await controller.SaveTenantTerms(tenantId, new TenantFactoryTermsRequest(PricingBases.PerFactory, 1, 280_000m, 0, 5), default));
        Assert.Equal((15, 35), (perFactory.OfficeSeats, perFactory.FieldSeats)); // at least Enterprise's minimum of 10
        Assert.Equal(decimal.Round((280_000m + 5 * 12_000m) / 12, 2), perFactory.MonthlyValue); // the agreed price plus 5 extra users

        var perSeat = PricingOk(await controller.SaveTenantTerms(tenantId, new TenantFactoryTermsRequest(PricingBases.PerSeat), default));
        Assert.Equal(PricingBases.PerSeat, perSeat.PricingBasis);
        await Assert.ThrowsAsync<NotFoundException>(() => controller.TenantTerms(PlatformConstants.RootTenantId, default));
    }

    [Fact]
    public async Task Postgres_PricingQueries_AllTranslate()
    {
        await FactoryPostgres.RunAsync("pricing", async connection =>
        {
            var (db, context) = await FactoryPostgres.SeedAsync(connection);
            await using var _ = db;
            var tenantId = context.TenantId!.Value;
            var plan = await db.LicensePlans.SingleAsync(x => x.PlanCode == ServiceOpsPlans.Professional);
            if (!await db.TenantLicenses.AnyAsync(x => x.TenantId == tenantId))
                db.TenantLicenses.Add(new TenantLicense { TenantId = tenantId, LicensePlanId = plan.Id, Status = "Active", OfficeSeats = 5, FieldSeats = 5 });
            await db.SaveChangesAsync();
            var controller = new PlatformPricingController(db, context, new NoOpAuditLogService(), new IncludingLicenseGuard(db));

            var book = PricingOk(await controller.PriceBook(default));
            PricingOk(await controller.SavePriceBook(Request(book), default));
            var terms = new QuoteTermsRequest(ServiceOpsPlans.Professional, true, false, 3);
            var quote = PricingOk(await controller.CreateQuote(new SaveQuoteRequest("Kericho Tea", null, null, tenantId, null, null, terms), default));
            Assert.Empty(quote.Request.Terms!.Hardware!); // an empty list, never null
            Assert.Single(PricingOk(await controller.Quotes(null, default)));
            Assert.Equal("Kericho", PricingOk(await controller.Quote(quote.Quote.Id, default)).Quote.TenantName);
            Assert.Equal(PricingBases.PerFactory, PricingOk(await controller.SaveTenantTerms(tenantId, new TenantFactoryTermsRequest(PricingBases.PerFactory, 3), default)).PricingBasis);
            Assert.True(await LicensePricing.MonthlyRecurringRevenueAsync(db, x => x.TenantId == tenantId, default) > 0);
        });
    }

    private static PlatformPricingController PricingController(AppDbContext db) =>
        new(db, new FakeTenantContext(PlatformConstants.RootTenantId, Guid.NewGuid(), isAdmin: true, isSuperAdmin: true), new NoOpAuditLogService(), new IncludingLicenseGuard(db));

    private static T PricingOk<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static SavePriceBookRequest Request(PriceBookResponse b) => new(b.Editions, b.Currency, b.VatPercent, b.VisitorsFactoryAnnualPrice, b.SecurityFactoryAnnualPrice,
        b.SecuritySuiteFactoryAnnualPrice, b.IncludedOfficeUsersPerFactory, b.IncludedFieldUsersPerFactory, b.ExtraUserMonthlyPrice, b.DiscountTiers, b.ImplementationFeeMin,
        b.ImplementationFeeMax, b.ImplementationFeeDefault, b.OnPremisesMultiplier, b.OnPremisesSetupFee, b.PilotMonths, b.PilotFee, b.PilotCreditedToYearOne, b.Hardware,
        b.QuoteValidDays, b.QuoteTerms);

    /// <summary>Returns the tenant's licence with its edition loaded, as the real guard does.</summary>
    private sealed class IncludingLicenseGuard(AppDbContext db) : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            db.TenantLicenses.Include(x => x.LicensePlan).SingleAsync(x => x.TenantId == tenantId, cancellationToken);
    }
}
