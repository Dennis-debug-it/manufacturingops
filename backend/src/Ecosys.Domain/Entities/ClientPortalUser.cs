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
    public DateTime? LastLoginAt { get; set; }
    public Guid? InvitedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public ICollection<ClientPortalLoginToken> LoginTokens { get; set; } = new List<ClientPortalLoginToken>();
}
