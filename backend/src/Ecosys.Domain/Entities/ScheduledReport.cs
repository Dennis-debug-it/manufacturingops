using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class ScheduledReport : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Human-readable name, e.g. "Monthly Revenue Report".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Revenue | Aging | ClientStatement | WorkOrderSummary | Invoices</summary>
    public string ReportType { get; set; } = string.Empty;

    /// <summary>Daily | Weekly | Monthly | Quarterly</summary>
    public string Frequency { get; set; } = "Monthly";

    /// <summary>Day of month (1–28) for Monthly / Quarterly.</summary>
    public int? DayOfMonth { get; set; }

    /// <summary>Day of week name (Monday–Sunday) for Weekly.</summary>
    public string? DayOfWeek { get; set; }

    /// <summary>Local time-of-day to send (HH:mm, stored as string).</summary>
    public string TimeOfDay { get; set; } = "08:00";

    /// <summary>LastWeek | LastMonth | LastQuarter | LastYear | YTD</summary>
    public string DateRangePreset { get; set; } = "LastMonth";

    /// <summary>For Revenue: client | workType | month</summary>
    public string? GroupBy { get; set; }

    /// <summary>For ClientStatement: specific client ID, or null for all clients.</summary>
    public string? ClientId { get; set; }

    /// <summary>Factory reports: the group, factory or section reported on; null for everything.</summary>
    public Guid? OrgNodeId { get; set; }

    /// <summary>Comma-separated recipient email addresses.</summary>
    public string Recipients { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime? LastRunAt { get; set; }

    public DateTime? NextRunAt { get; set; }

    /// <summary>Success | Failed | Pending</summary>
    public string? LastRunStatus { get; set; }

    public Tenant? Tenant { get; set; }
}
