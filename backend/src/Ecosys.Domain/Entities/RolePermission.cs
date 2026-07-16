using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// Tenant-editable mapping of a tenant role to the capabilities it grants.
/// One row per (TenantId, Role). This is the source of truth for a user's
/// effective permissions — Admin is granted everything implicitly and has no row.
/// </summary>
public sealed class RolePermission : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>One of <see cref="Ecosys.Shared.Auth.AppRoles.TenantRoles"/>, lower-cased.</summary>
    public string Role { get; set; } = string.Empty;

    public bool CanViewWorkOrders { get; set; }
    public bool CanCreateWorkOrders { get; set; }
    public bool CanAssignWorkOrders { get; set; }
    public bool CanCompleteWorkOrders { get; set; }
    public bool CanViewKnowledge { get; set; }
    public bool CanManageKnowledge { get; set; }
    public bool CanViewMaterials { get; set; }
    public bool CanRequestMaterials { get; set; }
    public bool CanManageMaterials { get; set; }
    public bool CanApproveMaterials { get; set; }
    public bool CanIssueMaterials { get; set; }
    public bool CanManageAssets { get; set; }
    public bool CanManageSettings { get; set; }
    public bool CanViewReports { get; set; }
    public bool CanViewFinance { get; set; }
    public bool CanManageFinance { get; set; }
    public bool CanViewFinanceRates { get; set; }
    public bool CanManageFinanceRates { get; set; }
    public bool CanViewFinanceApprovals { get; set; }
    public bool CanManageFinanceApprovals { get; set; }
    public bool CanViewBillableWork { get; set; }
    public bool CanManageBillableWork { get; set; }
    public bool CanOverrideMaterialStock { get; set; }

    public Tenant? Tenant { get; set; }
}
