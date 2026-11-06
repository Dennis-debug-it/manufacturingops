using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// Document types that support a configurable approval matrix. Kept as string
/// constants (not an enum) so persisted values stay stable and new types can be
/// added without a migration.
/// </summary>
public static class ApprovalDocumentTypes
{
    public const string PurchaseOrder = "PurchaseOrder";
    public const string Expense = "Expense";

    /// <summary>A customer quotation, approved before it is sent (e.g. large or low-margin quotes).</summary>
    public const string Quotation = "Quotation";

    public static readonly string[] All = [PurchaseOrder, Expense, Quotation];

    public static bool IsSupported(string? value) =>
        value is not null && All.Contains(value, StringComparer.OrdinalIgnoreCase);
}

public static class ApprovalStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class ApprovalApproverTypes
{
    /// <summary>Anyone holding <see cref="ApprovalLevel.RequiredPermission"/> may decide.</summary>
    public const string Permission = "Permission";

    /// <summary>Only the users listed in <see cref="ApprovalLevel.ApproverUserIdsCsv"/> may decide.</summary>
    public const string SpecificUsers = "SpecificUsers";
}

/// <summary>One matrix per tenant per document type. Its ordered levels define who
/// must approve at each amount band.</summary>
public sealed class ApprovalMatrix : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Currency { get; set; } = "KES";
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public List<ApprovalLevel> Levels { get; set; } = [];
}

/// <summary>An ordered approval step within a matrix, scoped to an amount band.</summary>
public sealed class ApprovalLevel : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ApprovalMatrixId { get; set; }
    public int LevelOrder { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Inclusive lower bound. A level applies when amount &gt;= MinAmount.</summary>
    public decimal MinAmount { get; set; }

    /// <summary>Exclusive upper bound; null means unbounded (applies to any amount at or above MinAmount).</summary>
    public decimal? MaxAmount { get; set; }

    public string ApproverType { get; set; } = ApprovalApproverTypes.Permission;
    public string? RequiredPermission { get; set; }
    public string? ApproverUserIdsCsv { get; set; }

    /// <summary>Hours after which a still-pending step at this level triggers a reminder. Null disables reminders.</summary>
    public int? ReminderAfterHours { get; set; }

    public ApprovalMatrix? Matrix { get; set; }
}

/// <summary>A live approval workflow for one document instance.</summary>
public sealed class ApprovalRequest : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? ApprovalMatrixId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "KES";
    public string Status { get; set; } = ApprovalStatuses.Pending;

    /// <summary>The level currently awaiting a decision. Advances up the chain as levels approve.</summary>
    public int CurrentLevelOrder { get; set; }

    public Guid InitiatedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }

    public List<ApprovalRequestStep> Steps { get; set; } = [];
}

/// <summary>One row per level in the request's chain, capturing the approver rule
/// snapshot and the decision.</summary>
public sealed class ApprovalRequestStep : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ApprovalRequestId { get; set; }
    public int LevelOrder { get; set; }
    public string LevelName { get; set; } = string.Empty;

    // Snapshot of the level's approver rule so later matrix edits don't rewrite history.
    public string ApproverType { get; set; } = ApprovalApproverTypes.Permission;
    public string? RequiredPermission { get; set; }
    public string? ApproverUserIdsCsv { get; set; }

    public string Status { get; set; } = ApprovalStatuses.Pending;
    public Guid? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? Note { get; set; }

    public DateTime? DueAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }

    public ApprovalRequest? Request { get; set; }
}
