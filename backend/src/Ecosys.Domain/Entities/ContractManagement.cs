using Ecosys.Domain.Common;
using Ecosys.Domain.Enums;

namespace Ecosys.Domain.Entities;

public sealed class Contract : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ContractType ContractType { get; set; } = ContractType.ComprehensiveMaintenance;
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? DurationMonths { get; set; }
    public BillingModel BillingModel { get; set; } = BillingModel.Included;
    public string Currency { get; set; } = "KES";
    public decimal? ContractValue { get; set; }
    public int RenewalNoticeDays { get; set; } = 30;
    public string? Description { get; set; }
    public string? IncludedServices { get; set; }
    public string? ExcludedServices { get; set; }
    public string? Notes { get; set; }

    public DateTime? ActivatedAt { get; set; }
    public DateTime? SubmittedForApprovalAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ExpiryCheckedAt { get; set; }
    public Guid? RenewalOwnerUserId { get; set; }
    public Guid? AccountManagerUserId { get; set; }
    public Guid? InternalApproverUserId { get; set; }
    public DateTime? RenewalStartedAt { get; set; }
    public Guid? RenewedFromContractId { get; set; }
    public Guid? RenewedToContractId { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string? SuspensionReason { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? LastReminderSentAt { get; set; }
    public DateTime? NextReminderAt { get; set; }
    public bool AutoExpireEnabled { get; set; } = true;
    public RenewalRisk RenewalRisk { get; set; } = RenewalRisk.Low;
    public int GracePeriodDays { get; set; }
    public bool AllowPreferentialRatesDuringGracePeriod { get; set; }
    public bool AllowSlaDuringGracePeriod { get; set; }

    public decimal? ResponseTimeHours { get; set; }
    public decimal? ResolutionTimeHours { get; set; }
    public string? WorkingHoursPolicy { get; set; }
    public bool PauseSlaOnHold { get; set; }
    public string? EscalationEmail { get; set; }
    public string? PriorityLevel { get; set; }
    public bool GracePeriodAllowsSla { get; set; }

    public bool IsArchived { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? Client { get; set; }
    public User? RenewalOwnerUser { get; set; }
    public User? AccountManagerUser { get; set; }
    public User? InternalApproverUser { get; set; }
    public Contract? RenewedFromContract { get; set; }
    public Contract? RenewedToContract { get; set; }
    public ICollection<ContractSite> Sites { get; set; } = new List<ContractSite>();
    public ICollection<ContractAsset> Assets { get; set; } = new List<ContractAsset>();
    public ICollection<ContractAssetCategory> AssetCategories { get; set; } = new List<ContractAssetCategory>();
    public ICollection<ContractWorkOrderType> WorkOrderTypes { get; set; } = new List<ContractWorkOrderType>();
    public ICollection<ContractLifecycleEvent> LifecycleEvents { get; set; } = new List<ContractLifecycleEvent>();
    public ICollection<ContractReminderRule> ReminderRules { get; set; } = new List<ContractReminderRule>();
    public ICollection<ContractRateCard> RateCards { get; set; } = new List<ContractRateCard>();
    public ICollection<ContractPmEntitlement> PmEntitlements { get; set; } = new List<ContractPmEntitlement>();
    public ICollection<ContractRenewal> Renewals { get; set; } = new List<ContractRenewal>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

public sealed class ContractSite : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ContractId { get; set; }
    public Guid SiteId { get; set; }

    public Tenant? Tenant { get; set; }
    public Contract? Contract { get; set; }
    public Site? Site { get; set; }
}

public sealed class ContractAsset : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ContractId { get; set; }
    public Guid AssetId { get; set; }

    public Tenant? Tenant { get; set; }
    public Contract? Contract { get; set; }
    public Asset? Asset { get; set; }
}

public sealed class ContractAssetCategory : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ContractId { get; set; }
    public Guid? AssetCategoryId { get; set; }
    public string? AssetCategoryName { get; set; }

    public Tenant? Tenant { get; set; }
    public Contract? Contract { get; set; }
    public AssetCategory? AssetCategory { get; set; }
}

