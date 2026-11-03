using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class EmailDeliveryLog : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public Guid? OutboxMessageId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? ErrorCategory { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? TriggeredByUserId { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? CcAddressesJson { get; set; }
    public string? DocumentType { get; set; }
    public Guid? DocumentId { get; set; }
    public bool TriggeredAutomatically { get; set; }

    public Tenant? Tenant { get; set; }
    public EmailOutboxMessage? OutboxMessage { get; set; }
}
