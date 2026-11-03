using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class EtaNotificationEventTypes
{
    public const string Assigned = "Assigned";
    public const string EnRoute = "EnRoute";
    public const string Arrived = "Arrived";
    public const string Completed = "Completed";

    public static readonly string[] All = [Assigned, EnRoute, Arrived, Completed];
}

public sealed class EtaNotificationSettings : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }
}
