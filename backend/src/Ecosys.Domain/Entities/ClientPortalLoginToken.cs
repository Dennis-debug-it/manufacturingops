using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class ClientPortalLoginToken : AuditableEntity
{
    public Guid ClientPortalUserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public string? RequestedIp { get; set; }
    public string? UserAgent { get; set; }

    public ClientPortalUser? ClientPortalUser { get; set; }
}
