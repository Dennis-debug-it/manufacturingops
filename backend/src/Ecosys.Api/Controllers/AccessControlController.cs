using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController, Authorize, Route("api/access")]
public sealed class AccessControlController(AppDbContext dbContext, ITenantContext tenantContext, IUserAccessService userAccessService, IDynamicAccessService dynamicAccessService, IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("capabilities")]
    public ActionResult<object> Capabilities() => Ok(PermissionNames.All.Select(x => new { key = x, name = Humanize(x) }));

    [HttpGet("permission-groups")]
    public async Task<ActionResult> Groups(CancellationToken ct) => Ok(await dbContext.PermissionGroups.AsNoTracking().Include(x => x.Capabilities).Include(x => x.Users).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Description, x.IsActive, capabilities = x.Capabilities.Select(c => c.Capability), userCount = x.Users.Count }).ToListAsync(ct));

    [HttpPost("permission-groups")]
    public async Task<ActionResult> CreateGroup(GroupRequest request, CancellationToken ct)
    {
        EnsureCanAssign(); ValidateCapabilities(request.Capabilities);
        var group = new PermissionGroup { TenantId = TenantId, Name = request.Name.Trim(), Description = request.Description?.Trim(), IsActive = request.IsActive };
        group.Capabilities = request.Capabilities.Distinct(StringComparer.OrdinalIgnoreCase).Select(x => new GroupCapability { TenantId = TenantId, Capability = x }).ToList();
        dbContext.PermissionGroups.Add(group); await SaveAndAudit("permission_group.created", group.Id, $"Created permission group '{group.Name}'.", ct);
        return CreatedAtAction(nameof(Groups), new { id = group.Id }, new { group.Id });
    }

    [HttpPut("permission-groups/{id:guid}")]
    public async Task<ActionResult> UpdateGroup(Guid id, GroupRequest request, CancellationToken ct)
    {
        EnsureCanAssign(); ValidateCapabilities(request.Capabilities);
        var group = await dbContext.PermissionGroups.Include(x => x.Capabilities).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Permission group was not found.");
        group.Name=request.Name.Trim(); group.Description=request.Description?.Trim(); group.IsActive=request.IsActive; group.UpdatedAt=DateTime.UtcNow;
        dbContext.GroupCapabilities.RemoveRange(group.Capabilities); group.Capabilities=request.Capabilities.Distinct(StringComparer.OrdinalIgnoreCase).Select(x => new GroupCapability { TenantId=TenantId, PermissionGroupId=id, Capability=x }).ToList();
        await SaveAndAudit("permission_group.updated", id, $"Updated permission group '{group.Name}'.", ct); return NoContent();
    }

    [HttpDelete("permission-groups/{id:guid}")]
    public async Task<ActionResult> DeleteGroup(Guid id, CancellationToken ct) { EnsureCanAssign(); var row=await dbContext.PermissionGroups.SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new NotFoundException("Permission group was not found."); dbContext.Remove(row); await SaveAndAudit("permission_group.deleted",id,$"Deleted permission group '{row.Name}'.",ct); return NoContent(); }

    [HttpPut("users/{id:guid}/groups")]
    public async Task<ActionResult> SetGroups(Guid id, SetGroupsRequest request, CancellationToken ct)
    {
        EnsureCanChangeUser(id); var valid=await dbContext.PermissionGroups.Where(x=>request.GroupIds.Contains(x.Id)&&x.IsActive).Select(x=>x.Id).ToListAsync(ct); if(valid.Count!=request.GroupIds.Distinct().Count()) throw new BusinessRuleException("One or more permission groups are invalid.");
        dbContext.UserPermissionGroups.RemoveRange(await dbContext.UserPermissionGroups.Where(x=>x.UserId==id).ToListAsync(ct)); dbContext.UserPermissionGroups.AddRange(valid.Select(groupId=>new UserPermissionGroup{TenantId=TenantId,UserId=id,PermissionGroupId=groupId}));
        await SaveAndAudit("user_access.groups_changed",id,"Changed user permission-group memberships.",ct); return NoContent();
    }

    [HttpPut("users/{id:guid}/overrides")]
    public async Task<ActionResult> SetOverrides(Guid id, SetOverridesRequest request, CancellationToken ct)
    {
        EnsureCanChangeUser(id); ValidateCapabilities(request.Overrides.Select(x=>x.Capability));
        dbContext.UserPermissionOverrides.RemoveRange(await dbContext.UserPermissionOverrides.Where(x=>x.UserId==id).ToListAsync(ct)); dbContext.UserPermissionOverrides.AddRange(request.Overrides.Select(x=>new UserPermissionOverride{TenantId=TenantId,UserId=id,Capability=x.Capability,IsAllowed=x.Effect.Equals("allow",StringComparison.OrdinalIgnoreCase)}));
        await SaveAndAudit("user_access.overrides_changed",id,"Changed direct permission overrides.",ct); return NoContent();
    }

    [HttpGet("users/{id:guid}/effective")]
    public async Task<ActionResult> Effective(Guid id, CancellationToken ct) { userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings); return Ok(await dynamicAccessService.GetEffectiveAccessAsync(TenantId,id,ct)); }

    [HttpGet("scopes")]
    public async Task<ActionResult> Scopes(CancellationToken ct) { userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings); return Ok(await dbContext.AccessScopes.AsNoTracking().ToListAsync(ct)); }
    [HttpPut("users/{id:guid}/scopes")]
    public async Task<ActionResult> SetScopes(Guid id, ScopeRequest request, CancellationToken ct) { EnsureCanChangeUser(id); dbContext.AccessScopes.RemoveRange(await dbContext.AccessScopes.Where(x=>x.UserId==id).ToListAsync(ct)); dbContext.AccessScopes.AddRange(request.Scopes.Select(x=>new AccessScope{TenantId=TenantId,UserId=id,Capability=x.Capability,ScopeType=x.ScopeType,ScopeEntityId=x.ScopeEntityId})); await SaveAndAudit("user_access.scopes_changed",id,"Changed user access scopes.",ct); return NoContent(); }

    [HttpGet("approval-limits")]
    public async Task<ActionResult> Limits(CancellationToken ct) { userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings); return Ok(await dbContext.PermissionThresholds.AsNoTracking().ToListAsync(ct)); }
    [HttpPut("users/{id:guid}/approval-limits")]
    public async Task<ActionResult> SetLimits(Guid id, ThresholdRequest request, CancellationToken ct) { EnsureCanChangeUser(id); dbContext.PermissionThresholds.RemoveRange(await dbContext.PermissionThresholds.Where(x=>x.UserId==id).ToListAsync(ct)); dbContext.PermissionThresholds.AddRange(request.Limits.Select(x=>new PermissionThreshold{TenantId=TenantId,UserId=id,Capability=x.Capability,MaximumAmount=x.MaximumAmount,Currency=x.Currency.ToUpperInvariant()})); await SaveAndAudit("user_access.thresholds_changed",id,"Changed user approval limits.",ct); return NoContent(); }

    private void EnsureCanAssign() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
    private void EnsureCanChangeUser(Guid id) { EnsureCanAssign(); if(id==UserId) throw new ForbiddenException("Users cannot modify their own access."); }
    private static void ValidateCapabilities(IEnumerable<string> capabilities) { var invalid=capabilities.Where(x=>!PermissionNames.All.Contains(x,StringComparer.OrdinalIgnoreCase)).ToArray(); if(invalid.Length>0) throw new BusinessRuleException($"Unknown capabilities: {string.Join(", ",invalid)}"); }
    private async Task SaveAndAudit(string action, Guid id, string details, CancellationToken ct) { await dbContext.SaveChangesAsync(ct); await auditLogService.LogAsync(TenantId,UserId,action,"AccessControl",id.ToString(),details,ct); }
    private static string Humanize(string value) => string.Join(' ', value.Replace('.', '_').Split('_',StringSplitOptions.RemoveEmptyEntries).Select(x=>char.ToUpperInvariant(x[0])+x[1..]));
}

public sealed record GroupRequest(string Name, string? Description, bool IsActive, string[] Capabilities);
public sealed record SetGroupsRequest(Guid[] GroupIds);
public sealed record OverrideItem(string Capability, string Effect);
public sealed record SetOverridesRequest(OverrideItem[] Overrides);
public sealed record ScopeItem(string Capability, string ScopeType, Guid? ScopeEntityId);
public sealed record ScopeRequest(ScopeItem[] Scopes);
public sealed record ThresholdItem(string Capability, decimal MaximumAmount, string Currency);
public sealed record ThresholdRequest(ThresholdItem[] Limits);
