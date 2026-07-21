using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class User : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? DefaultBranchId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public bool HasAllBranchAccess { get; set; }
    public bool MustChangePassword { get; set; }
    public string? InviteTokenHash { get; set; }
    public DateTime? InviteTokenExpiresAt { get; set; }
    public DateTime? InviteAcceptedAt { get; set; }
    public DateTime? LastCredentialSentAt { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedOutUntil { get; set; }
    public DateTime? LastFailedLoginAt { get; set; }

    public Tenant? Tenant { get; set; }
    public Branch? DefaultBranch { get; set; }
    public UserPermission? Permission { get; set; }
    public Technician? TechnicianProfile { get; set; }
    public ICollection<UserCertification> Certifications { get; set; } = new List<UserCertification>();
    public ICollection<UserBranchAssignment> BranchAssignments { get; set; } = new List<UserBranchAssignment>();
    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    public ICollection<UserPermissionGroup> PermissionGroups { get; set; } = new List<UserPermissionGroup>();
    public ICollection<UserPermissionOverride> PermissionOverrides { get; set; } = new List<UserPermissionOverride>();
    public ICollection<AccessScope> AccessScopes { get; set; } = new List<AccessScope>();
    public ICollection<PermissionThreshold> PermissionThresholds { get; set; } = new List<PermissionThreshold>();
    public ICollection<Contract> RenewalOwnedContracts { get; set; } = new List<Contract>();
    public ICollection<Contract> AccountManagedContracts { get; set; } = new List<Contract>();
    public ICollection<Contract> InternallyApprovedContracts { get; set; } = new List<Contract>();
    public ICollection<ContractRenewal> ContractRenewalsOwned { get; set; } = new List<ContractRenewal>();
    public ICollection<ClientPreferentialRate> ApprovedPreferentialRates { get; set; } = new List<ClientPreferentialRate>();
    public ICollection<MaterialIssue> MaterialIssuesRequested { get; set; } = new List<MaterialIssue>();
    public ICollection<MaterialIssue> MaterialIssuesApproved { get; set; } = new List<MaterialIssue>();
    public ICollection<MaterialIssue> MaterialIssuesIssued { get; set; } = new List<MaterialIssue>();
    public ICollection<MaterialIssue> MaterialIssuesReceived { get; set; } = new List<MaterialIssue>();
}
