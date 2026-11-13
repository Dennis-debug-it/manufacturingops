using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// The access zones of each factory and their entry rules (escort, induction, PPE, permit). Shared by
/// Visitor Management and Security Patrol, so it sits outside either module's gate.
/// </summary>
[ApiController]
[Authorize]
[Route("api/access-zones")]
public sealed class AccessZonesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IOrgScopeService orgScopeService,
    IVisitorService visitorService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccessZoneResponse>>> List([FromQuery] Guid? factoryId, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        var nodes = await orgScopeService.VisibleNodesAsync(TenantId, UserId, factoryId, cancellationToken);
        return Ok(await dbContext.AccessZones.AsNoTracking()
            .Where(x => x.TenantId == TenantId && (includeInactive || x.IsActive) && (nodes == null || nodes.Contains(x.FactoryId)))
            .OrderBy(x => x.Factory!.Name).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Name)
            .Select(x => new AccessZoneResponse(x.Id, x.FactoryId, x.Factory!.Name, x.OrgNodeId, x.OrgNode != null ? x.OrgNode.Name : null, x.Name, x.Level,
                x.RequiresEscort, x.RequiresInduction, x.RequiresPermit, x.RequiredPpe, x.DisplayOrder, x.IsActive))
            .ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<AccessZoneResponse>> Create([FromBody] SaveAccessZoneRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var zone = new AccessZone { TenantId = TenantId, FactoryId = request.FactoryId };
        await ApplyAsync(zone, request, cancellationToken);
        zone.DisplayOrder = request.DisplayOrder ?? (await dbContext.AccessZones.Where(x => x.TenantId == TenantId && x.FactoryId == request.FactoryId).MaxAsync(x => (int?)x.DisplayOrder, cancellationToken) ?? 0) + 1;
        dbContext.AccessZones.Add(zone);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "access-zone.created", nameof(AccessZone), zone.Id.ToString(), $"Zone {zone.Name} ({zone.Level}) added.", cancellationToken);
        return Ok(await GetAsync(zone.Id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AccessZoneResponse>> Update(Guid id, [FromBody] SaveAccessZoneRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var zone = await dbContext.AccessZones.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken) ?? throw new NotFoundException("Zone not found.");
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, zone.FactoryId, cancellationToken);
        if (request.FactoryId != zone.FactoryId) throw new BusinessRuleException("A zone cannot move to another factory; add it there instead.");
        await ApplyAsync(zone, request, cancellationToken);
        if (request.DisplayOrder is { } order) zone.DisplayOrder = order;
        zone.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "access-zone.updated", nameof(AccessZone), zone.Id.ToString(), $"Zone {zone.Name} updated.", cancellationToken);
        return Ok(await GetAsync(zone.Id, cancellationToken));
    }

    /// <summary>Adds the usual tea factory zones (reception to substation) to a factory that has none.</summary>
    [HttpPost("seed")]
    public async Task<ActionResult<IReadOnlyList<AccessZoneResponse>>> Seed([FromBody] SeedAccessZonesRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, request.FactoryId, cancellationToken);
        var added = await visitorService.SeedZonesAsync(TenantId, request.FactoryId, cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "access-zone.seeded", nameof(OrgNode), request.FactoryId.ToString(), $"{added} standard zones added.", cancellationToken);
        return await List(request.FactoryId, false, cancellationToken);
    }

    private async Task ApplyAsync(AccessZone zone, SaveAccessZoneRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 150) throw new BusinessRuleException("Give the zone a name (up to 150 characters).");
        if (!await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Id == request.FactoryId && x.Level == OrgNodeLevels.Factory, cancellationToken))
            throw new BusinessRuleException("Choose the factory the zone is in.");
        if (await dbContext.AccessZones.AnyAsync(x => x.TenantId == TenantId && x.FactoryId == request.FactoryId && x.Id != zone.Id && x.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException($"This factory already has a zone called {name}.");
        if (request.OrgNodeId is { } node)
        {
            var subtree = await orgScopeService.GetSubtreeAsync(TenantId, request.FactoryId, cancellationToken);
            if (!subtree.Contains(node) || node == request.FactoryId) throw new BusinessRuleException("The matching section must be inside the zone's factory.");
        }

        zone.Name = name;
        zone.OrgNodeId = request.OrgNodeId;
        zone.Level = AccessZoneLevels.Find(request.Level) ?? throw new BusinessRuleException($"Level must be one of: {string.Join(", ", AccessZoneLevels.All)}.");
        zone.RequiresEscort = request.RequiresEscort;
        zone.RequiresInduction = request.RequiresInduction;
        zone.RequiresPermit = request.RequiresPermit;
        var ppe = (request.RequiredPpe ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        zone.RequiredPpe = ppe.Count == 0 ? null : string.Join(", ", ppe);
        if (zone.RequiredPpe?.Length > 500) throw new BusinessRuleException("List at most 500 characters of PPE.");
    }

    private async Task<AccessZoneResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.AccessZones.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new AccessZoneResponse(x.Id, x.FactoryId, x.Factory!.Name, x.OrgNodeId, x.OrgNode != null ? x.OrgNode.Name : null, x.Name, x.Level,
                x.RequiresEscort, x.RequiresInduction, x.RequiresPermit, x.RequiredPpe, x.DisplayOrder, x.IsActive))
            .SingleAsync(cancellationToken);

    private void EnsureCanManage()
    {
        if (!IsAdmin && !HasPermission(PermissionNames.VisitorsManage) && !HasPermission(PermissionNames.CanManageSettings))
            throw new ForbiddenException("Only visitor managers and settings administrators can change access zones.");
    }
}

public sealed record AccessZoneResponse(
    Guid Id, Guid FactoryId, string FactoryName, Guid? OrgNodeId, string? OrgNodeName, string Name, string Level,
    bool RequiresEscort, bool RequiresInduction, bool RequiresPermit, string? RequiredPpe, int DisplayOrder, bool IsActive);

public sealed record SaveAccessZoneRequest(
    Guid FactoryId, string? Name, string? Level, bool RequiresEscort = false, bool RequiresInduction = false, bool RequiresPermit = false,
    string? RequiredPpe = null, Guid? OrgNodeId = null, int? DisplayOrder = null, bool IsActive = true);

public sealed record SeedAccessZonesRequest(Guid FactoryId);
