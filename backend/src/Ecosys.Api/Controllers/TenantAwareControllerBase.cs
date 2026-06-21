using Ecosys.Shared.Errors;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Ecosys.Api.Controllers;

public abstract class TenantAwareControllerBase(ITenantContext tenantContext) : ControllerBase
{
    protected Guid TenantId => EnsureTenantAccessAndGetId();
    protected Guid UserId => EnsureTenantAccessAndGetUserId();
    protected bool IsAdmin => tenantContext.IsAdmin;
    protected bool HasPermission(string permissionName) => tenantContext.HasPermission(permissionName);
    protected bool IsTechnicianOnlyWorkspaceUser()
    {
        if (tenantContext.IsAdmin || tenantContext.IsSuperAdmin)
        {
            return false;
        }

        var managementPermissions = new[]
        {
            PermissionNames.CanCreateWorkOrders,
            PermissionNames.CanAssignWorkOrders,
            PermissionNames.CanManageKnowledge,
            PermissionNames.CanManageMaterials,
            PermissionNames.CanApproveMaterials,
            PermissionNames.CanIssueMaterials,
            PermissionNames.CanManageAssets,
            PermissionNames.CanManageSettings,
            PermissionNames.CanViewReports,
            PermissionNames.FinanceView,
            PermissionNames.FinanceManage,
            PermissionNames.FinanceViewRates,
            PermissionNames.FinanceManageRates,
            PermissionNames.FinanceViewApprovals,
            PermissionNames.FinanceManageApprovals,
            PermissionNames.FinanceViewBillableWork,
            PermissionNames.FinanceManageBillableWork,
            PermissionNames.MaterialsOverrideStock,
        };

        if (managementPermissions.Any(p => tenantContext.HasPermission(p)))
        {
            return false;
        }

        return tenantContext.HasPermission(PermissionNames.CanCompleteWorkOrders);
    }

    protected string UserDisplayName =>
        User.Identity?.Name
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
        ?? "Unknown User";

    private Guid EnsureTenantAccessAndGetId()
    {
        EnsureTenantOperationalAccess();
        return tenantContext.GetRequiredTenantId();
    }

    private Guid EnsureTenantAccessAndGetUserId()
    {
        EnsureTenantOperationalAccess();
        return tenantContext.GetRequiredUserId();
    }

    protected void EnsureTenantOperationalAccess()
    {
        if (tenantContext.IsSuperAdmin)
        {
            throw new ForbiddenException("SuperAdmin accounts cannot access tenant operational endpoints.");
        }
    }
}
