using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class NotificationSeverities
{
    public const string Info = "Info";
    public const string Warning = "Warning";
    public const string Critical = "Critical";

    public static readonly IReadOnlyCollection<string> All = [Info, Warning, Critical];
}

/// <summary>
/// One entry in a user's in-app inbox (the bell). Written by <c>INotificationDispatcher</c> only.
/// A row is also the durable record of a pending web push: <see cref="PushPendingSince"/> is set
/// when the recipient wants a push and cleared once the push worker has sent it, so a restart delays
/// a push rather than losing it. Rows are deleted 90 days after <see cref="AuditableEntity.CreatedAt"/>.
/// </summary>
public sealed class Notification : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    /// <summary>A <c>NotificationKeyDescriptor</c> key, e.g. <c>work-order.assigned</c>.</summary>
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>Plain text.</summary>
    public string Body { get; set; } = string.Empty;
    /// <summary>In-app link, e.g. <c>/projects/{id}?tab=valuations</c>.</summary>
    public string? Url { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string Severity { get; set; } = NotificationSeverities.Info;
    public DateTime? ReadAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime? PushPendingSince { get; set; }

    public Tenant? Tenant { get; set; }
    public User? User { get; set; }
}
