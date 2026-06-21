using Ecosys.Infrastructure.Persistence;
using Ecosys.Platform.Contracts.Audit;
using Ecosys.Platform.Contracts.Notifications;
using Ecosys.Platform.Contracts.Numbering;
using Ecosys.ServiceOps.Contracts.Common;
using Ecosys.ServiceOps.Entities;
using Ecosys.ServiceOps.Enums;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

internal sealed class WorkOrderService(
    EcosysDbContext dbContext,
    ITenantContext tenantContext,
    INumberSequenceService numberSequenceService,
    IAuditService auditService,
    INotificationService notificationService,
    IWorkOrderReportService workOrderReportService) : IWorkOrderService
{
    private static readonly Dictionary<WorkOrderStatus, WorkOrderStatus[]> AllowedTransitions = new()
    {
        [WorkOrderStatus.Open] = [WorkOrderStatus.Assigned, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.Assigned] = [WorkOrderStatus.InProgress, WorkOrderStatus.OnHold, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.InProgress] = [WorkOrderStatus.OnHold, WorkOrderStatus.Completed, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.OnHold] = [WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled],
        [WorkOrderStatus.Completed] = [WorkOrderStatus.Closed],
        [WorkOrderStatus.Closed] = [],
        [WorkOrderStatus.Cancelled] = []
    };

    public async Task<IReadOnlyCollection<WorkOrderResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return await dbContext.WorkOrders
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkOrderResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var workOrder = await dbContext.WorkOrders
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => Map(x))
            .SingleOrDefaultAsync(cancellationToken);

        return workOrder ?? throw new NotFoundException("Work order was not found.");
    }

    public async Task<WorkOrderResponse> CreateAsync(WorkOrderRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();

        await ServiceOpsSupport.EnsureExistsAsync(dbContext.Customers, request.CustomerId, tenantId, "Customer", cancellationToken);
        if (request.LocationId.HasValue)
        {
            await ServiceOpsSupport.EnsureExistsAsync(dbContext.Locations, request.LocationId.Value, tenantId, "Location", cancellationToken);
        }
        if (request.AssetId.HasValue)
        {
            await ServiceOpsSupport.EnsureExistsAsync(dbContext.Assets, request.AssetId.Value, tenantId, "Asset", cancellationToken);
        }

        var workOrderType = await dbContext.WorkOrderTypes
            .SingleOrDefaultAsync(x => x.Id == request.WorkOrderTypeId && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Work order type was not found.");

        if (request.AssignmentGroupId.HasValue)
        {
            await ServiceOpsSupport.EnsureExistsAsync(dbContext.AssignmentGroups, request.AssignmentGroupId.Value, tenantId, "Assignment group", cancellationToken);
        }

        var responseTarget = 240;
        var resolutionTarget = 2880;
        var slaPolicy = await dbContext.SlaPolicies.OrderBy(x => x.CreatedUtc).FirstOrDefaultAsync(cancellationToken);
        if (slaPolicy is not null)
        {
            responseTarget = slaPolicy.ResponseTargetMinutes;
            resolutionTarget = slaPolicy.ResolutionTargetMinutes;
        }

        var initialStatus = request.AssignedTechnicianId.HasValue || request.AssignmentGroupId.HasValue
            ? WorkOrderStatus.Assigned
            : WorkOrderStatus.Open;

        var workOrder = new WorkOrder
        {
            TenantId = tenantId,
            Number = await numberSequenceService.GenerateAsync(tenantId, "WorkOrder", cancellationToken),
            CustomerId = request.CustomerId,
            LocationId = request.LocationId,
            AssetId = request.AssetId,
            WorkOrderTypeId = request.WorkOrderTypeId,
            AssignmentGroupId = request.AssignmentGroupId,
            AssignedTechnicianId = request.AssignedTechnicianId,
            Title = request.Title.Trim(),
            Description = ServiceOpsSupport.Normalize(request.Description),
            Priority = request.Priority,
            Status = initialStatus,
            ResponseDueUtc = DateTime.UtcNow.AddMinutes(responseTarget),
            ResolutionDueUtc = DateTime.UtcNow.AddMinutes(resolutionTarget),
            SlaStatus = SlaStatus.WithinTarget
        };

        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(tenantId, tenantContext.UserId, "WorkOrder", "Create", nameof(WorkOrder), workOrder.Id, workOrder.Number, cancellationToken);

        if (workOrder.AssignedTechnicianId.HasValue)
        {
            await notificationService.QueueAsync(tenantId, workOrder.AssignedTechnicianId, "Work order assigned", $"Work order {workOrder.Number} has been assigned to you.", cancellationToken);
        }

        return Map(workOrder);
    }

    public async Task<WorkOrderResponse> ChangeStatusAsync(Guid id, WorkOrderStatusRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        ValidateTransition(workOrder.Status, request.Status);
        await EnforceCompletionRulesAsync(workOrder, request.Status, cancellationToken);

        workOrder.Status = request.Status;
        workOrder.ResolutionNotes = ServiceOpsSupport.Normalize(request.ResolutionNotes) ?? workOrder.ResolutionNotes;

        if (request.Status == WorkOrderStatus.Completed)
        {
            workOrder.CompletedAtUtc = DateTime.UtcNow;
            await workOrderReportService.GenerateAsync(workOrder.Id, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(tenantId, tenantContext.UserId, "WorkOrder", $"Status:{request.Status}", nameof(WorkOrder), workOrder.Id, workOrder.Number, cancellationToken);

        return Map(workOrder);
    }

    public async Task<WorkOrderUpdateResponse> AddUpdateAsync(Guid id, WorkOrderUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var update = new WorkOrderUpdate
        {
            TenantId = tenantId,
            WorkOrderId = workOrder.Id,
            UserId = tenantContext.UserId,
            Message = request.Message.Trim()
        };

        dbContext.WorkOrderUpdates.Add(update);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new WorkOrderUpdateResponse
        {
            Id = update.Id,
            WorkOrderId = update.WorkOrderId,
            UserId = update.UserId,
            Message = update.Message,
            CreatedUtc = update.CreatedUtc
        };
    }

    public async Task<IReadOnlyCollection<WorkOrderUpdateResponse>> GetUpdatesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return await dbContext.WorkOrderUpdates
            .AsNoTracking()
            .Where(x => x.WorkOrderId == id && x.TenantId == tenantId)
            .OrderBy(x => x.CreatedUtc)
            .Select(x => new WorkOrderUpdateResponse
            {
                Id = x.Id,
                WorkOrderId = x.WorkOrderId,
                UserId = x.UserId,
                Message = x.Message,
                CreatedUtc = x.CreatedUtc
            })
            .ToListAsync(cancellationToken);
    }

    public async Task AddImageAsync(Guid id, WorkOrderImageRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var exists = await dbContext.WorkOrders.AnyAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Work order was not found.");
        }

        dbContext.WorkOrderImages.Add(new WorkOrderImage
        {
            TenantId = tenantId,
            WorkOrderId = id,
            Type = request.Type,
            Url = request.Url.Trim()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnforceCompletionRulesAsync(WorkOrder workOrder, WorkOrderStatus targetStatus, CancellationToken cancellationToken)
    {
        if (targetStatus == WorkOrderStatus.Completed)
        {
            var workOrderType = await dbContext.WorkOrderTypes.SingleAsync(x => x.Id == workOrder.WorkOrderTypeId, cancellationToken);
            if (workOrderType.Code is "CORRECTIVE" or "INSTALLATION")
            {
                var hasAfterImage = await dbContext.WorkOrderImages.AnyAsync(
                    x => x.WorkOrderId == workOrder.Id && x.Type == WorkOrderImageType.After,
                    cancellationToken);

                if (!hasAfterImage)
                {
                    throw new BusinessRuleException("Corrective and Installation work orders require an AFTER image before completion.");
                }
            }

            var unreconciledMaterialsExist = await dbContext.MaterialRequests.AnyAsync(
                x => x.WorkOrderId == workOrder.Id && x.Status != MaterialRequestStatus.Reconciled,
                cancellationToken);

            if (unreconciledMaterialsExist)
            {
                throw new BusinessRuleException("Cannot complete a work order with unreconciled material requests.");
            }
        }

        if (targetStatus == WorkOrderStatus.Closed)
        {
            var report = await dbContext.WorkOrderReports.SingleOrDefaultAsync(x => x.WorkOrderId == workOrder.Id, cancellationToken);
            if (report is null || !report.IsApproved)
            {
                throw new BusinessRuleException("An approved report is required before closing a work order.");
            }

            var acknowledgement = await dbContext.WorkOrderAcknowledgements.SingleOrDefaultAsync(x => x.WorkOrderId == workOrder.Id, cancellationToken);
            if (acknowledgement is null)
            {
                throw new BusinessRuleException("Acknowledgement is required before closing a work order.");
            }
        }
    }

    private static void ValidateTransition(WorkOrderStatus currentStatus, WorkOrderStatus newStatus)
    {
        if (currentStatus == newStatus)
        {
            return;
        }

        if (!AllowedTransitions.TryGetValue(currentStatus, out var allowedStatuses) || !allowedStatuses.Contains(newStatus))
        {
            throw new BusinessRuleException($"Invalid work order transition from {currentStatus} to {newStatus}.");
        }
    }

    private static WorkOrderResponse Map(WorkOrder x) =>
        new()
        {
            Id = x.Id,
            Number = x.Number,
            CustomerId = x.CustomerId,
            LocationId = x.LocationId,
            AssetId = x.AssetId,
            WorkOrderTypeId = x.WorkOrderTypeId,
            AssignmentGroupId = x.AssignmentGroupId,
            AssignedTechnicianId = x.AssignedTechnicianId,
            Title = x.Title,
            Description = x.Description,
            Priority = x.Priority,
            Status = x.Status,
            ResolutionNotes = x.ResolutionNotes,
            ResponseDueUtc = x.ResponseDueUtc,
            ResolutionDueUtc = x.ResolutionDueUtc,
            CompletedAtUtc = x.CompletedAtUtc,
            SlaStatus = x.SlaStatus
        };
}
