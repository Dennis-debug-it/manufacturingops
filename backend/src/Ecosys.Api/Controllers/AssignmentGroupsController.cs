using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/assignment-groups")]
public sealed class AssignmentGroupsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AssignmentGroupDto>>> GetAll(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var groups = await QueryGroups().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return Ok(groups.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssignmentGroupDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var group = await QueryGroups().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Assignment group was not found.");
        return Ok(Map(group));
    }

    [HttpPost]
    public async Task<ActionResult<AssignmentGroupDto>> Create([FromBody] UpsertAssignmentGroupDto request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var branchId = request.BranchId.HasValue
            ? await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId, cancellationToken)
            : null;

        var group = new AssignmentGroup
        {
            TenantId = TenantId,
            BranchId = branchId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            SkillArea = request.SkillArea?.Trim(),
            IsActive = request.IsActive
        };

        dbContext.AssignmentGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncMembersAsync(group.Id, request.Members, cancellationToken);

        var persisted = await QueryGroups().SingleAsync(x => x.Id == group.Id, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = group.Id }, Map(persisted));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AssignmentGroupDto>> Update(Guid id, [FromBody] UpsertAssignmentGroupDto request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var group = await dbContext.AssignmentGroups.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Assignment group was not found.");

        group.BranchId = request.BranchId.HasValue
            ? await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId, cancellationToken)
            : null;
        group.Name = request.Name.Trim();
        group.Description = request.Description?.Trim();
        group.SkillArea = request.SkillArea?.Trim();
        group.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncMembersAsync(group.Id, request.Members, cancellationToken);

        var persisted = await QueryGroups().SingleAsync(x => x.Id == group.Id, cancellationToken);
        return Ok(Map(persisted));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var group = await dbContext.AssignmentGroups.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Assignment group was not found.");

        group.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IReadOnlyCollection<AssignmentGroupMemberDto>>> GetMembers(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var exists = await dbContext.AssignmentGroups.AnyAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Assignment group was not found.");
        }

        var members = await dbContext.AssignmentGroupMembers
            .Include(x => x.Technician)
            .ThenInclude(x => x!.User)
            .Where(x => x.TenantId == TenantId && x.AssignmentGroupId == id && x.IsActive)
            .OrderByDescending(x => x.IsLead)
            .ThenBy(x => x.Technician!.User != null ? x.Technician.User.FullName : x.Technician!.FullName)
            .ToListAsync(cancellationToken);

        return Ok(members.Select(MapMember).ToList());
    }

    [HttpPost("{id:guid}/members")]
    public async Task<ActionResult<AssignmentGroupMemberDto>> AddMember(Guid id, [FromBody] AddAssignmentGroupMemberDto request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        await EnsureGroupExistsAsync(id, cancellationToken);

        var technician = await ResolveTechnicianAsync(request.UserId, request.TechnicianId, cancellationToken);
        var membership = await dbContext.AssignmentGroupMembers
            .Include(x => x.Technician)
            .ThenInclude(x => x!.User)
            .SingleOrDefaultAsync(
                x => x.TenantId == TenantId && x.AssignmentGroupId == id && x.TechnicianId == technician.Id,
                cancellationToken);

        if (request.IsLead)
        {
            var existingLeads = await dbContext.AssignmentGroupMembers
                .Where(x => x.TenantId == TenantId && x.AssignmentGroupId == id && x.TechnicianId != technician.Id)
                .ToListAsync(cancellationToken);

            foreach (var existingLead in existingLeads)
            {
                existingLead.IsLead = false;
            }
        }

        membership ??= new AssignmentGroupMember
        {
            TenantId = TenantId,
            AssignmentGroupId = id,
            TechnicianId = technician.Id,
            AddedAt = DateTime.UtcNow
        };

        membership.IsLead = request.IsLead;
        membership.IsActive = true;

        if (dbContext.Entry(membership).State == EntityState.Detached)
        {
            dbContext.AssignmentGroupMembers.Add(membership);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var persisted = await dbContext.AssignmentGroupMembers
            .Include(x => x.Technician)
            .ThenInclude(x => x!.User)
            .SingleAsync(x => x.TenantId == TenantId && x.AssignmentGroupId == id && x.TechnicianId == technician.Id, cancellationToken);

        return Ok(MapMember(persisted));
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        await EnsureGroupExistsAsync(id, cancellationToken);

        var technicianId = await ResolveTechnicianIdForDeleteAsync(userId, cancellationToken);
        var memberships = await dbContext.AssignmentGroupMembers
            .Where(x => x.TenantId == TenantId && x.AssignmentGroupId == id && x.TechnicianId == technicianId)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return NoContent();
        }

        dbContext.AssignmentGroupMembers.RemoveRange(memberships);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<AssignmentGroup> QueryGroups() =>
        dbContext.AssignmentGroups
            .Include(x => x.Branch)
            .Include(x => x.Members.Where(member => member.IsActive))
            .ThenInclude(x => x.Technician)
            .ThenInclude(x => x!.User)
            .Where(x => x.TenantId == TenantId);

    private async Task SyncMembersAsync(Guid assignmentGroupId, IReadOnlyCollection<UpsertAssignmentGroupMemberDto>? members, CancellationToken cancellationToken)
    {
        var resolvedMembers = await ResolveMembersAsync(members, cancellationToken);
        var currentMembers = await dbContext.AssignmentGroupMembers
            .Where(x => x.TenantId == TenantId && x.AssignmentGroupId == assignmentGroupId)
            .ToListAsync(cancellationToken);

        dbContext.AssignmentGroupMembers.RemoveRange(currentMembers);
        dbContext.AssignmentGroupMembers.AddRange(resolvedMembers.Select(member => new AssignmentGroupMember
        {
            TenantId = TenantId,
            AssignmentGroupId = assignmentGroupId,
            TechnicianId = member.TechnicianId,
            IsLead = member.IsLead,
            IsActive = true,
            AddedAt = DateTime.UtcNow
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<ResolvedAssignmentGroupMember>> ResolveMembersAsync(
        IReadOnlyCollection<UpsertAssignmentGroupMemberDto>? members,
        CancellationToken cancellationToken)
    {
        var requestedMembers = members?.Where(x => x.UserId.HasValue || x.TechnicianId.HasValue).ToList() ?? [];
        var resolved = new List<ResolvedAssignmentGroupMember>(requestedMembers.Count);

        foreach (var member in requestedMembers)
        {
            var technician = await ResolveTechnicianAsync(member.UserId, member.TechnicianId, cancellationToken);
            if (resolved.Any(x => x.TechnicianId == technician.Id))
            {
                continue;
            }

            resolved.Add(new ResolvedAssignmentGroupMember(technician.Id, member.IsLead));
        }

        if (resolved.Count(x => x.IsLead) > 1)
        {
            throw new BusinessRuleException("Only one team lead can be assigned to a group.");
        }

        return resolved;
    }

    private async Task<Technician> ResolveTechnicianAsync(Guid? userId, Guid? technicianId, CancellationToken cancellationToken)
    {
        if (technicianId.HasValue && technicianId.Value != Guid.Empty)
        {
            return await dbContext.Technicians
                .Include(x => x.User)
                .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == technicianId.Value && x.IsActive, cancellationToken)
                ?? throw new BusinessRuleException("Cannot add inactive workforce member to assignment group.");
        }

        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            throw new BusinessRuleException("A user or workforce profile is required.");
        }

        var user = await dbContext.Users
            .Include(x => x.BranchAssignments)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == userId.Value, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var technician = await dbContext.Technicians
            .Include(x => x.User)
            .SingleOrDefaultAsync(
                x => x.TenantId == TenantId && (x.UserId == user.Id || x.Email.ToLower() == user.Email.ToLower()),
                cancellationToken);

        technician ??= new Technician
        {
            TenantId = TenantId,
            UserId = user.Id
        };

        technician.UserId = user.Id;
        technician.FullName = user.FullName;
        technician.Email = user.Email;
        technician.BranchId = user.DefaultBranchId ?? user.BranchAssignments.OrderByDescending(x => x.IsDefault).Select(x => (Guid?)x.BranchId).FirstOrDefault();
        technician.IsActive = user.IsActive;

        if (!technician.IsActive)
        {
            throw new BusinessRuleException("Cannot add inactive workforce member to assignment group.");
        }

        if (dbContext.Entry(technician).State == EntityState.Detached)
        {
            dbContext.Technicians.Add(technician);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return technician;
    }

    private async Task<Guid> ResolveTechnicianIdForDeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var technician = await dbContext.Technicians
            .Where(x => x.TenantId == TenantId && (x.UserId == userId || x.Id == userId))
            .Select(x => new { x.Id })
            .SingleOrDefaultAsync(cancellationToken);

        if (technician is null)
        {
            throw new NotFoundException("Assignment group member was not found.");
        }

        return technician.Id;
    }

    private async Task EnsureGroupExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        var exists = await dbContext.AssignmentGroups.AnyAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Assignment group was not found.");
        }
    }

    private static AssignmentGroupDto Map(AssignmentGroup group) =>
        new(
            group.Id,
            group.Name,
            group.Description,
            group.SkillArea,
            group.BranchId,
            group.Branch?.Name,
            group.IsActive,
            group.CreatedAt,
            group.UpdatedAt,
            group.Members
                .Where(member => member.IsActive)
                .OrderByDescending(member => member.IsLead)
                .ThenBy(member => member.Technician?.User?.FullName ?? member.Technician!.FullName)
                .Select(MapMember)
                .ToList());

    private static AssignmentGroupMemberDto MapMember(AssignmentGroupMember member) =>
        new(
            member.Id,
            member.AssignmentGroupId,
            member.Technician?.UserId,
            member.TechnicianId,
            member.Technician?.User?.FullName ?? member.Technician?.FullName,
            member.Technician?.User?.Email ?? member.Technician?.Email,
            member.IsLead,
            member.IsActive,
            member.AddedAt);

    private sealed record ResolvedAssignmentGroupMember(Guid TechnicianId, bool IsLead);
}

public sealed record UpsertAssignmentGroupDto(
    string Name,
    string? Description,
    string? SkillArea,
    Guid? BranchId,
    bool IsActive,
    IReadOnlyCollection<UpsertAssignmentGroupMemberDto>? Members);

public sealed record UpsertAssignmentGroupMemberDto(Guid? UserId, Guid? TechnicianId, bool IsLead);

public sealed record AddAssignmentGroupMemberDto(Guid? UserId, Guid? TechnicianId, bool IsLead);

public sealed record AssignmentGroupDto(
    Guid Id,
    string Name,
    string? Description,
    string? SkillArea,
    Guid? BranchId,
    string? BranchName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyCollection<AssignmentGroupMemberDto> Members);

public sealed record AssignmentGroupMemberDto(
    Guid Id,
    Guid AssignmentGroupId,
    Guid? UserId,
    Guid TechnicianId,
    string? MemberName,
    string? Email,
    bool IsLead,
    bool IsActive,
    DateTime AddedAt);
