using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// Response and repair targets for one priority × asset criticality pair. Jobs on assets with no
/// criticality use the C row. Response is met when the job is assigned; repair when it is completed.
/// </summary>
public sealed class SlaMatrixCell : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Priority { get; set; } = "Medium";
    public string Criticality { get; set; } = AssetCriticality.C;
    public decimal ResponseHours { get; set; }
    public decimal RepairHours { get; set; }

    public Tenant? Tenant { get; set; }
}

/// <summary>A reusable plan for a kind of job: the steps, tools, parts and expected hours.</summary>
public sealed class JobPlan : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string WorkType { get; set; } = "Corrective";
    /// <summary>Offered first for assets of this class; null for any asset.</summary>
    public Guid? AssetCategoryId { get; set; }
    public decimal? EstimatedHours { get; set; }
    public bool NeedsLineStop { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public AssetCategory? AssetCategory { get; set; }
    public ICollection<JobPlanStep> Steps { get; set; } = new List<JobPlanStep>();
    public ICollection<JobPlanResource> Resources { get; set; } = new List<JobPlanResource>();
}

public sealed class JobPlanStep : AuditableEntity
{
    public Guid JobPlanId { get; set; }
    public int Sequence { get; set; }
    public string Instruction { get; set; } = string.Empty;
    public int? EstimatedMinutes { get; set; }

    public JobPlan? JobPlan { get; set; }
}

public static class JobPlanResourceKinds
{
    public const string Tool = "Tool";
    public const string Part = "Part";
    public static readonly IReadOnlyList<string> All = [Tool, Part];
}

/// <summary>A tool or part the job needs. A part may point at a stock item.</summary>
public sealed class JobPlanResource : AuditableEntity
{
    public Guid JobPlanId { get; set; }
    public string Kind { get; set; } = JobPlanResourceKinds.Tool;
    public string Description { get; set; } = string.Empty;
    public Guid? MaterialItemId { get; set; }
    public decimal Quantity { get; set; } = 1;

    public JobPlan? JobPlan { get; set; }
    public MaterialItem? MaterialItem { get; set; }
}
