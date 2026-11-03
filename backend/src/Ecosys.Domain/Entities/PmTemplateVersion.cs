namespace Ecosys.Domain.Entities;

/// <summary>
/// Immutable snapshot of a PM template captured whenever a template is updated.
/// Enables auditing and rollback of checklist changes.
/// </summary>
public sealed class PmTemplateVersion : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PmTemplateId { get; set; }

    /// <summary>The version number this snapshot was taken FROM (i.e. before the increment).</summary>
    public int Version { get; set; }

    /// <summary>Full serialised state of the template at this version.</summary>
    public string SnapshotJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }

    public PmTemplate? PmTemplate { get; set; }
}
