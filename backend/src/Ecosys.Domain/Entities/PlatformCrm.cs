using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>A CRM contact (person) associated with a platform lead / prospect. Platform-scoped.</summary>
public sealed class CrmContact : AuditableEntity
{
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Role { get; set; }
    public Guid? LeadId { get; set; }

    public PlatformLead? Lead { get; set; }
}

/// <summary>A logged CRM interaction (call, email, meeting, note) against a contact or lead.</summary>
public sealed class CrmActivity : AuditableEntity
{
    public string Type { get; set; } = "Note"; // Call | Email | Meeting | Note
    public string Summary { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public Guid? ContactId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? LoggedByUserId { get; set; }
    public string LoggedByName { get; set; } = string.Empty;

    public CrmContact? Contact { get; set; }
    public PlatformLead? Lead { get; set; }
}

/// <summary>A CRM follow-up task. Platform-scoped.</summary>
public sealed class CrmTask : AuditableEntity
{
    public string Title { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; } = "Medium"; // Low | Medium | High
    public string Status { get; set; } = "Open"; // Open | InProgress | Done
    public Guid? RelatedContactId { get; set; }
    public Guid? RelatedLeadId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }

    public CrmContact? RelatedContact { get; set; }
    public PlatformLead? RelatedLead { get; set; }
}

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
