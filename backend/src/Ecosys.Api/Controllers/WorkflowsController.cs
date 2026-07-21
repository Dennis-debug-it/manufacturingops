using System.Text.Json;
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
[Route("api/settings/workflows")]
public sealed class WorkflowsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IWorkflowService workflowService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    private static readonly System.Text.RegularExpressions.Regex HexColorPattern =
        new(@"^#[0-9a-fA-F]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<WorkflowDefinitionResponse>>> GetAll(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        await workflowService.EnsureDefaultWorkflowAsync(TenantId, cancellationToken);

        var workflows = await dbContext.WorkflowDefinitions
            .Include(x => x.Statuses)
            .Where(x => x.TenantId == TenantId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(workflows.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkflowDefinitionResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var workflow = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("Workflow was not found.");

        return Ok(Map(workflow));
    }

    [HttpGet("{id:guid}/statuses")]
    public async Task<ActionResult<IReadOnlyCollection<WorkflowStatusResponse>>> GetStatuses(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var workflow = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("Workflow was not found.");

        return Ok(workflow.Statuses.OrderBy(x => x.SortOrder).Select(MapStatus).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<WorkflowDefinitionResponse>> Create([FromBody] UpsertWorkflowDefinitionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        ValidateDefinition(request);
        await EnsureUniqueNameAsync(request.Name, null, cancellationToken);

        var workflow = new WorkflowDefinition
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
        };

        if (request.IsDefault)
        {
            await ClearOtherDefaultsAsync(null, cancellationToken);
        }

        dbContext.WorkflowDefinitions.Add(workflow);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "Workflow created", nameof(WorkflowDefinition), workflow.Id.ToString(), $"Created workflow '{workflow.Name}'.", cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = workflow.Id }, Map(workflow));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkflowDefinitionResponse>> Update(Guid id, [FromBody] UpsertWorkflowDefinitionRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);
        ValidateDefinition(request);

        var workflow = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("Workflow was not found.");

        await EnsureUniqueNameAsync(request.Name, id, cancellationToken);

        if (request.IsDefault && !workflow.IsDefault)
        {
            await ClearOtherDefaultsAsync(id, cancellationToken);
        }

        if (!request.IsDefault && workflow.IsDefault)
        {
            var otherDefaultExists = await dbContext.WorkflowDefinitions
                .AnyAsync(x => x.TenantId == TenantId && x.Id != id && x.IsActive, cancellationToken);
            if (!otherDefaultExists)
            {
                throw new BusinessRuleException("At least one workflow must remain the default.");
            }
        }

        workflow.Name = request.Name.Trim();
        workflow.IsDefault = request.IsDefault;
        workflow.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "Workflow updated", nameof(WorkflowDefinition), workflow.Id.ToString(), $"Updated workflow '{workflow.Name}'.", cancellationToken);

        return Ok(Map(workflow));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var workflow = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("Workflow was not found.");

        if (workflow.IsDefault)
        {
            throw new BusinessRuleException("The default workflow cannot be deleted. Set another workflow as default first.");
        }

        var statusIds = workflow.Statuses.Select(x => x.Id).ToList();
        var isInUse = await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId && x.WorkflowStatusId != null && statusIds.Contains(x.WorkflowStatusId!.Value), cancellationToken);
        if (isInUse)
        {
            throw new BusinessRuleException("This workflow is in use by one or more work orders.");
        }

        dbContext.WorkflowDefinitions.Remove(workflow);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "Workflow deleted", nameof(WorkflowDefinition), workflow.Id.ToString(), $"Deleted workflow '{workflow.Name}'.", cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/statuses")]
    public async Task<ActionResult<WorkflowStatusResponse>> CreateStatus(Guid id, [FromBody] UpsertWorkflowStatusRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var workflow = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("Workflow was not found.");

        ValidateStatus(request);
        EnsureUniqueStatusName(workflow, request.Name, null);

        var status = new WorkflowStatus
        {
            TenantId = TenantId,
            WorkflowDefinitionId = workflow.Id,
            Name = request.Name.Trim(),
            Color = request.Color,
            SortOrder = request.SortOrder ?? (workflow.Statuses.Count == 0 ? 0 : workflow.Statuses.Max(x => x.SortOrder) + 1),
            IsInitial = request.IsInitial,
            IsFinal = request.IsFinal,
        };

        if (request.IsInitial)
        {
            foreach (var existing in workflow.Statuses.Where(x => x.IsInitial))
            {
                existing.IsInitial = false;
            }
        }

        dbContext.WorkflowStatuses.Add(status);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.AllowedNextStatusIds is not null)
        {
            ValidateAllowedNextIds(workflow, request.AllowedNextStatusIds, status.Id);
            status.AllowedNextStatusIdsJson = JsonSerializer.Serialize(request.AllowedNextStatusIds);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditLogService.LogAsync(TenantId, UserId, "Workflow status created", nameof(WorkflowStatus), status.Id.ToString(), $"Added status '{status.Name}' to workflow '{workflow.Name}'.", cancellationToken);

        return CreatedAtAction(nameof(GetStatuses), new { id = workflow.Id }, MapStatus(status));
    }

    [HttpPut("{id:guid}/statuses/{statusId:guid}")]
    public async Task<ActionResult<WorkflowStatusResponse>> UpdateStatus(Guid id, Guid statusId, [FromBody] UpsertWorkflowStatusRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var workflow = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("Workflow was not found.");

        var status = workflow.Statuses.SingleOrDefault(x => x.Id == statusId)
            ?? throw new NotFoundException("Workflow status was not found.");

        ValidateStatus(request);
        EnsureUniqueStatusName(workflow, request.Name, statusId);

        if (request.IsInitial)
        {
            foreach (var existing in workflow.Statuses.Where(x => x.IsInitial && x.Id != statusId))
            {
                existing.IsInitial = false;
            }
        }

        status.Name = request.Name.Trim();
        status.Color = request.Color;
        status.IsInitial = request.IsInitial;
        status.IsFinal = request.IsFinal;
        if (request.SortOrder.HasValue)
        {
            status.SortOrder = request.SortOrder.Value;
        }

        if (request.AllowedNextStatusIds is not null)
        {
            ValidateAllowedNextIds(workflow, request.AllowedNextStatusIds, statusId);
            status.AllowedNextStatusIdsJson = JsonSerializer.Serialize(request.AllowedNextStatusIds);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "Workflow status updated", nameof(WorkflowStatus), status.Id.ToString(), $"Updated status '{status.Name}' in workflow '{workflow.Name}'.", cancellationToken);

        return Ok(MapStatus(status));
    }

    [HttpDelete("{id:guid}/statuses/{statusId:guid}")]
    public async Task<IActionResult> DeleteStatus(Guid id, Guid statusId, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageSettings);

        var workflow = await LoadAsync(id, cancellationToken)
            ?? throw new NotFoundException("Workflow was not found.");

        var status = workflow.Statuses.SingleOrDefault(x => x.Id == statusId)
            ?? throw new NotFoundException("Workflow status was not found.");

        var isInUse = await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId && x.WorkflowStatusId == statusId, cancellationToken);
        if (isInUse)
        {
            throw new BusinessRuleException("This status is currently assigned to one or more work orders.");
        }

        foreach (var sibling in workflow.Statuses.Where(x => x.Id != statusId))
        {
            var allowed = WorkflowService.ParseAllowedNextStatusIds(sibling.AllowedNextStatusIdsJson);
            if (allowed.Contains(statusId))
            {
                sibling.AllowedNextStatusIdsJson = JsonSerializer.Serialize(allowed.Where(x => x != statusId).ToList());
            }
        }

        dbContext.WorkflowStatuses.Remove(status);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(TenantId, UserId, "Workflow status deleted", nameof(WorkflowStatus), status.Id.ToString(), $"Removed status '{status.Name}' from workflow '{workflow.Name}'.", cancellationToken);

        return NoContent();
    }

    private Task<WorkflowDefinition?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.WorkflowDefinitions
            .Include(x => x.Statuses)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);

    private async Task EnsureUniqueNameAsync(string name, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var exists = await dbContext.WorkflowDefinitions.AnyAsync(
            x => x.TenantId == TenantId && x.Id != currentId && x.Name.ToLower() == normalized,
            cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException("A workflow with this name already exists.");
        }
    }

    private async Task ClearOtherDefaultsAsync(Guid? currentId, CancellationToken cancellationToken)
    {
        var others = await dbContext.WorkflowDefinitions
            .Where(x => x.TenantId == TenantId && x.Id != currentId && x.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
        {
            other.IsDefault = false;
        }
    }

    private static void EnsureUniqueStatusName(WorkflowDefinition workflow, string name, Guid? currentId)
    {
        var normalized = name.Trim();
        var duplicate = workflow.Statuses.Any(x => x.Id != currentId && string.Equals(x.Name, normalized, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            throw new BusinessRuleException("A status with this name already exists in this workflow.");
        }
    }

    private static void ValidateAllowedNextIds(WorkflowDefinition workflow, IReadOnlyCollection<Guid> allowedNextStatusIds, Guid currentStatusId)
    {
        var validIds = workflow.Statuses.Select(x => x.Id).ToHashSet();
        foreach (var targetId in allowedNextStatusIds)
        {
            if (targetId == currentStatusId)
            {
                throw new BusinessRuleException("A status cannot transition to itself.");
            }

            if (!validIds.Contains(targetId))
            {
                throw new BusinessRuleException("Allowed transitions must reference statuses in the same workflow.");
            }
        }
    }

    private static void ValidateDefinition(UpsertWorkflowDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Workflow name is required.");
        }
    }

    private static void ValidateStatus(UpsertWorkflowStatusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleException("Status name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Color) || !HexColorPattern.IsMatch(request.Color))
        {
            throw new BusinessRuleException("Status color must be a hex value like #2563eb.");
        }
    }

    private static WorkflowDefinitionResponse Map(WorkflowDefinition workflow) =>
        new(
            workflow.Id,
            workflow.Name,
            workflow.IsDefault,
            workflow.IsActive,
            workflow.Statuses.OrderBy(x => x.SortOrder).Select(MapStatus).ToList(),
            workflow.CreatedAt,
            workflow.UpdatedAt);

    private static WorkflowStatusResponse MapStatus(WorkflowStatus status) =>
        new(
            status.Id,
            status.WorkflowDefinitionId,
            status.Name,
            status.Color,
            status.SortOrder,
            status.IsInitial,
            status.IsFinal,
            WorkflowService.ParseAllowedNextStatusIds(status.AllowedNextStatusIdsJson).ToList());
}

public sealed record UpsertWorkflowDefinitionRequest(string Name, bool IsDefault, bool IsActive);

public sealed record UpsertWorkflowStatusRequest(
    string Name,
    string Color,
    int? SortOrder,
    bool IsInitial,
    bool IsFinal,
    IReadOnlyCollection<Guid>? AllowedNextStatusIds);

public sealed record WorkflowDefinitionResponse(
    Guid Id,
    string Name,
    bool IsDefault,
    bool IsActive,
    IReadOnlyCollection<WorkflowStatusResponse> Statuses,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record WorkflowStatusResponse(
    Guid Id,
    Guid WorkflowDefinitionId,
    string Name,
    string Color,
    int SortOrder,
    bool IsInitial,
    bool IsFinal,
    IReadOnlyCollection<Guid> AllowedNextStatusIds);
