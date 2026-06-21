namespace Ecosys.Domain.Enums;

public enum WorkOrderHoldReason
{
    AwaitingMaterials = 1,
    AwaitingClient = 2,
    AwaitingVendor = 3,
    AwaitingApproval = 4,
    AwaitingSiteAccess = 5,
    AwaitingSpares = 6,
    Other = 7
}
