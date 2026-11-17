using System.Globalization;
using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public sealed record DiscountTier(int MinFactories, decimal Percent);

public sealed record HardwareItem(string Code, string Name, string Unit, decimal UnitCost, decimal MarginPercent)
{
    public decimal UnitPrice => decimal.Round(UnitCost * (1 + MarginPercent / 100m), 0, MidpointRounding.AwayFromZero);
}

public sealed record HardwareRequest(string Code, int Quantity);

/// <summary>What a quote (or a per-factory licence) asks for.</summary>
public sealed record PricingInput(
    string? Edition,
    bool Visitors,
    bool Security,
    int Factories,
    int ExtraOfficeUsers = 0,
    int ExtraFieldUsers = 0,
    decimal? ImplementationFeePerFactory = null,
    bool OnPremises = false,
    bool IncludePilot = false,
    decimal? DiscountPercentOverride = null,
    IReadOnlyList<HardwareRequest>? Hardware = null,
    /// <summary>An agreed price per factory per year replacing the list prices and discount (a tenant's licence).</summary>
    decimal? FactoryAnnualPriceOverride = null);

public static class PriceLineKinds
{
    public const string Subscription = "Subscription";
    public const string Discount = "Discount";
    public const string OneOff = "OneOff";
    public const string Hardware = "Hardware";
}

public sealed record PriceLine(string Kind, string Description, decimal Quantity, string Unit, decimal UnitPrice, decimal Amount);

public sealed record PilotTerms(int Months, decimal Fee, bool CreditedToYearOne, decimal Vat);

