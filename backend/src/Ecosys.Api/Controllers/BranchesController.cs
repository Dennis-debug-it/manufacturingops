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
[Route("api/branches")]
public sealed class BranchesController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    ILicenseGuardService licenseGuardService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<BranchResponse>>> GetAll(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var branches = await dbContext.Branches
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(branches.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BranchResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var branch = await dbContext.Branches.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Branch was not found.");

        return Ok(Map(branch));
    }

    [HttpPost]
    public async Task<ActionResult<BranchResponse>> Create([FromBody] UpsertBranchRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        Validate(request);
        await licenseGuardService.EnsureCanCreateBranchAsync(TenantId, cancellationToken);
        await EnsureUniqueAsync(request.Name, request.Code, null, cancellationToken);
        await EnsureParentBranchAsync(request.ParentBranchId, cancellationToken);

        var branch = new Branch
        {
            TenantId = TenantId,
            ParentBranchId = request.ParentBranchId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim().ToUpperInvariant(),
            Location = Normalize(request.Location),
            Address = Normalize(request.Address),
            ContactPerson = Normalize(request.ContactPerson),
            Phone = Normalize(request.Phone),
            Email = Normalize(request.Email),
            IsActive = request.IsActive
        };

        dbContext.Branches.Add(branch);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = branch.Id }, Map(branch));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BranchResponse>> Update(Guid id, [FromBody] UpsertBranchRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        Validate(request);

        var branch = await dbContext.Branches.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Branch was not found.");

        await EnsureUniqueAsync(request.Name, request.Code, branch.Id, cancellationToken);
        await EnsureParentBranchAsync(request.ParentBranchId, cancellationToken);

        branch.ParentBranchId = request.ParentBranchId == id ? throw new BusinessRuleException("A branch cannot be its own parent.") : request.ParentBranchId;
        branch.Name = request.Name.Trim();
        branch.Code = request.Code.Trim().ToUpperInvariant();
        branch.Location = Normalize(request.Location);
        branch.Address = Normalize(request.Address);
        branch.ContactPerson = Normalize(request.ContactPerson);
        branch.Phone = Normalize(request.Phone);
        branch.Email = Normalize(request.Email);
        branch.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(branch));
    }

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        DeactivateInternalAsync(id, cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        DeactivateInternalAsync(id, cancellationToken);

    private async Task<IActionResult> DeactivateInternalAsync(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var branch = await dbContext.Branches.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Branch was not found.");

        branch.IsActive = false;

        var affectedUsers = await dbContext.Users
            .Where(x => x.TenantId == TenantId && x.DefaultBranchId == id)
            .ToListAsync(cancellationToken);

        foreach (var user in affectedUsers)
        {
            user.DefaultBranchId = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void Validate(UpsertBranchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
        {
            throw new BusinessRuleException("Branch name and code are required.");
        }
    }

    private async Task EnsureParentBranchAsync(Guid? parentBranchId, CancellationToken cancellationToken)
    {
        if (!parentBranchId.HasValue)
        {
            return;
        }

        var exists = await dbContext.Branches.AnyAsync(x => x.TenantId == TenantId && x.IsActive && x.Id == parentBranchId.Value, cancellationToken);
        if (!exists)
        {
            throw new BusinessRuleException("Parent branch was not found for this tenant.");
        }
    }

    private async Task EnsureUniqueAsync(string name, string code, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        var normalizedCode = code.Trim().ToLowerInvariant();

        var nameExists = await dbContext.Branches.AnyAsync(
            x => x.TenantId == TenantId && x.Id != currentId && x.Name.ToLower() == normalizedName,
            cancellationToken);
        if (nameExists)
        {
            throw new BusinessRuleException("Branch name must be unique per tenant.");
        }

        var codeExists = await dbContext.Branches.AnyAsync(
            x => x.TenantId == TenantId && x.Id != currentId && x.Code.ToLower() == normalizedCode,
            cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("Branch code must be unique per tenant.");
        }
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BranchResponse Map(Branch branch) =>
        new(
            branch.Id,
            branch.TenantId,
            branch.ParentBranchId,
            branch.Name,
            branch.Code,
            branch.Location,
            branch.Address,
            branch.ContactPerson,
            branch.Phone,
            branch.Email,
            branch.IsActive,
            branch.CreatedAt,
            branch.UpdatedAt);
}

public sealed record UpsertBranchRequest(
    Guid? ParentBranchId,
    string Name,
    string Code,
    string? Location,
    string? Address,
    string? ContactPerson,
    string? Phone,
    string? Email,
    bool IsActive = true);

public sealed record BranchResponse(
    Guid Id,
    Guid TenantId,
    Guid? ParentBranchId,
    string Name,
    string Code,
    string? Location,
    string? Address,
    string? ContactPerson,
    string? Phone,
    string? Email,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
