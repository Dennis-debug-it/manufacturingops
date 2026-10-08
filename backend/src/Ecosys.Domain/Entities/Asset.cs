using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class Asset : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? SystemId { get; set; }
    public Guid? AssetCategoryId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetCode { get; set; } = string.Empty;
    public string? AssetType { get; set; }
    public string OwnershipType { get; set; } = "ClientOwned";
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public DateTime? InstallationDate { get; set; }
    public DateTime? CommissioningDate { get; set; }
    public DateTime? DecommissionDate { get; set; }
    public string? DecommissionReason { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public DateTime? WarrantyStartDate { get; set; }
    public int? WarrantyPeriodMonths { get; set; }
    public int? YearOfManufacture { get; set; }
    public string? CapacityRating { get; set; }
    public string? PhysicalDescription { get; set; }

    // Location within site
    public string? Location { get; set; }
    public string? BuildingBlock { get; set; }
    public string? FloorLevel { get; set; }
    public string? RoomArea { get; set; }
    public string? LocationDescription { get; set; }

    // Maintenance
    public string? RecommendedPmFrequency { get; set; }
    public int? PmFrequencyValue { get; set; }
    public string? PmFrequencyUnit { get; set; }
    public bool AutoSchedulePm { get; set; }
    public DateTime? LastPmDate { get; set; }
    public DateTime? NextPmDate { get; set; }
    public string? MeterLabel { get; set; }
    public decimal? CurrentMeterReading { get; set; }
    public decimal? MeterInterval { get; set; }
    public decimal? MeterBuffer { get; set; }
    public Guid? DefaultAssignmentGroupId { get; set; }

    public string? Notes { get; set; }
    public string Status { get; set; } = "Active";

    public Tenant? Tenant { get; set; }
    public Branch? Branch { get; set; }
    public Client? Client { get; set; }
    public Site? Site { get; set; }
    public OperationalSystem? System { get; set; }
    public AssetCategory? AssetCategory { get; set; }
    public AssignmentGroup? DefaultAssignmentGroup { get; set; }
    public ICollection<AssetCustomFieldValue> CustomFieldValues { get; set; } = new List<AssetCustomFieldValue>();
    public ICollection<ContractAsset> ContractAssets { get; set; } = new List<ContractAsset>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<PreventiveMaintenancePlan> PreventiveMaintenancePlans { get; set; } = new List<PreventiveMaintenancePlan>();
}
