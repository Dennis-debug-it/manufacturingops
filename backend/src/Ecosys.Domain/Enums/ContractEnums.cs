namespace Ecosys.Domain.Enums;

public enum ContractType
{
    ComprehensiveMaintenance = 1,
    PreventiveMaintenance = 2,
    CorrectiveCallout = 3,
    WarrantySupport = 4,
    Inspection = 5,
    InstallationSupport = 6,
    SlaSupport = 7,
    AdhocPayAsYouGo = 8
}

public enum ContractStatus
{
    Draft = 1,
    PendingApproval = 2,
    Active = 3,
    ExpiringSoon = 4,
    RenewalInProgress = 5,
    Renewed = 6,
    Expired = 7,
    Suspended = 8,
    Terminated = 9,
    Cancelled = 10
}

public enum BillingModel
{
    Included = 1,
    PreferentialRates = 2,
    TimeAndMaterials = 3,
    FixedPrice = 4,
    PerVisit = 5,
    Warranty = 6,
    ChargeableCallout = 7,
    Retainer = 8
}

public enum CoverageStatus
{
    Covered = 1,
    PartiallyCovered = 2,
    NotCovered = 3
}

public enum RenewalRisk
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
    Expired = 5
}

public enum ContractLifecycleEventType
{
    Created = 1,
    SubmittedForApproval = 2,
    Approved = 3,
    Activated = 4,
    MarkedExpiringSoon = 5,
    ReminderSent = 6,
    RenewalStarted = 7,
    Renewed = 8,
    Expired = 9,
    Suspended = 10,
    Resumed = 11,
    Terminated = 12,
    Cancelled = 13,
    Reopened = 14
}

public enum ContractRenewalStatus
{
    NotStarted = 1,
    InProgress = 2,
    AwaitingClient = 3,
    AwaitingApproval = 4,
    Completed = 5,
    Lost = 6,
    Cancelled = 7
}

public enum PeriodType
{
    Monthly = 1,
    Quarterly = 2,
    SemiAnnual = 3,
    Annual = 4
}