public sealed record PricingResult(
    string Currency,
    IReadOnlyList<PriceLine> Lines,
    /// <summary>The yearly subscription after the group discount.</summary>
    decimal AnnualSubscription,
    decimal MonthlyEquivalent,
    decimal PerFactoryPerMonth,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal OneOffTotal,
    decimal YearOneTotal,
    decimal VatPercent,
    decimal YearOneVat,
    decimal YearOneTotalWithVat,
    int IncludedOfficeUsers,
    int IncludedFieldUsers,
    PilotTerms? Pilot,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Prices a factory customer from the price book: edition and add-ons per factory per year, the group
/// discount by number of factories, extra users, and the one-off fees (implementation, on-premises setup,
/// hardware). Quotes and per-factory licences both use it, so the two always agree.
/// </summary>
public static class FactoryPricing
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly IReadOnlyDictionary<string, decimal> DefaultEditionPrices = new Dictionary<string, decimal>
    {
        [ServiceOpsPlans.Essentials] = 150_000m,
        [ServiceOpsPlans.Professional] = 220_000m,
        [ServiceOpsPlans.Enterprise] = 300_000m,
    };

    public static IReadOnlyList<DiscountTier> Tiers(PriceBook book) => Parse<List<DiscountTier>>(book.DiscountTiersJson) ?? [];
    public static IReadOnlyList<HardwareItem> Hardware(PriceBook book) => Parse<List<HardwareItem>>(book.HardwareJson) ?? [];
    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T? Parse<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, Json); } catch (JsonException) { return null; }
    }

    /// <summary>The group discount for this many factories: the highest tier reached.</summary>
    public static decimal GroupDiscount(PriceBook book, int factories) =>
        Tiers(book).Where(x => factories >= x.MinFactories).Select(x => x.Percent).DefaultIfEmpty(0m).Max();

    public static PricingResult Calculate(PriceBook book, IReadOnlyDictionary<string, decimal> editionPrices, PricingInput input)
    {
        if (input.Factories is < 1 or > 1000) throw new BusinessRuleException("Number of factories must be between 1 and 1,000.");
        if (input.ExtraOfficeUsers < 0 || input.ExtraFieldUsers < 0) throw new BusinessRuleException("Extra users cannot be negative.");
        if (input.DiscountPercentOverride is < 0 or > 100) throw new BusinessRuleException("A discount must be between 0 and 100%.");
        if (input.ImplementationFeePerFactory is < 0) throw new BusinessRuleException("The implementation fee cannot be negative.");
        var edition = string.IsNullOrWhiteSpace(input.Edition) ? null : input.Edition.Trim().ToLowerInvariant();
        if (edition is not null && !ServiceOpsPlans.IsEdition(edition)) throw new BusinessRuleException("Edition must be Essentials, Professional or Enterprise, or none.");
        if (edition is null && !input.Visitors && !input.Security) throw new BusinessRuleException("Choose an edition or at least one of Visitors and Security.");

        var lines = new List<PriceLine>();
        var warnings = new List<string>();
        var factories = input.Factories;
        var unit = factories == 1 ? "factory" : "factories";
        var onPrem = input.OnPremises ? book.OnPremisesMultiplier : 1m;
        var onPremNote = input.OnPremises ? " (on-premises)" : string.Empty;
        decimal perFactoryList = 0;

        void Recurring(string description, decimal perFactory)
        {
            var price = decimal.Round(perFactory * onPrem, 0, MidpointRounding.AwayFromZero);
            perFactoryList += price;
            lines.Add(new PriceLine(PriceLineKinds.Subscription, description + onPremNote, factories, unit, price, price * factories));
        }

        if (input.FactoryAnnualPriceOverride is { } agreed)
        {
            if (agreed < 0) throw new BusinessRuleException("The agreed price cannot be negative.");
            var parts = new[] { edition is null ? null : EditionName(edition), input.Visitors ? "Visitors" : null, input.Security ? "Security" : null }.Where(x => x is not null);
            lines.Add(new PriceLine(PriceLineKinds.Subscription, $"{string.Join(" + ", parts)}, agreed price per factory per year", factories, unit, agreed, agreed * factories));
            perFactoryList = agreed;
        }
        else
        {
            if (edition is not null)
            {
                var price = editionPrices.TryGetValue(edition, out var p) && p > 0 ? p : 0m;
                if (price == 0) warnings.Add($"{EditionName(edition)} has no price per factory yet; set it in the price book.");
                Recurring($"Maintenance {EditionName(edition)}, per factory per year", price);
            }

            if (input.Visitors && input.Security) Recurring("Visitor Management + Security Patrol (bundle), per factory per year", book.SecuritySuiteFactoryAnnualPrice);
            else if (input.Visitors) Recurring("Visitor Management add-on, per factory per year", book.VisitorsFactoryAnnualPrice);
            else if (input.Security) Recurring("Security Patrol add-on, per factory per year", book.SecurityFactoryAnnualPrice);
        }

        var listSubscription = lines.Sum(x => x.Amount);
        var discountPercent = input.FactoryAnnualPriceOverride is not null ? 0m : input.DiscountPercentOverride ?? GroupDiscount(book, factories);
        var discount = decimal.Round(listSubscription * discountPercent / 100m, 0, MidpointRounding.AwayFromZero);
        if (discount > 0)
        {
            var why = input.DiscountPercentOverride is not null ? "Agreed discount" : $"Group discount for {factories} factories";
            lines.Add(new PriceLine(PriceLineKinds.Discount, $"{why} ({discountPercent.ToString("0.##", CultureInfo.InvariantCulture)}%)", 1, "", -discount, -discount));
        }

        var extraUsers = input.ExtraOfficeUsers + input.ExtraFieldUsers;
        if (extraUsers > 0)
        {
            var yearly = book.ExtraUserMonthlyPrice * 12;
            lines.Add(new PriceLine(PriceLineKinds.Subscription, "Extra users beyond those included, per user per year", extraUsers, "users", yearly, yearly * extraUsers));
        }

        var annual = lines.Where(x => x.Kind is PriceLineKinds.Subscription or PriceLineKinds.Discount).Sum(x => x.Amount);

        var implementation = input.ImplementationFeePerFactory ?? book.ImplementationFeeDefault;
        if (implementation > 0)
        {
            lines.Add(new PriceLine(PriceLineKinds.OneOff, "Implementation: asset survey, data loading, tagging, set-up and on-site training", factories, unit, implementation, implementation * factories));
            if (implementation < book.ImplementationFeeMin || implementation > book.ImplementationFeeMax)
                warnings.Add($"The implementation fee is outside the usual {Money(book.ImplementationFeeMin)}–{Money(book.ImplementationFeeMax)} per factory.");
        }

        if (input.OnPremises && book.OnPremisesSetupFee > 0)
            lines.Add(new PriceLine(PriceLineKinds.OneOff, "On-premises installation and set-up", 1, "", book.OnPremisesSetupFee, book.OnPremisesSetupFee));

        var catalogue = Hardware(book).ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var request in input.Hardware ?? [])
        {
            if (request.Quantity <= 0) continue;
            if (request.Quantity > 100_000) throw new BusinessRuleException("A hardware quantity is too large.");
            if (!catalogue.TryGetValue(request.Code, out var item)) { warnings.Add($"Hardware '{request.Code}' is no longer in the catalogue and was left out."); continue; }
            lines.Add(new PriceLine(PriceLineKinds.Hardware, item.Name, request.Quantity, item.Unit, item.UnitPrice, item.UnitPrice * request.Quantity));
        }

        var oneOff = lines.Where(x => x.Kind is PriceLineKinds.OneOff or PriceLineKinds.Hardware).Sum(x => x.Amount);
        var yearOne = annual + oneOff;
        var vat = decimal.Round(yearOne * book.VatPercent / 100m, 0, MidpointRounding.AwayFromZero);
        var pilot = input.IncludePilot
            ? new PilotTerms(book.PilotMonths, book.PilotFee, book.PilotCreditedToYearOne, decimal.Round(book.PilotFee * book.VatPercent / 100m, 0, MidpointRounding.AwayFromZero))
            : null;

        return new PricingResult(
            book.Currency, lines, annual, decimal.Round(annual / 12m, 0, MidpointRounding.AwayFromZero), decimal.Round(annual / 12m / factories, 0, MidpointRounding.AwayFromZero),
            discountPercent, discount, oneOff, yearOne, book.VatPercent, vat, yearOne + vat,
            book.IncludedOfficeUsersPerFactory * factories + input.ExtraOfficeUsers, book.IncludedFieldUsersPerFactory * factories + input.ExtraFieldUsers,
            pilot, warnings);
    }

    /// <summary>Edition prices per factory from the plans (zero where not set).</summary>
    public static async Task<IReadOnlyDictionary<string, decimal>> EditionPricesAsync(AppDbContext db, CancellationToken cancellationToken) =>
        await db.LicensePlans.AsNoTracking().ToDictionaryAsync(x => x.PlanCode, x => x.FactoryAnnualPrice, cancellationToken);

    /// <summary>
    /// The platform's price book, created with the suggested prices the first time it is needed (and the
    /// editions' per-factory prices filled in where they are still zero).
    /// </summary>
    public static async Task<PriceBook> GetOrCreateAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var book = await db.PriceBooks.OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (book is not null) return book;
        book = new PriceBook();
        db.PriceBooks.Add(book);
        foreach (var plan in await db.LicensePlans.Where(x => x.FactoryAnnualPrice == 0).ToListAsync(cancellationToken))
            if (DefaultEditionPrices.TryGetValue(plan.PlanCode, out var price)) plan.FactoryAnnualPrice = price;
        await db.SaveChangesAsync(cancellationToken);
        return book;
    }

    /// <summary>What a per-factory licence asks for, priced like a quote.</summary>
    public static PricingInput InputFor(TenantLicense license) => new(
        license.LicensePlan?.PlanCode,
        ServiceOpsPlans.ParseOperationsAddOns(license.OperationsAddOns).Contains(ServiceOpsPlans.Visitors),
        ServiceOpsPlans.ParseOperationsAddOns(license.OperationsAddOns).Contains(ServiceOpsPlans.Security),
        Math.Max(1, license.LicensedFactories ?? 1),
        license.ExtraOfficeUsers,
        license.ExtraFieldUsers,
        ImplementationFeePerFactory: 0,
        FactoryAnnualPriceOverride: license.FactoryAnnualPriceOverride);

    public static string EditionName(string code) => code switch
    {
        ServiceOpsPlans.Essentials => "Essentials",
        ServiceOpsPlans.Professional => "Professional",
        ServiceOpsPlans.Enterprise => "Enterprise",
        _ => code,
    };

    public static string Money(decimal value) => "KES " + value.ToString("#,0", CultureInfo.InvariantCulture);
}
