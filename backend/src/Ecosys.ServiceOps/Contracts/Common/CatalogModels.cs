using Ecosys.ServiceOps.Enums;

namespace Ecosys.ServiceOps.Contracts.Common;

public class CustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
}

public sealed class CustomerResponse : CustomerRequest
{
    public Guid Id { get; init; }
}

public class LocationRequest
{
    public Guid CustomerId { get; set; }
    public Guid? ParentLocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
}

public sealed class LocationResponse : LocationRequest
{
    public Guid Id { get; init; }
}

public class AssetRequest
{
    public Guid CustomerId { get; set; }
    public Guid LocationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AssetResponse : AssetRequest
{
    public Guid Id { get; init; }
}

public sealed class WorkOrderRequest
{
    public Guid CustomerId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid WorkOrderTypeId { get; set; }
    public Guid? AssignmentGroupId { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;
}

public sealed class WorkOrderStatusRequest
{
    public WorkOrderStatus Status { get; set; }
    public string? ResolutionNotes { get; set; }
}

public sealed class WorkOrderResponse
{
    public Guid Id { get; init; }
    public required string Number { get; init; }
    public Guid CustomerId { get; init; }
    public Guid? LocationId { get; init; }
    public Guid? AssetId { get; init; }
    public Guid WorkOrderTypeId { get; init; }
    public Guid? AssignmentGroupId { get; init; }
    public Guid? AssignedTechnicianId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public WorkOrderPriority Priority { get; init; }
    public WorkOrderStatus Status { get; init; }
    public string? ResolutionNotes { get; init; }
    public DateTime? ResponseDueUtc { get; init; }
    public DateTime? ResolutionDueUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public SlaStatus SlaStatus { get; init; }
}

public sealed class WorkOrderUpdateRequest
{
    public string Message { get; set; } = string.Empty;
}

public sealed class WorkOrderUpdateResponse
{
    public Guid Id { get; init; }
    public Guid WorkOrderId { get; init; }
    public Guid? UserId { get; init; }
    public required string Message { get; init; }
    public DateTime CreatedUtc { get; init; }
}

public sealed class WorkOrderImageRequest
{
    public WorkOrderImageType Type { get; set; }
    public string Url { get; set; } = string.Empty;
}

public sealed class AcknowledgementRequest
{
    public string AcknowledgedBy { get; set; } = string.Empty;
}

public sealed class MaterialRequestLineRequest
{
    public Guid StoreItemId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class CreateMaterialRequest
{
    public Guid WorkOrderId { get; set; }
    public ICollection<MaterialRequestLineRequest> Lines { get; set; } = new List<MaterialRequestLineRequest>();
}

public sealed class MaterialActionRequest
{
    public Guid MaterialRequestId { get; set; }
}

public sealed class DashboardSummaryResponse
{
    public int OpenWorkOrders { get; init; }
    public int InProgressWorkOrders { get; init; }
    public int OverdueWorkOrders { get; init; }
    public int Customers { get; init; }
    public int Assets { get; init; }
}

public sealed class DashboardCountItem
{
    public required string Label { get; init; }
    public int Count { get; init; }
}

public sealed class TechnicianWorkloadItem
{
    public Guid? TechnicianId { get; init; }
    public required string TechnicianLabel { get; init; }
    public int AssignedOpenCount { get; init; }
}
