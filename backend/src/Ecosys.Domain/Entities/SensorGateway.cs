using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A system that sends meter readings in: a PLC or SCADA gateway, a data logger, a vehicle tracking
/// platform. It holds a key (only its hash is stored) and posts readings by tag; a meter's tag is
/// its <see cref="Meter.ExternalId"/>. A gateway limited to a location can only feed meters there.
/// </summary>
public sealed class SensorGateway : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>When set, only meters on assets at this location or below can be fed.</summary>
    public Guid? OrgNodeId { get; set; }

    /// <summary>The key's first characters, shown to tell keys apart and used to find the gateway.</summary>
    public string KeyPrefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// A steady reading is stored at most this often (seconds); one that changes the meter's
    /// condition is always stored. Keeps a sensor that reports every second from flooding the history.
    /// </summary>
    public int SampleSeconds { get; set; } = 300;

    /// <summary>
    /// MQTT: true trusts the broker's own authentication (its ACLs decide who may publish to this
    /// gateway's topic); false requires the key in each message.
    /// </summary>
    public bool MqttTrustBroker { get; set; }

    public DateTime? LastSeenAt { get; set; }
    public long AcceptedCount { get; set; }
    public long RejectedCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }

    public Tenant? Tenant { get; set; }
    public OrgNode? OrgNode { get; set; }
}
