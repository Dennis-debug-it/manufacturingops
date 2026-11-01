using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Converts deal and tender values into the tenant's reporting currency (<c>Tenant.Currency</c>)
/// using the tenant's own exchange rates — the same table Finance › Exchange Rates maintains, so
/// one rate is entered once and used everywhere. The latest rate already in effect wins, and a
/// rate entered either way round (USD→KES or KES→USD) is used. A currency with no rate converts
/// to null: totals leave it out and say so instead of adding foreign amounts as if they were local.
/// </summary>
public sealed class SalesCurrencyConverter
{
    private readonly Dictionary<string, SalesCurrencyRate> _rates;

    private SalesCurrencyConverter(string reportingCurrency, Dictionary<string, SalesCurrencyRate> rates)
    {
        ReportingCurrency = reportingCurrency;
        _rates = rates;
    }

    public string ReportingCurrency { get; }

    public IReadOnlyCollection<SalesCurrencyRate> Rates => _rates.Values.OrderBy(x => x.Currency).ToList();

    public static async Task<SalesCurrencyConverter> LoadAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var configured = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.Currency)
            .FirstOrDefaultAsync(cancellationToken);
        var reporting = Normalize(configured);
        if (reporting.Length == 0) reporting = "KES";

        var now = DateTime.UtcNow;
        var rows = await dbContext.ExchangeRates
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.EffectiveFrom <= now && x.Rate > 0
                && (x.BaseCurrency == reporting || x.QuoteCurrency == reporting))
            .ToListAsync(cancellationToken);

        var rates = rows
            .Where(x => x.BaseCurrency != x.QuoteCurrency)
            .Select(x => x.QuoteCurrency == reporting
                // 1 USD = 129 KES: a USD amount times 129 is KES.
                ? new SalesCurrencyRate(x.BaseCurrency, x.Rate, x.EffectiveFrom, x.CreatedAt, x.UpdatedByName)
                // Entered the other way round (1 KES = 0.0077 USD): invert it.
                : new SalesCurrencyRate(x.QuoteCurrency, 1m / x.Rate, x.EffectiveFrom, x.CreatedAt, x.UpdatedByName))
            .GroupBy(x => x.Currency)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.EnteredAt).First(),
                StringComparer.OrdinalIgnoreCase);

        return new SalesCurrencyConverter(reporting, rates);
    }

    public bool CanConvert(string currency) =>
        string.Equals(Normalize(currency), ReportingCurrency, StringComparison.Ordinal) || _rates.ContainsKey(Normalize(currency));

    public decimal? Convert(decimal amount, string currency)
    {
        var code = Normalize(currency);
        if (code == ReportingCurrency) return amount;
        return _rates.TryGetValue(code, out var rate) ? Math.Round(amount * rate.RateToReporting, 2) : null;
    }

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();
}

public sealed record SalesCurrencyRate(string Currency, decimal RateToReporting, DateTime EffectiveFrom, DateTime EnteredAt, string UpdatedByName);
