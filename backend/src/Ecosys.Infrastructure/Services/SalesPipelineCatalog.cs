using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public sealed record SalesPipelineStageView(string Key, string Name, int SortOrder, int Probability)
{
    public bool IsClosed => Key is SalesPipelineCatalog.WonKey or SalesPipelineCatalog.LostKey;
}

/// <summary>A pipeline with its stages in order; Won and Lost are always the last two.</summary>
public sealed record SalesPipelineView(Guid Id, string Name, int SortOrder, string? Module, bool IsDefault, bool IsActive, IReadOnlyList<SalesPipelineStageView> Stages)
{
    /// <summary>The stage with this key (or, for people typing, this name), ignoring case.</summary>
    public SalesPipelineStageView? Find(string? keyOrName)
    {
        var wanted = keyOrName?.Trim();
        if (string.IsNullOrEmpty(wanted)) return null;
        return Stages.FirstOrDefault(x => string.Equals(x.Key, wanted, StringComparison.OrdinalIgnoreCase))
            ?? Stages.FirstOrDefault(x => string.Equals(x.Name, wanted, StringComparison.OrdinalIgnoreCase))
            ?? Stages.FirstOrDefault(x => string.Equals(x.Key, wanted.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase));
    }

    public SalesPipelineStageView FirstOpen => Stages.First(x => !x.IsClosed);

    public IEnumerable<SalesPipelineStageView> OpenStages => Stages.Where(x => !x.IsClosed);

    /// <summary>Position in the pipeline; -1 for a key the pipeline does not have.</summary>
    public int Rank(string key) => Stages.ToList().FindIndex(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

    public int ProbabilityOf(string key) => Find(key)?.Probability ?? 0;
}

/// <summary>
/// The workspace's pipelines. Every workspace has a default pipeline, created on first use with the
/// standard stages (Lead → Qualified → Site Survey → Proposal → Negotiation → Won / Lost), so nothing
/// needs setting up. Deals with no pipeline are in the default one.
/// </summary>
public static class SalesPipelineCatalog
{
    public const string WonKey = "Won";
    public const string LostKey = "Lost";

    public static readonly IReadOnlyList<(string Key, string Name, int Probability)> DefaultStages =
    [
        ("Lead", "Lead", 10), ("Qualified", "Qualified", 25), ("SiteSurvey", "Site Survey", 40),
        ("Proposal", "Proposal", 60), ("Negotiation", "Negotiation", 80), (WonKey, "Won", 100), (LostKey, "Lost", 0),
    ];

    public static async Task<IReadOnlyList<SalesPipelineView>> LoadAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var pipelines = await dbContext.SalesPipelines.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Include(x => x.Stages)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        if (pipelines.All(x => !x.IsDefault))
        {
            var created = await CreateDefaultAsync(dbContext, tenantId, cancellationToken);
            pipelines.Insert(0, created);
        }
        return pipelines.Select(Map).ToList();
    }

    /// <summary>The pipeline with this id, else the default.</summary>
    public static SalesPipelineView For(IReadOnlyList<SalesPipelineView> pipelines, Guid? pipelineId) =>
        (pipelineId.HasValue ? pipelines.FirstOrDefault(x => x.Id == pipelineId) : null) ?? Default(pipelines);

    public static SalesPipelineView Default(IReadOnlyList<SalesPipelineView> pipelines) =>
        pipelines.FirstOrDefault(x => x.IsDefault) ?? pipelines[0];

    public static SalesPipelineView Map(SalesPipeline pipeline) =>
        new(pipeline.Id, pipeline.Name, pipeline.SortOrder, pipeline.Module, pipeline.IsDefault, pipeline.IsActive,
            pipeline.Stages.OrderBy(x => x.SortOrder).Select(x => new SalesPipelineStageView(x.Key, x.Name, x.SortOrder, x.Probability)).ToList());

    private static async Task<SalesPipeline> CreateDefaultAsync(AppDbContext dbContext, Guid tenantId, CancellationToken cancellationToken)
    {
        var pipeline = new SalesPipeline { TenantId = tenantId, Name = "Sales", SortOrder = 0, IsDefault = true };
        var order = 0;
        foreach (var (key, name, probability) in DefaultStages)
            pipeline.Stages.Add(new SalesPipelineStage { TenantId = tenantId, PipelineId = pipeline.Id, Key = key, Name = name, SortOrder = order += 10, Probability = probability });
        // Saved at once, so callers load the catalog before making changes of their own.
        dbContext.SalesPipelines.Add(pipeline);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.Entry(pipeline).State = EntityState.Detached;
        foreach (var stage in pipeline.Stages)
            dbContext.Entry(stage).State = EntityState.Detached;
        return pipeline;
    }
}
