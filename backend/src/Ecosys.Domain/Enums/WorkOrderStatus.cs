namespace Ecosys.Domain.Enums;

public enum WorkOrderStatus
{
    Draft = 1,
    New = 2,
    Open = 3,
    Assigned = 4,
    Scheduled = 5,
    InProgress = 6,
    OnHold = 7,
    Completed = 8,
    ClientAccepted = 9,
    Closed = 10,
    Cancelled = 11,
    Rejected = 12,
    Reopened = 13,
    WaitingOnParts = 14,
    WaitingOnClient = 15
}
