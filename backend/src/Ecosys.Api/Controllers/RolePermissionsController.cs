using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The tenant's role-permission matrix: which capabilities each assignable role grants.
/// Admin is not listed — it is implicitly granted every capability and cannot be edited.
/// </summary>
[ApiController]
[Authorize]
[Route("api/settings/role-permissions")]
public sealed class RolePermissionsController(
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IRolePermissionService rolePermissionService,
    IUserSessionService userSessionService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<RolePermissionMatrixResponse>> Get(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var rows = await rolePermissionService.GetMatrixAsync(TenantId, cancellationToken);
        return Ok(BuildResponse(rows));
    }

    [HttpPut]
    public async Task<ActionResult<RolePermissionMatrixResponse>> Save(
        [FromBody] SaveRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var updates = (request.Roles ?? [])
            .Where(row => !string.IsNullOrWhiteSpace(row.Role))
            .ToDictionary(row => row.Role, row => row.Permissions, StringComparer.OrdinalIgnoreCase);

        await rolePermissionService.SaveMatrixAsync(TenantId, updates, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            tenantContext.UserId,
            "settings.role_permissions.updated",
            nameof(RolePermission),
            TenantId.ToString(),
            $"Role permissions updated for: {string.Join(", ", updates.Keys)}.",
            severity: "Warning",
            cancellationToken: cancellationToken);

        // Permissions are baked into each user's JWT at login; revoke the sessions of everyone
        // holding a changed role so the new matrix takes effect on their next sign-in rather than
        // lingering for up to the token lifetime.
        await userSessionService.RevokeSessionsForRolesAsync(TenantId, updates.Keys.ToArray(), "Role permissions changed", cancellationToken);

        var rows = await rolePermissionService.GetMatrixAsync(TenantId, cancellationToken);
        return Ok(BuildResponse(rows));
    }

    private static RolePermissionMatrixResponse BuildResponse(IReadOnlyList<RolePermissionEntry> rows) =>
        new(
            rows.Select(row => new RolePermissionRowDto(row.Role, row.Permissions)).ToArray(),
            AppRoles.Admin);
}

public sealed record RolePermissionRowDto(string Role, UserPermissionsModel Permissions);

/// <param name="Roles">One row per assignable role.</param>
/// <param name="ImplicitAllAccessRole">Role that always has every capability and is not editable.</param>
public sealed record RolePermissionMatrixResponse(
    RolePermissionRowDto[] Roles,
    string ImplicitAllAccessRole);

public sealed record SaveRolePermissionsRequest(RolePermissionRowDto[] Roles);
