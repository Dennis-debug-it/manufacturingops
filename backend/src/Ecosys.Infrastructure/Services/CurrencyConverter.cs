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

    /// <summary>
    /// The currency for a new expense, bill or purchase order: the one chosen, as a three-letter code, else
    /// the workspace currency.
    /// </summary>
    public static async Task<string> ResolveAsync(AppDbContext dbContext, Guid tenantId, string? requested, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return await StockCurrencyAsync(dbContext, tenantId, cancellationToken);

        var code = Normalize(requested);
        return code.Length == 3 && code.All(char.IsAsciiLetterUpper)
            ? code
            : throw new BusinessRuleException("Currency must be a three-letter code such as KES or USD.");
    }

    /// <summary>The currency stock is valued in: the tenant's currency.</summary>
    public static async Task<string> StockCurrencyAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken = default) =>
        await dbContext.Tenants.AsNoTracking().Where(x => x.Id == tenantId).Select(x => x.Currency).SingleOrDefaultAsync(cancellationToken) ?? "KES";
}
