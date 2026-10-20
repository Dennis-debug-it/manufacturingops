using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Base for every tenant-workspace controller. The <see cref="AuthorizationPolicies.TenantStaff"/>
/// policy is declared here rather than on each controller so the tenant boundary is inherited:
/// a new controller is inside it by default and has to opt out deliberately, instead of a developer
/// having to remember a guard call on each of a hundred actions. That omission is what let a
/// client-portal token read — and silently deactivate — other customers' records (EC-001), and what
/// dropped platform staff into tenant query paths (EC-020).
///
/// It matters that this is an authorization filter and not a guard inside the action: the filter runs
/// before the action body, so no query executes and nothing can be committed by a caller who is about
/// to be refused. A guard reached from inside the body cannot make that promise — <c>DELETE
/// /api/clients/{id}</c> wrote and committed its cascade before its authorization failure surfaced.
///
/// MVC combines authorization attributes found anywhere on the controller's type hierarchy, so the
/// plain <c>[Authorize]</c> that derived controllers already carry stacks with this policy — both
/// must be satisfied. A derived controller that genuinely needs a different audience must say so
/// explicitly (and any <c>[AllowAnonymous]</c> action still wins, as it always has).
/// </summary>
[Authorize(Policy = AuthorizationPolicies.TenantStaff)]
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
            PermissionNames.CrmManage,
        };

        if (managementPermissions.Any(p => tenantContext.HasPermission(p)))
        {
            return false;
        }

        return tenantContext.HasPermission(PermissionNames.CanCompleteWorkOrders);
    }

    /// <summary>
    /// Technician-only workspace users may act only on jobs assigned to them (as assigned, lead or crew
    /// technician); branch scope alone would let them reach every job in their branch. Everyone else
    /// passes through unchanged. Matches the technician lookup used by the work order controllers:
    /// the active technician linked to this user, falling back to one with the same email.
    /// </summary>
    protected async Task EnsureTechnicianAssignedToWorkOrderAsync(AppDbContext dbContext, Guid workOrderId, CancellationToken cancellationToken)
    {
        if (!IsTechnicianOnlyWorkspaceUser())
        {
            return;
        }

        var technicians = dbContext.Technicians.Where(x => x.TenantId == TenantId && x.IsActive);
        var userId = UserId;
        var technicianId = await technicians.Where(x => x.UserId == userId).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (!technicianId.HasValue)
        {
            var email = await dbContext.Users.Where(x => x.TenantId == TenantId && x.Id == userId).Select(x => x.Email).SingleOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(email))
            {
                technicianId = await technicians.Where(x => x.Email == email).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken);
            }
        }

        var assigned = technicianId.HasValue && await dbContext.WorkOrders.AnyAsync(
            x => x.TenantId == TenantId && x.Id == workOrderId
                && (x.AssignedTechnicianId == technicianId.Value
                    || x.LeadTechnicianId == technicianId.Value
                    || x.TechnicianAssignments.Any(assignment => assignment.TechnicianId == technicianId.Value)),
            cancellationToken);

        if (!assigned)
        {
            throw new ForbiddenException("Technicians can only access jobs assigned to them.");
        }
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
        // Second layer behind the TenantStaff policy above: every action that touches TenantId or
        // UserId passes through here, so even a controller that somehow escapes the attribute (an
        // explicit [Authorize] override, a route reached outside the MVC filter pipeline) still
        // cannot resolve a tenant for a client-portal caller.
        if (tenantContext.IsPortalClient)
        {
            throw new ForbiddenException("Client portal accounts cannot access tenant operational endpoints.");
        }

        if (tenantContext.IsSuperAdmin)
        {
            throw new ForbiddenException("SuperAdmin accounts cannot access tenant operational endpoints.");
        }
    }
}
