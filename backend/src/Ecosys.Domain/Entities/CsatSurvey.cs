using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// One customer-satisfaction survey per completed work order. Created when the
/// completion email is queued; the emailed link carries a single-use token whose
/// hash is stored here. Portal users can respond without a token (channel Portal).
/// </summary>
public sealed class CsatSurvey : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? TechnicianId { get; set; }

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? SentAt { get; set; }

    public int? Score { get; set; }
    public string? Comment { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? Channel { get; set; }

    public WorkOrder? WorkOrder { get; set; }
}
