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

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public ICollection<ClientPortalLoginToken> LoginTokens { get; set; } = new List<ClientPortalLoginToken>();
}
