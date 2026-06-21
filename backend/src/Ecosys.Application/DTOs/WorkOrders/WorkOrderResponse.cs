using Ecosys.Domain.Enums;

namespace Ecosys.Application.DTOs.WorkOrders;

public sealed class WorkOrderResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid? AssetId { get; init; }
    public Guid? TechnicianId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public WorkOrderStatus Status { get; init; }
    public WorkOrderPriority Priority { get; init; }
    public WorkOrderType WorkType { get; init; } = WorkOrderType.CorrectiveMaintenance;
    public DateTime? ScheduledForUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime? UpdatedUtc { get; init; }
}
