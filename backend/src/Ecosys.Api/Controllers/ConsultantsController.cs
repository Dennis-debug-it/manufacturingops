using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>The tenant's consultant directory: firms and contact details shared by all projects.</summary>
[ApiController]
[Authorize]
[Route("api/consultants")]
public sealed class ConsultantsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IProjectAccessService projectAccessService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ConsultantResponse>>> List(
        [FromQuery] string? search, [FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsView);
        var query = dbContext.Consultants.Where(x => x.TenantId == TenantId);
        if (!includeInactive)
            query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.FirmName.ToLower().Contains(term)
                || (x.ContactPerson != null && x.ContactPerson.ToLower().Contains(term))
                || (x.Discipline != null && x.Discipline.ToLower().Contains(term)));
        }

        var consultants = await query.OrderBy(x => x.FirmName).Take(500).ToListAsync(cancellationToken);
        var projectCounts = await dbContext.ProjectConsultants
            .Where(x => x.TenantId == TenantId)
            .GroupBy(x => x.ConsultantId)
            .Select(g => new { ConsultantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConsultantId, x => x.Count, cancellationToken);
        return Ok(consultants.Select(x => Map(x, projectCounts.GetValueOrDefault(x.Id))).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConsultantResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsView);
        var consultant = await LoadAsync(id, cancellationToken);
        var count = await dbContext.ProjectConsultants.CountAsync(x => x.TenantId == TenantId && x.ConsultantId == id, cancellationToken);
        return Ok(Map(consultant, count));
    }

    [HttpPost]
    public async Task<ActionResult<ConsultantResponse>> Create([FromBody] UpsertConsultantRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var firmName = await ValidateAsync(request, null, cancellationToken);
        var consultant = new Consultant { TenantId = TenantId, FirmName = firmName };
        Apply(consultant, request);
        dbContext.Consultants.Add(consultant);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "projects.consultant.created", nameof(Consultant), consultant.Id.ToString(),
            $"Consultant '{consultant.FirmName}' added to the directory.", cancellationToken);
        return Ok(Map(consultant, 0));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ConsultantResponse>> Update(Guid id, [FromBody] UpsertConsultantRequest request, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var consultant = await LoadAsync(id, cancellationToken);
        consultant.FirmName = await ValidateAsync(request, id, cancellationToken);
        Apply(consultant, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        var count = await dbContext.ProjectConsultants.CountAsync(x => x.TenantId == TenantId && x.ConsultantId == id, cancellationToken);
        return Ok(Map(consultant, count));
    }

    /// <summary>
    /// Consultants are deactivated, not deleted, once a project uses them, so past projects keep
    /// their consultant. One that was never used is removed outright.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        projectAccessService.Ensure(PermissionNames.ProjectsManage);
        var consultant = await LoadAsync(id, cancellationToken);
        var used = await dbContext.ProjectConsultants.AnyAsync(x => x.TenantId == TenantId && x.ConsultantId == id, cancellationToken);
        if (used)
            consultant.IsActive = false;
        else
            dbContext.Consultants.Remove(consultant);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, used ? "projects.consultant.deactivated" : "projects.consultant.deleted",
            nameof(Consultant), consultant.Id.ToString(), $"Consultant '{consultant.FirmName}' {(used ? "deactivated" : "deleted")}.", cancellationToken);
        return NoContent();
    }

    private async Task<Consultant> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Consultants.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Consultant not found.");

    private async Task<string> ValidateAsync(UpsertConsultantRequest request, Guid? existingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FirmName))
            throw new BusinessRuleException("Firm name is required.");
        var firmName = request.FirmName.Trim();
        var lowered = firmName.ToLower();
        if (await dbContext.Consultants.AnyAsync(x => x.TenantId == TenantId && x.Id != existingId && x.FirmName.ToLower() == lowered, cancellationToken))
            throw new BusinessRuleException($"A consultant named '{firmName}' is already in the directory.");
        return firmName;
    }

    private static void Apply(Consultant consultant, UpsertConsultantRequest request)
    {
        consultant.Discipline = Clean(request.Discipline);
        consultant.ContactPerson = Clean(request.ContactPerson);
        consultant.Phone = Clean(request.Phone);
        consultant.Email = Clean(request.Email);
        consultant.PhysicalAddress = Clean(request.PhysicalAddress);
        consultant.Notes = Clean(request.Notes);
        consultant.IsActive = request.IsActive ?? consultant.IsActive;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ConsultantResponse Map(Consultant x, int projectCount) => new(
        x.Id, x.FirmName, x.Discipline, x.ContactPerson, x.Phone, x.Email, x.PhysicalAddress, x.Notes, x.IsActive, projectCount);
}

public sealed record ConsultantResponse(
    Guid Id, string FirmName, string? Discipline, string? ContactPerson, string? Phone, string? Email,
    string? PhysicalAddress, string? Notes, bool IsActive, int ProjectCount);

public sealed record UpsertConsultantRequest(
    string FirmName, string? Discipline, string? ContactPerson, string? Phone, string? Email,
    string? PhysicalAddress, string? Notes, bool? IsActive = null);
