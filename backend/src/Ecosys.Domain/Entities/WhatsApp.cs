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

    /// <summary>
    /// JSON array of the notification keys this tenant lets use WhatsApp (Release 3 design 4.2). Null
    /// means the defaults (<c>WhatsAppTemplateCatalogue.DefaultAllowedKeys</c>). The account fields above
    /// stay unused while every tenant sends from the shared Ecosys number.
    /// </summary>
    public string? AllowedNotificationKeys { get; set; }

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

/// <summary>
/// One WhatsApp template message waiting for, or past, delivery (Release 3 design 4.1). Tenant-owned so
/// each tenant's monthly usage can be counted against the platform cap.
/// </summary>
public sealed class WhatsAppOutboxMessage : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>The notification key it was sent for, or <c>whatsapp.verification</c> for a one-time code.</summary>
    public string NotificationKey { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    /// <summary>JSON array of body parameters in template order. Cleared once a one-time code is sent.</summary>
    public string? ParametersJson { get; set; }
    /// <summary>E.164, with the leading +.</summary>
    public string ToPhone { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public Guid? RecipientUserId { get; set; }
    public Guid? RecipientPortalUserId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    /// <summary>Pending | Sending | Sent | Delivered | Read | Failed | Skipped.</summary>
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? FailedAt { get; set; }
    /// <summary>Meta's message id (wamid…), used by the webhook to record delivery status.</summary>
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? IdempotencyKey { get; set; }

    public Tenant? Tenant { get; set; }
}

/// <summary>
/// A phone number that replied STOP to the shared Ecosys number. Platform-wide (not tenant-owned):
/// one sender, so STOP blocks that number for every tenant until it replies START (design 4.2).
/// </summary>
public sealed class WhatsAppOptOut : AuditableEntity
{
    /// <summary>E.164, with the leading +.</summary>
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime OptedOutAt { get; set; } = DateTime.UtcNow;
    /// <summary>What the person sent, e.g. STOP.</summary>
    public string? Keyword { get; set; }
}

/// <summary>A one-time code sent over WhatsApp to confirm a number. Stored hashed; 10 minutes, 5 attempts.</summary>
public sealed class WhatsAppVerificationCode : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Staff (or any tenant user) confirming their own number. Exactly one of this and <see cref="ClientPortalUserId"/>.</summary>
    public Guid? UserId { get; set; }
    public Guid? ClientPortalUserId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? ConsumedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
