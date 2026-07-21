using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

public sealed class WorkflowAndEscalationTests
{
    [Fact]
    public async Task EnsureDefaultWorkflowAsync_SeedsCanonicalFiveStageChain()
    {
        await using var dbContext = CreateDbContext();
        var tenant = await SeedTenantAsync(dbContext);
        var service = new WorkflowService(dbContext);

        await service.EnsureDefaultWorkflowAsync(tenant.Id, CancellationToken.None);

        var workflow = await dbContext.WorkflowDefinitions
            .Include(x => x.Statuses)
            .SingleAsync(x => x.TenantId == tenant.Id);

        Assert.True(workflow.IsDefault);
        Assert.True(workflow.IsActive);
        Assert.Equal(5, workflow.Statuses.Count);

        var ordered = workflow.Statuses.OrderBy(x => x.SortOrder).ToList();
        Assert.Equal(["New", "Assigned", "In Progress", "Completed", "Closed"], ordered.Select(x => x.Name));
        Assert.True(ordered[0].IsInitial);
        Assert.True(ordered[^1].IsFinal);
    }

    [Fact]
    public async Task EnsureDefaultWorkflowAsync_IsIdempotent()
    {
        await using var dbContext = CreateDbContext();
        var tenant = await SeedTenantAsync(dbContext);
        var service = new WorkflowService(dbContext);

        await service.EnsureDefaultWorkflowAsync(tenant.Id, CancellationToken.None);
        await service.EnsureDefaultWorkflowAsync(tenant.Id, CancellationToken.None);

        var workflowCount = await dbContext.WorkflowDefinitions.CountAsync(x => x.TenantId == tenant.Id);
        Assert.Equal(1, workflowCount);
    }

    [Fact]
    public async Task ApplyStatusTransitionAsync_AllowsConfiguredTransition()
    {
        await using var dbContext = CreateDbContext();
        var tenant = await SeedTenantAsync(dbContext);
        var service = new WorkflowService(dbContext);
        await service.EnsureDefaultWorkflowAsync(tenant.Id, CancellationToken.None);

        var workflow = await dbContext.WorkflowDefinitions
            .Include(x => x.Statuses)
            .SingleAsync(x => x.TenantId == tenant.Id);
        var ordered = workflow.Statuses.OrderBy(x => x.SortOrder).ToList();
        var initial = ordered[0];
        var next = ordered[1];

        var workOrder = await SeedWorkOrderAsync(dbContext, tenant.Id, initial.Id);

        var result = await service.ApplyStatusTransitionAsync(workOrder, next.Id, CancellationToken.None);

        Assert.Equal(next.Id, result.Id);
        Assert.Equal(next.Id, workOrder.WorkflowStatusId);
        Assert.Equal(next.Name, workOrder.Status);
    }

    [Fact]
    public async Task ApplyStatusTransitionAsync_RejectsDisallowedTransition()
    {
        await using var dbContext = CreateDbContext();
        var tenant = await SeedTenantAsync(dbContext);
        var service = new WorkflowService(dbContext);
        await service.EnsureDefaultWorkflowAsync(tenant.Id, CancellationToken.None);

        var workflow = await dbContext.WorkflowDefinitions
            .Include(x => x.Statuses)
            .SingleAsync(x => x.TenantId == tenant.Id);
        var ordered = workflow.Statuses.OrderBy(x => x.SortOrder).ToList();
        var initial = ordered[0];
        var lastStage = ordered[^1];

        var workOrder = await SeedWorkOrderAsync(dbContext, tenant.Id, initial.Id);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ApplyStatusTransitionAsync(workOrder, lastStage.Id, CancellationToken.None));

        Assert.Contains("not allowed", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyStatusTransitionAsync_RejectsChangeFromFinalStatus()
    {
        await using var dbContext = CreateDbContext();
        var tenant = await SeedTenantAsync(dbContext);
        var service = new WorkflowService(dbContext);
        await service.EnsureDefaultWorkflowAsync(tenant.Id, CancellationToken.None);

        var workflow = await dbContext.WorkflowDefinitions
            .Include(x => x.Statuses)
            .SingleAsync(x => x.TenantId == tenant.Id);
        var ordered = workflow.Statuses.OrderBy(x => x.SortOrder).ToList();
        var finalStage = ordered[^1];
        var initial = ordered[0];

        var workOrder = await SeedWorkOrderAsync(dbContext, tenant.Id, finalStage.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.ApplyStatusTransitionAsync(workOrder, initial.Id, CancellationToken.None));
    }

    [Fact]
    public async Task EvaluateAllTenantsAsync_FiresUnassignedRuleAndLogsEscalation()
    {
        await using var dbContext = CreateDbContext();
        var tenant = await SeedTenantAsync(dbContext);
        var workOrder = await SeedWorkOrderAsync(dbContext, tenant.Id, workflowStatusId: null, createdAt: DateTime.UtcNow.AddHours(-3));

        var rule = new EscalationRule
        {
            TenantId = tenant.Id,
            Name = "Unassigned escalation",
            IsActive = true,
            TriggerCondition = EscalationConditions.UnassignedAfterMins,
            TriggerValue = 60,
            Action = EscalationActions.ChangePriority,
            ActionValue = "Critical",
            CooldownMins = 60,
        };
        dbContext.EscalationRules.Add(rule);
        await dbContext.SaveChangesAsync();

        var service = new EscalationRuleService(dbContext, new NoOpEmailOutboxService(), NullLogger<EscalationRuleService>.Instance);

        var fired = await service.EvaluateAllTenantsAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1, fired);

        var updated = await dbContext.WorkOrders.SingleAsync(x => x.Id == workOrder.Id);
        Assert.Equal("Critical", updated.Priority);

        var firedRecord = await dbContext.EscalationFired.SingleAsync(x => x.RuleId == rule.Id && x.WorkOrderId == workOrder.Id);
        Assert.NotNull(firedRecord);

        var escalationEvent = await dbContext.WorkOrderEvents.SingleAsync(x => x.WorkOrderId == workOrder.Id && x.EventType == "Escalation");
        Assert.Contains(rule.Name, escalationEvent.Message);
    }

