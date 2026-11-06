using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// A person at a client or prospect: who to call, and what part they play in buying. Not a login;
/// people who sign in to the client portal are <see cref="ClientPortalUser"/>s.
/// </summary>
public sealed class ClientContact : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    /// <summary>DecisionMaker | Procurement | Technical | Finance | Other.</summary>
    public string Role { get; set; } = "Other";
    /// <summary>The person to reach first; at most one per client.</summary>
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public Client? Client { get; set; }
}

