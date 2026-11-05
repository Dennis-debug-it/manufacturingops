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
/// The configurable group → factory → section → sub-section hierarchy. Every user can read the
/// part of the tree they are scoped to; changing it needs admin or settings permission.
/// </summary>
[ApiController]
[Authorize]
[Route("api/org-nodes")]
public sealed class OrgNodesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IOrgScopeService orgScopeService,
    IFactorySettingsService factorySettingsService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<OrgNodeResponse>>> List([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        var nodes = await dbContext.OrgNodes
            .Where(x => x.TenantId == TenantId && (includeInactive || x.IsActive))
            .ToListAsync(cancellationToken);

        var assetCounts = await dbContext.Assets
            .Where(x => x.TenantId == TenantId && x.OrgNodeId != null)
            .GroupBy(x => x.OrgNodeId!.Value)
            .Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        // Users scoped below the top see their own subtree, plus its ancestors as read-only context
        // so the tree still has a root.
        var visible = scope.Nodes is null
            ? nodes.Select(x => x.Id).ToHashSet()
            : WithAncestors(nodes, scope.Nodes);

        var childCounts = nodes.Where(x => x.ParentId.HasValue).GroupBy(x => x.ParentId!.Value).ToDictionary(x => x.Key, x => x.Count());

        return Ok(nodes
            .Where(x => visible.Contains(x.Id))
            .OrderBy(x => OrgNodeLevels.Rank(x.Level))
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => Map(x, scope.Allows(x.Id), childCounts.GetValueOrDefault(x.Id), assetCounts.GetValueOrDefault(x.Id)))
            .ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrgNodeResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var node = await FindAsync(id, cancellationToken);
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, id, cancellationToken);
        return Ok(await MapAsync(node, cancellationToken));
    }

    [HttpGet("templates")]
    public ActionResult<IReadOnlyCollection<IndustryTemplateResponse>> Templates() =>
        Ok(IndustryTemplates.All
            .Select(x => new IndustryTemplateResponse(x.Key, x.Name, x.ProductionUnit, x.Sections.Select(s => new SectionTemplateResponse(s.Code, s.Name, s.Description)).ToList()))
            .ToList());

    [HttpPost]
    public async Task<ActionResult<OrgNodeResponse>> Create([FromBody] UpsertOrgNodeRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var level = await ValidateAsync(request, null, cancellationToken);
        await EnsureCanWriteUnderAsync(request.ParentId, cancellationToken);

        var node = new OrgNode
        {
            TenantId = TenantId,
            ParentId = request.ParentId,
            Level = level,
            Name = request.Name.Trim(),
            Code = request.Code.Trim().ToUpperInvariant(),
            Description = Normalize(request.Description),
            Location = Normalize(request.Location),
            DisplayOrder = request.DisplayOrder ?? 0,
            IsActive = request.IsActive ?? true,
            ExternalId = Normalize(request.ExternalId),
            ExternalSystem = Normalize(request.ExternalSystem)
        };

        dbContext.OrgNodes.Add(node);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "org-node.created", nameof(OrgNode), node.Id.ToString(),
            $"{node.Level} '{node.Name}' ({node.Code}) created.", cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = node.Id }, await MapAsync(node, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrgNodeResponse>> Update(Guid id, [FromBody] UpsertOrgNodeRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var node = await FindAsync(id, cancellationToken);
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, id, cancellationToken);
        var level = await ValidateAsync(request, node, cancellationToken);

        if (request.ParentId != node.ParentId)
        {
            await EnsureCanWriteUnderAsync(request.ParentId, cancellationToken);
            if (request.ParentId.HasValue)
            {
                var subtree = await orgScopeService.GetSubtreeAsync(TenantId, id, cancellationToken);
                if (subtree.Contains(request.ParentId.Value))
                {
                    throw new BusinessRuleException("A node cannot be moved under itself or one of its own children.");
                }
            }
        }

        if (request.IsActive == false && node.IsActive)
        {
            await EnsureCanDeactivateAsync(node, cancellationToken);
        }

        var moved = request.ParentId != node.ParentId;
        node.ParentId = request.ParentId;
        node.Level = level;
        node.Name = request.Name.Trim();
        node.Code = request.Code.Trim().ToUpperInvariant();
        node.Description = Normalize(request.Description);
        node.Location = Normalize(request.Location);
        node.DisplayOrder = request.DisplayOrder ?? node.DisplayOrder;
        node.IsActive = request.IsActive ?? node.IsActive;
        node.ExternalId = Normalize(request.ExternalId);
        node.ExternalSystem = Normalize(request.ExternalSystem);

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, moved ? "org-node.moved" : "org-node.updated", nameof(OrgNode), node.Id.ToString(),
            $"{node.Level} '{node.Name}' ({node.Code}) {(moved ? "moved" : "updated")}.", cancellationToken);

        return Ok(await MapAsync(node, cancellationToken));
    }

    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<OrgNodeResponse>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var node = await FindAsync(id, cancellationToken);
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, id, cancellationToken);
        await EnsureCanDeactivateAsync(node, cancellationToken);

        node.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "org-node.deactivated", nameof(OrgNode), node.Id.ToString(),
            $"{node.Level} '{node.Name}' deactivated.", cancellationToken);

        return Ok(await MapAsync(node, cancellationToken));
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<ActionResult<OrgNodeResponse>> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var node = await FindAsync(id, cancellationToken);
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, id, cancellationToken);

        if (node.ParentId.HasValue && !await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Id == node.ParentId && x.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("Reactivate the parent first.");
        }

        node.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapAsync(node, cancellationToken));
    }

    /// <summary>
    /// Adds a template's default sections to a factory. Sections that already exist (same name under
    /// this factory) are left alone, so it is safe to run again after the customer edits the list.
    /// </summary>
    [HttpPost("{id:guid}/apply-template")]
    public async Task<ActionResult<ApplyTemplateResponse>> ApplyTemplate(Guid id, [FromBody] ApplyTemplateRequest request, CancellationToken cancellationToken)
    {
        EnsureCanManage();
        var factory = await FindAsync(id, cancellationToken);
        await orgScopeService.EnsureCanAccessAsync(TenantId, UserId, id, cancellationToken);
        if (factory.Level != OrgNodeLevels.Factory)
        {
            throw new BusinessRuleException("Default sections can only be added to a factory.");
        }

        var settings = await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken);
        var template = IndustryTemplates.Find(request.TemplateKey ?? settings.IndustryTemplate)
            ?? throw new BusinessRuleException("Unknown industry template.");

        var existingNames = await dbContext.OrgNodes
            .Where(x => x.TenantId == TenantId && x.ParentId == id)
            .Select(x => x.Name.ToLower())
            .ToListAsync(cancellationToken);
        var usedCodes = (await dbContext.OrgNodes.Where(x => x.TenantId == TenantId).Select(x => x.Code).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = new List<OrgNode>();
        var order = existingNames.Count;
        foreach (var section in template.Sections)
        {
            if (existingNames.Contains(section.Name.ToLowerInvariant()))
            {
                continue;
            }

            var code = UniqueCode($"{factory.Code}-{section.Code}", usedCodes);
            var node = new OrgNode
            {
                TenantId = TenantId,
                ParentId = id,
                Level = OrgNodeLevels.Section,
                Name = section.Name,
                Code = code,
                Description = section.Description,
                DisplayOrder = ++order,
                TemplateKey = template.Key
            };
            dbContext.OrgNodes.Add(node);
            created.Add(node);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (created.Count > 0)
        {
            await auditLogService.LogAsync(TenantId, UserId, "org-node.template-applied", nameof(OrgNode), factory.Id.ToString(),
                $"Added {created.Count} {template.Name.ToLowerInvariant()} section(s) to '{factory.Name}'.", cancellationToken);
        }

        return Ok(new ApplyTemplateResponse(created.Count, template.Sections.Count - created.Count));
    }

    private void EnsureCanManage() => userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

    private async Task EnsureCanWriteUnderAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        var scope = await orgScopeService.GetScopeAsync(TenantId, UserId, cancellationToken);
        if (!parentId.HasValue && !scope.IsUnrestricted)
        {
            throw new ForbiddenException("Only users with access to the whole organisation can add top-level groups or factories.");
        }

        if (parentId.HasValue && !scope.Allows(parentId))
        {
            throw new ForbiddenException("You do not have access to this part of the organisation.");
        }
    }

    private async Task<string> ValidateAsync(UpsertOrgNodeRequest request, OrgNode? existing, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new BusinessRuleException("Code is required.");
        }

        var level = OrgNodeLevels.Normalize(request.Level)
            ?? throw new BusinessRuleException($"Level must be one of: {string.Join(", ", OrgNodeLevels.All)}.");

        if (existing is not null && level != existing.Level
            && await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.ParentId == existing.Id, cancellationToken))
        {
            throw new BusinessRuleException("The level of a node with children cannot be changed.");
        }

        var settings = await factorySettingsService.GetOrCreateAsync(TenantId, cancellationToken);
        if (level == OrgNodeLevels.Group && !settings.UseGroupLevel && existing?.Level != OrgNodeLevels.Group)
        {
            throw new BusinessRuleException("The group level is switched off in factory settings.");
        }

        if (level == OrgNodeLevels.SubSection && !settings.UseSubSectionLevel && existing?.Level != OrgNodeLevels.SubSection)
        {
            throw new BusinessRuleException("The sub-section level is switched off in factory settings.");
        }

        OrgNode? parent = null;
        if (request.ParentId.HasValue)
        {
            if (existing is not null && request.ParentId == existing.Id)
            {
                throw new BusinessRuleException("A node cannot be its own parent.");
            }

            parent = await dbContext.OrgNodes.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.ParentId, cancellationToken)
                ?? throw new BusinessRuleException("Parent was not found.");
            if (!parent.IsActive)
            {
                throw new BusinessRuleException("The parent is inactive.");
            }
        }

        var allowedParents = level switch
        {
            OrgNodeLevels.Group => new[] { OrgNodeLevels.Group },
            OrgNodeLevels.Factory => [OrgNodeLevels.Group],
            OrgNodeLevels.Section => [OrgNodeLevels.Factory],
            _ => [OrgNodeLevels.Section]
        };
        var parentRequired = level is OrgNodeLevels.Section or OrgNodeLevels.SubSection;

        if (parent is null && parentRequired)
        {
            throw new BusinessRuleException($"A {Describe(level)} must sit under a {Describe(allowedParents[0])}.");
        }

        if (parent is not null && !allowedParents.Contains(parent.Level))
        {
            throw new BusinessRuleException($"A {Describe(level)} cannot sit under a {Describe(parent.Level)}.");
        }

        var code = request.Code.Trim().ToUpperInvariant();
        if (await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.Code == code && x.Id != (existing == null ? Guid.Empty : existing.Id), cancellationToken))
        {
            throw new ConflictException($"Code '{code}' is already used.");
        }

        var name = request.Name.Trim().ToLower();
        if (await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.ParentId == request.ParentId && x.Name.ToLower() == name && x.Id != (existing == null ? Guid.Empty : existing.Id), cancellationToken))
        {
            throw new ConflictException($"'{request.Name.Trim()}' already exists at this level.");
        }

        return level;
    }

    private async Task EnsureCanDeactivateAsync(OrgNode node, CancellationToken cancellationToken)
    {
        if (await dbContext.OrgNodes.AnyAsync(x => x.TenantId == TenantId && x.ParentId == node.Id && x.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("Deactivate or move the nodes beneath it first.");
        }

        var activeAssets = await dbContext.Assets.CountAsync(x => x.TenantId == TenantId && x.OrgNodeId == node.Id && x.Status == "Active", cancellationToken);
        if (activeAssets > 0)
        {
            throw new BusinessRuleException($"{activeAssets} active asset(s) are located here. Transfer or retire them first.");
        }
    }

    private async Task<OrgNode> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.OrgNodes.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Organisation node was not found.");

    private async Task<OrgNodeResponse> MapAsync(OrgNode node, CancellationToken cancellationToken)
    {
        var children = await dbContext.OrgNodes.CountAsync(x => x.TenantId == TenantId && x.ParentId == node.Id, cancellationToken);
        var assets = await dbContext.Assets.CountAsync(x => x.TenantId == TenantId && x.OrgNodeId == node.Id, cancellationToken);
        return Map(node, true, children, assets);
    }

    private static OrgNodeResponse Map(OrgNode node, bool canAccess, int childCount, int assetCount) => new(
        node.Id,
        node.ParentId,
        node.Level,
        node.Name,
        node.Code,
        node.Description,
        node.Location,
        node.DisplayOrder,
        node.IsActive,
        node.TemplateKey,
        node.ExternalId,
        node.ExternalSystem,
        canAccess,
        childCount,
        assetCount);

    private static HashSet<Guid> WithAncestors(IReadOnlyCollection<OrgNode> nodes, IReadOnlySet<Guid> scoped)
    {
        var parents = nodes.ToDictionary(x => x.Id, x => x.ParentId);
        var result = new HashSet<Guid>(scoped);
        foreach (var id in scoped)
        {
            var current = parents.GetValueOrDefault(id);
            while (current.HasValue && result.Add(current.Value))
            {
                current = parents.GetValueOrDefault(current.Value);
            }
        }

        return result;
    }

    private static string UniqueCode(string baseCode, HashSet<string> used)
    {
        var code = baseCode.ToUpperInvariant();
        if (code.Length > 50)
        {
            code = code[..50];
        }

        var candidate = code;
        for (var i = 2; used.Contains(candidate); i++)
        {
            candidate = $"{code[..Math.Min(code.Length, 46)]}-{i}";
        }

        used.Add(candidate);
        return candidate;
    }

    private static string Describe(string level) => level switch
    {
        OrgNodeLevels.SubSection => "sub-section",
        _ => level.ToLowerInvariant()
    };

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record UpsertOrgNodeRequest(
    Guid? ParentId,
    string Level,
    string Name,
    string Code,
    string? Description = null,
    string? Location = null,
    int? DisplayOrder = null,
    bool? IsActive = null,
    string? ExternalId = null,
    string? ExternalSystem = null);

public sealed record OrgNodeResponse(
    Guid Id,
    Guid? ParentId,
    string Level,
    string Name,
    string Code,
    string? Description,
    string? Location,
    int DisplayOrder,
    bool IsActive,
    string? TemplateKey,
    string? ExternalId,
    string? ExternalSystem,
    bool CanAccess,
    int ChildCount,
    int AssetCount);

public sealed record ApplyTemplateRequest(string? TemplateKey);

public sealed record ApplyTemplateResponse(int Created, int Skipped);

public sealed record SectionTemplateResponse(string Code, string Name, string Description);

public sealed record IndustryTemplateResponse(string Key, string Name, string ProductionUnit, IReadOnlyCollection<SectionTemplateResponse> Sections);
