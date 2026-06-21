using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class Branch : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? ParentBranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public Branch? ParentBranch { get; set; }
    public ICollection<Branch> ChildBranches { get; set; } = new List<Branch>();
    public ICollection<UserBranchAssignment> UserAssignments { get; set; } = new List<UserBranchAssignment>();
    public ICollection<User> DefaultUsers { get; set; } = new List<User>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<Technician> Technicians { get; set; } = new List<Technician>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();
    public ICollection<PreventiveMaintenancePlan> PreventiveMaintenancePlans { get; set; } = new List<PreventiveMaintenancePlan>();
    public ICollection<BranchMaterialStock> MaterialStocks { get; set; } = new List<BranchMaterialStock>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<NumberingSetting> NumberingSettings { get; set; } = new List<NumberingSetting>();
    public ICollection<StockTransfer> OutgoingTransfers { get; set; } = new List<StockTransfer>();
    public ICollection<StockTransfer> IncomingTransfers { get; set; } = new List<StockTransfer>();
    public ICollection<AssignmentGroup> AssignmentGroups { get; set; } = new List<AssignmentGroup>();
    public ICollection<MonitoringSetting> MonitoringSettings { get; set; } = new List<MonitoringSetting>();
    public ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();
}
