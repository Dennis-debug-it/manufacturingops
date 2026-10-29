using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Pipelines and their stages (Settings › Sales). Everyone with CRM access sees the active pipelines
/// their plan includes; admins see and change all of them. Stages are given in order; Won and Lost
/// always end every pipeline. A stage that still holds deals cannot be removed, and a stage keeps its
/// key for life, so renaming never touches the deals in it.
/// </summary>
public sealed partial class SalesCrmController
{
    [HttpGet("pipelines")]
    public async Task<ActionResult<IReadOnlyList<SalesPipelineResponse>>> ListPipelines(CancellationToken cancellationToken)
    {
        EnsureCanView();
        var pipelines = await PipelinesAsync(cancellationToken);
        var modules = (await licenseGuardService.GetSnapshotAsync(TenantId, cancellationToken)).Modules;
        var isAdmin = tenantContext.IsAdmin;
        var defaultId = SalesPipelineCatalog.Default(pipelines).Id;
        var counts = await dbContext.SalesOpportunities.AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .GroupBy(x => new { Pipeline = x.PipelineId ?? defaultId, x.Stage })
            .Select(g => new { g.Key.Pipeline, g.Key.Stage, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return Ok(pipelines
            .Select(p => new { Pipeline = p, Available = p.Module is null || modules.Contains(p.Module, StringComparer.OrdinalIgnoreCase) })
            .Where(x => isAdmin || (x.Pipeline.IsActive && x.Available))
            .Select(x => new SalesPipelineResponse(
                x.Pipeline.Id, x.Pipeline.Name, x.Pipeline.SortOrder, x.Pipeline.Module, x.Pipeline.IsDefault, x.Pipeline.IsActive, x.Available,
                x.Pipeline.Stages.Select(s => new SalesPipelineStageResponse(
                    s.Key, s.Name, s.Probability, s.IsClosed,
                    counts.Where(c => c.Pipeline == x.Pipeline.Id && c.Stage == s.Key).Sum(c => c.Count))).ToList()))
            .ToList());
    }

    [HttpPost("pipelines")]
    public async Task<ActionResult<IReadOnlyList<SalesPipelineResponse>>> CreatePipeline([FromBody] UpsertSalesPipelineRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var existing = await PipelinesAsync(cancellationToken);
        var pipeline = new SalesPipeline { TenantId = TenantId, SortOrder = existing.Max(x => x.SortOrder) + 10 };
        ApplyPipeline(pipeline, request, inUse: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        dbContext.SalesPipelines.Add(pipeline);
        await SetDefaultAsync(pipeline, request.IsDefault, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        _pipelines = null;
        return await ListPipelines(cancellationToken);
    }

    [HttpPut("pipelines/{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<SalesPipelineResponse>>> UpdatePipeline(Guid id, [FromBody] UpsertSalesPipelineRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var pipelines = await PipelinesAsync(cancellationToken);
        var pipeline = await dbContext.SalesPipelines.Include(x => x.Stages).SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Pipeline not found.");
        var inUse = (await InPipeline(dbContext.SalesOpportunities.Where(x => x.TenantId == TenantId), SalesPipelineCatalog.For(pipelines, id))
                .Select(x => x.Stage).Distinct().ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (pipeline.IsDefault && !request.IsActive)
            throw new BusinessRuleException("The default pipeline cannot be switched off; make another one the default first.");
        ApplyPipeline(pipeline, request, inUse);
        if (request.SortOrder.HasValue) pipeline.SortOrder = request.SortOrder.Value;
        await SetDefaultAsync(pipeline, request.IsDefault, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        _pipelines = null;
        return await ListPipelines(cancellationToken);
    }

    [HttpDelete("pipelines/{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<SalesPipelineResponse>>> DeletePipeline(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var pipeline = await dbContext.SalesPipelines.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Pipeline not found.");
        if (pipeline.IsDefault)
            throw new BusinessRuleException("The default pipeline cannot be deleted.");
        if (await dbContext.SalesOpportunities.AnyAsync(x => x.TenantId == TenantId && x.PipelineId == id, cancellationToken))
            throw new BusinessRuleException($"The {pipeline.Name} pipeline still has deals. Move them, or switch the pipeline off instead.");
        dbContext.SalesPipelines.Remove(pipeline);
        await dbContext.SaveChangesAsync(cancellationToken);
        _pipelines = null;
        return await ListPipelines(cancellationToken);
    }

    /// <summary>Sets name, module and stages. Open stages come from the request in order; Won and Lost are kept at the end.</summary>
    private void ApplyPipeline(SalesPipeline pipeline, UpsertSalesPipelineRequest request, HashSet<string> inUse)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Give the pipeline a name.");
        var open = request.Stages ?? [];
        if (open.Count == 0)
            throw new BusinessRuleException("A pipeline needs at least one stage before Won and Lost.");
        if (open.Any(x => string.IsNullOrWhiteSpace(x.Name)))
            throw new BusinessRuleException("Every stage needs a name.");
        if (open.Any(x => x.Probability is < 0 or > 100))
            throw new BusinessRuleException("Stage probabilities run from 0 to 100.");
        var names = open.Select(x => x.Name.Trim()).Concat(["Won", "Lost"]).ToList();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
            throw new BusinessRuleException("Stage names must be different from each other (and from Won and Lost).");
        var module = string.IsNullOrWhiteSpace(request.Module) ? null : request.Module.Trim().ToLowerInvariant();
        if (module is not null && !ServiceOpsPlans.AllModules.Contains(module, StringComparer.OrdinalIgnoreCase))
            throw new BusinessRuleException($"Unknown module '{request.Module}'.");

        var keptKeys = open.Where(x => !string.IsNullOrWhiteSpace(x.Key)).Select(x => x.Key!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed = pipeline.Stages.Where(x => x.Key is not SalesPipelineCatalog.WonKey and not SalesPipelineCatalog.LostKey && !keptKeys.Contains(x.Key)).ToList();
        var blocked = removed.Where(x => inUse.Contains(x.Key)).Select(x => x.Name).ToList();
        if (blocked.Count > 0)
            throw new BusinessRuleException($"Move the deals out of {string.Join(", ", blocked)} before removing {(blocked.Count == 1 ? "it" : "them")}.");
        foreach (var stage in removed)
        {
            pipeline.Stages.Remove(stage);
            dbContext.SalesPipelineStages.Remove(stage);
        }

        pipeline.Name = request.Name.Trim();
        pipeline.Module = module;
        pipeline.IsActive = request.IsActive;
        var order = 0;
        var taken = pipeline.Stages.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in open)
        {
            var stage = string.IsNullOrWhiteSpace(item.Key) ? null : pipeline.Stages.FirstOrDefault(x => string.Equals(x.Key, item.Key.Trim(), StringComparison.OrdinalIgnoreCase));
            if (stage is null)
            {
                stage = new SalesPipelineStage { TenantId = TenantId, PipelineId = pipeline.Id, Key = UniqueKey(item.Name, taken) };
                taken.Add(stage.Key);
                pipeline.Stages.Add(stage);
            }
            stage.Name = item.Name.Trim();
            stage.Probability = item.Probability;
            stage.SortOrder = order += 10;
        }
        foreach (var (key, probability) in new[] { (SalesPipelineCatalog.WonKey, 100), (SalesPipelineCatalog.LostKey, 0) })
        {
            var stage = pipeline.Stages.FirstOrDefault(x => x.Key == key);
            if (stage is null)
            {
                stage = new SalesPipelineStage { TenantId = TenantId, PipelineId = pipeline.Id, Key = key, Name = key, Probability = probability };
                pipeline.Stages.Add(stage);
            }
            stage.SortOrder = order += 10;
        }
    }

    /// <summary>A stage key from its name ("Site visit booked" → "SiteVisitBooked"), made unique within the pipeline.</summary>
    private static string UniqueKey(string name, HashSet<string> taken)
    {
        var words = new string(name.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var key = string.Concat(words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
        if (key.Length == 0) key = "Stage";
        if (key.Length > 34) key = key[..34];
        var candidate = key;
        for (var i = 2; taken.Contains(candidate) || candidate is "Open" or "Stalled"; i++)
            candidate = $"{key}{i}";
        return candidate;
    }

    private async Task SetDefaultAsync(SalesPipeline pipeline, bool makeDefault, CancellationToken cancellationToken)
    {
        if (!makeDefault || pipeline.IsDefault)
            return;
        if (!pipeline.IsActive)
            throw new BusinessRuleException("Switch the pipeline on before making it the default.");
        await dbContext.SalesPipelines.Where(x => x.TenantId == TenantId && x.IsDefault && x.Id != pipeline.Id)
            .ForEachAsync(x => x.IsDefault = false, cancellationToken);
        // Deals with no pipeline were in the old default; pin them there before the default moves.
        var oldDefault = SalesPipelineCatalog.Default(await PipelinesAsync(cancellationToken));
        await dbContext.SalesOpportunities.Where(x => x.TenantId == TenantId && x.PipelineId == null)
            .ForEachAsync(x => x.PipelineId = oldDefault.Id, cancellationToken);
        pipeline.IsDefault = true;
    }
}

public sealed record SalesPipelineStageResponse(string Key, string Name, int Probability, bool IsClosed, int DealCount);

public sealed record SalesPipelineResponse(
    Guid Id, string Name, int SortOrder, string? Module, bool IsDefault, bool IsActive, bool Available, IReadOnlyList<SalesPipelineStageResponse> Stages);

public sealed record UpsertSalesPipelineStageRequest([StringLength(40)] string? Key, [StringLength(60)] string Name, int Probability);

public sealed record UpsertSalesPipelineRequest(
    [StringLength(80)] string Name,
    [StringLength(40)] string? Module,
    bool IsActive,
    bool IsDefault,
    IReadOnlyList<UpsertSalesPipelineStageRequest>? Stages,
    int? SortOrder = null);
