using System.Text.Json;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public interface IWorkflowService
{
    Task<WorkflowDefinition?> GetActiveWorkflowAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<WorkflowStatus>> GetActiveWorkflowStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureDefaultWorkflowAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<WorkflowStatus> ApplyStatusTransitionAsync(WorkOrder workOrder, Guid targetWorkflowStatusId, CancellationToken cancellationToken = default);
}

public sealed class WorkflowService(AppDbContext dbContext) : IWorkflowService
{
    public static readonly (string Name, string Color)[] DefaultChain =
    [
        ("New", "#64748b"),
        ("Assigned", "#2563eb"),
        ("In Progress", "#f59e0b"),
        ("Completed", "#16a34a"),
        ("Closed", "#334155"),
    ];

    public Task<WorkflowDefinition?> GetActiveWorkflowAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        dbContext.WorkflowDefinitions
            .Include(x => x.Statuses)
            .Where(x => x.TenantId == tenantId && x.IsActive && x.IsDefault)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<WorkflowStatus>> GetActiveWorkflowStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var workflow = await GetActiveWorkflowAsync(tenantId, cancellationToken);
        return workflow?.Statuses.OrderBy(x => x.SortOrder).ToList() ?? [];
    }

    public async Task EnsureDefaultWorkflowAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var hasAny = await dbContext.WorkflowDefinitions.AnyAsync(x => x.TenantId == tenantId, cancellationToken);
        if (hasAny)
        {
            return;
        }

        var workflow = new WorkflowDefinition
        {
            TenantId = tenantId,
            Name = "Default Workflow",
            IsDefault = true,
            IsActive = true,
        };

        var statuses = new List<WorkflowStatus>();
        for (var i = 0; i < DefaultChain.Length; i++)
        {
            var (name, color) = DefaultChain[i];
            statuses.Add(new WorkflowStatus
            {
                TenantId = tenantId,
                WorkflowDefinition = workflow,
                Name = name,
                Color = color,
                SortOrder = i,
                IsInitial = i == 0,
                IsFinal = i == DefaultChain.Length - 1,
            });
        }

        for (var i = 0; i < statuses.Count - 1; i++)
        {
            statuses[i].AllowedNextStatusIdsJson = JsonSerializer.Serialize(new[] { statuses[i + 1].Id });
        }

        workflow.Statuses = statuses;
        dbContext.WorkflowDefinitions.Add(workflow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowStatus> ApplyStatusTransitionAsync(WorkOrder workOrder, Guid targetWorkflowStatusId, CancellationToken cancellationToken = default)
    {
        var workflow = await GetActiveWorkflowAsync(workOrder.TenantId, cancellationToken)
            ?? throw new BusinessRuleException("No active status workflow is configured for this tenant.");

        var targetStatus = workflow.Statuses.SingleOrDefault(x => x.Id == targetWorkflowStatusId)
            ?? throw new BusinessRuleException("Target status does not belong to the active workflow.");

        if (workOrder.WorkflowStatusId.HasValue)
        {
            var currentStatus = workflow.Statuses.SingleOrDefault(x => x.Id == workOrder.WorkflowStatusId.Value);
            if (currentStatus is not null)
            {
                if (currentStatus.IsFinal)
                {
                    throw new BusinessRuleException("This work order's status is final and cannot be changed.");
                }

                var allowedIds = ParseAllowedNextStatusIds(currentStatus.AllowedNextStatusIdsJson);
                if (!allowedIds.Contains(targetWorkflowStatusId))
                {
                    throw new BusinessRuleException($"Transition from '{currentStatus.Name}' to '{targetStatus.Name}' is not allowed by the active workflow.");
                }
            }
        }

        workOrder.WorkflowStatusId = targetStatus.Id;
        workOrder.Status = targetStatus.Name;
        return targetStatus;
    }

    public static IReadOnlyCollection<Guid> ParseAllowedNextStatusIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
