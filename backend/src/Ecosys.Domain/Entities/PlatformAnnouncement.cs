using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>A platform-wide announcement / banner shown to tenant workspaces.</summary>
public sealed class PlatformAnnouncement : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Audience { get; set; } = "AllTenants"; // AllTenants | TrialTenants | ActiveTenants
    public string Status { get; set; } = "Draft"; // Draft | Published | Archived
    public DateTime? PublishedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
