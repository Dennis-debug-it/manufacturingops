using System.Globalization;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Sales results over a period: what was won and lost and why, who and which lead sources are
/// winning, how RFP/RFQ bids turned out, and the forward forecast of open deals by expected close
/// month. Every value is in the reporting currency; deals in a currency with no rate are counted
/// but left out of the values, and <see cref="SalesReport.UnconvertedDeals"/> says how many.
/// </summary>
[ApiController]
[Authorize]
[Route("api/crm/reports")]
public sealed class SalesReportsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    public const int ForecastMonths = 6;
    private const int MaxLostReasons = 10;
    private static readonly TimeSpan DefaultPeriod = TimeSpan.FromDays(90);

    /// <param name="from">First day of the period (inclusive). Defaults to 90 days ago.</param>
    /// <param name="to">Last day of the period (inclusive). Defaults to today.</param>
    [HttpGet]
    public async Task<ActionResult<SalesReport>> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] Guid? ownerUserId,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        if (!SalesCrmAccess.CanView(tenantContext))
            throw new ForbiddenException("You do not have access to the sales pipeline.");

        var now = DateTime.UtcNow;
        var start = DateTime.SpecifyKind((from ?? now - DefaultPeriod).Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind((to ?? now).Date.AddDays(1), DateTimeKind.Utc);
        if (end <= start)
            throw new BusinessRuleException("The end of the period must be on or after its start.");

        var converter = await SalesCurrencyConverter.LoadAsync(dbContext, TenantId, cancellationToken);
        // Someone limited to their own deals gets their own report, whatever owner they ask for.
        ownerUserId = await SalesVisibility.OwnerScopeAsync(dbContext, tenantContext, TenantId, cancellationToken) ?? ownerUserId;

        var dealQuery = dbContext.SalesOpportunities.AsNoTracking().Where(x => x.TenantId == TenantId);
        if (ownerUserId.HasValue)
            dealQuery = dealQuery.Where(x => x.OwnerUserId == ownerUserId.Value);
        var deals = (await dealQuery
            .Select(x => new { x.Stage, x.EstimatedValue, x.Currency, x.Probability, x.Source, x.LostReason, x.OwnerUserId, x.OwnerName, x.ExpectedCloseDate, x.CreatedAt, x.WonAt, x.LostAt })
            .ToListAsync(cancellationToken))
            .Select(x => new Deal(
                x.Stage, x.Probability, x.Source, x.LostReason, x.OwnerUserId, x.OwnerName, x.ExpectedCloseDate, x.CreatedAt,
                x.Stage == SalesStages.Won && x.WonAt >= start && x.WonAt < end ? x.WonAt : null,
                x.Stage == SalesStages.Lost && x.LostAt >= start && x.LostAt < end,
                converter.Convert(x.EstimatedValue, x.Currency)))
            .ToList();

        var won = deals.Where(x => x.WonInPeriodAt.HasValue).ToList();
        var lost = deals.Where(x => x.LostInPeriod).ToList();
        var open = deals.Where(x => !SalesStages.IsClosed(x.Stage)).ToList();
        var created = deals.Where(x => x.CreatedAt >= start && x.CreatedAt < end).ToList();

        var owners = deals
            .Where(x => x.WonInPeriodAt.HasValue || x.LostInPeriod || !SalesStages.IsClosed(x.Stage))
            .GroupBy(x => x.OwnerUserId)
            .Select(g =>
            {
                var gWon = g.Where(x => x.WonInPeriodAt.HasValue).ToList();
                var gOpen = g.Where(x => !SalesStages.IsClosed(x.Stage)).ToList();
                var gLost = g.Count(x => x.LostInPeriod);
                return new SalesOwnerPerformance(
                    g.Key, g.Select(x => x.OwnerName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                    gOpen.Count, Sum(gOpen), Weighted(gOpen),
                    gWon.Count, Sum(gWon), gLost, WinRate(gWon.Count, gLost));
            })
            .OrderByDescending(x => x.WonValue).ThenByDescending(x => x.OpenValue)
            .ToList();

        var sources = deals
            .Where(x => x.WonInPeriodAt.HasValue || x.LostInPeriod || (x.CreatedAt >= start && x.CreatedAt < end))
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Source) ? null : x.Source.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var gWon = g.Where(x => x.WonInPeriodAt.HasValue).ToList();
                var gLost = g.Count(x => x.LostInPeriod);
                return new SalesSourcePerformance(
                    g.Key, g.Count(x => x.CreatedAt >= start && x.CreatedAt < end),
                    gWon.Count, Sum(gWon), gLost, WinRate(gWon.Count, gLost));
            })
            .OrderByDescending(x => x.WonValue).ThenByDescending(x => x.NewDeals)
            .ToList();

        // Free text, so "Price" and "price " are the same reason.
        var lostReasons = lost
            .GroupBy(x => string.IsNullOrWhiteSpace(x.LostReason) ? "Not given" : x.LostReason.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new SalesLostReason(g.Key, g.Count(), Sum(g)))
            .OrderByDescending(x => x.Count).ThenByDescending(x => x.Value)
            .Take(MaxLostReasons)
            .ToList();

        var tenderQuery = dbContext.SalesTenders.AsNoTracking().Where(x => x.TenantId == TenantId);
        if (ownerUserId.HasValue)
            tenderQuery = tenderQuery.Where(x => x.OwnerUserId == ownerUserId.Value);
        var tenders = await tenderQuery
            .Where(x => (x.SubmittedAt >= start && x.SubmittedAt < end) || (x.DecidedAt >= start && x.DecidedAt < end))
            .Select(x => new { x.Status, x.SubmittedAt, x.DecidedAt })
            .ToListAsync(cancellationToken);
        int Decided(string status) => tenders.Count(x => x.Status == status && x.DecidedAt >= start && x.DecidedAt < end);
        var tendersWon = Decided(SalesTenderStatuses.Won);
        var tendersLost = Decided(SalesTenderStatuses.Lost);

        return Ok(new SalesReport(
            converter.ReportingCurrency, start, end.AddDays(-1),
            created.Count,
            won.Count, Sum(won), lost.Count, Sum(lost), WinRate(won.Count, lost.Count),
            won.Count == 0 ? null : Math.Round(Sum(won) / won.Count, 2),
            won.Count == 0 ? null : Math.Round((decimal)won.Average(x => (x.WonInPeriodAt!.Value - x.CreatedAt).TotalDays), 1),
            open.Count, Sum(open), Weighted(open),
            BuildForecast(open, now),
            owners, sources, lostReasons,
            new SalesTenderOutcomes(
                tenders.Count(x => x.SubmittedAt >= start && x.SubmittedAt < end),
                tendersWon, tendersLost,
                Decided(SalesTenderStatuses.NoBid), Decided(SalesTenderStatuses.Cancelled),
                WinRate(tendersWon, tendersLost)),
            deals.Count(x => x.Value is null
                && (x.WonInPeriodAt.HasValue || x.LostInPeriod || !SalesStages.IsClosed(x.Stage) || (x.CreatedAt >= start && x.CreatedAt < end)))));
    }

    /// <summary>
    /// Open deals by the month they are expected to close, from this month on. Dates already in
    /// the past are "overdue" (the deal needs a new date or a decision), anything beyond the window
    /// is "later", and deals with no date are "none".
    /// </summary>
    internal static List<SalesForecastBucket> BuildForecast(IReadOnlyCollection<Deal> open, DateTime now)
    {
        var thisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = thisMonth.AddMonths(ForecastMonths);

        SalesForecastBucket Bucket(string key, DateTime? month, IEnumerable<Deal> items)
        {
            var list = items.ToList();
            return new SalesForecastBucket(key, month, list.Count, Sum(list), Weighted(list));
        }

        var buckets = new List<SalesForecastBucket>
        {
            Bucket("overdue", null, open.Where(x => x.ExpectedCloseDate < thisMonth)),
        };
        for (var month = thisMonth; month < windowEnd; month = month.AddMonths(1))
        {
            var next = month.AddMonths(1);
            buckets.Add(Bucket(month.ToString("yyyy-MM", CultureInfo.InvariantCulture), month, open.Where(x => x.ExpectedCloseDate >= month && x.ExpectedCloseDate < next)));
        }
        buckets.Add(Bucket("later", null, open.Where(x => x.ExpectedCloseDate >= windowEnd)));
        buckets.Add(Bucket("none", null, open.Where(x => x.ExpectedCloseDate is null)));
        return buckets;
    }

    private static decimal Sum(IEnumerable<Deal> deals) => deals.Sum(x => x.Value ?? 0);

    private static decimal Weighted(IEnumerable<Deal> deals) => Math.Round(deals.Sum(x => (x.Value ?? 0) * x.Probability / 100m), 2);

    private static decimal? WinRate(int won, int lost) => won + lost == 0 ? null : Math.Round(won * 100m / (won + lost), 1);

    /// <summary>One deal, reduced to what the report needs, with its value already converted.</summary>
    internal sealed record Deal(
        string Stage, int Probability, string? Source, string? LostReason, Guid? OwnerUserId, string? OwnerName,
        DateTime? ExpectedCloseDate, DateTime CreatedAt, DateTime? WonInPeriodAt, bool LostInPeriod, decimal? Value);
}