    [Fact]
    public async Task EvaluateAllTenantsAsync_RespectsCooldownAndDoesNotRefireImmediately()
    {
        await using var dbContext = CreateDbContext();
        var tenant = await SeedTenantAsync(dbContext);
        var workOrder = await SeedWorkOrderAsync(dbContext, tenant.Id, workflowStatusId: null, createdAt: DateTime.UtcNow.AddHours(-3));

        var rule = new EscalationRule
        {
            TenantId = tenant.Id,
            Name = "Unassigned escalation",
            IsActive = true,
            TriggerCondition = EscalationConditions.UnassignedAfterMins,
            TriggerValue = 60,
            Action = EscalationActions.ChangePriority,
            ActionValue = "Critical",
            CooldownMins = 120,
        };
        dbContext.EscalationRules.Add(rule);
        await dbContext.SaveChangesAsync();

        var service = new EscalationRuleService(dbContext, new NoOpEmailOutboxService(), NullLogger<EscalationRuleService>.Instance);

        var firstRun = await service.EvaluateAllTenantsAsync(DateTime.UtcNow, CancellationToken.None);
        var secondRun = await service.EvaluateAllTenantsAsync(DateTime.UtcNow.AddMinutes(10), CancellationToken.None);

        Assert.Equal(1, firstRun);
        Assert.Equal(0, secondRun);

        var firedCount = await dbContext.EscalationFired.CountAsync(x => x.RuleId == rule.Id && x.WorkOrderId == workOrder.Id);
        Assert.Equal(1, firedCount);
    }

    [Fact]
    public async Task EvaluateAllTenantsAsync_DoesNotFireForAssignedWorkOrder()
    {
        await using var dbContext = CreateDbContext();
        var tenant = await SeedTenantAsync(dbContext);
        var workOrder = await SeedWorkOrderAsync(dbContext, tenant.Id, workflowStatusId: null, createdAt: DateTime.UtcNow.AddHours(-3));
        workOrder.AssignedTechnicianId = Guid.NewGuid();
        await dbContext.SaveChangesAsync();

        var rule = new EscalationRule
        {
            TenantId = tenant.Id,
            Name = "Unassigned escalation",
            IsActive = true,
            TriggerCondition = EscalationConditions.UnassignedAfterMins,
            TriggerValue = 60,
            Action = EscalationActions.ChangePriority,
            ActionValue = "Critical",
            CooldownMins = 60,
        };
        dbContext.EscalationRules.Add(rule);
        await dbContext.SaveChangesAsync();

        var service = new EscalationRuleService(dbContext, new NoOpEmailOutboxService(), NullLogger<EscalationRuleService>.Instance);

        var fired = await service.EvaluateAllTenantsAsync(DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(0, fired);
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-workflow-escalation-{Guid.NewGuid():N}")
            .Options);

    private static async Task<Tenant> SeedTenantAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}".Substring(0, 12),
            CompanyName = "Acme Facilities",
            Email = "ops@acme.test",
            Country = "Kenya",
            PrimaryColor = "#0F4C81",
            SecondaryColor = "#F4B942",
            ShowPoweredByEcosys = true,
        };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        return tenant;
    }

    private static async Task<WorkOrder> SeedWorkOrderAsync(
        AppDbContext dbContext,
        Guid tenantId,
        Guid? workflowStatusId,
        DateTime? createdAt = null)
    {
        var client = new Client
        {
            TenantId = tenantId,
            ClientName = "Acme Manufacturing",
            Email = "client@acme.test",
            IsActive = true,
        };

        var workOrder = new WorkOrder
        {
            TenantId = tenantId,
            ClientId = client.Id,
            WorkOrderNumber = $"WO-{Guid.NewGuid():N}".Substring(0, 12),
            Title = "Generator inspection",
            Priority = "Medium",
            Status = "New",
            WorkflowStatusId = workflowStatusId,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };

        dbContext.AddRange(client, workOrder);
        await dbContext.SaveChangesAsync();
        return workOrder;
    }

    private sealed class NoOpEmailOutboxService : IEmailOutboxService
    {
        public Task CancelAsync(Guid outboxMessageId, string? errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<EmailOutboxMessage>> GetPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<EmailOutboxMessage>>([]);
        public Task<bool> MarkSendingAsync(Guid outboxMessageId, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task MarkSentAsync(Guid outboxMessageId, DateTime utcNow, string? providerMessageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkFailedAsync(Guid outboxMessageId, string? errorCategory, string? errorMessage, DateTime utcNow, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<EmailOutboxMessage> QueueEmailAsync(QueueEmailRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmailOutboxMessage
            {
                TenantId = request.TenantId,
                EventKey = request.EventKey,
                TemplateKey = request.TemplateKey,
                RecipientEmail = request.RecipientEmail,
                Subject = request.Subject,
                Status = EmailOutboxStatuses.Pending,
            });
        public Task RetryAsync(Guid outboxMessageId, DateTime nextAttemptAt, string? errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
