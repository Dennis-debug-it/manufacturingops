namespace Ecosys.ServiceOps.Contracts.Common;

public interface ICustomerService
{
    Task<IReadOnlyCollection<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken cancellationToken = default);
}

public interface ILocationService
{
    Task<IReadOnlyCollection<LocationResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<LocationResponse> CreateAsync(LocationRequest request, CancellationToken cancellationToken = default);
}

public interface IAssetService
{
    Task<IReadOnlyCollection<AssetResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AssetResponse> CreateAsync(AssetRequest request, CancellationToken cancellationToken = default);
}

public interface IWorkOrderService
{
    Task<IReadOnlyCollection<WorkOrderResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WorkOrderResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkOrderResponse> CreateAsync(WorkOrderRequest request, CancellationToken cancellationToken = default);
    Task<WorkOrderResponse> ChangeStatusAsync(Guid id, WorkOrderStatusRequest request, CancellationToken cancellationToken = default);
    Task<WorkOrderUpdateResponse> AddUpdateAsync(Guid id, WorkOrderUpdateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<WorkOrderUpdateResponse>> GetUpdatesAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddImageAsync(Guid id, WorkOrderImageRequest request, CancellationToken cancellationToken = default);
}

public interface IMaterialService
{
    Task<Guid> CreateRequestAsync(CreateMaterialRequest request, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid materialRequestId, CancellationToken cancellationToken = default);
    Task IssueAsync(Guid materialRequestId, CancellationToken cancellationToken = default);
    Task UseAsync(Guid materialRequestId, CancellationToken cancellationToken = default);
    Task ReturnAsync(Guid materialRequestId, CancellationToken cancellationToken = default);
    Task ReconcileAsync(Guid materialRequestId, CancellationToken cancellationToken = default);
}

public interface IWorkOrderReportService
{
    Task GenerateAsync(Guid workOrderId, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid workOrderId, CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(Guid workOrderId, AcknowledgementRequest request, CancellationToken cancellationToken = default);
    Task<byte[]> CreatePdfAsync(Guid workOrderId, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DashboardCountItem>> GetWorkOrdersAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DashboardCountItem>> GetMaterialsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TechnicianWorkloadItem>> GetTechniciansAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