/// <summary>All values are in <see cref="ReportingCurrency"/>. <see cref="From"/> and <see cref="To"/> are inclusive days.</summary>
public sealed record SalesReport(
    string ReportingCurrency, DateTime From, DateTime To,
    int NewDeals,
    int WonCount, decimal WonValue, int LostCount, decimal LostValue, decimal? WinRate,
    decimal? AverageWonValue, decimal? AverageDaysToWin,
    int OpenCount, decimal OpenValue, decimal WeightedValue,
    IReadOnlyCollection<SalesForecastBucket> Forecast,
    IReadOnlyCollection<SalesOwnerPerformance> Owners,
    IReadOnlyCollection<SalesSourcePerformance> Sources,
    IReadOnlyCollection<SalesLostReason> LostReasons,
    SalesTenderOutcomes Tenders,
    int UnconvertedDeals);

/// <summary><see cref="Key"/> is "overdue", "later", "none" or the month as yyyy-MM (then <see cref="Month"/> is set).</summary>
public sealed record SalesForecastBucket(string Key, DateTime? Month, int Count, decimal Value, decimal WeightedValue);

public sealed record SalesOwnerPerformance(
    Guid? OwnerUserId, string? OwnerName,
    int OpenCount, decimal OpenValue, decimal WeightedValue,
    int WonCount, decimal WonValue, int LostCount, decimal? WinRate);

public sealed record SalesSourcePerformance(string? Source, int NewDeals, int WonCount, decimal WonValue, int LostCount, decimal? WinRate);

public sealed record SalesLostReason(string Reason, int Count, decimal Value);

public sealed record SalesTenderOutcomes(int Submitted, int Won, int Lost, int NoBid, int Cancelled, decimal? WinRate);
