using Ecosys.Application.DTOs.WorkOrders;

namespace Ecosys.Application.Abstractions.Services;

public interface IWorkOrderService
{
    Task<IReadOnlyCollection<WorkOrderResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WorkOrderResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkOrderResponse> CreateAsync(CreateWorkOrderRequest request, CancellationToken cancellationToken = default);
    Task<WorkOrderResponse?> UpdateAsync(Guid id, UpdateWorkOrderRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
