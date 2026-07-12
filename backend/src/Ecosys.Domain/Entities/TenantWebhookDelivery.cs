using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>A single outbound delivery attempt of a tenant webhook, for the delivery-log view.</summary>
public sealed class TenantWebhookDelivery : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid WebhookId { get; set; }
    public string Event { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending | Success | Failed
    public int? ResponseStatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
    public TenantWebhook? Webhook { get; set; }
}
