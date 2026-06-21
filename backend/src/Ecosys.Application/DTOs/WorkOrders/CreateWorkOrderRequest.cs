using System.ComponentModel.DataAnnotations;
using Ecosys.Domain.Enums;
using Ecosys.Application.Validation;

namespace Ecosys.Application.DTOs.WorkOrders;

public sealed class CreateWorkOrderRequest
{
    [NotEmptyGuid]
    public Guid TenantId { get; set; }

    public Guid? AssetId { get; set; }
    public Guid? TechnicianId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    [EnumDataType(typeof(WorkOrderStatus))]
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Open;

    [Required]
    [EnumDataType(typeof(WorkOrderPriority))]
    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;

    [Required]
    [EnumDataType(typeof(WorkOrderType))]
    public WorkOrderType WorkType { get; set; } = WorkOrderType.CorrectiveMaintenance;

    public DateTime? ScheduledForUtc { get; set; }
}
