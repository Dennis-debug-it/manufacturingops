using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>A person at a client who receives notifications (email, WhatsApp) and can attend activities.</summary>
public sealed class ClientPortalUser : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? InvitedByUserId { get; set; }

    // WhatsApp (Release 3 design 4.2/4.4). A contact gets client messages only when ReceivesWhatsApp is
    // set, with consent recorded: recorded by office staff (who, when and how).
    /// <summary>E.164, with the leading +.</summary>
    public string? WhatsAppNumber { get; set; }
    /// <summary>Set when the contact confirmed the number with a one-time code from the portal.</summary>
    public DateTime? WhatsAppVerifiedAt { get; set; }
    public DateTime? WhatsAppConsentAt { get; set; }
    /// <summary>The staff user who recorded consent; null when the contact opted in from the portal.</summary>
    public Guid? WhatsAppConsentRecordedByUserId { get; set; }
    /// <summary>Portal | InPerson | Phone | InWriting.</summary>
    public string? WhatsAppConsentMethod { get; set; }
    public bool ReceivesWhatsApp { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
}
