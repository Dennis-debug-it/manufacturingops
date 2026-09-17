using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class Tenant : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Country { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = "Africa/Nairobi";
    public string Currency { get; set; } = "KES";
    public string? Industry { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? County { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? TaxPin { get; set; }
    public string? LegalName { get; set; }
    public string? TradingName { get; set; }
    public string? PostalAddress { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? Website { get; set; }
    public string? VatNumber { get; set; }
    public string? RegistrationNumber { get; set; }
    public string Status { get; set; } = "Active";
    public string? PlanName { get; set; }
    public string? LicenseStatus { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxBranches { get; set; }
    public DateTime? TrialStartsAt { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public bool TrialExtensionUsed { get; set; }
    public DateTime? TrialExtendedAt { get; set; }
    public Guid? TrialExtendedByUserId { get; set; }
    public DateTime? SubscriptionStartsAt { get; set; }
    public DateTime? SubscriptionEndsAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public Guid? DeactivatedByUserId { get; set; }
    public string? DeactivationReason { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#0F4C81";
    public string SecondaryColor { get; set; } = "#F4B942";
    public bool ShowPoweredByEcosys { get; set; } = true;
    /// <summary>JSON blob storing the tenant's shared/base report template configuration.</summary>
    public string? ReportTemplateConfigJson { get; set; }
    /// <summary>JSON map of per-document-type design overrides (keyed by document-type slug). Absent types fall back to the shared base config.</summary>
    public string? DocumentDesignsJson { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<UserBranchAssignment> UserBranchAssignments { get; set; } = new List<UserBranchAssignment>();
    public ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
    public ICollection<Client> Clients { get; set; } = new List<Client>();
    public ICollection<OperationalSystem> Systems { get; set; } = new List<OperationalSystem>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<Technician> Technicians { get; set; } = new List<Technician>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    public ICollection<MaterialItem> MaterialItems { get; set; } = new List<MaterialItem>();
    public ICollection<BranchMaterialStock> BranchMaterialStocks { get; set; } = new List<BranchMaterialStock>();
    public ICollection<StockTransfer> StockTransfers { get; set; } = new List<StockTransfer>();
    public ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<PreventiveMaintenancePlan> PreventiveMaintenancePlans { get; set; } = new List<PreventiveMaintenancePlan>();
    public ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<TenantLicense> TenantLicenses { get; set; } = new List<TenantLicense>();
    public ICollection<AssignmentGroup> AssignmentGroups { get; set; } = new List<AssignmentGroup>();
    public ICollection<WorkOrderEvent> WorkOrderEvents { get; set; } = new List<WorkOrderEvent>();
    public ICollection<PmTemplate> PmTemplates { get; set; } = new List<PmTemplate>();
    public ICollection<PmReport> PmReports { get; set; } = new List<PmReport>();
    public ICollection<WorkOrderChecklistItem> WorkOrderChecklistItems { get; set; } = new List<WorkOrderChecklistItem>();
    public ICollection<TenantTemplatePackInstallation> TemplatePackInstallations { get; set; } = new List<TenantTemplatePackInstallation>();
    public ICollection<TenantSecurityPolicy> TenantSecurityPolicies { get; set; } = new List<TenantSecurityPolicy>();
    public ICollection<TenantOperationsPolicy> TenantOperationsPolicies { get; set; } = new List<TenantOperationsPolicy>();
    public ICollection<TenantFinanceDefaults> TenantFinanceDefaults { get; set; } = new List<TenantFinanceDefaults>();
    public ICollection<EmailSetting> EmailSettings { get; set; } = new List<EmailSetting>();
    public ICollection<TenantNotificationSetting> TenantNotificationSettings { get; set; } = new List<TenantNotificationSetting>();
    public ICollection<TenantNotificationRecipient> TenantNotificationRecipients { get; set; } = new List<TenantNotificationRecipient>();
    public ICollection<IntakeProtocol> IntakeProtocols { get; set; } = new List<IntakeProtocol>();
    public ICollection<EmailIntakeSetting> EmailIntakeSettings { get; set; } = new List<EmailIntakeSetting>();
    public ICollection<MonitoringSetting> MonitoringSettings { get; set; } = new List<MonitoringSetting>();
    public ICollection<MonitoringWebhookIntegration> MonitoringWebhookIntegrations { get; set; } = new List<MonitoringWebhookIntegration>();
    public ICollection<NotificationSetting> NotificationSettings { get; set; } = new List<NotificationSetting>();
    public ICollection<PlatformQuotation> PlatformQuotations { get; set; } = new List<PlatformQuotation>();
    public ICollection<PlatformInvoice> PlatformInvoices { get; set; } = new List<PlatformInvoice>();
    public ICollection<PlatformPayment> PlatformPayments { get; set; } = new List<PlatformPayment>();
    public ICollection<PlatformExpense> PlatformExpenses { get; set; } = new List<PlatformExpense>();
    public ICollection<PlatformLead> ConvertedLeads { get; set; } = new List<PlatformLead>();
    public ICollection<WorkOrderPhotoEvidence> WorkOrderPhotoEvidence { get; set; } = new List<WorkOrderPhotoEvidence>();
    public ICollection<WorkOrderSignature> WorkOrderSignatures { get; set; } = new List<WorkOrderSignature>();
    public ICollection<WorkOrderMaterialUsage> WorkOrderMaterialUsages { get; set; } = new List<WorkOrderMaterialUsage>();
    public ICollection<ClientPreferentialRate> ClientPreferentialRates { get; set; } = new List<ClientPreferentialRate>();
    public ICollection<FinanceApprovalRule> FinanceApprovalRules { get; set; } = new List<FinanceApprovalRule>();
    public ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();
    public ICollection<KnowledgeDocument> KnowledgeDocuments { get; set; } = new List<KnowledgeDocument>();
    public ICollection<KnowledgeDocumentChunk> KnowledgeDocumentChunks { get; set; } = new List<KnowledgeDocumentChunk>();
}
