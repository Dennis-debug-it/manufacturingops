using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class FailureCodeKinds
{
    /// <summary>What was wrong (ISO 14224 failure mode).</summary>
    public const string Problem = "Problem";
    /// <summary>Why it happened (failure mechanism / cause).</summary>
    public const string Cause = "Cause";
    /// <summary>What was done about it (maintenance activity).</summary>
    public const string Remedy = "Remedy";

    public static readonly IReadOnlyList<string> All = [Problem, Cause, Remedy];

    public static string? Find(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A structured failure code recorded when a job is completed, so reliability can be analysed
/// (top failure modes, repeat faults). Codes may be limited to an asset class.
/// </summary>
public sealed class FailureCode : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Kind { get; set; } = FailureCodeKinds.Problem;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Limits the code to one asset class; null applies to every class.</summary>
    public Guid? AssetCategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public Tenant? Tenant { get; set; }
    public AssetCategory? AssetCategory { get; set; }
}
