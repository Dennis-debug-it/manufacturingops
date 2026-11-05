using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>Job plan templates (steps, tools, parts, hours) and the priority × criticality SLA matrix.</summary>
[ApiController]
[Authorize]
public sealed class JobPlansController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IFactorySlaService slaService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("api/factory/job-plans")]
    public async Task<ActionResult<IReadOnlyCollection<JobPlanResponse>>> List([FromQuery] Guid? assetCategoryId, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var query = Load().Where(x => x.TenantId == TenantId && (includeInactive || x.IsActive));
        // An asset's own class's plans first, then the general ones; other classes' plans are left out.
        if (assetCategoryId.HasValue) query = query.Where(x => x.AssetCategoryId == null || x.AssetCategoryId == assetCategoryId);
        var plans = await query.OrderByDescending(x => x.AssetCategoryId != null).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        return Ok(plans.Select(Map).ToList());
    }

    [HttpGet("api/factory/job-plans/{id:guid}")]
    public async Task<ActionResult<JobPlanResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        return Ok(Map(await FindAsync(id, cancellationToken)));
    }

    [HttpPost("api/factory/job-plans")]
    public async Task<ActionResult<JobPlanResponse>> Create([FromBody] UpsertJobPlanRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var plan = new JobPlan { TenantId = TenantId };
        await ApplyAsync(plan, request, cancellationToken);
        dbContext.JobPlans.Add(plan);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(plan));
    }

    /// <summary>Replaces the plan's details, steps and resources. Jobs already raised keep the steps they were given.</summary>
    [HttpPut("api/factory/job-plans/{id:guid}")]
    public async Task<ActionResult<JobPlanResponse>> Update(Guid id, [FromBody] UpsertJobPlanRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var plan = await FindAsync(id, cancellationToken);
        dbContext.JobPlanSteps.RemoveRange(plan.Steps);
        dbContext.JobPlanResources.RemoveRange(plan.Resources);
        plan.Steps.Clear();
        plan.Resources.Clear();
        await ApplyAsync(plan, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(plan));
    }

    [HttpGet("api/factory/sla-matrix")]
    public async Task<ActionResult<IReadOnlyCollection<SlaMatrixCellResponse>>> Matrix(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var cells = await slaService.GetMatrixAsync(TenantId, cancellationToken);
        return Ok(cells
            .OrderByDescending(x => FactoryPriorities.All.ToList().IndexOf(x.Priority))
            .ThenBy(x => x.Criticality)
            .Select(x => new SlaMatrixCellResponse(x.Priority, x.Criticality, x.ResponseHours, x.RepairHours))
            .ToList());
    }

    /// <summary>Updates the targets. Applies to jobs raised from now on.</summary>
    [HttpPut("api/factory/sla-matrix")]
    public async Task<ActionResult<IReadOnlyCollection<SlaMatrixCellResponse>>> UpdateMatrix([FromBody] IReadOnlyCollection<SlaMatrixCellResponse> request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        var cells = await slaService.GetMatrixAsync(TenantId, cancellationToken);
        foreach (var item in request)
        {
            var cell = cells.FirstOrDefault(x => x.Priority == FactoryPriorities.Find(item.Priority) && x.Criticality == item.Criticality?.Trim().ToUpperInvariant())
                ?? throw new BusinessRuleException($"No target for {item.Priority} / {item.Criticality}.");
            if (item.ResponseHours <= 0 || item.RepairHours <= 0 || item.RepairHours < item.ResponseHours)
            {
                throw new BusinessRuleException($"{cell.Priority} / {cell.Criticality}: times must be above zero, and repair no sooner than response.");
            }

            cell.ResponseHours = item.ResponseHours;
            cell.RepairHours = item.RepairHours;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await Matrix(cancellationToken);
    }

    private void EnsureCanManage() => userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanManageSettings, PermissionNames.CanAssignWorkOrders);

    private IQueryable<JobPlan> Load() => dbContext.JobPlans
        .Include(x => x.Steps)
        .Include(x => x.Resources).ThenInclude(x => x.MaterialItem)
        .Include(x => x.AssetCategory);

    private async Task<JobPlan> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await Load().SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Job plan was not found.");

    private async Task ApplyAsync(JobPlan plan, UpsertJobPlanRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Name the job plan.");
        var name = request.Name.Trim();
        if (await dbContext.JobPlans.AnyAsync(x => x.TenantId == TenantId && x.Id != plan.Id && x.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException($"A job plan called '{name}' already exists.");
        if (request.AssetCategoryId is { } categoryId && !await dbContext.AssetCategories.AnyAsync(x => x.TenantId == TenantId && x.Id == categoryId, cancellationToken))
            throw new BusinessRuleException("Asset class was not found.");

        plan.Name = name;
        plan.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        plan.WorkType = FactoryWorkTypes.Find(request.WorkType) ?? throw new BusinessRuleException("Unknown job type.");
        plan.AssetCategoryId = request.AssetCategoryId;
        plan.EstimatedHours = request.EstimatedHours is <= 0 ? throw new BusinessRuleException("Estimated hours must be above zero.") : request.EstimatedHours;
        plan.NeedsLineStop = request.NeedsLineStop;
        plan.IsActive = request.IsActive ?? true;

        var sequence = 0;
        foreach (var step in (request.Steps ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Instruction)))
        {
            plan.Steps.Add(new JobPlanStep { JobPlanId = plan.Id, Sequence = ++sequence, Instruction = step.Instruction.Trim(), EstimatedMinutes = step.EstimatedMinutes });
        }

        var partIds = (request.Resources ?? []).Where(x => x.MaterialItemId.HasValue).Select(x => x.MaterialItemId!.Value).Distinct().ToList();
        var parts = await dbContext.MaterialItems.Where(x => x.TenantId == TenantId && partIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ItemName, cancellationToken);
        foreach (var resource in request.Resources ?? [])
        {
            var kind = JobPlanResourceKinds.All.FirstOrDefault(x => string.Equals(x, resource.Kind, StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException("A resource is a Tool or a Part.");
            if (resource.MaterialItemId is { } partId && !parts.ContainsKey(partId)) throw new BusinessRuleException("A part was not found.");
            var description = !string.IsNullOrWhiteSpace(resource.Description) ? resource.Description.Trim()
                : resource.MaterialItemId is { } id ? parts[id] : throw new BusinessRuleException("Describe each tool and part.");
            if (resource.Quantity <= 0) throw new BusinessRuleException("Quantities must be above zero.");
            plan.Resources.Add(new JobPlanResource { JobPlanId = plan.Id, Kind = kind, Description = description, MaterialItemId = resource.MaterialItemId, Quantity = resource.Quantity });
        }

        foreach (var step in plan.Steps) dbContext.Entry(step).State = EntityState.Added;
        foreach (var resource in plan.Resources) dbContext.Entry(resource).State = EntityState.Added;
    }

    internal static JobPlanResponse Map(JobPlan plan) => new(
        plan.Id, plan.Name, plan.Description, plan.WorkType, plan.AssetCategoryId, plan.AssetCategory?.Name,
        plan.EstimatedHours, plan.NeedsLineStop, plan.IsActive,
        plan.Steps.OrderBy(x => x.Sequence).Select(x => new JobPlanStepDto(x.Instruction, x.EstimatedMinutes)).ToList(),
        plan.Resources.OrderBy(x => x.Kind).ThenBy(x => x.Description).Select(x => new JobPlanResourceDto(x.Kind, x.Description, x.MaterialItemId, x.Quantity)).ToList());
}

public sealed record JobPlanStepDto([StringLength(1000)] string Instruction, int? EstimatedMinutes = null);
public sealed record JobPlanResourceDto(string Kind, [StringLength(300)] string? Description, Guid? MaterialItemId = null, decimal Quantity = 1);

public sealed record UpsertJobPlanRequest(
    [StringLength(200)] string Name,
    string WorkType,
    [StringLength(2000)] string? Description = null,
    Guid? AssetCategoryId = null,
    decimal? EstimatedHours = null,
    bool NeedsLineStop = false,
    bool? IsActive = null,
    IReadOnlyCollection<JobPlanStepDto>? Steps = null,
    IReadOnlyCollection<JobPlanResourceDto>? Resources = null);

public sealed record JobPlanResponse(
    Guid Id, string Name, string? Description, string WorkType, Guid? AssetCategoryId, string? AssetCategoryName,
    decimal? EstimatedHours, bool NeedsLineStop, bool IsActive,
    IReadOnlyCollection<JobPlanStepDto> Steps, IReadOnlyCollection<JobPlanResourceDto> Resources);

public sealed record SlaMatrixCellResponse(string Priority, string Criticality, decimal ResponseHours, decimal RepairHours);
