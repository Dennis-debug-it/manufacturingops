using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// One email read from a workspace's intake mailbox and what was done with it. Also the guard against
/// processing the same email twice (unique per workspace and Message-ID).
/// </summary>
public sealed class EmailIntakeMessage : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    /// <summary>See <c>EmailIntakeOutcomes</c>.</summary>
    public string Outcome { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public Guid? WorkOrderId { get; set; }
    public Guid? ClientId { get; set; }
    public bool AutoReplySent { get; set; }

    public Tenant? Tenant { get; set; }
    public WorkOrder? WorkOrder { get; set; }
}
