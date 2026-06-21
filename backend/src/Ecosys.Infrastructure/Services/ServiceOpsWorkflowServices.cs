using Ecosys.Infrastructure.Persistence;
using Ecosys.Platform.Contracts.Audit;
using Ecosys.Platform.Contracts.Numbering;
using Ecosys.ServiceOps.Contracts.Common;
using Ecosys.ServiceOps.Entities;
using Ecosys.ServiceOps.Enums;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

internal sealed class MaterialService(
    EcosysDbContext dbContext,
    ITenantContext tenantContext,
    INumberSequenceService numberSequenceService,
    IAuditService auditService) : IMaterialService
{
    public async Task<Guid> CreateRequestAsync(CreateMaterialRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.Id == request.WorkOrderId && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var materialRequest = new MaterialRequest
        {
            TenantId = tenantId,
            WorkOrderId = workOrder.Id,
            Number = await numberSequenceService.GenerateAsync(tenantId, "MaterialRequest", cancellationToken),
            Status = MaterialRequestStatus.Requested,
            Lines = new List<MaterialRequestLine>()
        };

        foreach (var line in request.Lines)
        {
            await ServiceOpsSupport.EnsureExistsAsync(dbContext.StoreItems, line.StoreItemId, tenantId, "Store item", cancellationToken);
            materialRequest.Lines.Add(new MaterialRequestLine
            {
                TenantId = tenantId,
                StoreItemId = line.StoreItemId,
                RequestedQuantity = line.Quantity
            });
        }

        dbContext.MaterialRequests.Add(materialRequest);
        await dbContext.SaveChangesAsync(cancellationToken);
        return materialRequest.Id;
    }

    public Task ApproveAsync(Guid materialRequestId, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(materialRequestId, MaterialRequestStatus.Requested, MaterialRequestStatus.Approved, cancellationToken);

    public async Task IssueAsync(Guid materialRequestId, CancellationToken cancellationToken = default)
    {
        var materialRequest = await GetMaterialRequestAsync(materialRequestId, MaterialRequestStatus.Approved, cancellationToken);

        foreach (var line in materialRequest.Lines)
        {
            var storeItem = await dbContext.StoreItems.SingleAsync(x => x.Id == line.StoreItemId, cancellationToken);
            if (storeItem.QuantityOnHand < line.RequestedQuantity)
            {
                throw new BusinessRuleException($"Insufficient stock for store item {storeItem.Sku}.");
            }

            storeItem.QuantityOnHand -= line.RequestedQuantity;
            line.IssuedQuantity = line.RequestedQuantity;
        }

        materialRequest.Status = MaterialRequestStatus.Issued;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(materialRequest.TenantId, tenantContext.UserId, "Materials", "Issue", nameof(MaterialRequest), materialRequest.Id, materialRequest.Number, cancellationToken);
    }

    public async Task UseAsync(Guid materialRequestId, CancellationToken cancellationToken = default)
    {
        var materialRequest = await GetMaterialRequestAsync(materialRequestId, MaterialRequestStatus.Issued, cancellationToken);

        foreach (var line in materialRequest.Lines)
        {
            line.UsedQuantity = line.IssuedQuantity;
            dbContext.MaterialUsages.Add(new MaterialUsage
            {
                TenantId = materialRequest.TenantId,
                WorkOrderId = materialRequest.WorkOrderId,
                StoreItemId = line.StoreItemId,
                QuantityUsed = line.UsedQuantity
            });
        }

        materialRequest.Status = MaterialRequestStatus.Used;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReturnAsync(Guid materialRequestId, CancellationToken cancellationToken = default)
    {
        var materialRequest = await GetMaterialRequestAsync(materialRequestId, MaterialRequestStatus.Used, cancellationToken);

        foreach (var line in materialRequest.Lines)
        {
            var returnQuantity = Math.Max(line.IssuedQuantity - line.UsedQuantity, 0);
            line.ReturnedQuantity = returnQuantity;

            if (returnQuantity > 0)
            {
                var storeItem = await dbContext.StoreItems.SingleAsync(x => x.Id == line.StoreItemId, cancellationToken);
                storeItem.QuantityOnHand += returnQuantity;
            }
        }

        materialRequest.Status = MaterialRequestStatus.Returned;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReconcileAsync(Guid materialRequestId, CancellationToken cancellationToken = default)
    {
        var materialRequest = await dbContext.MaterialRequests
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == materialRequestId && x.TenantId == tenantContext.GetRequiredTenantId(), cancellationToken)
            ?? throw new NotFoundException("Material request was not found.");

        if (materialRequest.Status is not MaterialRequestStatus.Returned and not MaterialRequestStatus.Used and not MaterialRequestStatus.Issued)
        {
            throw new BusinessRuleException("Material request is not ready for reconciliation.");
        }

        var hasMismatch = materialRequest.Lines.Any(x => x.IssuedQuantity != x.UsedQuantity + x.ReturnedQuantity);
        if (hasMismatch)
        {
            throw new BusinessRuleException("Material reconciliation quantities do not balance.");
        }

        materialRequest.Status = MaterialRequestStatus.Reconciled;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(materialRequest.TenantId, tenantContext.UserId, "Materials", "Reconcile", nameof(MaterialRequest), materialRequest.Id, materialRequest.Number, cancellationToken);
    }

    private async Task ChangeStatusAsync(Guid materialRequestId, MaterialRequestStatus expectedStatus, MaterialRequestStatus newStatus, CancellationToken cancellationToken)
    {
        var materialRequest = await GetMaterialRequestAsync(materialRequestId, expectedStatus, cancellationToken);
        materialRequest.Status = newStatus;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(materialRequest.TenantId, tenantContext.UserId, "Materials", newStatus.ToString(), nameof(MaterialRequest), materialRequest.Id, materialRequest.Number, cancellationToken);
    }

    private async Task<MaterialRequest> GetMaterialRequestAsync(Guid materialRequestId, MaterialRequestStatus expectedStatus, CancellationToken cancellationToken)
    {
        var materialRequest = await dbContext.MaterialRequests
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == materialRequestId && x.TenantId == tenantContext.GetRequiredTenantId(), cancellationToken)
            ?? throw new NotFoundException("Material request was not found.");

        if (materialRequest.Status != expectedStatus)
        {
            throw new BusinessRuleException($"Material request must be in status {expectedStatus}.");
        }

        return materialRequest;
    }
}

internal sealed class WorkOrderReportService(
    EcosysDbContext dbContext,
    ITenantContext tenantContext,
    IPdfRenderer pdfRenderer,
    IAuditService auditService) : IWorkOrderReportService
{
    public async Task GenerateAsync(Guid workOrderId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.Id == workOrderId && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");

        var updates = await dbContext.WorkOrderUpdates.Where(x => x.WorkOrderId == workOrderId).OrderBy(x => x.CreatedUtc).Select(x => x.Message).ToListAsync(cancellationToken);
        var materials = await dbContext.MaterialRequests.Where(x => x.WorkOrderId == workOrderId).Select(x => $"{x.Number} - {x.Status}").ToListAsync(cancellationToken);
        var images = await dbContext.WorkOrderImages.Where(x => x.WorkOrderId == workOrderId).Select(x => $"{x.Type}: {x.Url}").ToListAsync(cancellationToken);

        var content = string.Join(
            Environment.NewLine,
            new[]
            {
                $"Work Order: {workOrder.Number}",
                $"Title: {workOrder.Title}",
                $"Status: {workOrder.Status}",
                $"Priority: {workOrder.Priority}",
                $"Response SLA Due: {workOrder.ResponseDueUtc:u}",
                $"Resolution SLA Due: {workOrder.ResolutionDueUtc:u}",
                "Updates:",
                string.Join(Environment.NewLine, updates.DefaultIfEmpty("None")),
                "Materials:",
                string.Join(Environment.NewLine, materials.DefaultIfEmpty("None")),
                "Images:",
                string.Join(Environment.NewLine, images.DefaultIfEmpty("None"))
            });

        var report = await dbContext.WorkOrderReports.SingleOrDefaultAsync(x => x.WorkOrderId == workOrderId, cancellationToken);
        if (report is null)
        {
            dbContext.WorkOrderReports.Add(new WorkOrderReport
            {
                TenantId = tenantId,
                WorkOrderId = workOrderId,
                Content = content
            });
        }
        else
        {
            report.Content = content;
            report.IsApproved = false;
            report.ApprovedUtc = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveAsync(Guid workOrderId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var report = await dbContext.WorkOrderReports.SingleOrDefaultAsync(x => x.WorkOrderId == workOrderId && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Work order report was not found.");

        report.IsApproved = true;
        report.ApprovedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(tenantId, tenantContext.UserId, "Report", "Approve", nameof(WorkOrderReport), report.Id, null, cancellationToken);
    }

    public async Task AcknowledgeAsync(Guid workOrderId, AcknowledgementRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var existing = await dbContext.WorkOrderAcknowledgements.SingleOrDefaultAsync(x => x.WorkOrderId == workOrderId && x.TenantId == tenantId, cancellationToken);
        if (existing is null)
        {
            dbContext.WorkOrderAcknowledgements.Add(new WorkOrderAcknowledgement
            {
                TenantId = tenantId,
                WorkOrderId = workOrderId,
                AcknowledgedBy = request.AcknowledgedBy.Trim()
            });
        }
        else
        {
            existing.AcknowledgedBy = request.AcknowledgedBy.Trim();
            existing.AcknowledgedUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<byte[]> CreatePdfAsync(Guid workOrderId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var workOrder = await dbContext.WorkOrders.SingleOrDefaultAsync(x => x.Id == workOrderId && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Work order was not found.");
        var report = await dbContext.WorkOrderReports.SingleOrDefaultAsync(x => x.WorkOrderId == workOrderId && x.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Work order report was not found.");
        var acknowledgement = await dbContext.WorkOrderAcknowledgements.SingleOrDefaultAsync(x => x.WorkOrderId == workOrderId && x.TenantId == tenantId, cancellationToken);

        return pdfRenderer.RenderWorkOrderReportPdf(
            $"Work Order Report {workOrder.Number}",
            [report.Content, $"Acknowledgement: {(acknowledgement is null ? "Pending" : $"{acknowledgement.AcknowledgedBy} @ {acknowledgement.AcknowledgedUtc:u}")}"]);
    }
}

internal sealed class DashboardService(
    EcosysDbContext dbContext,
    ITenantContext tenantContext) : IDashboardService
{
    public async Task<DashboardSummaryResponse> GetSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureDashboardTenantAccess(tenantId);

        var now = DateTime.UtcNow;
        return new DashboardSummaryResponse
        {
            OpenWorkOrders = await dbContext.WorkOrders.CountAsync(x => x.Status == WorkOrderStatus.Open, cancellationToken),
            InProgressWorkOrders = await dbContext.WorkOrders.CountAsync(x => x.Status == WorkOrderStatus.InProgress, cancellationToken),
            OverdueWorkOrders = await dbContext.WorkOrders.CountAsync(x => x.ResolutionDueUtc < now && x.Status != WorkOrderStatus.Closed && x.Status != WorkOrderStatus.Cancelled, cancellationToken),
            Customers = await dbContext.Customers.CountAsync(cancellationToken),
            Assets = await dbContext.Assets.CountAsync(cancellationToken)
        };
    }

    public async Task<IReadOnlyCollection<DashboardCountItem>> GetWorkOrdersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureDashboardTenantAccess(tenantId);
        return await dbContext.WorkOrders
            .GroupBy(x => x.Status)
            .Select(x => new DashboardCountItem { Label = x.Key.ToString(), Count = x.Count() })
            .OrderBy(x => x.Label)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DashboardCountItem>> GetMaterialsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureDashboardTenantAccess(tenantId);
        return await dbContext.MaterialRequests
            .GroupBy(x => x.Status)
            .Select(x => new DashboardCountItem { Label = x.Key.ToString(), Count = x.Count() })
            .OrderBy(x => x.Label)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TechnicianWorkloadItem>> GetTechniciansAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        EnsureDashboardTenantAccess(tenantId);
        return await dbContext.WorkOrders
            .Where(x => x.Status != WorkOrderStatus.Closed && x.Status != WorkOrderStatus.Cancelled)
            .GroupBy(x => x.AssignedTechnicianId)
            .Select(x => new TechnicianWorkloadItem
            {
                TechnicianId = x.Key,
                TechnicianLabel = x.Key.HasValue ? x.Key.Value.ToString() : "Unassigned",
                AssignedOpenCount = x.Count()
            })
            .OrderByDescending(x => x.AssignedOpenCount)
            .ToListAsync(cancellationToken);
    }

    private void EnsureDashboardTenantAccess(Guid tenantId)
    {
        if (tenantContext.IsSuperAdmin)
        {
            throw new ForbiddenException("Platform users cannot access tenant operational dashboard data.");
        }

        if (tenantContext.TenantId != tenantId)
        {
            throw new ForbiddenException("Dashboard access is forbidden for the requested tenant.");
        }
    }
}