public sealed class ContractWorkOrderType : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ContractId { get; set; }
    public Guid? WorkOrderTypeId { get; set; }
    public string? WorkOrderTypeName { get; set; }

    public Tenant? Tenant { get; set; }
    public Contract? Contract { get; set; }
}

public sealed class ContractLifecycleEvent : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ContractId { get; set; }
    public ContractLifecycleEventType EventType { get; set; }
    public ContractStatus? OldStatus { get; set; }
    public ContractStatus? NewStatus { get; set; }
    public string? Note { get; set; }
    public string? Reason { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
    public Contract? Contract { get; set; }
    public User? CreatedByUser { get; set; }
}

public sealed class ContractReminderRule : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? ContractId { get; set; }
    public int DaysBeforeExpiry { get; set; }
    public bool NotifyAccountManager { get; set; } = true;
    public bool NotifyRenewalOwner { get; set; } = true;
    public bool NotifySupervisor { get; set; }
    public bool NotifyFinance { get; set; }
    public bool NotifyTenantAdmin { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;
    public bool EscalationEnabled { get; set; }
    public int? EscalationAfterDays { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public Contract? Contract { get; set; }
}

public sealed class ContractRateCard : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ContractId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Currency { get; set; } = "KES";
    public bool IsDefault { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public Contract? Contract { get; set; }
    public ICollection<ContractRateRule> Rules { get; set; } = new List<ContractRateRule>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}

public sealed class ContractRateRule : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RateCardId { get; set; }
    public Guid? WorkOrderTypeId { get; set; }
    public string? WorkOrderTypeName { get; set; }
    public Guid? AssetCategoryId { get; set; }
    public string? AssetCategoryName { get; set; }
    public Guid? SiteId { get; set; }
    public string? Priority { get; set; }
    public BillingModel BillingModel { get; set; } = BillingModel.PreferentialRates;
    public decimal? CalloutFee { get; set; }
    public decimal? FixedVisitFee { get; set; }
    public decimal? LabourHourlyRate { get; set; }
    public decimal? OvertimeHourlyRate { get; set; }
    public decimal? WeekendHourlyRate { get; set; }
    public decimal? PublicHolidayHourlyRate { get; set; }
    public decimal? EmergencyMultiplier { get; set; }
    public decimal? TravelCharge { get; set; }
    public decimal? MaterialMarkupPercent { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? MinimumBillableHours { get; set; }
    public decimal? IncludedLabourHours { get; set; }
    public bool VatApplicable { get; set; } = true;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public ContractRateCard? RateCard { get; set; }
    public AssetCategory? AssetCategory { get; set; }
    public Site? Site { get; set; }
}

public sealed class ContractPmEntitlement : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ContractId { get; set; }
    public Guid? AssetId { get; set; }
    public string? AssetCategory { get; set; }
    public string WorkOrderType { get; set; } = "PreventiveMaintenance";
    public int VisitsPerPeriod { get; set; }
    public PeriodType PeriodType { get; set; } = PeriodType.Annual;
    public int IncludedVisits { get; set; }
    public int UsedVisits { get; set; }
    public int RemainingVisits { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public Contract? Contract { get; set; }
    public Asset? Asset { get; set; }
}

public sealed class ContractRenewal : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid OriginalContractId { get; set; }
    public Guid? RenewedContractId { get; set; }
    public ContractRenewalStatus RenewalStatus { get; set; } = ContractRenewalStatus.NotStarted;
    public DateTime? RenewalStartedAt { get; set; }
    public DateTime? RenewalCompletedAt { get; set; }
    public Guid? OwnerUserId { get; set; }
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }
    public Contract? OriginalContract { get; set; }
    public Contract? RenewedContract { get; set; }
    public User? OwnerUser { get; set; }
}
