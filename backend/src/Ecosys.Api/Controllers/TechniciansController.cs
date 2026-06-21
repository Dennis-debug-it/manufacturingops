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
[Route("api/technicians")]
public sealed class TechniciansController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService,
    IBranchAccessService branchAccessService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TechnicianResponse>>> GetAll([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, branchId, cancellationToken);

        var technicians = await dbContext.Technicians
            .Include(x => x.Branch)
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .WhereAccessible(scope, x => x.BranchId)
            .OrderBy(x => x.FullName)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);

        return Ok(technicians);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TechnicianResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var scope = await branchAccessService.GetQueryScopeAsync(TenantId, null, cancellationToken);

        var technician = await dbContext.Technicians
            .Include(x => x.Branch)
            .Where(x => x.TenantId == TenantId && x.IsActive && x.Id == id)
            .WhereAccessible(scope, x => x.BranchId)
            .Select(x => Map(x))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Technician was not found.");

        return Ok(technician);
    }

    [HttpPost]
    public async Task<ActionResult<TechnicianResponse>> Create([FromBody] UpsertTechnicianRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        Validate(request);

        var branchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId, cancellationToken);
        var technician = new Technician
        {
            TenantId = TenantId,
            BranchId = branchId,
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Phone = request.Phone?.Trim(),
            SkillCategory = request.SkillCategory?.Trim(),
            AssignmentGroup = request.AssignmentGroup?.Trim(),
            IsActive = true
        };

        dbContext.Technicians.Add(technician);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (technician.BranchId.HasValue)
        {
            await dbContext.Entry(technician).Reference(x => x.Branch).LoadAsync(cancellationToken);
        }

        return CreatedAtAction(nameof(Get), new { id = technician.Id }, Map(technician));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TechnicianResponse>> Update(Guid id, [FromBody] UpsertTechnicianRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        Validate(request);
        var technician = await dbContext.Technicians
            .Include(x => x.Branch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Technician was not found.");

        technician.BranchId = await branchAccessService.ResolveBranchIdForWriteAsync(TenantId, request.BranchId ?? technician.BranchId, cancellationToken);
        technician.FullName = request.FullName.Trim();
        technician.Email = request.Email.Trim().ToLowerInvariant();
        technician.Phone = request.Phone?.Trim();
        technician.SkillCategory = request.SkillCategory?.Trim();
        technician.AssignmentGroup = request.AssignmentGroup?.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(technician));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var technician = await dbContext.Technicians.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Technician was not found.");

        technician.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Technician deleted",
            nameof(Technician),
            technician.Id.ToString(),
            $"Soft deleted technician '{technician.FullName}'.",
            cancellationToken);

        return NoContent();
    }

    private static void Validate(UpsertTechnicianRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BusinessRuleException("Full name and email are required.");
        }
    }

    private static TechnicianResponse Map(Technician technician) =>
        new(
            technician.Id,
            technician.BranchId,
            technician.Branch?.Name,
            technician.FullName,
            technician.Email,
            technician.Phone,
            technician.SkillCategory,
            technician.AssignmentGroup,
            technician.LastKnownLatitude,
            technician.LastKnownLongitude,
            technician.LastLocationAt,
            technician.IsTrackingActive,
            technician.ActiveWorkOrderId,
            technician.IsActive,
            technician.CreatedAt);
}

public sealed record UpsertTechnicianRequest(
    Guid? BranchId,
    string FullName,
    string Email,
    string? Phone,
    string? SkillCategory,
    string? AssignmentGroup);

public sealed record TechnicianResponse(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    string FullName,
    string Email,
    string? Phone,
    string? SkillCategory,
    string? AssignmentGroup,
    decimal? LastKnownLatitude,
    decimal? LastKnownLongitude,
    DateTime? LastLocationAt,
    bool IsTrackingActive,
    Guid? ActiveWorkOrderId,
    bool IsActive,
    DateTime CreatedAt);
