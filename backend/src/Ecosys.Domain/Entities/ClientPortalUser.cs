using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class ClientPortalUser : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Created by non-admin staff while the tenant requires approval; cannot sign in until an admin activates it.</summary>
    public bool IsPendingApproval { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public Guid? InvitedByUserId { get; set; }
    /// <summary>
    /// Random per-contact value behind the permanent portal link (the link itself is derived from it and
    /// the server signing key, so it is never stored). Null = no link issued. Replacing it revokes the link.
    /// </summary>
    public string? AccessLinkNonce { get; set; }
    public DateTime? AccessLinkCreatedAt { get; set; }

    // WhatsApp (Release 3 design 4.2/4.4). A contact gets client messages only when ReceivesWhatsApp is
    // set, with consent recorded: by the contact from the portal (a confirmed code), or by office staff
    // (who, when and how) for a contact who doesn't use the portal.
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
    public ICollection<ClientPortalLoginToken> LoginTokens { get; set; } = new List<ClientPortalLoginToken>();
}
