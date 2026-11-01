using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <param name="Probability">The chance of winning, 1–99.</param>
/// <param name="Reasons">What moved it, in plain words, for the deal page.</param>
/// <param name="BasedOnDeals">Closed deals in the deal's pipeline the estimate rests on.</param>
public sealed record SalesDealScore(int Probability, IReadOnlyList<string> Reasons, int BasedOnDeals);

/// <summary>
/// Win-probability scoring from the workspace's own history (the last 12 months of closed deals): of the
/// deals that reached this stage of this pipeline, how many were won (smoothed, so a handful of deals
/// cannot say 0% or 100%), then adjusted for the lead source's record, for a deal that has gone quiet
/// and for one well past its expected close date. Below <see cref="MinimumClosedDeals"/> closed deals in
/// a pipeline there is too little history, and no score is given.
/// </summary>
public sealed class SalesScoring
{
    public const int MinimumClosedDeals = 10;
    public const int MinimumSourceDeals = 5;
    private static readonly TimeSpan History = TimeSpan.FromDays(365);

    private readonly Dictionary<(Guid Pipeline, string Stage), (int Won, int Lost)> _byStage = [];
    private readonly Dictionary<Guid, int> _closedByPipeline = [];
    private readonly Dictionary<string, (int Won, int Lost)> _bySource = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<SalesPipelineView> _pipelines;
    private readonly int _stalledAfterDays;
    private int _won;
    private int _lost;

    private SalesScoring(IReadOnlyList<SalesPipelineView> pipelines, int stalledAfterDays)
    {
        _pipelines = pipelines;
        _stalledAfterDays = stalledAfterDays;
    }

    public static async Task<SalesScoring> LoadAsync(AppDbContext dbContext, Guid tenantId, IReadOnlyList<SalesPipelineView> pipelines, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow - History;
        var closed = await dbContext.SalesOpportunities.AsNoTracking()
            .Where(x => x.TenantId == tenantId && ((x.Stage == "Won" && x.WonAt >= since) || (x.Stage == "Lost" && x.LostAt >= since)))
            .Select(x => new { x.Id, x.PipelineId, x.Stage, x.Source })
            .ToListAsync(cancellationToken);
        var ids = closed.Select(x => x.Id).ToList();
        var reached = (await dbContext.SalesStageChanges.AsNoTracking()
                .Where(x => x.TenantId == tenantId && ids.Contains(x.OpportunityId))
                .Select(x => new { x.OpportunityId, x.ToStage })
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.OpportunityId, x => x.ToStage);
        var stalled = await dbContext.TenantSalesSettings.AsNoTracking()
            .Where(x => x.TenantId == tenantId).Select(x => (int?)x.StalledAfterDays).FirstOrDefaultAsync(cancellationToken) ?? 14;

        var scoring = new SalesScoring(pipelines, stalled);
        var defaultId = SalesPipelineCatalog.Default(pipelines).Id;
        foreach (var deal in closed)
        {
            var won = deal.Stage == "Won";
            var pipelineId = deal.PipelineId ?? defaultId;
            var pipeline = SalesPipelineCatalog.For(pipelines, pipelineId);
            // Every deal starts in the first stage; later stages count when the deal moved into them.
            var stages = reached[deal.Id].Append(pipeline.FirstOpen.Key).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var stage in stages)
            {
                var key = (pipelineId, stage);
                var (w, l) = scoring._byStage.GetValueOrDefault(key);
                scoring._byStage[key] = won ? (w + 1, l) : (w, l + 1);
            }
            scoring._closedByPipeline[pipelineId] = scoring._closedByPipeline.GetValueOrDefault(pipelineId) + 1;
            if (!string.IsNullOrWhiteSpace(deal.Source))
            {
                var (sw, sl) = scoring._bySource.GetValueOrDefault(deal.Source);
                scoring._bySource[deal.Source] = won ? (sw + 1, sl) : (sw, sl + 1);
            }
            if (won) scoring._won++; else scoring._lost++;
        }
        return scoring;
    }

    /// <summary>The score for an open deal, or null when its pipeline has too little history.</summary>
    public SalesDealScore? Score(SalesOpportunity deal, DateTime now)
    {
        if (deal.Stage is "Won" or "Lost")
            return null;
        var pipeline = SalesPipelineCatalog.For(_pipelines, deal.PipelineId);
        var closed = _closedByPipeline.GetValueOrDefault(pipeline.Id);
        if (closed < MinimumClosedDeals)
            return null;

        var (won, lost) = _byStage.GetValueOrDefault((pipeline.Id, deal.Stage));
        var stageName = pipeline.Find(deal.Stage)?.Name ?? deal.Stage;
        var chance = (won + 1d) / (won + lost + 2d);
        var reasons = new List<string> { $"{won} of {won + lost} deals that reached {stageName} were won" };

        if (!string.IsNullOrWhiteSpace(deal.Source) && _bySource.TryGetValue(deal.Source, out var source)
            && source.Won + source.Lost >= MinimumSourceDeals && _won + _lost > 0)
        {
            var overall = (_won + 1d) / (_won + _lost + 2d);
            var factor = Math.Clamp((source.Won + 1d) / (source.Won + source.Lost + 2d) / overall, 0.6, 1.4);
            if (Math.Abs(factor - 1) >= 0.1)
            {
                chance *= factor;
                reasons.Add($"{deal.Source} leads win {(factor > 1 ? "more" : "less")} often than average");
            }
        }

        var idleDays = (int)Math.Floor((now - (deal.LastActivityAt ?? deal.CreatedAt)).TotalDays);
        if (_stalledAfterDays > 0 && idleDays >= 2 * _stalledAfterDays)
        {
            chance *= 0.5;
            reasons.Add($"Nothing logged for {idleDays} days");
        }
        else if (_stalledAfterDays > 0 && idleDays >= _stalledAfterDays)
        {
            chance *= 0.7;
            reasons.Add($"Nothing logged for {idleDays} days");
        }

        if (deal.ExpectedCloseDate is { } close && close < now.AddDays(-30))
        {
            chance *= 0.8;
            reasons.Add("Over a month past its expected close date");
        }

        return new SalesDealScore((int)Math.Clamp(Math.Round(chance * 100), 1, 99), reasons, closed);
    }
}
