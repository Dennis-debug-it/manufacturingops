using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>Per-tenant WhatsApp Business API configuration (one row per tenant).</summary>
public sealed class WhatsAppSetting : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string? BusinessAccountId { get; set; }
    public string? PhoneNumberId { get; set; }
    public string? AccessToken { get; set; }
    public string? WebhookVerifyToken { get; set; }
    public bool IsEnabled { get; set; }
    public bool AutoReplyEnabled { get; set; }
    public string? AutoReplyMessage { get; set; }

    public Tenant? Tenant { get; set; }
}

/// <summary>A WhatsApp conversation thread with a single contact.</summary>
public sealed class WhatsAppConversation : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string LastMessagePreview { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public int UnreadCount { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<WhatsAppMessage> Messages { get; set; } = new List<WhatsAppMessage>();
}

public sealed class WhatsAppMessage : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ConversationId { get; set; }
    public string Direction { get; set; } = "Outbound"; // Inbound | Outbound
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Sent"; // Sent | Delivered | Read | Failed
    public string? TemplateName { get; set; }

    public Tenant? Tenant { get; set; }
    public WhatsAppConversation? Conversation { get; set; }
}

/// <summary>A pre-approved WhatsApp message template.</summary>
public sealed class WhatsAppTemplate : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Utility"; // Utility | Marketing | Authentication
    public string Language { get; set; } = "en";
    public string Status { get; set; } = "Pending"; // Approved | Pending | Rejected
    public string BodyPreview { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }
}
