using Ecosys.Domain.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Reflection;

namespace Ecosys.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext? tenantContext = null) : DbContext(options)
{
    private Guid CurrentTenantFilterId => tenantContext?.TenantId ?? Guid.Empty;
    private bool ShouldBypassTenantFilter => tenantContext?.IsAuthenticated != true || tenantContext.IsSuperAdmin;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<OrgNode> OrgNodes => Set<OrgNode>();
    public DbSet<UserOrgNodeAssignment> UserOrgNodeAssignments => Set<UserOrgNodeAssignment>();
    public DbSet<FactorySettings> FactorySettings => Set<FactorySettings>();
    public DbSet<AssetLifecycleEvent> AssetLifecycleEvents => Set<AssetLifecycleEvent>();
    public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<AssetTag> AssetTags => Set<AssetTag>();
    public DbSet<AssetDocument> AssetDocuments => Set<AssetDocument>();
    public DbSet<AssetDocumentVersion> AssetDocumentVersions => Set<AssetDocumentVersion>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<ConditionEvent> ConditionEvents => Set<ConditionEvent>();
    public DbSet<SensorGateway> SensorGateways => Set<SensorGateway>();
    public DbSet<VehicleProfile> VehicleProfiles => Set<VehicleProfile>();
    public DbSet<FuelLog> FuelLogs => Set<FuelLog>();
    public DbSet<TelematicsConnection> TelematicsConnections => Set<TelematicsConnection>();
    public DbSet<AccessZone> AccessZones => Set<AccessZone>();
    public DbSet<Visitor> Visitors => Set<Visitor>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<VisitAttendee> VisitAttendees => Set<VisitAttendee>();
    public DbSet<VisitZone> VisitZones => Set<VisitZone>();
    public DbSet<VehicleMovement> VehicleMovements => Set<VehicleMovement>();
    public DbSet<VisitorCard> VisitorCards => Set<VisitorCard>();
    public DbSet<VisitorPass> VisitorPasses => Set<VisitorPass>();
    public DbSet<VisitorPassZone> VisitorPassZones => Set<VisitorPassZone>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<PatrolRoute> PatrolRoutes => Set<PatrolRoute>();
    public DbSet<PatrolRouteCheckpoint> PatrolRouteCheckpoints => Set<PatrolRouteCheckpoint>();
    public DbSet<PatrolSchedule> PatrolSchedules => Set<PatrolSchedule>();
    public DbSet<PatrolRun> PatrolRuns => Set<PatrolRun>();
    public DbSet<CheckpointScan> CheckpointScans => Set<CheckpointScan>();
    public DbSet<ShiftHandover> ShiftHandovers => Set<ShiftHandover>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserBranchAssignment> UserBranchAssignments => Set<UserBranchAssignment>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<PendingSignup> PendingSignups => Set<PendingSignup>();
    public DbSet<ApprovalMatrix> ApprovalMatrices => Set<ApprovalMatrix>();
    public DbSet<ApprovalLevel> ApprovalLevels => Set<ApprovalLevel>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalRequestStep> ApprovalRequestSteps => Set<ApprovalRequestStep>();
    public DbSet<ClientPortalUser> ClientPortalUsers => Set<ClientPortalUser>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<OperationalSystem> Systems => Set<OperationalSystem>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<AttendanceEntry> AttendanceEntries => Set<AttendanceEntry>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<TimeAttendanceSetting> TimeAttendanceSettings => Set<TimeAttendanceSetting>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseOrderReceiptEvent> PurchaseOrderReceiptEvents => Set<PurchaseOrderReceiptEvent>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<WhatsAppSetting> WhatsAppSettings => Set<WhatsAppSetting>();
    public DbSet<WhatsAppConversation> WhatsAppConversations => Set<WhatsAppConversation>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();
    public DbSet<WhatsAppTemplate> WhatsAppTemplates => Set<WhatsAppTemplate>();
    public DbSet<WhatsAppOutboxMessage> WhatsAppOutboxMessages => Set<WhatsAppOutboxMessage>();
    public DbSet<WhatsAppOptOut> WhatsAppOptOuts => Set<WhatsAppOptOut>();
    public DbSet<WhatsAppVerificationCode> WhatsAppVerificationCodes => Set<WhatsAppVerificationCode>();
    public DbSet<TenantModuleSetting> TenantModuleSettings => Set<TenantModuleSetting>();
    public DbSet<UserDashboardLayout> UserDashboardLayouts => Set<UserDashboardLayout>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<FailureCode> FailureCodes => Set<FailureCode>();
    public DbSet<SlaMatrixCell> SlaMatrixCells => Set<SlaMatrixCell>();
    public DbSet<CheckSchedule> CheckSchedules => Set<CheckSchedule>();
    public DbSet<CheckScheduleAsset> CheckScheduleAssets => Set<CheckScheduleAsset>();
    public DbSet<CheckRun> CheckRuns => Set<CheckRun>();
    public DbSet<CheckRunAnswer> CheckRunAnswers => Set<CheckRunAnswer>();
    public DbSet<PmPlanRouteAsset> PmPlanRouteAssets => Set<PmPlanRouteAsset>();
    public DbSet<PmPlanEvent> PmPlanEvents => Set<PmPlanEvent>();
    public DbSet<MaintenanceWindow> MaintenanceWindows => Set<MaintenanceWindow>();
    public DbSet<MaintenanceWindowJob> MaintenanceWindowJobs => Set<MaintenanceWindowJob>();
    public DbSet<MaintenanceWindowHandbackItem> MaintenanceWindowHandbackItems => Set<MaintenanceWindowHandbackItem>();
    public DbSet<OverhaulPlan> OverhaulPlans => Set<OverhaulPlan>();
    public DbSet<OverhaulItem> OverhaulItems => Set<OverhaulItem>();
    public DbSet<ComplianceItem> ComplianceItems => Set<ComplianceItem>();
    public DbSet<ComplianceRecord> ComplianceRecords => Set<ComplianceRecord>();
    public DbSet<TechnicianSkill> TechnicianSkills => Set<TechnicianSkill>();
    public DbSet<TechnicianCertificate> TechnicianCertificates => Set<TechnicianCertificate>();
    public DbSet<RosterEntry> RosterEntries => Set<RosterEntry>();
    public DbSet<WorkPermit> WorkPermits => Set<WorkPermit>();
    public DbSet<WorkPermitCheck> WorkPermitChecks => Set<WorkPermitCheck>();
    public DbSet<IsolationPoint> IsolationPoints => Set<IsolationPoint>();
    public DbSet<LotoLock> LotoLocks => Set<LotoLock>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<TrainingRecord> TrainingRecords => Set<TrainingRecord>();
    public DbSet<PpeIssue> PpeIssues => Set<PpeIssue>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<StoreBin> StoreBins => Set<StoreBin>();
    public DbSet<StoreStock> StoreStocks => Set<StoreStock>();
    public DbSet<AssetPart> AssetParts => Set<AssetPart>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<SpareUnit> SpareUnits => Set<SpareUnit>();
    public DbSet<SpareUnitEvent> SpareUnitEvents => Set<SpareUnitEvent>();
    public DbSet<StoreTransfer> StoreTransfers => Set<StoreTransfer>();
    public DbSet<StoreCount> StoreCounts => Set<StoreCount>();
    public DbSet<StoreCountLine> StoreCountLines => Set<StoreCountLine>();
    public DbSet<PurchaseRequisition> PurchaseRequisitions => Set<PurchaseRequisition>();
    public DbSet<PurchaseRequisitionLine> PurchaseRequisitionLines => Set<PurchaseRequisitionLine>();
    public DbSet<VendorCatalogueItem> VendorCatalogueItems => Set<VendorCatalogueItem>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<ContractorDocument> ContractorDocuments => Set<ContractorDocument>();
    public DbSet<MaintenanceAgreement> MaintenanceAgreements => Set<MaintenanceAgreement>();
    public DbSet<MaintenanceAgreementAsset> MaintenanceAgreementAssets => Set<MaintenanceAgreementAsset>();
    public DbSet<ContractorInvoice> ContractorInvoices => Set<ContractorInvoice>();
    public DbSet<ContractorInvoiceLine> ContractorInvoiceLines => Set<ContractorInvoiceLine>();
    public DbSet<CostCentre> CostCentres => Set<CostCentre>();
    public DbSet<MaintenanceBudget> MaintenanceBudgets => Set<MaintenanceBudget>();
    public DbSet<ComplianceStandard> ComplianceStandards => Set<ComplianceStandard>();
    public DbSet<StandardRequirement> StandardRequirements => Set<StandardRequirement>();
    public DbSet<NonConformity> NonConformities => Set<NonConformity>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<WasteDisposal> WasteDisposals => Set<WasteDisposal>();
    public DbSet<FoodSafetyRecord> FoodSafetyRecords => Set<FoodSafetyRecord>();
    public DbSet<GlassRegisterItem> GlassRegisterItems => Set<GlassRegisterItem>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<JobPlan> JobPlans => Set<JobPlan>();
    public DbSet<JobPlanStep> JobPlanSteps => Set<JobPlanStep>();
    public DbSet<JobPlanResource> JobPlanResources => Set<JobPlanResource>();
    public DbSet<WorkOrderTemplate> WorkOrderTemplates => Set<WorkOrderTemplate>();
    public DbSet<MaterialItem> MaterialItems => Set<MaterialItem>();
    public DbSet<BranchMaterialStock> BranchMaterialStocks => Set<BranchMaterialStock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockCount> StockCounts => Set<StockCount>();
    public DbSet<Tool> Tools => Set<Tool>();
    public DbSet<ToolAssignment> ToolAssignments => Set<ToolAssignment>();
    public DbSet<ToolCalibration> ToolCalibrations => Set<ToolCalibration>();
    public DbSet<StockCountLine> StockCountLines => Set<StockCountLine>();
    public DbSet<HeldPart> HeldParts => Set<HeldPart>();
    public DbSet<DirectPurchase> DirectPurchases => Set<DirectPurchase>();
    public DbSet<MaterialRequest> MaterialRequests => Set<MaterialRequest>();
    public DbSet<MaterialRequestLine> MaterialRequestLines => Set<MaterialRequestLine>();
    public DbSet<PartsRequest> PartsRequests => Set<PartsRequest>();
    public DbSet<PartsRequestItem> PartsRequestItems => Set<PartsRequestItem>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<PermissionGroup> PermissionGroups => Set<PermissionGroup>();
    public DbSet<UserPermissionGroup> UserPermissionGroups => Set<UserPermissionGroup>();
    public DbSet<GroupCapability> GroupCapabilities => Set<GroupCapability>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<AccessScope> AccessScopes => Set<AccessScope>();
    public DbSet<PermissionThreshold> PermissionThresholds => Set<PermissionThreshold>();
    public DbSet<PreventiveMaintenancePlan> PreventiveMaintenancePlans => Set<PreventiveMaintenancePlan>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmailSetting> EmailSettings => Set<EmailSetting>();
    public DbSet<EmailDeliveryLog> EmailDeliveryLogs => Set<EmailDeliveryLog>();
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<TenantNotificationSetting> TenantNotificationSettings => Set<TenantNotificationSetting>();
    public DbSet<TenantNotificationRecipient> TenantNotificationRecipients => Set<TenantNotificationRecipient>();
    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<EmailIntakeSetting> EmailIntakeSettings => Set<EmailIntakeSetting>();
    public DbSet<EmailIntakeMessage> EmailIntakeMessages => Set<EmailIntakeMessage>();
    public DbSet<NumberingSetting> NumberingSettings => Set<NumberingSetting>();
    public DbSet<LicensePlan> LicensePlans => Set<LicensePlan>();
    public DbSet<LicenseAddOn> LicenseAddOns => Set<LicenseAddOn>();
    public DbSet<TenantLicense> TenantLicenses => Set<TenantLicense>();
    public DbSet<MrrSnapshot> MrrSnapshots => Set<MrrSnapshot>();
    public DbSet<AssignmentGroup> AssignmentGroups => Set<AssignmentGroup>();
    public DbSet<AssignmentGroupMember> AssignmentGroupMembers => Set<AssignmentGroupMember>();
    public DbSet<CertificationDefinition> CertificationDefinitions => Set<CertificationDefinition>();
    public DbSet<UserCertification> UserCertifications => Set<UserCertification>();
    public DbSet<WorkOrderAssignment> WorkOrderAssignments => Set<WorkOrderAssignment>();
    public DbSet<WorkOrderTechnicianAssignment> WorkOrderTechnicianAssignments => Set<WorkOrderTechnicianAssignment>();
    public DbSet<WorkOrderAssignmentHistory> WorkOrderAssignmentHistories => Set<WorkOrderAssignmentHistory>();
    public DbSet<WorkOrderEvent> WorkOrderEvents => Set<WorkOrderEvent>();
    public DbSet<WorkOrderNote> WorkOrderNotes => Set<WorkOrderNote>();
    public DbSet<WorkOrderChecklistItem> WorkOrderChecklistItems => Set<WorkOrderChecklistItem>();
    public DbSet<PmTemplate> PmTemplates => Set<PmTemplate>();
    public DbSet<PmTemplateQuestion> PmTemplateQuestions => Set<PmTemplateQuestion>();
    public DbSet<PmTemplateVersion> PmTemplateVersions => Set<PmTemplateVersion>();
    public DbSet<SystemTemplatePack> SystemTemplatePacks => Set<SystemTemplatePack>();
    public DbSet<TenantTemplatePackInstallation> TenantTemplatePackInstallations => Set<TenantTemplatePackInstallation>();
    public DbSet<PmReport> PmReports => Set<PmReport>();
    public DbSet<TenantSecurityPolicy> TenantSecurityPolicies => Set<TenantSecurityPolicy>();
    public DbSet<TenantOperationsPolicy> TenantOperationsPolicies => Set<TenantOperationsPolicy>();
    public DbSet<IntakeProtocol> IntakeProtocols => Set<IntakeProtocol>();
    public DbSet<MonitoringSetting> MonitoringSettings => Set<MonitoringSetting>();
    public DbSet<MonitoringWebhookIntegration> MonitoringWebhookIntegrations => Set<MonitoringWebhookIntegration>();
    public DbSet<TenantApiKey> TenantApiKeys => Set<TenantApiKey>();
    public DbSet<TenantWebhook> TenantWebhooks => Set<TenantWebhook>();
    public DbSet<TenantWebhookDelivery> TenantWebhookDeliveries => Set<TenantWebhookDelivery>();
    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
    public DbSet<PlatformQuotation> PlatformQuotations => Set<PlatformQuotation>();
    public DbSet<PlatformQuotationLine> PlatformQuotationLines => Set<PlatformQuotationLine>();
    public DbSet<PlatformInvoice> PlatformInvoices => Set<PlatformInvoice>();
    public DbSet<PlatformInvoiceLine> PlatformInvoiceLines => Set<PlatformInvoiceLine>();
    public DbSet<PlatformPayment> PlatformPayments => Set<PlatformPayment>();
    public DbSet<PlatformExpense> PlatformExpenses => Set<PlatformExpense>();
    public DbSet<PlatformDocumentTemplate> PlatformDocumentTemplates => Set<PlatformDocumentTemplate>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<PlatformAnnouncement> PlatformAnnouncements => Set<PlatformAnnouncement>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<WorkOrderPhotoEvidence> WorkOrderPhotoEvidence => Set<WorkOrderPhotoEvidence>();
    public DbSet<WorkOrderSignature> WorkOrderSignatures => Set<WorkOrderSignature>();
    public DbSet<WorkOrderMaterialUsage> WorkOrderMaterialUsages => Set<WorkOrderMaterialUsage>();
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<AssetCategoryField> AssetCategoryFields => Set<AssetCategoryField>();
    public DbSet<AssetCustomFieldValue> AssetCustomFieldValues => Set<AssetCustomFieldValue>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<PmTemplateSection> PmTemplateSections => Set<PmTemplateSection>();
    public DbSet<SlaDefinition> SlaDefinitions => Set<SlaDefinition>();
    public DbSet<SlaRule> SlaRules => Set<SlaRule>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStatus> WorkflowStatuses => Set<WorkflowStatus>();
    public DbSet<EscalationRule> EscalationRules => Set<EscalationRule>();
    public DbSet<EscalationFired> EscalationFired => Set<EscalationFired>();
    public DbSet<SlaEscalation> SlaEscalations => Set<SlaEscalation>();
    public DbSet<SlaRecord> SlaRecords => Set<SlaRecord>();
    public DbSet<EtaNotificationSettings> EtaNotificationSettings => Set<EtaNotificationSettings>();
    public DbSet<ClientPreferentialRate> ClientPreferentialRates => Set<ClientPreferentialRate>();
    public DbSet<FinanceApprovalRule> FinanceApprovalRules => Set<FinanceApprovalRule>();
    public DbSet<MaterialIssue> MaterialIssues => Set<MaterialIssue>();
    public DbSet<MaterialIssueLine> MaterialIssueLines => Set<MaterialIssueLine>();
    public DbSet<RateCard> RateCards => Set<RateCard>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractSite> ContractSites => Set<ContractSite>();
    public DbSet<ContractAsset> ContractAssets => Set<ContractAsset>();
    public DbSet<ContractAssetCategory> ContractAssetCategories => Set<ContractAssetCategory>();
    public DbSet<ContractWorkOrderType> ContractWorkOrderTypes => Set<ContractWorkOrderType>();
    public DbSet<ContractLifecycleEvent> ContractLifecycleEvents => Set<ContractLifecycleEvent>();
    public DbSet<ContractReminderRule> ContractReminderRules => Set<ContractReminderRule>();
    public DbSet<ContractRateCard> ContractRateCards => Set<ContractRateCard>();
    public DbSet<ContractRateRule> ContractRateRules => Set<ContractRateRule>();
    public DbSet<ContractPmEntitlement> ContractPmEntitlements => Set<ContractPmEntitlement>();
    public DbSet<ContractRenewal> ContractRenewals => Set<ContractRenewal>();
    public DbSet<WorkOrderTimeLog> WorkOrderTimeLogs => Set<WorkOrderTimeLog>();
    public DbSet<WorkOrderVisit> WorkOrderVisits => Set<WorkOrderVisit>();
    public DbSet<PartsReturn> PartsReturns => Set<PartsReturn>();
    public DbSet<PartsReturnLine> PartsReturnLines => Set<PartsReturnLine>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<ScheduledReport> ScheduledReports => Set<ScheduledReport>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityAttendee> ActivityAttendees => Set<ActivityAttendee>();
    public DbSet<ActivityMinutesAddendum> ActivityMinutesAddenda => Set<ActivityMinutesAddendum>();
    public DbSet<ActionItem> ActionItems => Set<ActionItem>();
    public DbSet<ActivityReminderLog> ActivityReminderLogs => Set<ActivityReminderLog>();
    public DbSet<CalendarFeedToken> CalendarFeedTokens => Set<CalendarFeedToken>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        NormalizeUtcFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditFields();
        NormalizeUtcFields();
        return base.SaveChanges();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Country).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Industry).HasMaxLength(100);
            entity.Property(x => x.ContactName).HasMaxLength(200);
            entity.Property(x => x.ContactEmail).HasMaxLength(256);
            entity.Property(x => x.ContactPhone).HasMaxLength(50);
            entity.Property(x => x.County).HasMaxLength(100);
            entity.Property(x => x.City).HasMaxLength(100);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.TaxPin).HasMaxLength(100);
            entity.Property(x => x.LegalName).HasMaxLength(200);
            entity.Property(x => x.TradingName).HasMaxLength(200);
            entity.Property(x => x.PostalAddress).HasMaxLength(500);
            entity.Property(x => x.PhysicalAddress).HasMaxLength(500);
            entity.Property(x => x.Website).HasMaxLength(300);
            entity.Property(x => x.VatNumber).HasMaxLength(100);
            entity.Property(x => x.RegistrationNumber).HasMaxLength(100);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PlanName).HasMaxLength(100);
            entity.Property(x => x.LicenseStatus).HasMaxLength(50);
            entity.Property(x => x.LogoUrl).HasMaxLength(500);
            entity.Property(x => x.PrimaryColor).HasMaxLength(20).IsRequired();
            entity.Property(x => x.SecondaryColor).HasMaxLength(20).IsRequired();
            entity.Property(x => x.SuspendedAt);
            entity.Property(x => x.DeactivatedAt);
            entity.Property(x => x.DeactivationReason).HasMaxLength(1000);
            entity.Property(x => x.SubscriptionStartsAt);
            entity.HasIndex(x => x.Slug).IsUnique();
        });

        modelBuilder.Entity<MrrSnapshot>(entity =>
        {
            entity.ToTable("mrr_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Mrr).HasPrecision(18, 2);
            entity.HasIndex(x => x.SnapshotDate).IsUnique();
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.ToTable("branches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(250);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.ContactPerson).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ParentBranchId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Branches)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ParentBranch)
                .WithMany(x => x.ChildBranches)
                .HasForeignKey(x => x.ParentBranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrgNode>(entity =>
        {
            entity.ToTable("org_nodes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Level).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Location).HasMaxLength(250);
            entity.Property(x => x.TemplateKey).HasMaxLength(50);
            entity.Property(x => x.ExternalId).HasMaxLength(100);
            entity.Property(x => x.ExternalSystem).HasMaxLength(50);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ParentId, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Level });
            entity.HasIndex(x => new { x.TenantId, x.UpdatedAt });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserOrgNodeAssignment>(entity =>
        {
            entity.ToTable("user_org_node_assignments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.OrgNodeId }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.OrgNode)
                .WithMany(x => x.UserAssignments)
                .HasForeignKey(x => x.OrgNodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FactorySettings>(entity =>
        {
            entity.ToTable("factory_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PmHoursPerTechnicianWeek).HasPrecision(6, 1).HasDefaultValue(30m);
            entity.Property(x => x.WindowCarryoverEscalateAt).HasDefaultValue(2);
            entity.Property(x => x.WindowCarryoverToOverhaulAt).HasDefaultValue(3);
            entity.Property(x => x.ProductionUnitValue).HasPrecision(18, 4);
            entity.Property(x => x.VatRatePercent).HasPrecision(5, 2).HasDefaultValue(16m);
            entity.Property(x => x.PeakSeasonMonths).HasMaxLength(40).HasDefaultValue("3,4,5,10,11,12");
            entity.Property(x => x.PersonalDataRetentionYears).HasDefaultValue(7);
            entity.Property(x => x.DataResidency).HasMaxLength(200);
            entity.Property(x => x.WalkInsNeedHostApproval).HasDefaultValue(true);
            entity.Property(x => x.VisitOverstayGraceMinutes).HasDefaultValue(30);
            entity.Property(x => x.VisitorRetentionMonths).HasDefaultValue(12);
            entity.Property(x => x.IndustryTemplate).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ProductionUnit).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ProductionUnitShort).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TagPrefix).HasMaxLength(10).IsRequired().HasDefaultValue("TAG");
            entity.Property(x => x.NextTagNumber).HasDefaultValue(1).IsConcurrencyToken();
            entity.Property(x => x.RequireWorkOrderApproval).HasDefaultValue(true);
            entity.Property(x => x.AutoApproveBreakdowns).HasDefaultValue(true);
            entity.Property(x => x.ReworkWindowDays).HasDefaultValue(30);
            entity.Property(x => x.OperatorReportingEnabled).HasDefaultValue(true);
            entity.Property(x => x.OperatorReportKey).HasMaxLength(64);
            entity.HasIndex(x => x.OperatorReportKey).IsUnique();
            entity.Property(x => x.ShiftsJson).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.HasEmail).HasDefaultValue(true);
            entity.Property(x => x.Username).HasMaxLength(100);
            entity.Property(x => x.PasswordHash).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.WhatsAppNumber).HasMaxLength(20);
            entity.Property(x => x.JobTitle).HasMaxLength(150);
            entity.Property(x => x.Department).HasMaxLength(150);
            entity.Property(x => x.InviteTokenHash).HasMaxLength(512);
            entity.Property(x => x.FailedLoginCount).HasDefaultValue(0);
            entity.HasIndex(x => x.Email).IsUnique();
            // Username is unique per tenant, not globally — two tenants may each have a "jsmith".
            // Filtered so accounts without a username (there shouldn't be any going forward, but
            // the column stays nullable for defensiveness) never collide on NULL.
            entity.HasIndex(x => new { x.TenantId, x.Username })
                .IsUnique()
                .HasFilter("\"Username\" IS NOT NULL");
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Permission)
                .WithOne(x => x.User)
                .HasForeignKey<UserPermission>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DefaultBranch)
                .WithMany(x => x.DefaultUsers)
                .HasForeignKey(x => x.DefaultBranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.TechnicianProfile)
                .WithOne(x => x.User)
                .HasForeignKey<Technician>(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.Certifications)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserBranchAssignment>(entity =>
        {
            entity.ToTable("user_branch_assignments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.BranchId }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.UserBranchAssignments)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany(x => x.BranchAssignments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.UserAssignments)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.JwtId).HasMaxLength(200);
            entity.Property(x => x.IpAddress).HasMaxLength(100);
            entity.Property(x => x.UserAgent).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.LoginAt });
            entity.HasIndex(x => x.JwtId);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.UserSessions)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PendingSignup>(entity =>
        {
            entity.ToTable("pending_signups");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Country).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Industry).HasMaxLength(100);
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.CompletedAt).IsConcurrencyToken();
            entity.Property(x => x.RequestedIp).HasMaxLength(100);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.Email);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Purpose).HasMaxLength(30).IsRequired();
            entity.Property(x => x.UsedAt).IsConcurrencyToken();
            entity.Property(x => x.SupersededAt).IsConcurrencyToken();
            entity.Property(x => x.RevokedAt).IsConcurrencyToken();
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.RequestedIp).HasMaxLength(100);
            entity.Property(x => x.UserAgent).HasMaxLength(1000);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ExpiresAt });
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApprovalMatrix>(entity =>
        {
            entity.ToTable("approval_matrices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(8);
            entity.HasIndex(x => new { x.TenantId, x.DocumentType }).IsUnique();
            entity.HasMany(x => x.Levels)
                .WithOne(x => x.Matrix)
                .HasForeignKey(x => x.ApprovalMatrixId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApprovalLevel>(entity =>
        {
            entity.ToTable("approval_levels");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.MinAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.MaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.ApproverType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.RequiredPermission).HasMaxLength(128);
            entity.Property(x => x.ApproverUserIdsCsv).HasMaxLength(2000);
            entity.HasIndex(x => new { x.ApprovalMatrixId, x.LevelOrder }).IsUnique();
        });

        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.ToTable("approval_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.DocumentNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Currency).HasMaxLength(8);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.DocumentType, x.DocumentId });
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasMany(x => x.Steps)
                .WithOne(x => x.Request)
                .HasForeignKey(x => x.ApprovalRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApprovalRequestStep>(entity =>
        {
            entity.ToTable("approval_request_steps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LevelName).HasMaxLength(120);
            entity.Property(x => x.ApproverType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.RequiredPermission).HasMaxLength(128);
            entity.Property(x => x.ApproverUserIdsCsv).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.DecidedByName).HasMaxLength(200);
            entity.Property(x => x.Note).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.Status });
        });

        modelBuilder.Entity<ClientPortalUser>(entity =>
        {
            entity.Property(x => x.WhatsAppNumber).HasMaxLength(20);
            entity.Property(x => x.WhatsAppConsentMethod).HasMaxLength(20);
            entity.ToTable("client_portal_users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ClientId });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client)
                .WithMany(x => x.PortalUsers)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("clients");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.IsProspect });
            entity.Property(x => x.ClientName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ClientType).HasMaxLength(100);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Location).HasMaxLength(250);
            entity.Property(x => x.ContactPerson).HasMaxLength(200);
            entity.Property(x => x.ContactPhone).HasMaxLength(50);
            entity.Property(x => x.SlaPlan).HasMaxLength(100);
            entity.Property(x => x.ActiveDurationUnit).HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.HasIndex(x => new { x.TenantId, x.ClientName }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Clients)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SlaDefinition)
                .WithMany(x => x.Clients)
                .HasForeignKey(x => x.SlaDefinitionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OperationalSystem>(entity =>
        {
            entity.ToTable("systems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SystemName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SystemType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Manufacturer).HasMaxLength(150);
            entity.Property(x => x.Model).HasMaxLength(150);
            entity.Property(x => x.SerialNumber).HasMaxLength(150);
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.HasIndex(x => new { x.TenantId, x.SiteId, x.SystemName }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Systems)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client)
                .WithMany(x => x.Systems)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Site)
                .WithMany(x => x.Systems)
                .HasForeignKey(x => x.SiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("assets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AssetName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.AssetCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AssetType).HasMaxLength(100);
            entity.Property(x => x.OwnershipType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(250);
            entity.Property(x => x.BuildingBlock).HasMaxLength(100);
            entity.Property(x => x.FloorLevel).HasMaxLength(100);
            entity.Property(x => x.RoomArea).HasMaxLength(100);
            entity.Property(x => x.LocationDescription).HasMaxLength(500);
            entity.Property(x => x.SerialNumber).HasMaxLength(150);
            entity.Property(x => x.Manufacturer).HasMaxLength(150);
            entity.Property(x => x.Model).HasMaxLength(150);
            entity.Property(x => x.CapacityRating).HasMaxLength(100);
            entity.Property(x => x.PhysicalDescription).HasMaxLength(2000);
            entity.Property(x => x.DecommissionReason).HasMaxLength(500);
            entity.Property(x => x.RecommendedPmFrequency).HasMaxLength(100);
            entity.Property(x => x.PmFrequencyUnit).HasMaxLength(30);
            entity.Property(x => x.MeterLabel).HasMaxLength(100);
            entity.Property(x => x.CurrentMeterReading).HasPrecision(18, 4);
            entity.Property(x => x.MeterInterval).HasPrecision(18, 4);
            entity.Property(x => x.MeterBuffer).HasPrecision(18, 4);
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.AssetCode }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Client)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Site)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.SiteId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.System)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.SystemId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AssetCategory)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.AssetCategoryId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DefaultAssignmentGroup)
                .WithMany()
                .HasForeignKey(x => x.DefaultAssignmentGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.OrgNode)
                .WithMany()
                .HasForeignKey(x => x.OrgNodeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.OrgNodeId });
            entity.Property(x => x.Criticality).HasMaxLength(1);
            entity.Property(x => x.OperationalStatus).HasMaxLength(30).IsRequired().HasDefaultValue(AssetOperationalStatuses.Operational);
            entity.Property(x => x.LostProductionPerHour).HasPrecision(18, 4);
            entity.Property(x => x.DisposalMethod).HasMaxLength(200);
            entity.Property(x => x.ExternalId).HasMaxLength(100);
            entity.Property(x => x.ExternalSystem).HasMaxLength(50);
            entity.HasIndex(x => new { x.TenantId, x.ExternalSystem, x.ExternalId });
            entity.HasIndex(x => new { x.TenantId, x.UpdatedAt });
            entity.HasOne(x => x.ParentAsset)
                .WithMany(x => x.Components)
                .HasForeignKey(x => x.ParentAssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CheckSchedule>(entity =>
        {
            entity.ToTable("check_schedules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Frequency).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.OrgNodeId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PmTemplate).WithMany().HasForeignKey(x => x.PmTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OrgNode).WithMany().HasForeignKey(x => x.OrgNodeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CheckScheduleAsset>(entity =>
        {
            entity.ToTable("check_schedule_assets");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CheckScheduleId, x.AssetId }).IsUnique();
            entity.HasOne(x => x.CheckSchedule).WithMany(x => x.Assets).HasForeignKey(x => x.CheckScheduleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CheckRun>(entity =>
        {
            entity.ToTable("check_runs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PeriodKey).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ShiftName).HasMaxLength(40);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CompletedByName).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            // One check per schedule, asset and shift, however many times the list is generated.
            entity.HasIndex(x => new { x.CheckScheduleId, x.AssetId, x.PeriodKey }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status, x.DueBy });
            entity.HasIndex(x => new { x.TenantId, x.OrgNodeId, x.DueFrom });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CheckSchedule).WithMany().HasForeignKey(x => x.CheckScheduleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CheckRunAnswer>(entity =>
        {
            entity.ToTable("check_run_answers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SectionName).HasMaxLength(200);
            entity.Property(x => x.Prompt).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ResponseType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(30);
            entity.Property(x => x.Value).HasMaxLength(1000);
            entity.Property(x => x.NumberValue).HasPrecision(18, 4);
            entity.Property(x => x.MinValue).HasPrecision(18, 4);
            entity.Property(x => x.MaxValue).HasPrecision(18, 4);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.FailAction).HasMaxLength(20).IsRequired();
            entity.HasOne(x => x.CheckRun).WithMany(x => x.Answers).HasForeignKey(x => x.CheckRunId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SlaMatrixCell>(entity =>
        {
            entity.ToTable("sla_matrix_cells");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Priority).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Criticality).HasMaxLength(1).IsRequired();
            entity.Property(x => x.ResponseHours).HasPrecision(9, 2);
            entity.Property(x => x.RepairHours).HasPrecision(9, 2);
            entity.HasIndex(x => new { x.TenantId, x.Priority, x.Criticality }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobPlan>(entity =>
        {
            entity.ToTable("job_plans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequiredPermitTypes).HasMaxLength(200);
            entity.HasOne<CertificationDefinition>().WithMany().HasForeignKey(x => x.RequiredCertificationId).OnDelete(DeleteBehavior.SetNull);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.WorkType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.EstimatedHours).HasPrecision(9, 2);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AssetCategory).WithMany().HasForeignKey(x => x.AssetCategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<JobPlanStep>(entity =>
        {
            entity.ToTable("job_plan_steps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Instruction).HasMaxLength(1000).IsRequired();
            entity.HasOne(x => x.JobPlan).WithMany(x => x.Steps).HasForeignKey(x => x.JobPlanId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobPlanResource>(entity =>
        {
            entity.ToTable("job_plan_resources");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 4);
            entity.HasOne(x => x.JobPlan).WithMany(x => x.Resources).HasForeignKey(x => x.JobPlanId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FailureCode>(entity =>
        {
            entity.ToTable("failure_codes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.Kind, x.Code }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AssetCategory).WithMany().HasForeignKey(x => x.AssetCategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AssetDocument>(entity =>
        {
            entity.ToTable("asset_documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DocumentType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.AssetId });
            entity.HasIndex(x => new { x.TenantId, x.UpdatedAt });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssetDocumentVersion>(entity =>
        {
            entity.ToTable("asset_document_versions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.UploadedByName).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantId, x.DocumentId, x.VersionNumber }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Document).WithMany(x => x.Versions).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssetTag>(entity =>
        {
            entity.ToTable("asset_tags");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TagNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Symbology).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RetiredReason).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.TagNumber }).IsUnique();
            // One active tag per asset, enforced by the database as well as the API.
            entity.HasIndex(x => new { x.TenantId, x.AssetId })
                .IsUnique()
                .HasFilter("\"Status\" = 'Active'")
                .HasDatabaseName("IX_asset_tags_TenantId_AssetId_Active");
            entity.HasIndex(x => new { x.TenantId, x.UpdatedAt });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Meter>(entity =>
        {
            entity.ToTable("meters");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.MeterType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(30).IsRequired();
            entity.Property(x => x.CurrentValue).HasPrecision(18, 4);
            entity.Property(x => x.MinThreshold).HasPrecision(18, 4);
            entity.Property(x => x.MaxThreshold).HasPrecision(18, 4);
            entity.Property(x => x.WarningMin).HasPrecision(18, 4);
            entity.Property(x => x.WarningMax).HasPrecision(18, 4);
            entity.Property(x => x.ConditionAction).HasMaxLength(20).IsRequired().HasDefaultValue(ConditionActions.Alert);
            entity.Property(x => x.TriggerAfterReadings).HasDefaultValue(1);
            entity.Property(x => x.AlarmPriority).HasMaxLength(20).IsRequired().HasDefaultValue("High");
            entity.Property(x => x.ConditionState).HasMaxLength(20).IsRequired().HasDefaultValue(ConditionLevels.Normal);
            entity.HasOne<JobPlan>().WithMany().HasForeignKey(x => x.AlarmJobPlanId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.ConditionState });
            entity.Property(x => x.ExternalId).HasMaxLength(100);
            entity.Property(x => x.ExternalSystem).HasMaxLength(50);
            entity.HasIndex(x => new { x.TenantId, x.AssetId, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.UpdatedAt });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VehicleProfile>(entity =>
        {
            entity.ToTable("vehicle_profiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RegistrationNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.VehicleType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Make).HasMaxLength(100);
            entity.Property(x => x.VehicleModel).HasMaxLength(100);
            entity.Property(x => x.PayloadKg).HasPrecision(18, 2);
            entity.Property(x => x.FuelType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Route).HasMaxLength(500);
            entity.Property(x => x.DriverName).HasMaxLength(150);
            entity.Property(x => x.DriverPhone).HasMaxLength(30);
            entity.Property(x => x.TrackerDeviceId).HasMaxLength(100);
            entity.Property(x => x.LastSpeedKph).HasPrecision(8, 1);
            entity.Property(x => x.LastPositionSource).HasMaxLength(150);
            entity.HasIndex(x => x.AssetId).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.RegistrationNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.TrackerDeviceId }).IsUnique().HasFilter("\"TrackerDeviceId\" IS NOT NULL");
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.OdometerMeter).WithMany().HasForeignKey(x => x.OdometerMeterId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.EngineHoursMeter).WithMany().HasForeignKey(x => x.EngineHoursMeterId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FuelLog>(entity =>
        {
            entity.ToTable("fuel_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Litres).HasPrecision(10, 2);
            entity.Property(x => x.Cost).HasPrecision(18, 2);
            entity.Property(x => x.OdometerKm).HasPrecision(18, 1);
            entity.Property(x => x.Station).HasMaxLength(150);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.RecordedByName).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantId, x.AssetId, x.FilledAt });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessZone>(entity =>
        {
            entity.ToTable("access_zones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Level).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RequiredPpe).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.Name }).IsUnique();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OrgNode).WithMany().HasForeignKey(x => x.OrgNodeId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Visitor>(entity =>
        {
            entity.ToTable("visitors");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Company).HasMaxLength(200);
            entity.Property(x => x.IdType).HasMaxLength(30);
            entity.Property(x => x.IdNumberMasked).HasMaxLength(20);
            entity.Property(x => x.IdNumberHash).HasMaxLength(64);
            entity.Property(x => x.BlockedReason).HasMaxLength(500);
            entity.Property(x => x.BlockedByName).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantId, x.Phone });
            entity.HasIndex(x => new { x.TenantId, x.IdNumberHash });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contractor).WithMany().HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Visit>(entity =>
        {
            entity.ToTable("visits");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.VisitNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(30).IsRequired();
            entity.Property(x => x.HostName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(20).IsRequired();
            entity.Property(x => x.VehicleRegistration).HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.CreatedByName).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.PassId, x.ExpectedFrom });
            entity.HasOne<VisitorPass>().WithMany().HasForeignKey(x => x.PassId).OnDelete(DeleteBehavior.SetNull);
            entity.Property(x => x.DecidedByName).HasMaxLength(200);
            entity.Property(x => x.DecisionNote).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.VisitNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.Status, x.ExpectedFrom });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.HostUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.WorkPermit).WithMany().HasForeignKey(x => x.WorkPermitId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Contractor).WithMany().HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VisitAttendee>(entity =>
        {
            entity.ToTable("visit_attendees");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BadgeNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.BadgeToken).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CheckedInByName).HasMaxLength(200);
            entity.Property(x => x.CheckedOutByName).HasMaxLength(200);
            entity.Property(x => x.EscortName).HasMaxLength(200);
            entity.Property(x => x.OverrideReason).HasMaxLength(500);
            entity.Property(x => x.OverriddenByName).HasMaxLength(200);
            entity.Property(x => x.AccountedForByName).HasMaxLength(200);
            entity.Property(x => x.CardNumber).HasMaxLength(30);
            entity.Property(x => x.CardIssuedByName).HasMaxLength(200);
            entity.HasIndex(x => x.CardId);
            entity.HasOne(x => x.Card).WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.BadgeToken).IsUnique();
            entity.HasIndex(x => new { x.VisitId, x.VisitorId }).IsUnique();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Visit).WithMany(x => x.Attendees).HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Visitor).WithMany().HasForeignKey(x => x.VisitorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Checkpoint>(entity =>
        {
            entity.ToTable("checkpoints");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(10).IsRequired();
            entity.Property(x => x.TagToken).HasMaxLength(64).IsRequired();
            entity.Property(x => x.NfcSerial).HasMaxLength(64);
            entity.Property(x => x.Instructions).HasMaxLength(1000);
            entity.Property(x => x.Questions).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => x.TagToken).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.NfcSerial }).IsUnique().HasFilter("\"NfcSerial\" IS NOT NULL");
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AccessZone).WithMany().HasForeignKey(x => x.AccessZoneId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PatrolRoute>(entity =>
        {
            entity.ToTable("patrol_routes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.Name }).IsUnique();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PatrolRouteCheckpoint>(entity =>
        {
            entity.ToTable("patrol_route_checkpoints");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.RouteId, x.CheckpointId }).IsUnique();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Route).WithMany(x => x.Checkpoints).HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Checkpoint).WithMany().HasForeignKey(x => x.CheckpointId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PatrolSchedule>(entity =>
        {
            entity.ToTable("patrol_schedules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FixedTimes).HasMaxLength(200);
            entity.Property(x => x.Days).HasMaxLength(20).IsRequired();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Route).WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PatrolRun>(entity =>
        {
            entity.ToTable("patrol_runs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GuardName).HasMaxLength(200);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.ScheduleId, x.DueFrom }).IsUnique().HasFilter("\"ScheduleId\" IS NOT NULL");
            entity.HasIndex(x => new { x.TenantId, x.Status, x.CloseAt });
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.DueFrom });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Route).WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<PatrolSchedule>().WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CheckpointScan>(entity =>
        {
            entity.ToTable("checkpoint_scans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GuardName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.DeviceId).HasMaxLength(100);
            entity.Property(x => x.Method).HasMaxLength(10).IsRequired();
            entity.Property(x => x.NfcSerial).HasMaxLength(64);
            entity.Property(x => x.Validation).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ValidationNote).HasMaxLength(500);
            entity.Property(x => x.AnswersJson).HasMaxLength(4000);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.ClientScanId }).IsUnique();
            entity.HasIndex(x => new { x.RunId, x.ScannedAt });
            entity.HasIndex(x => new { x.TenantId, x.CheckpointId, x.ScannedAt });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Run).WithMany(x => x.Scans).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Checkpoint).WithMany().HasForeignKey(x => x.CheckpointId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShiftHandover>(entity =>
        {
            entity.ToTable("shift_handovers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ShiftName).HasMaxLength(50);
            entity.Property(x => x.OutgoingName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.IncomingName).HasMaxLength(200);
            entity.Property(x => x.OpenIssues).HasMaxLength(4000);
            entity.Property(x => x.KeysAndEquipment).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.HandedOverAt });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VisitorCard>(entity =>
        {
            entity.ToTable("visitor_cards");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CardNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Colour).HasMaxLength(50);
            entity.Property(x => x.Token).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ChipSerial).HasMaxLength(64);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.CardNumber }).IsUnique();
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ChipSerial }).IsUnique().HasFilter("\"ChipSerial\" IS NOT NULL");
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.Status });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VehicleMovement>(entity =>
        {
            entity.ToTable("vehicle_movements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Registration).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(200);
            entity.Property(x => x.DriverName).HasMaxLength(200);
            entity.Property(x => x.DriverPhone).HasMaxLength(30);
            entity.Property(x => x.EnteredByName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ExitedByName).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.ExitNotes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.ExitedAt });
            entity.HasIndex(x => new { x.TenantId, x.Registration, x.EnteredAt });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Visit).WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VisitorPass>(entity =>
        {
            entity.ToTable("visitor_passes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PassNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.HostName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Days).HasMaxLength(20).IsRequired();
            entity.Property(x => x.BadgeToken).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.VehicleRegistration).HasMaxLength(20);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.IssuedByName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.RevokedByName).HasMaxLength(200);
            entity.Property(x => x.RevokedReason).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.PassNumber }).IsUnique();
            entity.HasIndex(x => x.BadgeToken).IsUnique();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Visitor).WithMany().HasForeignKey(x => x.VisitorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contractor).WithMany().HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.HostUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VisitorPassZone>(entity =>
        {
            entity.ToTable("visitor_pass_zones");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PassId, x.AccessZoneId }).IsUnique();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Pass).WithMany(x => x.Zones).HasForeignKey(x => x.PassId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AccessZone).WithMany().HasForeignKey(x => x.AccessZoneId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VisitZone>(entity =>
        {
            entity.ToTable("visit_zones");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.VisitId, x.AccessZoneId }).IsUnique();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Visit).WithMany(x => x.Zones).HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AccessZone).WithMany().HasForeignKey(x => x.AccessZoneId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TelematicsConnection>(entity =>
        {
            entity.ToTable("telematics_connections");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(30).IsRequired();
            entity.Property(x => x.BaseUrl).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(200);
            entity.Property(x => x.EncryptedPassword).HasMaxLength(4000);
            entity.Property(x => x.LastSyncError).HasMaxLength(1000);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SensorGateway>(entity =>
        {
            entity.ToTable("sensor_gateways");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.KeyPrefix).HasMaxLength(20).IsRequired();
            entity.Property(x => x.KeyHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(500);
            entity.HasIndex(x => x.KeyPrefix);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.OrgNode).WithMany().HasForeignKey(x => x.OrgNodeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ConditionEvent>(entity =>
        {
            entity.ToTable("condition_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Level).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Direction).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.PeakValue).HasPrecision(18, 4);
            entity.Property(x => x.AcknowledgedByName).HasMaxLength(200);
            entity.Property(x => x.AcknowledgementNote).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.Status, x.StartedAt });
            entity.HasIndex(x => new { x.TenantId, x.MeterId, x.StartedAt });
            // At most one open excursion per meter.
            entity.HasIndex(x => x.MeterId).IsUnique().HasFilter("\"Status\" = 'Open'");
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Meter).WithMany().HasForeignKey(x => x.MeterId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MeterReading>(entity =>
        {
            entity.ToTable("meter_readings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Value).HasPrecision(18, 4);
            entity.Property(x => x.Delta).HasPrecision(18, 4);
            entity.Property(x => x.Source).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Condition).HasMaxLength(20).IsRequired().HasDefaultValue(ConditionLevels.Normal);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.RecordedByName).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantId, x.MeterId, x.ReadAt });
            entity.HasIndex(x => new { x.TenantId, x.AssetId, x.ReadAt });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Meter).WithMany(x => x.Readings).HasForeignKey(x => x.MeterId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AssetLifecycleEvent>(entity =>
        {
            entity.ToTable("asset_lifecycle_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.FromValue).HasMaxLength(200);
            entity.Property(x => x.ToValue).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.PerformedByName).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantId, x.AssetId, x.OccurredAt });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset)
                .WithMany(x => x.LifecycleEvents)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Technician>(entity =>
        {
            entity.ToTable("technicians");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Trade).HasMaxLength(50);
            entity.Property(x => x.HourlyRate).HasPrecision(18, 2);
            entity.Property(x => x.OvertimeMultiplier).HasPrecision(4, 2).HasDefaultValue(1.5m);
            entity.HasOne<OrgNode>().WithMany().HasForeignKey(x => x.HomeOrgNodeId).OnDelete(DeleteBehavior.SetNull);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.SkillCategory).HasMaxLength(100);
            entity.Property(x => x.AssignmentGroup).HasMaxLength(100);
            entity.Property(x => x.AvailabilityStatus).HasMaxLength(40).IsRequired().HasDefaultValue("Available");
            entity.Property(x => x.AvailabilityNote).HasMaxLength(500);
            entity.Property(x => x.LastKnownLatitude).HasPrecision(9, 6);
            entity.Property(x => x.LastKnownLongitude).HasPrecision(9, 6);
            entity.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.Technicians)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithOne(x => x.TechnicianProfile)
                .HasForeignKey<Technician>(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.Technicians)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ActiveWorkOrder)
                .WithMany()
                .HasForeignKey(x => x.ActiveWorkOrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CertificationDefinition>(entity =>
        {
            entity.ToTable("certification_definitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Issuer).HasMaxLength(160);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserCertification>(entity =>
        {
            entity.ToTable("user_certifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CertificateNumber).HasMaxLength(120);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.CertificationDefinitionId }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany(x => x.Certifications)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CertificationDefinition)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.CertificationDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.Property(x => x.RequestSource).HasMaxLength(30);
            entity.Property(x => x.RequestedByName).HasMaxLength(200);
            entity.Property(x => x.VerificationNotes).HasMaxLength(2000);
            entity.Property(x => x.RequiredPermitTypes).HasMaxLength(200);
            entity.HasOne<CertificationDefinition>().WithMany().HasForeignKey(x => x.RequiredCertificationId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Contractor>().WithMany().HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<MaintenanceAgreement>().WithMany().HasForeignKey(x => x.MaintenanceAgreementId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.OrgNode).WithMany().HasForeignKey(x => x.OrgNodeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.FailureProblemCode).WithMany().HasForeignKey(x => x.FailureProblemCodeId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.FailureCauseCode).WithMany().HasForeignKey(x => x.FailureCauseCodeId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.FailureRemedyCode).WithMany().HasForeignKey(x => x.FailureRemedyCodeId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.ReworkOfWorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.JobPlan).WithMany().HasForeignKey(x => x.JobPlanId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.OrgNodeId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.AssetId, x.FailureProblemCodeId, x.CompletedAt });
            entity.ToTable("work_orders", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("CK_work_orders_assignment_type", "\"AssignmentType\" IN ('Unassigned', 'IndividualTechnician', 'MultipleTechnicians', 'AssignmentGroup')");
                tableBuilder.HasCheckConstraint("CK_work_orders_priority", "\"Priority\" IN ('Low', 'Medium', 'High', 'Critical')");
                tableBuilder.HasCheckConstraint("CK_work_orders_status", "\"Status\" IN ('New', 'Open', 'Assigned', 'Scheduled', 'In Progress', 'On Hold', 'Waiting on Parts', 'Waiting on Client', 'Completed', 'Client Accepted', 'Closed', 'Cancelled', 'Rejected', 'Reopened', 'Requested', 'Approved', 'Planned', 'Verified')");
                // The service work types, then the factory ones (FactoryWorkTypes).
                tableBuilder.HasCheckConstraint("CK_work_orders_work_type", "\"WorkType\" IN ('NewInstallation', 'PreventiveMaintenance', 'CorrectiveMaintenance', 'InspectionAssessment', 'Corrective', 'Preventive', 'Inspection', 'Statutory', 'Modification', 'Safety')");
                tableBuilder.HasCheckConstraint("CK_work_orders_hold_reason", "\"HoldReason\" IS NULL OR \"HoldReason\" IN ('Awaiting Materials', 'Awaiting Client', 'Awaiting Vendor', 'Awaiting Approval', 'Awaiting Site Access', 'Awaiting Spares', 'Other', 'Waiting for Window', 'Waiting for Shutdown', 'Waiting for Contractor')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AssignmentType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.AssignedTechnicianIdsJson).HasMaxLength(4000);
            entity.Property(x => x.WorkOrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.Priority).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.WorkType).HasMaxLength(50).IsRequired().HasDefaultValue(WorkOrderTypes.CorrectiveMaintenance);
            entity.Property(x => x.ServiceType).HasMaxLength(100);
            entity.Property(x => x.EstimatedDuration).HasPrecision(10, 2);
            entity.Property(x => x.ActualDuration).HasPrecision(10, 2);
            entity.Property(x => x.HoldReason).HasMaxLength(100);
            entity.Property(x => x.HoldNote).HasMaxLength(4000);
            entity.Property(x => x.HoldStartedAt);
            entity.Property(x => x.HoldEndedAt);
            entity.Property(x => x.WorkDoneNotes).HasMaxLength(4000);
            entity.Property(x => x.JobCardNotes).HasMaxLength(4000);
            entity.Property(x => x.AcknowledgedByName).HasMaxLength(200);
            entity.Property(x => x.AcknowledgementComments).HasMaxLength(4000);
            entity.Property(x => x.ReportStatus).HasMaxLength(30);
            entity.Property(x => x.ReportNumber).HasMaxLength(50);
            entity.Property(x => x.ReportProblemText).HasMaxLength(4000);
            entity.Property(x => x.ReportFindingsText).HasMaxLength(4000);
            entity.Property(x => x.ReportWorkDoneText).HasMaxLength(4000);
            entity.Property(x => x.PmTemplateNameSnapshot).HasMaxLength(150);
            entity.Property(x => x.AppliedSlaSource).HasMaxLength(80);
            entity.Property(x => x.AppliedRateSource).HasMaxLength(80);
            entity.Property(x => x.BillingModel).HasMaxLength(80);
            entity.Property(x => x.CoverageStatus).HasMaxLength(40);
            entity.Property(x => x.PricingStatus).HasMaxLength(40);
            entity.Property(x => x.AppliedResponseHours).HasPrecision(10, 2);
            entity.Property(x => x.AppliedResolutionHours).HasPrecision(10, 2);
            entity.Property(x => x.EstimatedLabourHours).HasPrecision(10, 2);
            entity.Property(x => x.EstimatedMaterialCost).HasPrecision(18, 2);
            entity.Property(x => x.EstimatedCharge).HasPrecision(18, 2);
            entity.Property(x => x.FinalCharge).HasPrecision(18, 2);
            entity.Property(x => x.PricingNotes).HasMaxLength(4000);
            entity.Property(x => x.ContractSnapshotJson);
            entity.Property(x => x.SlaSnapshotJson);
            entity.Property(x => x.PricingSnapshotJson);
            entity.Property(x => x.SlaResponseDeadline);
            entity.Property(x => x.SlaResolutionDeadline);
            entity.Property(x => x.SlaResponseBreached);
            entity.Property(x => x.SlaResolutionBreached);
            entity.Property(x => x.SlaResponseBreachedAt);
            entity.Property(x => x.SlaResolutionBreachedAt);
            entity.Property(x => x.TrackingToken).HasMaxLength(40);
            entity.Property(x => x.IsRepeatVisit).HasDefaultValue(false);
            entity.Property(x => x.RepeatVisitCount).HasDefaultValue(0);
            entity.Property(x => x.PlannedMaterialsJson);
            entity.Property(x => x.PreviousStatus).HasMaxLength(50);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderNumber }).IsUnique();
            // The work order list's sorts and filters.
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.DueDate });
            entity.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.ScheduledStartAt }).HasFilter("\"ScheduledStartAt\" IS NOT NULL");
            entity.HasIndex(x => x.TrackingToken).IsUnique().HasFilter("\"TrackingToken\" IS NOT NULL");
            entity.HasIndex(x => x.ParentWorkOrderId);
            entity.HasIndex(x => x.FollowUpOfWorkOrderId);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Client)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Asset)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AssignedTechnician)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.AssignedTechnicianId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.LeadTechnician)
                .WithMany()
                .HasForeignKey(x => x.LeadTechnicianId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Site)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.SiteId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.System)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.SystemId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Contract)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.ContractId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.RateCard)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.RateCardId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ClosedByUser)
                .WithMany()
                .HasForeignKey(x => x.ClosedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AssignmentGroup)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.AssignmentGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.PreventiveMaintenancePlan)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.PreventiveMaintenancePlanId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.PmTemplate)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.PmTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.WorkOrderTemplate)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.WorkOrderTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ParentWorkOrder)
                .WithMany(x => x.ChildWorkOrders)
                .HasForeignKey(x => x.ParentWorkOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.FollowUpOfWorkOrder)
                .WithMany(x => x.FollowUpWorkOrders)
                .HasForeignKey(x => x.FollowUpOfWorkOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Assignments)
                .WithOne(x => x.WorkOrder)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.TechnicianAssignments)
                .WithOne(x => x.WorkOrder)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.AssignmentHistory)
                .WithOne(x => x.WorkOrder)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.ChecklistItems)
                .WithOne(x => x.WorkOrder)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.PhotoEvidence)
                .WithOne(x => x.WorkOrder)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Signatures)
                .WithOne(x => x.WorkOrder)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.MaterialUsages)
                .WithOne(x => x.WorkOrder)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkflowStatus)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.WorkflowStatusId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkOrderTemplate>(entity =>
        {
            entity.ToTable("work_order_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.JobType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.DefaultPriority).HasMaxLength(50).IsRequired();
            entity.Property(x => x.RequiredSkillsJson);
            entity.Property(x => x.DefaultMaterialsJson);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ChecklistTemplate)
                .WithMany()
                .HasForeignKey(x => x.ChecklistTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkflowDefinition>(entity =>
        {
            entity.ToTable("workflow_definitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowStatus>(entity =>
        {
            entity.ToTable("workflow_statuses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Color).HasMaxLength(20).IsRequired();
            entity.Property(x => x.AllowedNextStatusIdsJson).HasMaxLength(4000);
            entity.HasIndex(x => new { x.WorkflowDefinitionId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkflowDefinition)
                .WithMany(x => x.Statuses)
                .HasForeignKey(x => x.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EscalationRule>(entity =>
        {
            entity.ToTable("escalation_rules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.TriggerCondition).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ActionValue).HasMaxLength(100);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EscalationFired>(entity =>
        {
            entity.ToTable("escalation_fired");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.RuleId, x.WorkOrderId, x.FiredAt });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Rule)
                .WithMany(x => x.FiredEvents)
                .HasForeignKey(x => x.RuleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany()
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SlaDefinition>(entity =>
        {
            entity.ToTable("sla_definitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlanName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.PlanName }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SlaRule>(entity =>
        {
            entity.ToTable("sla_rules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Priority).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ResponseTargetHours).HasPrecision(10, 2);
            entity.Property(x => x.ResolutionTargetHours).HasPrecision(10, 2);
            entity.HasIndex(x => new { x.SlaDefinitionId, x.Priority }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SlaDefinition)
                .WithMany(x => x.Rules)
                .HasForeignKey(x => x.SlaDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SlaEscalation>(entity =>
        {
            entity.ToTable("sla_escalations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NotifyRoles).HasMaxLength(400);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SlaRule)
                .WithMany(x => x.Escalations)
                .HasForeignKey(x => x.SlaRuleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.EscalateToUser)
                .WithMany()
                .HasForeignKey(x => x.EscalateToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SlaRecord>(entity =>
        {
            entity.ToTable("sla_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PauseReason).HasMaxLength(200);
            entity.HasIndex(x => x.WorkOrderId).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany()
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SlaRule)
                .WithMany()
                .HasForeignKey(x => x.SlaRuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EtaNotificationSettings>(entity =>
        {
            entity.ToTable("eta_notification_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.SubjectTemplate).HasMaxLength(300).IsRequired();
            entity.Property(x => x.BodyTemplate).HasMaxLength(4000).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.EventType }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MaterialItem>(entity =>
        {
            entity.ToTable("material_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ItemCode).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(50).IsRequired();
            entity.Property(x => x.QuantityOnHand).HasPrecision(18, 2);
            entity.Property(x => x.ReorderLevel).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.Barcode).HasMaxLength(100);
            entity.Property(x => x.ReorderQuantity).HasPrecision(18, 2);
            entity.Property(x => x.PurchaseUnitOfMeasure).HasMaxLength(50);
            entity.Property(x => x.PurchaseUnitFactor).HasPrecision(18, 4).HasDefaultValue(1m);
            entity.Property(x => x.ExternalId).HasMaxLength(100);
            entity.Property(x => x.ExternalSystem).HasMaxLength(50);
            entity.Property(x => x.ChemicalKind).HasMaxLength(50);
            entity.Property(x => x.HazardClass).HasMaxLength(200);
            entity.Property(x => x.FoodGradeClass).HasMaxLength(10);
            entity.HasOne<Attachment>().WithMany().HasForeignKey(x => x.SdsAttachmentId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.ItemCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Barcode });
            entity.HasOne(x => x.PreferredSupplier)
                .WithMany()
                .HasForeignKey(x => x.PreferredSupplierId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.MaterialItems)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BranchMaterialStock>(entity =>
        {
            entity.ToTable("branch_material_stocks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuantityOnHand).HasPrecision(18, 2);
            entity.Property(x => x.ReorderLevel).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.MaterialId }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.BranchMaterialStocks)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.MaterialStocks)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Material)
                .WithMany(x => x.BranchStocks)
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MovementType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.QuantityChange).HasPrecision(18, 2);
            entity.Property(x => x.BatchOrSerial).HasMaxLength(500);
            entity.Property(x => x.BalanceAfter).HasPrecision(18, 2);
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.ReferenceNumber).HasMaxLength(100);
            entity.Property(x => x.UnitCost).HasPrecision(18, 4);
            entity.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.StoreId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.MaterialId, x.CreatedAt });
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.StockMovements)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.StockMovements)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Material)
                .WithMany(x => x.StockMovements)
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany()
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.MaterialRequest)
                .WithMany()
                .HasForeignKey(x => x.MaterialRequestId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StockTransfer>(entity =>
        {
            entity.ToTable("stock_transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.ReferenceNumber).HasMaxLength(100);
            entity.HasIndex(x => new { x.TenantId, x.ReferenceNumber }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.StockTransfers)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FromBranch)
                .WithMany(x => x.OutgoingTransfers)
                .HasForeignKey(x => x.FromBranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ToBranch)
                .WithMany(x => x.IncomingTransfers)
                .HasForeignKey(x => x.ToBranchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Material)
                .WithMany(x => x.StockTransfers)
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RequestedByUser)
                .WithMany()
                .HasForeignKey(x => x.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ApprovedByUser)
                .WithMany()
                .HasForeignKey(x => x.ApprovedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CompletedByUser)
                .WithMany()
                .HasForeignKey(x => x.CompletedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MaterialRequest>(entity =>
        {
            entity.ToTable("material_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.RequestNumber }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.MaterialRequests)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.MaterialRequests)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.WorkOrder)
                .WithMany(x => x.MaterialRequests)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RequestedByUser)
                .WithMany()
                .HasForeignKey(x => x.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaterialRequestLine>(entity =>
        {
            entity.ToTable("material_request_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuantityRequested).HasPrecision(18, 2);
            entity.Property(x => x.QuantityIssued).HasPrecision(18, 2);
            entity.Property(x => x.QuantityUsed).HasPrecision(18, 2);
            entity.Property(x => x.QuantityReturned).HasPrecision(18, 2);
            entity.HasOne(x => x.MaterialRequest)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.MaterialRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem)
                .WithMany(x => x.MaterialRequestLines)
                .HasForeignKey(x => x.MaterialItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PartsRequest>(entity =>
        {
            entity.ToTable("parts_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RejectionReason).HasMaxLength(2000);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.WorkOrder)
                .WithMany()
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Technician)
                .WithMany()
                .HasForeignKey(x => x.TechnicianId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PartsRequestItem>(entity =>
        {
            entity.ToTable("parts_request_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.QuantityRequested).HasPrecision(18, 2);
            entity.Property(x => x.QuantityApproved).HasPrecision(18, 2);
            entity.HasOne(x => x.PartsRequest)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.PartsRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.InventoryItem)
                .WithMany()
                .HasForeignKey(x => x.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.ToTable("user_permissions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Role }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<RolePermission>(modelBuilder);

        modelBuilder.Entity<PermissionGroup>(entity =>
        {
            entity.ToTable("permission_groups");
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });
        modelBuilder.Entity<UserPermissionGroup>(entity =>
        {
            entity.ToTable("user_permission_groups");
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.PermissionGroupId }).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.PermissionGroups).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PermissionGroup).WithMany(x => x.Users).HasForeignKey(x => x.PermissionGroupId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<GroupCapability>(entity =>
        {
            entity.ToTable("group_capabilities");
            entity.Property(x => x.Capability).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.PermissionGroupId, x.Capability }).IsUnique();
            entity.HasOne(x => x.PermissionGroup).WithMany(x => x.Capabilities).HasForeignKey(x => x.PermissionGroupId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UserPermissionOverride>(entity =>
        {
            entity.ToTable("user_permission_overrides");
            entity.Property(x => x.Capability).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.Capability }).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.PermissionOverrides).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AccessScope>(entity =>
        {
            entity.ToTable("access_scopes");
            entity.Property(x => x.Capability).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ScopeType).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.User).WithMany(x => x.AccessScopes).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PermissionGroup).WithMany().HasForeignKey(x => x.PermissionGroupId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PermissionThreshold>(entity =>
        {
            entity.ToTable("permission_thresholds");
            entity.Property(x => x.Capability).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.MaximumAmount).HasPrecision(18, 2);
            entity.HasOne(x => x.User).WithMany(x => x.PermissionThresholds).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PermissionGroup).WithMany().HasForeignKey(x => x.PermissionGroupId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<PermissionGroup>(modelBuilder);
        ApplyTenantFilter<UserPermissionGroup>(modelBuilder);
        ApplyTenantFilter<GroupCapability>(modelBuilder);
        ApplyTenantFilter<UserPermissionOverride>(modelBuilder);
        ApplyTenantFilter<AccessScope>(modelBuilder);
        ApplyTenantFilter<PermissionThreshold>(modelBuilder);

        modelBuilder.Entity<PreventiveMaintenancePlan>(entity =>
        {
            entity.ToTable("preventive_maintenance_plans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Frequency).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TriggerType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.FrequencyUnit).HasMaxLength(50).IsRequired();
            entity.Property(x => x.MeterInterval).HasPrecision(18, 4);
            entity.Property(x => x.MeterBuffer).HasPrecision(18, 4);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.HoldReason).HasMaxLength(500);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.PreventiveMaintenancePlans)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.PreventiveMaintenancePlans)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Asset)
                .WithMany(x => x.PreventiveMaintenancePlans)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site)
                .WithMany()
                .HasForeignKey(x => x.SiteId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.PmTemplate)
                .WithMany(x => x.Plans)
                .HasForeignKey(x => x.PmTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DefaultAssignmentGroup)
                .WithMany()
                .HasForeignKey(x => x.DefaultAssignmentGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.ScheduleMode).HasMaxLength(20).IsRequired().HasDefaultValue(PmScheduleModes.Floating);
            entity.Property(x => x.TolerancePercent).HasDefaultValue(10);
            entity.Property(x => x.LastMeterValue).HasPrecision(18, 4);
            entity.Property(x => x.NextMeterDue).HasPrecision(18, 4);
            entity.Property(x => x.Priority).HasMaxLength(20).IsRequired().HasDefaultValue("Medium");
            entity.Property(x => x.EstimatedHours).HasPrecision(10, 2);
            entity.Property(x => x.Basis).HasMaxLength(100);
            entity.Property(x => x.DeferReason).HasMaxLength(1000);
            entity.HasOne(x => x.Meter).WithMany().HasForeignKey(x => x.MeterId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.OrgNode).WithMany().HasForeignKey(x => x.OrgNodeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.OrgNodeId, x.Status });
        });

        modelBuilder.Entity<PmPlanRouteAsset>(entity =>
        {
            entity.ToTable("pm_plan_route_assets");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Plan).WithMany(x => x.RouteAssets).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.PlanId, x.AssetId }).IsUnique();
        });

        modelBuilder.Entity<MaintenanceWindow>(entity =>
        {
            entity.ToTable("maintenance_windows");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CapacityHours).HasPrecision(10, 1);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.SkipReason).HasMaxLength(1000);
            entity.Property(x => x.PackedByName).HasMaxLength(200);
            entity.Property(x => x.HandedBackByName).HasMaxLength(200);
            entity.Property(x => x.HandbackNotes).HasMaxLength(2000);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.WindowDate }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
        });

        modelBuilder.Entity<MaintenanceWindowJob>(entity =>
        {
            entity.ToTable("maintenance_window_jobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Outcome).HasMaxLength(20);
            entity.HasOne(x => x.Window).WithMany(x => x.Jobs).HasForeignKey(x => x.WindowId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.WindowId, x.WorkOrderId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId });
        });

        modelBuilder.Entity<MaintenanceWindowHandbackItem>(entity =>
        {
            entity.ToTable("maintenance_window_handback_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Prompt).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.DoneByName).HasMaxLength(200);
            entity.HasOne(x => x.Window).WithMany(x => x.HandbackItems).HasForeignKey(x => x.WindowId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OverhaulPlan>(entity =>
        {
            entity.ToTable("overhaul_plans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Budget).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.PlannedStart });
        });

        modelBuilder.Entity<OverhaulItem>(entity =>
        {
            entity.ToTable("overhaul_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.Source).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Priority).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.EstimatedHours).HasPrecision(10, 2);
            entity.Property(x => x.EstimatedCost).HasPrecision(18, 2);
            entity.Property(x => x.ActualCost).HasPrecision(18, 2);
            entity.Property(x => x.SparesNote).HasMaxLength(2000);
            entity.Property(x => x.ContractorName).HasMaxLength(200);
            entity.Property(x => x.RecommissionedByName).HasMaxLength(200);
            entity.Property(x => x.RecommissioningNotes).HasMaxLength(2000);
            entity.Property(x => x.AddedByName).HasMaxLength(200);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OverhaulPlan).WithMany(x => x.Items).HasForeignKey(x => x.OverhaulPlanId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId });
        });

        modelBuilder.Entity<ComplianceItem>(entity =>
        {
            entity.ToTable("compliance_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Kind).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Regulation).HasMaxLength(200);
            entity.Property(x => x.Provider).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.TemplateKey).HasMaxLength(100);
            entity.HasOne(x => x.OrgNode).WithMany().HasForeignKey(x => x.OrgNodeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.OpenWorkOrder).WithMany().HasForeignKey(x => x.OpenWorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.OrgNodeId, x.DueDate });
            entity.HasIndex(x => new { x.TenantId, x.IsActive, x.DueDate });
        });

        modelBuilder.Entity<ComplianceRecord>(entity =>
        {
            entity.ToTable("compliance_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Result).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CertificateNumber).HasMaxLength(100);
            entity.Property(x => x.Examiner).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RecordedByName).HasMaxLength(200);
            entity.HasOne(x => x.Item).WithMany(x => x.Records).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CertificateAttachment).WithMany().HasForeignKey(x => x.CertificateAttachmentId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.ItemId, x.DoneAt });
        });

        modelBuilder.Entity<TechnicianSkill>(entity =>
        {
            entity.ToTable("technician_skills");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Skill).HasMaxLength(100).IsRequired();
            entity.HasOne(x => x.Technician).WithMany(x => x.Skills).HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.TechnicianId, x.Skill }).IsUnique();
        });

        modelBuilder.Entity<TechnicianCertificate>(entity =>
        {
            entity.ToTable("technician_certificates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CertificateNumber).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.Technician).WithMany(x => x.Certificates).HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CertificationDefinition).WithMany().HasForeignKey(x => x.CertificationDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.ExpiresOn });
        });

        modelBuilder.Entity<RosterEntry>(entity =>
        {
            entity.ToTable("roster_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ShiftName).HasMaxLength(50);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.TechnicianId, x.WorkDate }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.WorkDate });
        });

        modelBuilder.Entity<WorkPermit>(entity =>
        {
            entity.ToTable("work_permits");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PermitNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PermitType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Hazards).HasMaxLength(2000);
            entity.Property(x => x.WorkDescription).HasMaxLength(2000);
            entity.Property(x => x.IssuedToName).HasMaxLength(200);
            entity.Property(x => x.RequestedByName).HasMaxLength(200);
            entity.Property(x => x.ApprovedByName).HasMaxLength(200);
            entity.Property(x => x.DecisionNote).HasMaxLength(1000);
            entity.Property(x => x.GasTest).HasMaxLength(500);
            entity.Property(x => x.ClosedByName).HasMaxLength(200);
            entity.Property(x => x.CloseOutNotes).HasMaxLength(2000);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.TenantId, x.PermitNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.Status });
        });

        modelBuilder.Entity<WorkPermitCheck>(entity =>
        {
            entity.ToTable("work_permit_checks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Prompt).HasMaxLength(500).IsRequired();
            entity.Property(x => x.DoneByName).HasMaxLength(200);
            entity.HasOne(x => x.Permit).WithMany(x => x.Checks).HasForeignKey(x => x.PermitId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IsolationPoint>(entity =>
        {
            entity.ToTable("isolation_points");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EnergyType).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(200);
            entity.Property(x => x.Method).HasMaxLength(1000);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.AssetId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<LotoLock>(entity =>
        {
            entity.ToTable("loto_locks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LockNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.AppliedByName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.RemovedByName).HasMaxLength(200);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.IsolationPoint).WithMany().HasForeignKey(x => x.IsolationPointId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkPermit>().WithMany().HasForeignKey(x => x.PermitId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId });
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.ToTable("incidents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IncidentNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Kind).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Severity).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.PersonsInvolved).HasMaxLength(1000);
            entity.Property(x => x.ImmediateAction).HasMaxLength(2000);
            entity.Property(x => x.RootCause).HasMaxLength(2000);
            entity.Property(x => x.Lessons).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ReportedByName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ClosedByName).HasMaxLength(200);
            entity.HasOne(x => x.OrgNode).WithMany().HasForeignKey(x => x.OrgNodeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CorrectiveWorkOrder).WithMany().HasForeignKey(x => x.CorrectiveWorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.IncidentNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.OccurredAt });
            entity.Property(x => x.Category).HasMaxLength(20).IsRequired().HasDefaultValue(IncidentCategories.Safety);
            entity.Property(x => x.OwnerName).HasMaxLength(200);
            entity.Property(x => x.ResponseStatus).HasMaxLength(20);
            entity.Property(x => x.PoliceObNumber).HasMaxLength(50);
            entity.HasIndex(x => new { x.TenantId, x.Category, x.Status });
            entity.HasOne<Checkpoint>().WithMany().HasForeignKey(x => x.CheckpointId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<PatrolRun>().WithMany().HasForeignKey(x => x.PatrolRunId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Visit>().WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TrainingRecord>(entity =>
        {
            entity.ToTable("training_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PersonName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Company).HasMaxLength(200);
            entity.Property(x => x.Course).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Trainer).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.CompletedOn });
        });

        modelBuilder.Entity<PpeIssue>(entity =>
        {
            entity.ToTable("ppe_issues");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PersonName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Item).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Size).HasMaxLength(20);
            entity.Property(x => x.IssuedByName).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.IssuedOn });
        });

        modelBuilder.Entity<Store>(entity =>
        {
            entity.ToTable("stores");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(200);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.FactoryId });
        });

        modelBuilder.Entity<StoreBin>(entity =>
        {
            entity.ToTable("store_bins");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(200);
            entity.HasOne(x => x.Store).WithMany(x => x.Bins).HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.StoreId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<StoreStock>(entity =>
        {
            entity.ToTable("store_stocks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OnHand).HasPrecision(18, 2);
            entity.Property(x => x.Reserved).HasPrecision(18, 2);
            entity.Property(x => x.MinLevel).HasPrecision(18, 2);
            entity.Property(x => x.ReorderPoint).HasPrecision(18, 2);
            entity.Property(x => x.MaxLevel).HasPrecision(18, 2);
            entity.Property(x => x.AverageCost).HasPrecision(18, 4);
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Bin).WithMany().HasForeignKey(x => x.BinId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.StoreId, x.MaterialItemId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.MaterialItemId });
        });

        modelBuilder.Entity<AssetPart>(entity =>
        {
            entity.ToTable("asset_parts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuantityFitted).HasPrecision(18, 2);
            entity.Property(x => x.Position).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.AssetId, x.MaterialItemId, x.Position }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.MaterialItemId });
        });

        modelBuilder.Entity<StockReservation>(entity =>
        {
            entity.ToTable("stock_reservations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.IssuedQuantity).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ReservedByName).HasMaxLength(200);
            entity.Property(x => x.KittedByName).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId });
            entity.HasIndex(x => new { x.StoreId, x.MaterialItemId, x.Status });
        });

        modelBuilder.Entity<SpareUnit>(entity =>
        {
            entity.ToTable("spare_units");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SerialNumber).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.RepairReference).HasMaxLength(100);
            entity.Property(x => x.TotalRepairCost).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.RepairSupplier).WithMany().HasForeignKey(x => x.RepairSupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.MaterialItemId, x.SerialNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
        });

        modelBuilder.Entity<SpareUnitEvent>(entity =>
        {
            entity.ToTable("spare_unit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ByName).HasMaxLength(200);
            entity.Property(x => x.Cost).HasPrecision(18, 2);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne(x => x.SpareUnit).WithMany(x => x.Events).HasForeignKey(x => x.SpareUnitId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.SpareUnitId, x.OccurredAt });
        });

        modelBuilder.Entity<StoreTransfer>(entity =>
        {
            entity.ToTable("store_transfers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TransferNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 4);
            entity.Property(x => x.SerialNumbers).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.RequestedByName).HasMaxLength(200);
            entity.Property(x => x.DispatchedByName).HasMaxLength(200);
            entity.Property(x => x.ReceivedByName).HasMaxLength(200);
            entity.Property(x => x.RejectReason).HasMaxLength(500);
            entity.HasOne(x => x.FromStore).WithMany().HasForeignKey(x => x.FromStoreId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ToStore).WithMany().HasForeignKey(x => x.ToStoreId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.TransferNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
        });

        modelBuilder.Entity<StoreCount>(entity =>
        {
            entity.ToTable("store_counts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CountNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.StartedByName).HasMaxLength(200);
            entity.Property(x => x.PostedByName).HasMaxLength(200);
            entity.Property(x => x.VarianceValue).HasPrecision(18, 2);
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StoreBin>().WithMany().HasForeignKey(x => x.BinId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.CountNumber }).IsUnique();
        });

        modelBuilder.Entity<StoreCountLine>(entity =>
        {
            entity.ToTable("store_count_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExpectedQuantity).HasPrecision(18, 2);
            entity.Property(x => x.CountedQuantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 4);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.StoreCount).WithMany(x => x.Lines).HasForeignKey(x => x.StoreCountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.StoreCountId, x.MaterialItemId }).IsUnique();
        });

        modelBuilder.Entity<PurchaseRequisition>(entity =>
        {
            entity.ToTable("purchase_requisitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequisitionNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.RequestedByName).HasMaxLength(200);
            entity.Property(x => x.DecidedByName).HasMaxLength(200);
            entity.Property(x => x.RejectReason).HasMaxLength(500);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.RequisitionNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
        });

        modelBuilder.Entity<PurchaseRequisitionLine>(entity =>
        {
            entity.ToTable("purchase_requisition_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 4);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.HasOne(x => x.Requisition).WithMany(x => x.Lines).HasForeignKey(x => x.RequisitionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VendorCatalogueItem>(entity =>
        {
            entity.ToTable("vendor_catalogue_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.VendorPartNumber).HasMaxLength(100);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 4);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.MinOrderQuantity).HasPrecision(18, 2);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.SupplierId, x.MaterialItemId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.MaterialItemId });
        });

        modelBuilder.Entity<Contractor>(entity =>
        {
            entity.ToTable("contractors");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Services).HasMaxLength(500);
            entity.Property(x => x.ContactPerson).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.RegistrationNumber).HasMaxLength(100);
            entity.Property(x => x.TaxPin).HasMaxLength(50);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.ExternalId).HasMaxLength(100);
            entity.Property(x => x.ExternalSystem).HasMaxLength(50);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<ContractorDocument>(entity =>
        {
            entity.ToTable("contractor_documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.Contractor).WithMany(x => x.Documents).HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.ExpiresOn });
        });

        modelBuilder.Entity<MaintenanceAgreement>(entity =>
        {
            entity.ToTable("maintenance_agreements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AgreementNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Scope).HasMaxLength(4000);
            entity.Property(x => x.Value).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasOne(x => x.Contractor).WithMany().HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.AgreementNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status, x.EndDate });
        });

        modelBuilder.Entity<MaintenanceAgreementAsset>(entity =>
        {
            entity.ToTable("maintenance_agreement_assets");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Agreement).WithMany(x => x.Assets).HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.AgreementId, x.AssetId }).IsUnique();
        });

        modelBuilder.Entity<ContractorInvoice>(entity =>
        {
            entity.Property(x => x.VatAmount).HasPrecision(18, 2);
            entity.ToTable("contractor_invoices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.InvoiceNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.MatchIssues).HasMaxLength(4000);
            entity.Property(x => x.DecidedByName).HasMaxLength(200);
            entity.Property(x => x.DecisionNote).HasMaxLength(1000);
            entity.HasOne(x => x.Contractor).WithMany().HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.ContractorId, x.InvoiceNumber }).IsUnique();
        });

        modelBuilder.Entity<ContractorInvoiceLine>(entity =>
        {
            entity.ToTable("contractor_invoice_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasOne(x => x.Invoice).WithMany(x => x.Lines).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId });
        });

        modelBuilder.Entity<CostCentre>(entity =>
        {
            entity.ToTable("cost_centres");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.ExternalId).HasMaxLength(100);
            entity.Property(x => x.ExternalSystem).HasMaxLength(50);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.OrgNode).WithMany().HasForeignKey(x => x.OrgNodeId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<MaintenanceBudget>(entity =>
        {
            entity.ToTable("maintenance_budgets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CostCentre).WithMany().HasForeignKey(x => x.CostCentreId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.Year, x.CostCentreId, x.Category }).IsUnique();
        });

        modelBuilder.Entity<ComplianceStandard>(entity =>
        {
            entity.ToTable("compliance_standards");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.TemplateKey).HasMaxLength(50);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<StandardRequirement>(entity =>
        {
            entity.ToTable("standard_requirements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Clause).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Guidance).HasMaxLength(2000);
            entity.Property(x => x.EvidenceSources).HasMaxLength(500).IsRequired();
            entity.HasOne(x => x.Standard).WithMany(x => x.Requirements).HasForeignKey(x => x.StandardId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.StandardId, x.Sequence });
        });

        modelBuilder.Entity<NonConformity>(entity =>
        {
            entity.ToTable("non_conformities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Severity).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Finding).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.RootCause).HasMaxLength(2000);
            entity.Property(x => x.CorrectiveAction).HasMaxLength(2000);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ClosureEvidence).HasMaxLength(2000);
            entity.Property(x => x.ClosedByName).HasMaxLength(200);
            entity.Property(x => x.RaisedByName).HasMaxLength(200);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Standard).WithMany().HasForeignKey(x => x.StandardId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Requirement).WithMany().HasForeignKey(x => x.RequirementId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<AuditEvent>().WithMany().HasForeignKey(x => x.AuditEventId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Auditor).HasMaxLength(200);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Outcome).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Standard).WithMany().HasForeignKey(x => x.StandardId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.ScheduledFor });
        });

        modelBuilder.Entity<WasteDisposal>(entity =>
        {
            entity.ToTable("waste_disposals");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WasteType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.Unit).HasMaxLength(20).IsRequired();
            entity.Property(x => x.HandlerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LicenceNumber).HasMaxLength(100);
            entity.Property(x => x.ConsignmentNote).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.RecordedByName).HasMaxLength(200);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Contractor>().WithMany().HasForeignKey(x => x.ContractorId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Attachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.DisposedOn });
        });

        modelBuilder.Entity<FoodSafetyRecord>(entity =>
        {
            entity.ToTable("food_safety_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Area).HasMaxLength(200);
            entity.Property(x => x.Method).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.DoneByName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.VerifiedByName).HasMaxLength(200);
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.CorrectiveWorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.FactoryId, x.Kind, x.DoneAt });
        });

        modelBuilder.Entity<GlassRegisterItem>(entity =>
        {
            entity.ToTable("glass_register_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Location).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Material).HasMaxLength(30).IsRequired();
            entity.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConsentRecord>(entity =>
        {
            entity.ToTable("consent_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SubjectName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Purpose).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(50).IsRequired();
            entity.Property(x => x.RecordedByName).HasMaxLength(200);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Technician>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PmPlanEvent>(entity =>
        {
            entity.ToTable("pm_plan_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            entity.Property(x => x.DueMeterValue).HasPrecision(18, 4);
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.ActorName).HasMaxLength(200);
            entity.HasOne(x => x.Plan).WithMany(x => x.Events).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.PlanId, x.OccurredAt });
            entity.HasIndex(x => new { x.TenantId, x.Kind, x.OccurredAt });
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.ToTable("import_batches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ImportType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.ImportBatches)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.Property(x => x.ActorName).HasMaxLength(200);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Details).HasMaxLength(4000);
            entity.Property(x => x.IpAddress).HasMaxLength(100);
            entity.Property(x => x.UserAgent).HasMaxLength(1000);
            entity.Property(x => x.Severity).HasMaxLength(50).IsRequired();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EmailSetting>(entity =>
        {
            entity.ToTable("email_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ManagedEmailAddress).HasMaxLength(256);
            entity.Property(x => x.CustomFromAddress).HasMaxLength(256);
            entity.Property(x => x.ManagedEmailStatus).HasMaxLength(32).IsRequired().HasDefaultValue(ManagedEmailStatuses.Pending);
            entity.Property(x => x.ManagedEmailStatusDetail).HasMaxLength(1000);
            entity.Property(x => x.EmailSignature).HasMaxLength(4000);
            entity.HasIndex(x => x.ManagedEmailAddress).IsUnique();
            entity.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Host).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(200);
            entity.Property(x => x.ReplyToEmail).HasMaxLength(256);
            entity.Property(x => x.FinanceReplyToEmail).HasMaxLength(256);
            entity.Property(x => x.OperationsReplyToEmail).HasMaxLength(256);
            entity.Property(x => x.Password).HasMaxLength(500);
            entity.Property(x => x.EncryptedSecret).HasMaxLength(4000);
            entity.Property(x => x.SenderName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SenderAddress).HasMaxLength(256).IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.EmailSettings)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantNotificationSetting>(entity =>
        {
            entity.ToTable("tenant_notification_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NotificationKey).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.NotificationKey }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.TenantNotificationSettings)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantNotificationRecipient>(entity =>
        {
            entity.ToTable("tenant_notification_recipients");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RecipientGroup).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.RecipientGroup, x.Email }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.TenantNotificationRecipients)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserNotificationPreference>(entity =>
        {
            entity.ToTable("user_notification_preferences");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NotificationKey).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.NotificationKey }).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.ToTable("push_subscriptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Endpoint).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.P256dh).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Auth).HasMaxLength(100).IsRequired();
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.HasIndex(x => x.Endpoint).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.UserId });
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications", tableBuilder =>
                tableBuilder.HasCheckConstraint("CK_notifications_severity", "\"Severity\" IN ('Info', 'Warning', 'Critical')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(InboxNotifications.TitleMaxLength).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(InboxNotifications.BodyMaxLength).IsRequired();
            entity.Property(x => x.Url).HasMaxLength(InboxNotifications.UrlMaxLength);
            entity.Property(x => x.EntityType).HasMaxLength(80);
            entity.Property(x => x.Severity).HasMaxLength(20).IsRequired();
            // The bell: a user's live rows, newest first.
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.ArchivedAt, x.CreatedAt })
                .IsDescending(false, false, false, true);
            // The push worker and housekeeping.
            entity.HasIndex(x => x.PushPendingSince).HasFilter("\"PushPendingSince\" IS NOT NULL");
            entity.HasIndex(x => x.CreatedAt);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<Notification>(modelBuilder);

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Path).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.Key }).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<EmailIntakeMessage>(entity =>
        {
            entity.ToTable("email_intake_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MessageId).HasMaxLength(500).IsRequired();
            entity.Property(x => x.FromAddress).HasMaxLength(320).IsRequired();
            entity.Property(x => x.FromName).HasMaxLength(200);
            entity.Property(x => x.Subject).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Outcome).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.MessageId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EmailIntakeSetting>(entity =>
        {
            entity.ToTable("email_intake_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IntakeEmailAddress).HasMaxLength(256);
            entity.Property(x => x.MailboxProvider).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Host).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(200);
            entity.Property(x => x.EncryptedPassword).HasMaxLength(4000);
            entity.Property(x => x.DefaultPriority).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SubjectParsingRules).HasMaxLength(4000);
            entity.Property(x => x.AllowedSenderDomains).HasMaxLength(2000);
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.EmailIntakeSettings)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DefaultClient)
                .WithMany()
                .HasForeignKey(x => x.DefaultClientId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DefaultBranch)
                .WithMany()
                .HasForeignKey(x => x.DefaultBranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DefaultAssignmentGroup)
                .WithMany()
                .HasForeignKey(x => x.DefaultAssignmentGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NumberingSetting>(entity =>
        {
            entity.ToTable("numbering_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Prefix).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ResetFrequency).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.DocumentType })
                .HasFilter("\"BranchId\" IS NULL")
                .IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.DocumentType })
                .HasFilter("\"BranchId\" IS NOT NULL")
                .IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.NumberingSettings)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LicensePlan>(entity =>
        {
            entity.ToTable("license_plans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlanCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.OfficeSeatMonthlyPrice).HasPrecision(18, 2);
            entity.Property(x => x.OfficeSeatAnnualPrice).HasPrecision(18, 2);
            entity.Property(x => x.FieldSeatMonthlyPrice).HasPrecision(18, 2);
            entity.Property(x => x.FieldSeatAnnualPrice).HasPrecision(18, 2);
            entity.HasIndex(x => x.PlanCode).IsUnique();
            // The three editions. Prices start at zero ("not priced") and are set on the platform; the
            // Operations/Finance/Suite plans were retired by the MoveToEditionsAndSeatTypes migration.
            var seededAt = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
            entity.HasData(
                new LicensePlan { Id = Guid.Parse("55555555-5555-5555-5555-555555555561"), PlanCode = "essentials", DisplayName = "Ecosys Factory Maintenance Essentials", MinOfficeSeats = 2, IsActive = true, CreatedAt = seededAt },
                new LicensePlan { Id = Guid.Parse("55555555-5555-5555-5555-555555555562"), PlanCode = "professional", DisplayName = "Ecosys Factory Maintenance Professional", MinOfficeSeats = 3, IsActive = true, CreatedAt = seededAt },
                new LicensePlan { Id = Guid.Parse("55555555-5555-5555-5555-555555555563"), PlanCode = "enterprise", DisplayName = "Ecosys Factory Maintenance Enterprise", MinOfficeSeats = 10, IsActive = true, CreatedAt = seededAt });
        });

        modelBuilder.Entity<LicenseAddOn>(entity =>
        {
            entity.ToTable("license_add_ons");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
            entity.Property(x => x.AnnualPrice).HasPrecision(18, 2);
            entity.Property(x => x.ExtraMessagePrice).HasPrecision(18, 2);
            entity.Property(x => x.ExtraNumberPrice).HasPrecision(18, 2);
            entity.HasIndex(x => x.Code).IsUnique();
            // Starting values in KES; the platform edits them.
            var seededAt = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
            entity.HasData(
                new LicenseAddOn
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555571"), Code = "whatsapp_notifications", DisplayName = "WhatsApp Notifications",
                    MonthlyPrice = 1500m, AnnualPrice = 15000m, IncludedMessages = 500, ExtraMessagePrice = 3m, IsActive = true, CreatedAt = seededAt
                },
                new LicenseAddOn
                {
                    Id = Guid.Parse("55555555-5555-5555-5555-555555555572"), Code = "whatsapp_business", DisplayName = "WhatsApp Business",
                    MonthlyPrice = 4500m, AnnualPrice = 45000m, IncludedMessages = 1500, ExtraMessagePrice = 3m, ExtraNumberPrice = 2500m, IsActive = true, CreatedAt = seededAt
                });
        });

        modelBuilder.Entity<TenantLicense>(entity =>
        {
            entity.ToTable("tenant_licenses", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("CK_tenant_licenses_OfficeSeats_Positive", "\"OfficeSeats\" > 0");
                tableBuilder.HasCheckConstraint("CK_tenant_licenses_FieldSeats_NonNegative", "\"FieldSeats\" >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.BillingCycle).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.SuspensionReason).HasMaxLength(30);
            entity.Property(x => x.IsPerpetual).HasDefaultValue(false);
            entity.Property(x => x.WhatsAppAddOn).HasMaxLength(50);
            entity.Property(x => x.OperationsAddOns).HasMaxLength(100);
            entity.Property(x => x.OfficeSeatPriceOverride).HasPrecision(18, 2);
            entity.Property(x => x.FieldSeatPriceOverride).HasPrecision(18, 2);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.TenantLicenses)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.LicensePlan)
                .WithMany(x => x.TenantLicenses)
                .HasForeignKey(x => x.LicensePlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssignmentGroup>(entity =>
        {
            entity.ToTable("assignment_groups");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.SkillArea).HasMaxLength(150);
            entity.HasIndex(x => new { x.TenantId, x.BranchId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.AssignmentGroups)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.AssignmentGroups)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AssignmentGroupMember>(entity =>
        {
            entity.ToTable("assignment_group_members");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AddedAt).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.AssignmentGroupId, x.TechnicianId }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AssignmentGroup)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.AssignmentGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Technician)
                .WithMany(x => x.AssignmentGroups)
                .HasForeignKey(x => x.TechnicianId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkOrderAssignment>(entity =>
        {
            entity.ToTable("work_order_assignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AssignmentStatus).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.HasIndex(x => x.WorkOrderId).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.AssignmentGroupId, x.AssignmentStatus });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AssignmentGroup)
                .WithMany(x => x.WorkOrderAssignments)
                .HasForeignKey(x => x.AssignmentGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AssignedByUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkOrderTechnicianAssignment>(entity =>
        {
            entity.ToTable("work_order_technician_assignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.HasIndex(x => new { x.WorkOrderId, x.TechnicianId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.TechnicianId, x.Status });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Technician)
                .WithMany(x => x.WorkOrderAssignments)
                .HasForeignKey(x => x.TechnicianId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AssignedByUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkOrderAssignmentHistory>(entity =>
        {
            entity.ToTable("work_order_assignment_history");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.PerformedAt });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FromGroup)
                .WithMany(x => x.AssignmentHistoryFromGroups)
                .HasForeignKey(x => x.FromGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ToGroup)
                .WithMany(x => x.AssignmentHistoryToGroups)
                .HasForeignKey(x => x.ToGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.FromTechnician)
                .WithMany(x => x.AssignmentHistoryFromTechnicians)
                .HasForeignKey(x => x.FromTechnicianId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ToTechnician)
                .WithMany(x => x.AssignmentHistoryToTechnicians)
                .HasForeignKey(x => x.ToTechnicianId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.PerformedByUser)
                .WithMany()
                .HasForeignKey(x => x.PerformedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkOrderEvent>(entity =>
        {
            entity.ToTable("work_order_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.Property(x => x.OldStatus).HasMaxLength(50);
            entity.Property(x => x.NewStatus).HasMaxLength(50);
            entity.Property(x => x.Reason).HasMaxLength(250);
            entity.Property(x => x.Note).HasMaxLength(2000);
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Latitude).HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasPrecision(9, 6);
            entity.Property(x => x.MetadataJson);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.OccurredAt });
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.WorkOrderEvents)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ActorUser)
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkOrderNote>(entity =>
        {
            entity.ToTable("work_order_notes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Content).HasMaxLength(4000).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.CreatedAt });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany(x => x.Notes)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PmTemplate>(entity =>
        {
            entity.ToTable("pm_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.WorkType).HasMaxLength(80).HasDefaultValue("PreventiveMaintenance").IsRequired();
            entity.Property(x => x.OptionalReportSectionsJson).HasMaxLength(4000);
            entity.Property(x => x.EstimatedDurationHours).HasPrecision(10, 2);
            entity.Property(x => x.Version).HasDefaultValue(1);
            entity.Property(x => x.DeletedAt);
            entity.Property(x => x.DeletedByUserId);
            entity.HasIndex(x => new { x.TenantId, x.Category, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.TemplatePackId, x.Code }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.PmTemplates)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TemplatePack)
                .WithMany(x => x.ChecklistTemplates)
                .HasForeignKey(x => x.TemplatePackId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AssetCategory)
                .WithMany()
                .HasForeignKey(x => x.AssetCategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PmTemplateQuestion>(entity =>
        {
            entity.ToTable("pm_template_questions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FailAction).HasMaxLength(20);
            entity.Property(x => x.FeedsMeterType).HasMaxLength(30);
            entity.Property(x => x.SectionName).HasMaxLength(150);
            entity.Property(x => x.Prompt).HasMaxLength(500).IsRequired();
            entity.Property(x => x.HelperText).HasMaxLength(1000);
            entity.Property(x => x.ResponseType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(50);
            entity.Property(x => x.DefaultValue).HasMaxLength(4000);
            entity.Property(x => x.MinValue).HasPrecision(18, 4);
            entity.Property(x => x.MaxValue).HasPrecision(18, 4);
            entity.Property(x => x.TechnicianRemarksAllowed).HasDefaultValue(true);
            entity.Property(x => x.OptionsJson).HasMaxLength(4000);
            entity.HasOne(x => x.PmTemplate)
                .WithMany(x => x.Questions)
                .HasForeignKey(x => x.PmTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Section)
                .WithMany(x => x.Questions)
                .HasForeignKey(x => x.SectionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PmTemplateVersion>(entity =>
        {
            entity.ToTable("pm_template_versions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SnapshotJson).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();
            entity.HasOne(x => x.PmTemplate)
                .WithMany()
                .HasForeignKey(x => x.PmTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.PmTemplateId, x.Version });
        });

        modelBuilder.Entity<SystemTemplatePack>(entity =>
        {
            entity.ToTable("system_template_packs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.ContractorType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Version).HasDefaultValue(1);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<TenantTemplatePackInstallation>(entity =>
        {
            entity.ToTable("tenant_template_pack_installations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.SourceTemplatePackId }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.TemplatePackInstallations)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SourceTemplatePack)
                .WithMany(x => x.Installations)
                .HasForeignKey(x => x.SourceTemplatePackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkOrderChecklistItem>(entity =>
        {
            entity.ToTable("work_order_checklist_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SectionName).HasMaxLength(150);
            entity.Property(x => x.QuestionText).HasMaxLength(500).IsRequired();
            entity.Property(x => x.HelperText).HasMaxLength(1000);
            entity.Property(x => x.InputType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.QuestionType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(50);
            entity.Property(x => x.DefaultValue).HasMaxLength(4000);
            entity.Property(x => x.MinValue).HasPrecision(18, 4);
            entity.Property(x => x.MaxValue).HasPrecision(18, 4);
            entity.Property(x => x.TechnicianRemarksAllowed).HasDefaultValue(true);
            entity.Property(x => x.NumberValue).HasPrecision(18, 4);
            entity.Property(x => x.TextValue).HasMaxLength(4000);
            entity.Property(x => x.FailureNote).HasMaxLength(2000);
            entity.Property(x => x.ResponseValue).HasMaxLength(4000);
            entity.Property(x => x.Remarks).HasMaxLength(4000);
            entity.Property(x => x.OptionsJson).HasMaxLength(4000);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.SortOrder });
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.WorkOrderChecklistItems)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany(x => x.ChecklistItems)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PmTemplateQuestion)
                .WithMany()
                .HasForeignKey(x => x.PmTemplateQuestionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CompletedByUser)
                .WithMany()
                .HasForeignKey(x => x.CompletedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Attachment)
                .WithMany()
                .HasForeignKey(x => x.AttachmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PmReport>(entity =>
        {
            entity.ToTable("pm_reports");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Summary).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.AnswersJson).HasMaxLength(16000).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.GeneratedAt });
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.PmReports)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany(x => x.PmReports)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PmTemplate)
                .WithMany(x => x.Reports)
                .HasForeignKey(x => x.PmTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TenantSecurityPolicy>(entity =>
        {
            entity.ToTable("tenant_security_policies");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.TenantSecurityPolicies)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantOperationsPolicy>(entity =>
        {
            entity.ToTable("tenant_operations_policies");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.TenantOperationsPolicies)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<IntakeProtocol>(entity =>
        {
            entity.ToTable("intake_protocols");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.SourceType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.CriteriaJson).HasMaxLength(32000).IsRequired();
            entity.Property(x => x.ActionsJson).HasMaxLength(32000).IsRequired();
            entity.Property(x => x.SourceConfigJson).HasMaxLength(32000).IsRequired();
            entity.Property(x => x.LastTriggerStatus).HasMaxLength(120);
            entity.Property(x => x.LastError).HasMaxLength(4000);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.IntakeProtocols)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MonitoringSetting>(entity =>
        {
            entity.ToTable("monitoring_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProviderName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EndpointLabel).HasMaxLength(200);
            entity.Property(x => x.WebhookSecret).HasMaxLength(500);
            entity.Property(x => x.DefaultPriority).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.MonitoringSettings)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DefaultBranch)
                .WithMany(x => x.MonitoringSettings)
                .HasForeignKey(x => x.DefaultBranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TenantApiKey>(entity =>
        {
            entity.ToTable("tenant_api_keys");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Label).HasMaxLength(150).IsRequired();
            entity.Property(x => x.KeyPrefix).HasMaxLength(20).IsRequired();
            entity.Property(x => x.KeyHash).HasMaxLength(512).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Label });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantWebhook>(entity =>
        {
            entity.ToTable("tenant_webhooks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Url).HasMaxLength(500).IsRequired();
            entity.Property(x => x.EventsJson).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.SecretHash).HasMaxLength(512).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantWebhookDelivery>(entity =>
        {
            entity.ToTable("tenant_webhook_deliveries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Event).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.WebhookId, x.AttemptedAt });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Webhook)
                .WithMany()
                .HasForeignKey(x => x.WebhookId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MonitoringWebhookIntegration>(entity =>
        {
            entity.ToTable("monitoring_webhook_integrations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.ToolType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EndpointSlug).HasMaxLength(100).IsRequired();
            entity.Property(x => x.SecretHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.DefaultPriority).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PayloadMappingJson).HasMaxLength(16000);
            entity.Property(x => x.LastStatus).HasMaxLength(100);
            entity.Property(x => x.LastError).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasIndex(x => x.EndpointSlug).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.MonitoringWebhookIntegrations)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DefaultClient)
                .WithMany()
                .HasForeignKey(x => x.DefaultClientId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DefaultAsset)
                .WithMany()
                .HasForeignKey(x => x.DefaultAssetId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DefaultBranch)
                .WithMany()
                .HasForeignKey(x => x.DefaultBranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.DefaultAssignmentGroup)
                .WithMany()
                .HasForeignKey(x => x.DefaultAssignmentGroupId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NotificationSetting>(entity =>
        {
            entity.ToTable("notification_settings");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.NotificationSettings)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailDeliveryLog>(entity =>
        {
            entity.ToTable("email_delivery_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TemplateKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.RecipientEmail).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ErrorCategory).HasMaxLength(120);
            entity.Property(x => x.ErrorMessage).HasMaxLength(4000);
            entity.Property(x => x.DocumentType).HasMaxLength(80);
            entity.HasIndex(x => new { x.TenantId, x.DocumentType, x.DocumentId });
            entity.Property(x => x.ProviderMessageId).HasMaxLength(200);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.EventKey);
            entity.HasIndex(x => x.TemplateKey);
            entity.HasIndex(x => x.OutboxMessageId).IsUnique(false);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.OutboxMessage)
                .WithMany()
                .HasForeignKey(x => x.OutboxMessageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EmailOutboxMessage>(entity =>
        {
            entity.ToTable("email_outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TemplateKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.RecipientEmail).HasMaxLength(256).IsRequired();
            entity.Property(x => x.RecipientName).HasMaxLength(200);
            entity.Property(x => x.SenderName).HasMaxLength(200);
            entity.Property(x => x.SenderEmail).HasMaxLength(256);
            entity.Property(x => x.ReplyToEmail).HasMaxLength(256);
            entity.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            entity.Property(x => x.HtmlBody).HasMaxLength(64000);
            entity.Property(x => x.TextBody).HasMaxLength(32000);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ErrorCategory).HasMaxLength(120);
            entity.Property(x => x.ErrorMessage).HasMaxLength(4000);
            entity.Property(x => x.CcAddressesJson).HasColumnType("text");
            entity.Property(x => x.AttachmentsJson).HasColumnType("text");
            entity.Property(x => x.DocumentType).HasMaxLength(80);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.DocumentType, x.DocumentId });
            entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
            entity.HasIndex(x => x.CreatedAt);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PlatformQuotation>(entity =>
        {
            entity.ToTable("platform_quotations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuotationNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CustomerEmail).HasMaxLength(256);
            entity.Property(x => x.Currency).HasMaxLength(12).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.DiscountRate).HasPrecision(9, 4);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxRate).HasPrecision(9, 4);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.HasIndex(x => x.QuotationNumber).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.PlatformQuotations)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Quotation)
                .HasForeignKey(x => x.QuotationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlatformQuotationLine>(entity =>
        {
            entity.ToTable("platform_quotation_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PlatformInvoice>(entity =>
        {
            entity.ToTable("platform_invoices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.InvoiceNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CustomerEmail).HasMaxLength(256);
            entity.Property(x => x.Currency).HasMaxLength(12).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.DiscountRate).HasPrecision(9, 4);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxRate).HasPrecision(9, 4);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.Property(x => x.AmountPaid).HasPrecision(18, 2);
            entity.Property(x => x.Balance).HasPrecision(18, 2);
            entity.HasIndex(x => x.InvoiceNumber).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.PlatformInvoices)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Quotation)
                .WithMany()
                .HasForeignKey(x => x.QuotationId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Invoice)
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlatformInvoiceLine>(entity =>
        {
            entity.ToTable("platform_invoice_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PlatformPayment>(entity =>
        {
            entity.ToTable("platform_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PaymentNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(12).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => x.PaymentNumber).IsUnique();
            entity.HasOne(x => x.Invoice)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.PlatformPayments)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PlatformExpense>(entity =>
        {
            entity.ToTable("platform_expenses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Vendor).HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(12).IsRequired();
            entity.Property(x => x.PaymentMethod).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.AttachmentUrl).HasMaxLength(1000);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.PlatformExpenses)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PlatformDocumentTemplate>(entity =>
        {
            entity.ToTable("platform_document_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PreviewText).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(300);
            entity.Property(x => x.HeaderHtml).HasMaxLength(12000);
            entity.Property(x => x.BodyHtml).HasMaxLength(64000);
            entity.Property(x => x.FooterHtml).HasMaxLength(12000);
            entity.Property(x => x.TermsHtml).HasMaxLength(24000);
            entity.Property(x => x.SignatureHtml).HasMaxLength(8000);
            entity.Property(x => x.PageSize).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Orientation).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.Type, x.IsDefault });
        });

        modelBuilder.Entity<PlatformSetting>(entity =>
        {
            entity.ToTable("platform_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.JsonValue).HasMaxLength(64000).IsRequired();
            entity.HasIndex(x => x.Category).IsUnique();
        });

        modelBuilder.Entity<PlatformAnnouncement>(entity =>
        {
            entity.ToTable("platform_announcements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(8000).IsRequired();
            entity.Property(x => x.Audience).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.ToTable("attachments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.MimeType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.PublicUrl).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.UploadedByUser)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkOrderPhotoEvidence>(entity =>
        {
            entity.ToTable("work_order_photo_evidence");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Caption).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.UploadedAt });
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.WorkOrderPhotoEvidence)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany(x => x.PhotoEvidence)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Attachment)
                .WithMany()
                .HasForeignKey(x => x.AttachmentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.UploadedByUser)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkOrderSignature>(entity =>
        {
            entity.ToTable("work_order_signatures");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SignatureType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SignerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SignerRole).HasMaxLength(150);
            entity.Property(x => x.SignatureDataUrl).HasMaxLength(32000).IsRequired();
            entity.Property(x => x.Comment).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.SignatureType }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.WorkOrderSignatures)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany(x => x.Signatures)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CapturedByUser)
                .WithMany()
                .HasForeignKey(x => x.CapturedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkOrderMaterialUsage>(entity =>
        {
            entity.ToTable("work_order_material_usages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuantityUsed).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.MaterialItemId, x.UsedAt });
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.WorkOrderMaterialUsages)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany(x => x.MaterialUsages)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem)
                .WithMany()
                .HasForeignKey(x => x.MaterialItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Asset)
                .WithMany()
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.UsedByUser)
                .WithMany()
                .HasForeignKey(x => x.UsedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClientPreferentialRate>(entity =>
        {
            entity.ToTable("client_preferential_rates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RateName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.RateType).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ItemType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.DefaultRate).HasPrecision(18, 2);
            entity.Property(x => x.PreferentialRate).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(12).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.ClientId, x.RateName, x.ItemType, x.EffectiveFrom }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany(x => x.ClientPreferentialRates).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client).WithMany(x => x.PreferentialRates).HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ApprovedByUser).WithMany(x => x.ApprovedPreferentialRates).HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FinanceApprovalRule>(entity =>
        {
            entity.ToTable("finance_approval_rules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ApprovalArea).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ThresholdAmount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(12).IsRequired();
            entity.Property(x => x.RequiredRole).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.ApprovalArea, x.Sequence }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany(x => x.FinanceApprovalRules).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RequiredApproverUser).WithMany().HasForeignKey(x => x.RequiredApproverUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.RequiredAssignmentGroup).WithMany().HasForeignKey(x => x.RequiredAssignmentGroupId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MaterialIssue>(entity =>
        {
            entity.ToTable("material_issues");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IssueNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.IssueStatus).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RejectionReason).HasMaxLength(1000);
            entity.Property(x => x.TotalEstimatedCost).HasPrecision(18, 2);
            entity.Property(x => x.TotalIssuedCost).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.IssueNumber }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany(x => x.MaterialIssues).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch).WithMany(x => x.MaterialIssues).HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.WorkOrder).WithMany(x => x.MaterialIssues).HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.System).WithMany(x => x.MaterialIssues).HasForeignKey(x => x.SystemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.RequestedByUser).WithMany(x => x.MaterialIssuesRequested).HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.IssuedByUser).WithMany(x => x.MaterialIssuesIssued).HasForeignKey(x => x.IssuedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ApprovedByUser).WithMany(x => x.MaterialIssuesApproved).HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.RejectedByUser).WithMany().HasForeignKey(x => x.RejectedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.IssuedToUser).WithMany(x => x.MaterialIssuesReceived).HasForeignKey(x => x.IssuedToUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MaterialIssueLine>(entity =>
        {
            entity.ToTable("material_issue_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuantityRequested).HasPrecision(18, 2);
            entity.Property(x => x.QuantityIssued).HasPrecision(18, 2);
            entity.Property(x => x.QuantityReturned).HasPrecision(18, 2);
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.MaterialIssue).WithMany(x => x.Lines).HasForeignKey(x => x.MaterialIssueId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssetCategory>(entity =>
        {
            entity.ToTable("asset_categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DefaultCriticality).HasMaxLength(1);
            entity.Property(x => x.DefaultMeterTypes).HasMaxLength(200);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.ContractorType).HasMaxLength(120);
            entity.Property(x => x.ParentCategoryName).HasMaxLength(150);
            entity.Property(x => x.Icon).HasMaxLength(100);
            entity.Property(x => x.IsSystemTemplate).HasDefaultValue(false);
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.HasIndex(x => new { x.TemplatePackId, x.Code }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TemplatePack)
                .WithMany(x => x.AssetCategories)
                .HasForeignKey(x => x.TemplatePackId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.Fields)
                .WithOne(x => x.AssetCategory)
                .HasForeignKey(x => x.AssetCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssetCategoryField>(entity =>
        {
            entity.ToTable("asset_category_fields");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FieldName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FieldLabel).HasMaxLength(200).IsRequired();
            entity.Property(x => x.FieldType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(50);
            entity.Property(x => x.HelpText).HasMaxLength(1000);
        });

        modelBuilder.Entity<AssetCustomFieldValue>(entity =>
        {
            entity.ToTable("asset_custom_field_values");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Value).HasMaxLength(4000);
            entity.HasIndex(x => new { x.AssetId, x.FieldDefinitionId }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset)
                .WithMany(x => x.CustomFieldValues)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FieldDefinition)
                .WithMany()
                .HasForeignKey(x => x.FieldDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.ToTable("sites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SiteCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.SiteName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SiteType).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired();
            entity.Property(x => x.StreetAddress).HasMaxLength(300);
            entity.Property(x => x.AreaEstate).HasMaxLength(200);
            entity.Property(x => x.TownCity).HasMaxLength(100);
            entity.Property(x => x.County).HasMaxLength(100);
            entity.Property(x => x.Country).HasMaxLength(100);
            entity.Property(x => x.Region).HasMaxLength(100);
            entity.Property(x => x.ContactPerson).HasMaxLength(200);
            entity.Property(x => x.ContactPhone).HasMaxLength(50);
            entity.Property(x => x.ContactEmail).HasMaxLength(256);
            entity.Property(x => x.AlternateContact).HasMaxLength(200);
            entity.Property(x => x.OperatingHours).HasMaxLength(500);
            entity.Property(x => x.AccessNotes).HasMaxLength(2000);
            entity.Property(x => x.SpecialInstructions).HasMaxLength(2000);
            entity.Property(x => x.Latitude).HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasPrecision(9, 6);
            entity.HasIndex(x => new { x.TenantId, x.SiteCode }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client)
                .WithMany(x => x.Sites)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.ToTable("contracts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ContractNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ContractType).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(x => x.BillingModel).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(12).IsRequired();
            entity.Property(x => x.ContractValue).HasPrecision(18, 2);
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.IncludedServices).HasMaxLength(4000);
            entity.Property(x => x.ExcludedServices).HasMaxLength(4000);
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.Property(x => x.RenewalBehavior).HasMaxLength(40).IsRequired();
            entity.Property(x => x.TerminationReason).HasMaxLength(2000);
            entity.Property(x => x.SuspensionReason).HasMaxLength(2000);
            entity.Property(x => x.CancellationReason).HasMaxLength(2000);
            entity.Property(x => x.RenewalRisk).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.ResponseTimeHours).HasPrecision(10, 2);
            entity.Property(x => x.ResolutionTimeHours).HasPrecision(10, 2);
            entity.Property(x => x.WorkingHoursPolicy).HasMaxLength(200);
            entity.Property(x => x.EscalationEmail).HasMaxLength(256);
            entity.Property(x => x.PriorityLevel).HasMaxLength(100);
            entity.Property(x => x.GracePeriodPolicy).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SlaMatrixJson).HasColumnType("text");
            entity.HasIndex(x => new { x.TenantId, x.ContractNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ClientId, x.Status, x.EndDate });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client).WithMany(x => x.Contracts).HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RenewalOwnerUser).WithMany(x => x.RenewalOwnedContracts).HasForeignKey(x => x.RenewalOwnerUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AccountManagerUser).WithMany(x => x.AccountManagedContracts).HasForeignKey(x => x.AccountManagerUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.InternalApproverUser).WithMany(x => x.InternallyApprovedContracts).HasForeignKey(x => x.InternalApproverUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.RenewedFromContract).WithOne(x => x.RenewedToContract).HasForeignKey<Contract>(x => x.RenewedFromContractId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ContractSite>(entity =>
        {
            entity.ToTable("contract_sites");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.ContractId, x.SiteId }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contract).WithMany(x => x.Sites).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany(x => x.ContractSites).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractAsset>(entity =>
        {
            entity.ToTable("contract_assets");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.ContractId, x.AssetId }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contract).WithMany(x => x.Assets).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany(x => x.ContractAssets).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractAssetCategory>(entity =>
        {
            entity.ToTable("contract_asset_categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AssetCategoryName).HasMaxLength(160);
            entity.HasIndex(x => new { x.TenantId, x.ContractId, x.AssetCategoryId, x.AssetCategoryName }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contract).WithMany(x => x.AssetCategories).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AssetCategory).WithMany(x => x.ContractAssetCategories).HasForeignKey(x => x.AssetCategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ContractWorkOrderType>(entity =>
        {
            entity.ToTable("contract_work_order_types");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WorkOrderTypeName).HasMaxLength(120);
            entity.HasIndex(x => new { x.TenantId, x.ContractId, x.WorkOrderTypeId, x.WorkOrderTypeName }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contract).WithMany(x => x.WorkOrderTypes).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractLifecycleEvent>(entity =>
        {
            entity.ToTable("contract_lifecycle_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(x => x.OldStatus).HasConversion<string>().HasMaxLength(80);
            entity.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(80);
            entity.Property(x => x.Note).HasMaxLength(4000);
            entity.Property(x => x.Reason).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.ContractId, x.CreatedAt });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contract).WithMany(x => x.LifecycleEvents).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ContractReminderRule>(entity =>
        {
            entity.ToTable("contract_reminder_rules");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.ContractId, x.DaysBeforeExpiry }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contract).WithMany(x => x.ReminderRules).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractRateCard>(entity =>
        {
            entity.ToTable("contract_rate_cards");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Currency).HasMaxLength(12).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.ContractId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contract).WithMany(x => x.RateCards).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractRateRule>(entity =>
        {
            entity.ToTable("contract_rate_rules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WorkOrderTypeName).HasMaxLength(120);
            entity.Property(x => x.AssetCategoryName).HasMaxLength(160);
            entity.Property(x => x.Priority).HasMaxLength(60);
            entity.Property(x => x.BillingModel).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(x => x.CalloutFee).HasPrecision(18, 2);
            entity.Property(x => x.FixedVisitFee).HasPrecision(18, 2);
            entity.Property(x => x.LabourHourlyRate).HasPrecision(18, 2);
            entity.Property(x => x.OvertimeHourlyRate).HasPrecision(18, 2);
            entity.Property(x => x.WeekendHourlyRate).HasPrecision(18, 2);
            entity.Property(x => x.PublicHolidayHourlyRate).HasPrecision(18, 2);
            entity.Property(x => x.EmergencyMultiplier).HasPrecision(10, 2);
            entity.Property(x => x.TravelCharge).HasPrecision(18, 2);
            entity.Property(x => x.MaterialMarkupPercent).HasPrecision(10, 2);
            entity.Property(x => x.DiscountPercent).HasPrecision(10, 2);
            entity.Property(x => x.MinimumBillableHours).HasPrecision(10, 2);
            entity.Property(x => x.IncludedLabourHours).HasPrecision(10, 2);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.RateCardId, x.SiteId, x.AssetCategoryId, x.WorkOrderTypeId, x.Priority });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RateCard).WithMany(x => x.Rules).HasForeignKey(x => x.RateCardId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AssetCategory).WithMany().HasForeignKey(x => x.AssetCategoryId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ContractPmEntitlement>(entity =>
        {
            entity.ToTable("contract_pm_entitlements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AssetCategory).HasMaxLength(160);
            entity.Property(x => x.WorkOrderType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PeriodType).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.ContractId, x.AssetId, x.AssetCategory, x.WorkOrderType, x.EffectiveFrom });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contract).WithMany(x => x.PmEntitlements).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ContractRenewal>(entity =>
        {
            entity.ToTable("contract_renewals");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RenewalStatus).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.HasIndex(x => new { x.TenantId, x.OriginalContractId }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.OriginalContract).WithMany(x => x.Renewals).HasForeignKey(x => x.OriginalContractId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RenewedContract).WithMany().HasForeignKey(x => x.RenewedContractId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.OwnerUser).WithMany(x => x.ContractRenewalsOwned).HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PmTemplateSection>(entity =>
        {
            entity.ToTable("pm_template_sections");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SectionName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasOne(x => x.PmTemplate)
                .WithMany(x => x.Sections)
                .HasForeignKey(x => x.PmTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        ApplyTenantQueryFilters(modelBuilder);
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType is null || !typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            typeof(AppDbContext)
                .GetMethod(nameof(ApplyTenantFilter), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [modelBuilder]);
        }

        ApplyOptionalTenantFilter<AssetCategory>(modelBuilder, entity =>
            ShouldBypassTenantFilter
            || entity.TenantId == CurrentTenantFilterId);

        ApplyOptionalTenantFilter<PmTemplate>(modelBuilder, entity =>
            ShouldBypassTenantFilter
            || entity.TenantId == CurrentTenantFilterId);

        modelBuilder.Entity<RateCard>(entity =>
        {
            entity.ToTable("rate_cards");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.UnitRate).HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SkillLevel).HasMaxLength(40);
            entity.Property(x => x.TimeType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.TaxRate).HasPrecision(8, 4);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Category, x.TimeType });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<RateCard>(modelBuilder);

        modelBuilder.Entity<WorkOrderTimeLog>(entity =>
        {
            entity.ToTable("work_order_time_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LabourCost).HasPrecision(18, 2);
            entity.Property(x => x.TechnicianName).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany(x => x.TimeLogs)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Technician)
                .WithMany()
                .HasForeignKey(x => x.TechnicianId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<WorkOrderTimeLog>(modelBuilder);

        // ── WorkOrderVisit ───────────────────────────────────────────────────
        modelBuilder.Entity<WorkOrderVisit>(entity =>
        {
            entity.ToTable("work_order_visits");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId, x.TechnicianId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<WorkOrderVisit>(modelBuilder);

        // ── PartsReturn ──────────────────────────────────────────────────────
        modelBuilder.Entity<PartsReturn>(entity =>
        {
            entity.ToTable("parts_returns");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReturnNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RejectionReason).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.ReturnNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId });
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RequestedByUser).WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProcessedByUser).WithMany().HasForeignKey(x => x.ProcessedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.Lines).WithOne(x => x.PartsReturn).HasForeignKey(x => x.PartsReturnId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<PartsReturn>(modelBuilder);

        modelBuilder.Entity<StockCount>(entity =>
        {
            entity.ToTable("stock_counts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CountNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.CountNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(x => x.IsBlind).HasDefaultValue(false);
            entity.HasOne(x => x.StartedByUser).WithMany().HasForeignKey(x => x.StartedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PostedByUser).WithMany().HasForeignKey(x => x.PostedByUserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.Lines).WithOne(x => x.StockCount).HasForeignKey(x => x.StockCountId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<StockCount>(modelBuilder);

        modelBuilder.Entity<HeldPart>(entity =>
        {
            entity.ToTable("held_parts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.SourceReference).HasMaxLength(60);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.ResolutionReference).HasMaxLength(100);
            entity.Property(x => x.ResolutionNote).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ResolvedByUser).WithMany().HasForeignKey(x => x.ResolvedByUserId).OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<HeldPart>(modelBuilder);

        modelBuilder.Entity<DirectPurchase>(entity =>
        {
            entity.ToTable("direct_purchases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.SupplierName).HasMaxLength(200);
            entity.Property(x => x.SupplierReference).HasMaxLength(100);
            entity.Property(x => x.InvoiceReference).HasMaxLength(100);
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.PurchasedByUser).WithMany().HasForeignKey(x => x.PurchasedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ReconciledByUser).WithMany().HasForeignKey(x => x.ReconciledByUserId).OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<DirectPurchase>(modelBuilder);

        modelBuilder.Entity<StockCountLine>(entity =>
        {
            entity.ToTable("stock_count_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NeedsRecount).HasDefaultValue(false);
            entity.Property(x => x.ExpectedQuantity).HasPrecision(18, 2);
            entity.Property(x => x.CountedQuantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => new { x.StockCountId, x.MaterialItemId }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
        });
        ApplyTenantFilter<StockCountLine>(modelBuilder);

        modelBuilder.Entity<Tool>(entity =>
        {
            entity.ToTable("tools");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ToolCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.SerialNumber).HasMaxLength(100);
            entity.Property(x => x.Manufacturer).HasMaxLength(100);
            entity.Property(x => x.Model).HasMaxLength(100);
            entity.Property(x => x.PurchaseCost).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Condition).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.ToolCode }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.HomeBranch).WithMany().HasForeignKey(x => x.HomeBranchId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CurrentAssignment).WithMany().HasForeignKey(x => x.CurrentAssignmentId).OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.Assignments).WithOne(x => x.Tool).HasForeignKey(x => x.ToolId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Calibrations).WithOne(x => x.Tool).HasForeignKey(x => x.ToolId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<Tool>(modelBuilder);

        modelBuilder.Entity<ToolAssignment>(entity =>
        {
            entity.ToTable("tool_assignments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ConditionOut).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ConditionIn).HasMaxLength(30);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.ReturnNotes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.TechnicianId, x.ReturnedAt });
            entity.HasIndex(x => x.ToolId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CheckedOutByUser).WithMany().HasForeignKey(x => x.CheckedOutByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ReturnedByUser).WithMany().HasForeignKey(x => x.ReturnedByUserId).OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<ToolAssignment>(modelBuilder);

        modelBuilder.Entity<ToolCalibration>(entity =>
        {
            entity.ToTable("tool_calibrations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CertificateNumber).HasMaxLength(100);
            entity.Property(x => x.PerformedBy).HasMaxLength(200);
            entity.Property(x => x.Result).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => x.ToolId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ToolCalibration>(modelBuilder);

        modelBuilder.Entity<PartsReturnLine>(entity =>
        {
            entity.ToTable("parts_return_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.Condition).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.MaterialItem).WithMany().HasForeignKey(x => x.MaterialItemId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── TenantInvoice ────────────────────────────────────────────────────
        // ── TenantPayment ────────────────────────────────────────────────────
        // ── PaymentAllocation ────────────────────────────────────────────────
        // ── CreditNote ───────────────────────────────────────────────────────
        // ── TenantQuotation ──────────────────────────────────────────────────
        // ── Sales CRM ────────────────────────────────────────────────────────
        modelBuilder.Entity<ClientContact>(entity =>
        {
            entity.ToTable("client_contacts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.JobTitle).HasMaxLength(120);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Role).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.TenantId, x.ClientId });
            entity.HasIndex(x => new { x.TenantId, x.Email });
        });
        ApplyTenantFilter<ClientContact>(modelBuilder);

        // ── CreditTerm ───────────────────────────────────────────────────────
        // ── ScheduledReport ──────────────────────────────────────────────────
        modelBuilder.Entity<ScheduledReport>(entity =>
        {
            entity.HasOne<OrgNode>().WithMany().HasForeignKey(x => x.OrgNodeId).OnDelete(DeleteBehavior.SetNull);
            entity.ToTable("scheduled_reports");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ReportType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Frequency).HasMaxLength(20).IsRequired();
            entity.Property(x => x.DayOfWeek).HasMaxLength(10);
            entity.Property(x => x.TimeOfDay).HasMaxLength(5);
            entity.Property(x => x.DateRangePreset).HasMaxLength(30);
            entity.Property(x => x.GroupBy).HasMaxLength(30);
            entity.Property(x => x.ClientId).HasMaxLength(100);
            entity.Property(x => x.Recipients).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.LastRunStatus).HasMaxLength(20);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ScheduledReport>(modelBuilder);

        modelBuilder.Entity<AttendanceEntry>(entity =>
        {
            entity.ToTable("attendance_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired().HasDefaultValue("ClockedIn");
            entity.Property(x => x.LastLocation).HasMaxLength(300);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.TechnicianId, x.WorkDate });
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Technician)
                .WithMany()
                .HasForeignKey(x => x.TechnicianId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CurrentWorkOrder)
                .WithMany()
                .HasForeignKey(x => x.CurrentWorkOrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<AttendanceEntry>(modelBuilder);

        modelBuilder.Entity<LeaveType>(entity =>
        {
            entity.ToTable("leave_types");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.AnnualAllowanceDays).HasPrecision(6, 2);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<LeaveType>(modelBuilder);

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.ToTable("leave_requests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LeaveTypeCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired().HasDefaultValue("Pending");
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.DecisionNote).HasMaxLength(1000);
            entity.Property(x => x.TotalDays).HasPrecision(6, 2);
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.TechnicianId });
            entity.HasIndex(x => new { x.TenantId, x.UserId });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Technician)
                .WithMany()
                .HasForeignKey(x => x.TechnicianId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DecidedByUser)
                .WithMany()
                .HasForeignKey(x => x.DecidedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<LeaveRequest>(modelBuilder);

        modelBuilder.Entity<TimeAttendanceSetting>(entity =>
        {
            entity.ToTable("time_attendance_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OvertimeThresholdHoursPerDay).HasPrecision(5, 2);
            entity.Property(x => x.AutoClockOutAfterHours).HasPrecision(5, 2);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<TimeAttendanceSetting>(modelBuilder);

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SupplierName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ContactPerson).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.SupplierName });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<Supplier>(modelBuilder);

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("purchase_orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PoNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired().HasDefaultValue("Draft");
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired().HasDefaultValue("KES");
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.PoNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.PurchaseOrders)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        ApplyTenantFilter<PurchaseOrder>(modelBuilder);

        modelBuilder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.ToTable("purchase_order_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ItemName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.QuantityOrdered).HasPrecision(14, 3);
            entity.Property(x => x.QuantityReceived).HasPrecision(14, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
            entity.HasIndex(x => x.PurchaseOrderId);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PurchaseOrder)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.MaterialItemId);
            entity.HasOne(x => x.MaterialItem)
                .WithMany()
                .HasForeignKey(x => x.MaterialItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        ApplyTenantFilter<PurchaseOrderLine>(modelBuilder);

        modelBuilder.Entity<PurchaseOrderReceiptEvent>(entity =>
        {
            entity.ToTable("purchase_order_receipt_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ItemName).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(14, 3);
            entity.Property(x => x.ReceivedByName).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.PurchaseOrderId);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PurchaseOrder)
                .WithMany(x => x.ReceiptEvents)
                .HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PurchaseOrderLine)
                .WithMany()
                .HasForeignKey(x => x.PurchaseOrderLineId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany()
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<PurchaseOrderReceiptEvent>(modelBuilder);

        modelBuilder.Entity<ExchangeRate>(entity =>
        {
            entity.ToTable("exchange_rates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BaseCurrency).HasMaxLength(10).IsRequired();
            entity.Property(x => x.QuoteCurrency).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Rate).HasPrecision(18, 6);
            entity.Property(x => x.UpdatedByName).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantId, x.BaseCurrency, x.QuoteCurrency, x.EffectiveFrom });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ExchangeRate>(modelBuilder);

        modelBuilder.Entity<WhatsAppSetting>(entity =>
        {
            entity.ToTable("whatsapp_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BusinessAccountId).HasMaxLength(120);
            entity.Property(x => x.PhoneNumberId).HasMaxLength(120);
            entity.Property(x => x.AccessToken).HasMaxLength(500);
            entity.Property(x => x.WebhookVerifyToken).HasMaxLength(200);
            entity.Property(x => x.AutoReplyMessage).HasMaxLength(1000);
            entity.Property(x => x.AllowedNotificationKeys).HasMaxLength(4000);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<WhatsAppSetting>(modelBuilder);

        modelBuilder.Entity<WhatsAppConversation>(entity =>
        {
            entity.ToTable("whatsapp_conversations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ContactName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ContactPhone).HasMaxLength(40).IsRequired();
            entity.Property(x => x.LastMessagePreview).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.LastMessageAt });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<WhatsAppConversation>(modelBuilder);

        modelBuilder.Entity<WhatsAppMessage>(entity =>
        {
            entity.ToTable("whatsapp_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Direction).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TemplateName).HasMaxLength(200);
            entity.HasIndex(x => x.ConversationId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<WhatsAppMessage>(modelBuilder);

        modelBuilder.Entity<WhatsAppTemplate>(entity =>
        {
            entity.ToTable("whatsapp_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Language).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.BodyPreview).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<WhatsAppTemplate>(modelBuilder);

        // WhatsApp reminders (Release 3 design 4.4).
        modelBuilder.Entity<WhatsAppOutboxMessage>(entity =>
        {
            entity.ToTable("whatsapp_outbox_messages", tableBuilder =>
                tableBuilder.HasCheckConstraint("CK_whatsapp_outbox_messages_status",
                    "\"Status\" IN ('Pending', 'Sending', 'Sent', 'Delivered', 'Read', 'Failed', 'Skipped')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NotificationKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.TemplateName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Language).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ParametersJson).HasMaxLength(4000);
            entity.Property(x => x.ToPhone).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RecipientName).HasMaxLength(200);
            entity.Property(x => x.EntityType).HasMaxLength(80);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ProviderMessageId).HasMaxLength(128);
            entity.Property(x => x.ErrorMessage).HasMaxLength(1000);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(200);
            // The worker's queue.
            entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
            // The tenant's log and monthly count.
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasIndex(x => x.ProviderMessageId).IsUnique().HasFilter("\"ProviderMessageId\" IS NOT NULL");
            entity.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<WhatsAppOutboxMessage>(modelBuilder);

        // Platform-wide by design: no tenant, no tenant filter.
        modelBuilder.Entity<WhatsAppOptOut>(entity =>
        {
            entity.ToTable("whatsapp_opt_outs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Keyword).HasMaxLength(40);
            entity.HasIndex(x => x.PhoneNumber).IsUnique();
        });

        modelBuilder.Entity<WhatsAppVerificationCode>(entity =>
        {
            entity.ToTable("whatsapp_verification_codes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CodeHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.ClientPortalUserId, x.CreatedAt });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<WhatsAppVerificationCode>(modelBuilder);

        modelBuilder.Entity<TenantModuleSetting>(entity =>
        {
            entity.ToTable("tenant_module_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ModuleKey).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ValueJson).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.ModuleKey }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<TenantModuleSetting>(modelBuilder);

        modelBuilder.Entity<UserDashboardLayout>(entity =>
        {
            entity.ToTable("user_dashboard_layouts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LayoutJson).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<UserDashboardLayout>(modelBuilder);

        ConfigureActivities(modelBuilder);
    }

    private void ConfigureActivities(ModelBuilder modelBuilder)
    {
        // ── Activities, action items and the calendar feed (Release 3 step 3b) ──

        modelBuilder.Entity<Activity>(entity =>
        {
            entity.ToTable("activities", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("CK_activities_type", "\"Type\" IN ('Meeting', 'SiteVisit', 'Inspection', 'Call')");
                tableBuilder.HasCheckConstraint("CK_activities_status", "\"Status\" IN ('Scheduled', 'Held', 'Cancelled')");
                tableBuilder.HasCheckConstraint("CK_activities_times", "\"EndsAt\" >= \"StartsAt\"");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(500);
            entity.Property(x => x.EntityType).HasMaxLength(30);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Agenda).HasMaxLength(4000);
            entity.Property(x => x.Minutes).HasMaxLength(20000);
            entity.Property(x => x.CancellationReason).HasMaxLength(500);
            // The calendar (by time), the linked record's page, and the reminder worker.
            entity.HasIndex(x => new { x.TenantId, x.StartsAt });
            entity.HasIndex(x => new { x.TenantId, x.OrganizerUserId, x.StartsAt });
            entity.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
            entity.HasIndex(x => new { x.Status, x.StartsAt });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.OrganizerUser).WithMany().HasForeignKey(x => x.OrganizerUserId).OnDelete(DeleteBehavior.Restrict);
        });
        ApplyTenantFilter<Activity>(modelBuilder);

        modelBuilder.Entity<ActivityAttendee>(entity =>
        {
            entity.ToTable("activity_attendees", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("CK_activity_attendees_response", "\"Response\" IN ('Pending', 'Accepted', 'Declined')");
                // Exactly one kind of attendee.
                tableBuilder.HasCheckConstraint("CK_activity_attendees_one_kind",
                    "(CASE WHEN \"UserId\" IS NULL THEN 0 ELSE 1 END)"
                    + " + (CASE WHEN \"ClientContactId\" IS NULL THEN 0 ELSE 1 END)"
                    + " + (CASE WHEN \"ExternalEmail\" IS NULL AND \"ExternalName\" IS NULL THEN 0 ELSE 1 END) = 1");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalName).HasMaxLength(200);
            entity.Property(x => x.ExternalEmail).HasMaxLength(256);
            entity.Property(x => x.Response).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.ActivityId });
            // "Activities I attend" for the calendar and the reminder worker.
            entity.HasIndex(x => new { x.TenantId, x.UserId });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Activity).WithMany(x => x.Attendees).HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ClientContact).WithMany().HasForeignKey(x => x.ClientContactId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ActivityAttendee>(modelBuilder);

        modelBuilder.Entity<ActivityMinutesAddendum>(entity =>
        {
            entity.ToTable("activity_minutes_addenda");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Text).HasMaxLength(4000).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.ActivityId });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Activity).WithMany(x => x.Addenda).HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AuthorUser).WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });
        ApplyTenantFilter<ActivityMinutesAddendum>(modelBuilder);

        modelBuilder.Entity<ActionItem>(entity =>
        {
            entity.ToTable("action_items", tableBuilder =>
                tableBuilder.HasCheckConstraint("CK_action_items_status", "\"Status\" IN ('Open', 'Done', 'Cancelled')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.AssigneeExternalName).HasMaxLength(200);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(30);
            entity.HasIndex(x => new { x.TenantId, x.AssigneeUserId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
            entity.HasIndex(x => new { x.TenantId, x.ActivityId });
            entity.HasIndex(x => new { x.Status, x.DueDate });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Activity).WithMany().HasForeignKey(x => x.ActivityId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AssigneeUser).WithMany().HasForeignKey(x => x.AssigneeUserId).OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<ActionItem>(modelBuilder);

        modelBuilder.Entity<ActivityReminderLog>(entity =>
        {
            entity.ToTable("activity_reminder_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NotificationKey).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.EntityId, x.NotificationKey, x.ForTime }).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
        });
        ApplyTenantFilter<ActivityReminderLog>(modelBuilder);

        modelBuilder.Entity<CalendarFeedToken>(entity =>
        {
            entity.ToTable("calendar_feed_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            // The anonymous feed looks a token up by its hash alone.
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.UserId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<CalendarFeedToken>(modelBuilder);

    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(entity =>
                ShouldBypassTenantFilter
                || entity.TenantId == CurrentTenantFilterId);
    }

    private static void ApplyOptionalTenantFilter<TEntity>(
        ModelBuilder modelBuilder,
        Expression<Func<TEntity, bool>> filter)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }

    private void ApplyAuditFields()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }
    }

    private void NormalizeUtcFields()
    {
        foreach (var entry in ChangeTracker.Entries<Tenant>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.TrialEndsAt = EnsureUtc(entry.Entity.TrialEndsAt);
            entry.Entity.SubscriptionStartsAt = EnsureUtc(entry.Entity.SubscriptionStartsAt);
            entry.Entity.SubscriptionEndsAt = EnsureUtc(entry.Entity.SubscriptionEndsAt);
            entry.Entity.SuspendedAt = EnsureUtc(entry.Entity.SuspendedAt);
            entry.Entity.DeactivatedAt = EnsureUtc(entry.Entity.DeactivatedAt);
        }

        foreach (var entry in ChangeTracker.Entries<TenantLicense>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.StartsAt = EnsureUtc(entry.Entity.StartsAt);
            entry.Entity.TrialEndsAt = EnsureUtc(entry.Entity.TrialEndsAt);
            entry.Entity.ExpiresAt = EnsureUtc(entry.Entity.ExpiresAt);
            entry.Entity.NextBillingDate = EnsureUtc(entry.Entity.NextBillingDate);
            entry.Entity.SuspendedAt = EnsureUtc(entry.Entity.SuspendedAt);
            entry.Entity.CancelledAt = EnsureUtc(entry.Entity.CancelledAt);
            entry.Entity.OverdueWarningSentAt = EnsureUtc(entry.Entity.OverdueWarningSentAt);
        }

        foreach (var entry in ChangeTracker.Entries<PlatformQuotation>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.ValidUntil = EnsureUtc(entry.Entity.ValidUntil);
        }

        foreach (var entry in ChangeTracker.Entries<PlatformInvoice>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.IssueDate = EnsureUtc(entry.Entity.IssueDate);
            entry.Entity.DueDate = EnsureUtc(entry.Entity.DueDate);
        }

        foreach (var entry in ChangeTracker.Entries<WorkOrder>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.DueDate = EnsureUtc(entry.Entity.DueDate);
            entry.Entity.WorkStartedAt = EnsureUtc(entry.Entity.WorkStartedAt);
            entry.Entity.ArrivalAt = EnsureUtc(entry.Entity.ArrivalAt);
            entry.Entity.DepartureAt = EnsureUtc(entry.Entity.DepartureAt);
            entry.Entity.CompletedAt = EnsureUtc(entry.Entity.CompletedAt);
            entry.Entity.ClosedAt = EnsureUtc(entry.Entity.ClosedAt);
            entry.Entity.AcknowledgementDate = EnsureUtc(entry.Entity.AcknowledgementDate);
            entry.Entity.SlaResponseDeadline = EnsureUtc(entry.Entity.SlaResponseDeadline);
            entry.Entity.SlaResolutionDeadline = EnsureUtc(entry.Entity.SlaResolutionDeadline);
            entry.Entity.SlaResponseBreachedAt = EnsureUtc(entry.Entity.SlaResponseBreachedAt);
            entry.Entity.SlaResolutionBreachedAt = EnsureUtc(entry.Entity.SlaResolutionBreachedAt);
        }

        foreach (var entry in ChangeTracker.Entries<Client>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.ActiveStartDate = EnsureUtc(entry.Entity.ActiveStartDate);
            entry.Entity.ActiveEndDate = EnsureUtc(entry.Entity.ActiveEndDate);
        }

        foreach (var entry in ChangeTracker.Entries<OperationalSystem>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.InstallationDate = EnsureUtc(entry.Entity.InstallationDate);
        }

        foreach (var entry in ChangeTracker.Entries<Asset>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.InstallationDate = EnsureUtc(entry.Entity.InstallationDate);
            entry.Entity.CommissioningDate = EnsureUtc(entry.Entity.CommissioningDate);
            entry.Entity.DecommissionDate = EnsureUtc(entry.Entity.DecommissionDate);
            entry.Entity.WarrantyExpiryDate = EnsureUtc(entry.Entity.WarrantyExpiryDate);
            entry.Entity.WarrantyStartDate = EnsureUtc(entry.Entity.WarrantyStartDate);
            entry.Entity.LastPmDate = EnsureUtc(entry.Entity.LastPmDate);
            entry.Entity.NextPmDate = EnsureUtc(entry.Entity.NextPmDate);
        }

        foreach (var entry in ChangeTracker.Entries<PreventiveMaintenancePlan>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.LastPmDate = EnsureUtc(entry.Entity.LastPmDate);
            entry.Entity.NextPmDate = EnsureUtc(entry.Entity.NextPmDate);
            entry.Entity.LastGeneratedAt = EnsureUtc(entry.Entity.LastGeneratedAt);
        }

        foreach (var entry in ChangeTracker.Entries<MaterialIssue>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.IssuedAt = EnsureUtc(entry.Entity.IssuedAt);
            entry.Entity.ApprovedAt = EnsureUtc(entry.Entity.ApprovedAt);
            entry.Entity.RejectedAt = EnsureUtc(entry.Entity.RejectedAt);
        }

        foreach (var entry in ChangeTracker.Entries<PlatformPayment>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.PaidAt = EnsureUtc(entry.Entity.PaidAt);
        }

        foreach (var entry in ChangeTracker.Entries<PlatformExpense>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.ExpenseDate = EnsureUtc(entry.Entity.ExpenseDate);
            entry.Entity.ApprovedAt = EnsureUtc(entry.Entity.ApprovedAt);
        }

        foreach (var entry in ChangeTracker.Entries<PasswordResetToken>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.ExpiresAt = EnsureUtc(entry.Entity.ExpiresAt);
            entry.Entity.UsedAt = EnsureUtc(entry.Entity.UsedAt);
        }

        foreach (var entry in ChangeTracker.Entries<EmailDeliveryLog>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.LastAttemptAt = EnsureUtc(entry.Entity.LastAttemptAt);
            entry.Entity.NextAttemptAt = EnsureUtc(entry.Entity.NextAttemptAt);
            entry.Entity.SentAt = EnsureUtc(entry.Entity.SentAt);
        }

        foreach (var entry in ChangeTracker.Entries<EmailOutboxMessage>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.NextAttemptAt = EnsureUtc(entry.Entity.NextAttemptAt);
            entry.Entity.LastAttemptAt = EnsureUtc(entry.Entity.LastAttemptAt);
            entry.Entity.SentAt = EnsureUtc(entry.Entity.SentAt);
        }
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.ToUniversalTime();
    }

    private static DateTime? EnsureUtc(DateTime? value) =>
        value.HasValue ? EnsureUtc(value.Value) : null;
}
