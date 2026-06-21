using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class EmailOutboxMessage : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string? SenderName { get; set; }
    public string? SenderEmail { get; set; }
    public string? ReplyToEmail { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? HtmlBody { get; set; }
    public string? TextBody { get; set; }
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorCategory { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? TriggeredByUserId { get; set; }

    public Tenant? Tenant { get; set; }
}
