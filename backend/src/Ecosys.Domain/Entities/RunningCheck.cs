using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class CheckFailActions
{
    /// <summary>Nothing beyond recording the failure.</summary>
    public const string None = "None";
    /// <summary>Raise a corrective job straight away.</summary>
    public const string WorkOrder = "WorkOrder";
    /// <summary>Log a defect to fix in the next weekly maintenance window (a job that needs the line stopped).</summary>
    public const string WindowDefect = "WindowDefect";

    public static readonly IReadOnlyList<string> All = [None, WorkOrder, WindowDefect];

    public static string? Find(string? value) =>
        All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class CheckFrequencies
{
    public const string PerShift = "PerShift";
    public const string Daily = "Daily";
    public static readonly IReadOnlyList<string> All = [PerShift, Daily];
}

public static class CheckRunStatuses
{
    public const string Due = "Due";
    public const string Completed = "Completed";
    public const string Missed = "Missed";
}

/// <summary>
/// A running check: a checklist done on a set of assets every shift or every day, by operators
/// or artisans, usually by scanning each asset or walking them as a route.
/// </summary>
public sealed class CheckSchedule : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid PmTemplateId { get; set; }
    public string Frequency { get; set; } = CheckFrequencies.PerShift;
    /// <summary>Where the check runs; supervisors of this part of the tree hear about missed checks.</summary>
    public Guid OrgNodeId { get; set; }
    /// <summary>Walked in order as a route, rather than done asset by asset as they are scanned.</summary>
    public bool IsRoute { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public PmTemplate? PmTemplate { get; set; }
    public OrgNode? OrgNode { get; set; }
    public ICollection<CheckScheduleAsset> Assets { get; set; } = new List<CheckScheduleAsset>();
}

public sealed class CheckScheduleAsset : AuditableEntity
{
    public Guid CheckScheduleId { get; set; }
    public Guid AssetId { get; set; }
    public int Sequence { get; set; }

    public CheckSchedule? CheckSchedule { get; set; }
    public Asset? Asset { get; set; }
}

/// <summary>One check due on one asset for one shift (or day).</summary>
public sealed class CheckRun : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid CheckScheduleId { get; set; }
    public Guid AssetId { get; set; }
    public Guid OrgNodeId { get; set; }
    public Guid PmTemplateId { get; set; }
    public int TemplateVersion { get; set; }
    /// <summary>The shift or day this check belongs to, e.g. "2026-11-05/Day".</summary>
    public string PeriodKey { get; set; } = string.Empty;
    public string? ShiftName { get; set; }
    public DateTime DueFrom { get; set; }
    public DateTime DueBy { get; set; }
    public int Sequence { get; set; }
    public string Status { get; set; } = CheckRunStatuses.Due;
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? CompletedByName { get; set; }
    public int FailedCount { get; set; }
    public string? Notes { get; set; }
    public string? SignatureDataUrl { get; set; }
    public DateTime? MissedNotifiedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public CheckSchedule? CheckSchedule { get; set; }
    public Asset? Asset { get; set; }
    public ICollection<CheckRunAnswer> Answers { get; set; } = new List<CheckRunAnswer>();
}

/// <summary>An answer, with a copy of the question as it was, so editing the template never changes past checks.</summary>
public sealed class CheckRunAnswer : AuditableEntity
{
    public Guid CheckRunId { get; set; }
    public Guid? QuestionId { get; set; }
    public string? SectionName { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string ResponseType { get; set; } = "passfail";
    public string? Unit { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? Value { get; set; }
    public decimal? NumberValue { get; set; }
    /// <summary>Null when the question has no pass/fail meaning (text, photo…).</summary>
    public bool? Passed { get; set; }
    public string? Note { get; set; }
    public Guid? AttachmentId { get; set; }
    public string FailAction { get; set; } = CheckFailActions.None;
    public Guid? RaisedWorkOrderId { get; set; }
    public int DisplayOrder { get; set; }

    public CheckRun? CheckRun { get; set; }
}
