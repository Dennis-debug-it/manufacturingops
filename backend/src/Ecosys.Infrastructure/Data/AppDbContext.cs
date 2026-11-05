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
    public DbSet<User> Users => Set<User>();
    public DbSet<UserBranchAssignment> UserBranchAssignments => Set<UserBranchAssignment>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<PendingSignup> PendingSignups => Set<PendingSignup>();
    public DbSet<CsatSurvey> CsatSurveys => Set<CsatSurvey>();
    public DbSet<ApprovalMatrix> ApprovalMatrices => Set<ApprovalMatrix>();
    public DbSet<ApprovalLevel> ApprovalLevels => Set<ApprovalLevel>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalRequestStep> ApprovalRequestSteps => Set<ApprovalRequestStep>();
    public DbSet<ClientPortalUser> ClientPortalUsers => Set<ClientPortalUser>();
    public DbSet<ClientPortalLoginToken> ClientPortalLoginTokens => Set<ClientPortalLoginToken>();
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
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<SupplierBill> SupplierBills => Set<SupplierBill>();
    public DbSet<SupplierBillPayment> SupplierBillPayments => Set<SupplierBillPayment>();
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
    public DbSet<JobPlan> JobPlans => Set<JobPlan>();
    public DbSet<JobPlanStep> JobPlanSteps => Set<JobPlanStep>();
    public DbSet<JobPlanResource> JobPlanResources => Set<JobPlanResource>();
    public DbSet<WorkOrderTemplate> WorkOrderTemplates => Set<WorkOrderTemplate>();
    public DbSet<MaterialItem> MaterialItems => Set<MaterialItem>();
    public DbSet<BranchMaterialStock> BranchMaterialStocks => Set<BranchMaterialStock>();
    public DbSet<VanMaterialStock> VanMaterialStocks => Set<VanMaterialStock>();
    public DbSet<Van> Vans => Set<Van>();
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
    public DbSet<TenantFinanceDefaults> TenantFinanceDefaults => Set<TenantFinanceDefaults>();
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
    public DbSet<PlatformLead> PlatformLeads => Set<PlatformLead>();
    public DbSet<CrmContact> CrmContacts => Set<CrmContact>();
    public DbSet<CrmActivity> CrmActivities => Set<CrmActivity>();
    public DbSet<CrmTask> CrmTasks => Set<CrmTask>();
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
    public DbSet<KnowledgeCategory> KnowledgeCategories => Set<KnowledgeCategory>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeDocumentChunk> KnowledgeDocumentChunks => Set<KnowledgeDocumentChunk>();
    public DbSet<KnowledgeTag> KnowledgeTags => Set<KnowledgeTag>();
    public DbSet<KnowledgeArticleTag> KnowledgeArticleTags => Set<KnowledgeArticleTag>();
    public DbSet<KnowledgeArticleVersion> KnowledgeArticleVersions => Set<KnowledgeArticleVersion>();
    public DbSet<KnowledgeArticleFeedback> KnowledgeArticleFeedback => Set<KnowledgeArticleFeedback>();
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
    public DbSet<WorkOrderBillableItem> WorkOrderBillableItems => Set<WorkOrderBillableItem>();
    public DbSet<WorkOrderTimeLog> WorkOrderTimeLogs => Set<WorkOrderTimeLog>();
    public DbSet<WorkOrderVisit> WorkOrderVisits => Set<WorkOrderVisit>();
    public DbSet<PartsReturn> PartsReturns => Set<PartsReturn>();
    public DbSet<PartsReturnLine> PartsReturnLines => Set<PartsReturnLine>();
    public DbSet<TenantInvoice> TenantInvoices => Set<TenantInvoice>();
    public DbSet<TenantInvoiceLine> TenantInvoiceLines => Set<TenantInvoiceLine>();
    public DbSet<TenantPayment> TenantPayments => Set<TenantPayment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();
    public DbSet<CreditNoteLine> CreditNoteLines => Set<CreditNoteLine>();
    public DbSet<TenantQuotation> TenantQuotations => Set<TenantQuotation>();
    public DbSet<TenantQuotationLine> TenantQuotationLines => Set<TenantQuotationLine>();
    public DbSet<SalesOpportunity> SalesOpportunities => Set<SalesOpportunity>();
    public DbSet<SalesActivity> SalesActivities => Set<SalesActivity>();
    public DbSet<SalesTender> SalesTenders => Set<SalesTender>();
    public DbSet<TenantSalesSettings> TenantSalesSettings => Set<TenantSalesSettings>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<SalesAssignmentRule> SalesAssignmentRules => Set<SalesAssignmentRule>();
    public DbSet<SalesStageChange> SalesStageChanges => Set<SalesStageChange>();
    public DbSet<SalesTarget> SalesTargets => Set<SalesTarget>();
    public DbSet<SalesPipeline> SalesPipelines => Set<SalesPipeline>();
    public DbSet<SalesPipelineStage> SalesPipelineStages => Set<SalesPipelineStage>();
    public DbSet<SalesCustomField> SalesCustomFields => Set<SalesCustomField>();
    public DbSet<SalesCustomFieldValue> SalesCustomFieldValues => Set<SalesCustomFieldValue>();
    public DbSet<SalesSequence> SalesSequences => Set<SalesSequence>();
    public DbSet<SalesSequenceStep> SalesSequenceSteps => Set<SalesSequenceStep>();
    public DbSet<SalesSequenceEnrollment> SalesSequenceEnrollments => Set<SalesSequenceEnrollment>();
    public DbSet<SalesSavedView> SalesSavedViews => Set<SalesSavedView>();
    public DbSet<SalesOpportunityContact> SalesOpportunityContacts => Set<SalesOpportunityContact>();
    public DbSet<CreditTerm> CreditTerms => Set<CreditTerm>();
    public DbSet<ScheduledReport> ScheduledReports => Set<ScheduledReport>();
    public DbSet<ProjectType> ProjectTypes => Set<ProjectType>();
    public DbSet<ProjectStage> ProjectStages => Set<ProjectStage>();
    public DbSet<ProjectTemplate> ProjectTemplates => Set<ProjectTemplate>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Consultant> Consultants => Set<Consultant>();
    public DbSet<ProjectConsultant> ProjectConsultants => Set<ProjectConsultant>();
    public DbSet<ProjectMilestone> ProjectMilestones => Set<ProjectMilestone>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<ProjectVariation> ProjectVariations => Set<ProjectVariation>();
    public DbSet<ProjectWorker> ProjectWorkers => Set<ProjectWorker>();
    public DbSet<ProjectWorkerRate> ProjectWorkerRates => Set<ProjectWorkerRate>();
    public DbSet<ProjectMusterEntry> ProjectMusterEntries => Set<ProjectMusterEntry>();
    public DbSet<ProjectDiaryEntry> ProjectDiaryEntries => Set<ProjectDiaryEntry>();
    public DbSet<ProjectBudgetLine> ProjectBudgetLines => Set<ProjectBudgetLine>();
    public DbSet<ProjectCostEntry> ProjectCostEntries => Set<ProjectCostEntry>();
    public DbSet<ProjectValuation> ProjectValuations => Set<ProjectValuation>();
    public DbSet<ProjectRetentionRelease> ProjectRetentionReleases => Set<ProjectRetentionRelease>();
    public DbSet<ProjectReminderLog> ProjectReminderLogs => Set<ProjectReminderLog>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityAttendee> ActivityAttendees => Set<ActivityAttendee>();
    public DbSet<ActivityMinutesAddendum> ActivityMinutesAddenda => Set<ActivityMinutesAddendum>();
    public DbSet<ActionItem> ActionItems => Set<ActionItem>();
    public DbSet<ActivityReminderLog> ActivityReminderLogs => Set<ActivityReminderLog>();
    public DbSet<CalendarFeedToken> CalendarFeedTokens => Set<CalendarFeedToken>();
    public DbSet<WithholdingCertificate> WithholdingCertificates => Set<WithholdingCertificate>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RecordSalesStageChanges();
        ApplyAuditFields();
        NormalizeUtcFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        RecordSalesStageChanges();
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

        modelBuilder.Entity<CsatSurvey>(entity =>
        {
            entity.ToTable("csat_surveys");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Comment).HasMaxLength(2000);
            entity.Property(x => x.Channel).HasMaxLength(20);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.RespondedAt });
            entity.HasOne(x => x.WorkOrder)
                .WithMany()
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClientPortalUser>(entity =>
        {
            entity.Property(x => x.AccessLinkNonce).HasMaxLength(64);
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

        modelBuilder.Entity<ClientPortalLoginToken>(entity =>
        {
            entity.ToTable("client_portal_login_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.RequestedIp).HasMaxLength(100);
            entity.Property(x => x.UserAgent).HasMaxLength(1000);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.ClientPortalUserId, x.ExpiresAt });
            entity.HasOne(x => x.ClientPortalUser)
                .WithMany(x => x.LoginTokens)
                .HasForeignKey(x => x.ClientPortalUserId)
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
            entity.Property(x => x.CreditLimit).HasColumnType("numeric(18,2)");
            entity.Property(x => x.CurrentBalance).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            entity.HasOne(x => x.DefaultCreditTerm)
                .WithMany(x => x.Clients)
                .HasForeignKey(x => x.DefaultCreditTermId)
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
            entity.Property(x => x.ExternalId).HasMaxLength(100);
            entity.Property(x => x.ExternalSystem).HasMaxLength(50);
            entity.HasIndex(x => new { x.TenantId, x.AssetId, x.Name }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.UpdatedAt });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeterReading>(entity =>
        {
            entity.ToTable("meter_readings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Value).HasPrecision(18, 4);
            entity.Property(x => x.Delta).HasPrecision(18, 4);
            entity.Property(x => x.Source).HasMaxLength(20).IsRequired();
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
                tableBuilder.HasCheckConstraint("CK_work_orders_status", "\"Status\" IN ('New', 'Open', 'Assigned', 'Scheduled', 'In Progress', 'On Hold', 'Waiting on Parts', 'Waiting on Client', 'Completed', 'Client Accepted', 'Closed', 'Cancelled', 'Rejected', 'Reopened')");
                tableBuilder.HasCheckConstraint("CK_work_orders_work_type", "\"WorkType\" IN ('NewInstallation', 'PreventiveMaintenance', 'CorrectiveMaintenance', 'InspectionAssessment')");
                tableBuilder.HasCheckConstraint("CK_work_orders_hold_reason", "\"HoldReason\" IS NULL OR \"HoldReason\" IN ('Awaiting Materials', 'Awaiting Client', 'Awaiting Vendor', 'Awaiting Approval', 'Awaiting Site Access', 'Awaiting Spares', 'Other')");
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
            entity.HasIndex(x => x.TrackingToken).IsUnique().HasFilter("\"TrackingToken\" IS NOT NULL");
            entity.HasIndex(x => x.QuoteId);
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
            entity.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.ProjectId });
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
            entity.HasOne(x => x.Quote)
                .WithMany()
                .HasForeignKey(x => x.QuoteId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.SalesOpportunityId);
            entity.HasOne(x => x.SalesOpportunity)
                .WithMany(x => x.WorkOrders)
                .HasForeignKey(x => x.SalesOpportunityId)
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

        modelBuilder.Entity<Van>(entity =>
        {
            entity.ToTable("vans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.RegistrationNumber).HasMaxLength(50);
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Technician)
                .WithMany()
                .HasForeignKey(x => x.TechnicianId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.HomeBranch)
                .WithMany()
                .HasForeignKey(x => x.HomeBranchId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VanMaterialStock>(entity =>
        {
            entity.ToTable("van_material_stocks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuantityOnHand).HasPrecision(18, 2);
            entity.Property(x => x.ReorderLevel).HasPrecision(18, 2);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.VanId, x.MaterialId }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Van)
                .WithMany(x => x.MaterialStocks)
                .HasForeignKey(x => x.VanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Material)
                .WithMany(x => x.VanStocks)
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
            entity.HasIndex(x => new { x.TenantId, x.MaterialId, x.CreatedAt });
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.StockMovements)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch)
                .WithMany(x => x.StockMovements)
                .HasForeignKey(x => x.BranchId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Van)
                .WithMany()
                .HasForeignKey(x => x.VanId)
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
                new LicensePlan { Id = Guid.Parse("55555555-5555-5555-5555-555555555561"), PlanCode = "essentials", DisplayName = "Ecosys ServiceOps Essentials", MinOfficeSeats = 2, IsActive = true, CreatedAt = seededAt },
                new LicensePlan { Id = Guid.Parse("55555555-5555-5555-5555-555555555562"), PlanCode = "professional", DisplayName = "Ecosys ServiceOps Professional", MinOfficeSeats = 3, IsActive = true, CreatedAt = seededAt },
                new LicensePlan { Id = Guid.Parse("55555555-5555-5555-5555-555555555563"), PlanCode = "enterprise", DisplayName = "Ecosys ServiceOps Enterprise", MinOfficeSeats = 10, IsActive = true, CreatedAt = seededAt });
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

        modelBuilder.Entity<TenantFinanceDefaults>(entity =>
        {
            entity.ToTable("tenant_finance_defaults");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.Property(x => x.TaxName).HasMaxLength(50);
            entity.Property(x => x.DefaultPaymentTerms).HasMaxLength(50);
            entity.Property(x => x.DefaultVatRate).HasPrecision(5, 2);
            entity.HasOne(x => x.Tenant)
                .WithMany(x => x.TenantFinanceDefaults)
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

        modelBuilder.Entity<PlatformLead>(entity =>
        {
            entity.ToTable("platform_leads");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ContactPersonName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Country).HasMaxLength(120);
            entity.Property(x => x.Industry).HasMaxLength(120);
            entity.Property(x => x.CompanySize).HasMaxLength(120);
            entity.Property(x => x.Message).HasMaxLength(4000);
            entity.Property(x => x.PreferredContactMethod).HasMaxLength(40);
            entity.Property(x => x.Status).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(4000);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ConvertedTenantId);
            entity.HasOne(x => x.ConvertedTenant)
                .WithMany(x => x.ConvertedLeads)
                .HasForeignKey(x => x.ConvertedTenantId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CrmContact>(entity =>
        {
            entity.ToTable("crm_contacts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Role).HasMaxLength(120);
            entity.HasIndex(x => x.LeadId);
            entity.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CrmActivity>(entity =>
        {
            entity.ToTable("crm_activities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.LoggedByName).HasMaxLength(200);
            entity.HasIndex(x => x.ContactId);
            entity.HasIndex(x => x.LeadId);
            entity.HasOne(x => x.Contact).WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CrmTask>(entity =>
        {
            entity.ToTable("crm_tasks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Priority).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.AssignedToName).HasMaxLength(200);
            entity.HasIndex(x => x.Status);
            entity.HasOne(x => x.RelatedContact).WithMany().HasForeignKey(x => x.RelatedContactId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.RelatedLead).WithMany().HasForeignKey(x => x.RelatedLeadId).OnDelete(DeleteBehavior.SetNull);
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
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.ProjectId });
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
            entity.HasIndex(x => new { x.TenantId, x.SiteCode });
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

        modelBuilder.Entity<KnowledgeCategory>(entity =>
        {
            entity.ToTable("knowledge_categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Icon).HasMaxLength(60);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeArticle>(entity =>
        {
            entity.ToTable("knowledge_articles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(260).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(2000);
            entity.Property(x => x.Body).HasMaxLength(64000).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Visibility).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Category)
                .WithMany(x => x.Articles)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.UpdatedByUser)
                .WithMany()
                .HasForeignKey(x => x.UpdatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SourceWorkOrder)
                .WithMany()
                .HasForeignKey(x => x.SourceWorkOrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<KnowledgeArticleFeedback>(entity =>
        {
            entity.ToTable("knowledge_article_feedback");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Comment).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.ArticleId });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Article)
                .WithMany(x => x.Feedback)
                .HasForeignKey(x => x.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ClientPortalUser)
                .WithMany()
                .HasForeignKey(x => x.ClientPortalUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.ToTable("knowledge_documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.TagsJson).HasMaxLength(4000);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.FileType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ExtractedText);
            entity.HasIndex(x => new { x.TenantId, x.Title, x.Version });
            entity.HasOne(x => x.Tenant).WithMany(x => x.KnowledgeDocuments).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.LinkedClient).WithMany().HasForeignKey(x => x.LinkedClientId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.LinkedSite).WithMany().HasForeignKey(x => x.LinkedSiteId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.LinkedSystem).WithMany(x => x.KnowledgeDocuments).HasForeignKey(x => x.LinkedSystemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.LinkedAsset).WithMany().HasForeignKey(x => x.LinkedAssetId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.LinkedWorkOrder).WithMany().HasForeignKey(x => x.LinkedWorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<KnowledgeDocumentChunk>(entity =>
        {
            entity.ToTable("knowledge_document_chunks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SectionTitle).HasMaxLength(300);
            entity.Property(x => x.ChunkText).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.DocumentId, x.ChunkOrder }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany(x => x.KnowledgeDocumentChunks).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Document).WithMany(x => x.Chunks).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Article).WithMany().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<KnowledgeTag>(entity =>
        {
            entity.ToTable("knowledge_tags");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(140).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Slug }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeArticleTag>(entity =>
        {
            entity.ToTable("knowledge_article_tags");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ArticleId, x.TagId }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Article)
                .WithMany(x => x.ArticleTags)
                .HasForeignKey(x => x.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Tag)
                .WithMany(x => x.ArticleTags)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeArticleVersion>(entity =>
        {
            entity.ToTable("knowledge_article_versions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(64000).IsRequired();
            entity.HasIndex(x => new { x.ArticleId, x.VersionNumber }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Article)
                .WithMany(x => x.Versions)
                .HasForeignKey(x => x.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.UpdatedByUser)
                .WithMany()
                .HasForeignKey(x => x.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<WorkOrderBillableItem>(entity =>
        {
            entity.ToTable("work_order_billable_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ItemType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.UnitRate).HasPrecision(18, 4).IsRequired();
            entity.Property(x => x.LineTotal).HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.TaxRate).HasPrecision(8, 4).IsRequired();
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.GrandTotal).HasPrecision(18, 2).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.WorkOrderId });
            entity.HasIndex(x => new { x.TenantId, x.IsBilled });
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WorkOrder)
                .WithMany(x => x.BillableItems)
                .HasForeignKey(x => x.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RateCard)
                .WithMany()
                .HasForeignKey(x => x.RateCardId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<WorkOrderBillableItem>(modelBuilder);

        modelBuilder.Entity<WorkOrderTimeLog>(entity =>
        {
            entity.ToTable("work_order_time_logs");
            entity.HasKey(x => x.Id);
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
            entity.HasOne(x => x.Van).WithMany().HasForeignKey(x => x.VanId).OnDelete(DeleteBehavior.Restrict);
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
        modelBuilder.Entity<TenantInvoice>(entity =>
        {
            entity.ToTable("tenant_invoices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.InvoiceNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.DocumentType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.TermsAndConditions).HasMaxLength(4000);
            entity.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TotalAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.AmountPaid).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Balance).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            entity.Property(x => x.DepositAmount).HasColumnType("numeric(18,2)");
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client).WithMany(x => x.Invoices).HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreditTerm).WithMany(x => x.Invoices).HasForeignKey(x => x.CreditTermId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.InvoiceNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.DocumentType });
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.ClientId });
        });
        ApplyTenantFilter<TenantInvoice>(modelBuilder);

        modelBuilder.Entity<TenantInvoiceLine>(entity =>
        {
            entity.ToTable("tenant_invoice_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ItemType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Quantity).HasColumnType("numeric(18,4)");
            entity.Property(x => x.UnitRate).HasColumnType("numeric(18,4)");
            entity.Property(x => x.LineTotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TaxRate).HasColumnType("numeric(8,4)");
            entity.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.GrandTotal).HasColumnType("numeric(18,2)");
            entity.HasOne(x => x.Invoice).WithMany(x => x.Lines).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.BillableItem).WithMany().HasForeignKey(x => x.BillableItemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.InvoiceId);
        });

        // ── TenantPayment ────────────────────────────────────────────────────
        modelBuilder.Entity<TenantPayment>(entity =>
        {
            entity.ToTable("tenant_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PaymentNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Method).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.PaymentNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.InvoiceId });
        });
        ApplyTenantFilter<TenantPayment>(modelBuilder);

        // ── PaymentAllocation ────────────────────────────────────────────────
        modelBuilder.Entity<PaymentAllocation>(entity =>
        {
            entity.ToTable("payment_allocations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.TenantId, x.InvoiceId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.SourceType, x.SourceId });
        });
        ApplyTenantFilter<PaymentAllocation>(modelBuilder);

        // ── CreditNote ───────────────────────────────────────────────────────
        modelBuilder.Entity<CreditNote>(entity =>
        {
            entity.ToTable("credit_notes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CreditNoteNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TotalAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.AmountApplied).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Balance).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.CreditNoteNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.ClientId });
        });
        ApplyTenantFilter<CreditNote>(modelBuilder);

        modelBuilder.Entity<CreditNoteLine>(entity =>
        {
            entity.ToTable("credit_note_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ItemType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Quantity).HasColumnType("numeric(18,4)");
            entity.Property(x => x.UnitRate).HasColumnType("numeric(18,4)");
            entity.Property(x => x.LineTotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TaxRate).HasColumnType("numeric(8,4)");
            entity.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.GrandTotal).HasColumnType("numeric(18,2)");
            entity.HasOne(x => x.CreditNote).WithMany(x => x.Lines).HasForeignKey(x => x.CreditNoteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.CreditNoteId);
        });

        // ── TenantQuotation ──────────────────────────────────────────────────
        modelBuilder.Entity<TenantQuotation>(entity =>
        {
            entity.ToTable("tenant_quotations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuoteNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.AcceptedByName).HasMaxLength(150);
            entity.Property(x => x.AcceptedByTitle).HasMaxLength(120);
            entity.Property(x => x.AcceptedFromIp).HasMaxLength(64);
            entity.Property(x => x.Subject).HasMaxLength(300);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.Property(x => x.Subtotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TaxAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TotalAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ConvertedToInvoice).WithMany().HasForeignKey(x => x.ConvertedToInvoiceId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ConvertedToWorkOrder).WithMany().HasForeignKey(x => x.ConvertedToWorkOrderId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.SalesOpportunity).WithMany(x => x.Quotations).HasForeignKey(x => x.SalesOpportunityId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.SalesOpportunityId);
            entity.HasIndex(x => new { x.TenantId, x.QuoteNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
        });
        ApplyTenantFilter<TenantQuotation>(modelBuilder);

        // ── Sales CRM ────────────────────────────────────────────────────────
        modelBuilder.Entity<SalesOpportunity>(entity =>
        {
            entity.ToTable("sales_opportunities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Stage).HasMaxLength(40).IsRequired();
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.Property(x => x.ContactName).HasMaxLength(150);
            entity.Property(x => x.ContactEmail).HasMaxLength(256);
            entity.Property(x => x.ContactPhone).HasMaxLength(50);
            entity.Property(x => x.Location).HasMaxLength(250);
            entity.Property(x => x.Source).HasMaxLength(60);
            entity.Property(x => x.ServiceType).HasMaxLength(50);
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.EstimatedValue).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            entity.Property(x => x.OwnerName).HasMaxLength(150);
            entity.Property(x => x.LostReason).HasMaxLength(500);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.Stage });
            entity.HasIndex(x => new { x.TenantId, x.ClientId });
            entity.HasIndex(x => new { x.TenantId, x.OwnerUserId });
            entity.HasIndex(x => new { x.TenantId, x.RenewalOfContractId });
            entity.HasIndex(x => new { x.TenantId, x.PipelineId });
            entity.HasIndex(x => new { x.TenantId, x.SourceWorkOrderId });
        });
        ApplyTenantFilter<SalesOpportunity>(modelBuilder);

        modelBuilder.Entity<TenantSalesSettings>(entity =>
        {
            entity.ToTable("tenant_sales_settings");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId).IsUnique();
            entity.Property(x => x.DealVisibility).HasMaxLength(20).IsRequired();
            entity.Property(x => x.LeadEmailAddress).HasMaxLength(256);
            entity.Property(x => x.WebFormKey).HasMaxLength(64);
            entity.HasIndex(x => x.WebFormKey).IsUnique();
        });
        ApplyTenantFilter<TenantSalesSettings>(modelBuilder);

        modelBuilder.Entity<SalesSequence>(entity =>
        {
            entity.ToTable("sales_sequences");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.TenantId);
        });
        ApplyTenantFilter<SalesSequence>(modelBuilder);

        modelBuilder.Entity<SalesSequenceStep>(entity =>
        {
            entity.ToTable("sales_sequence_steps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(200);
            entity.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            entity.HasOne(x => x.Sequence).WithMany(x => x.Steps).HasForeignKey(x => x.SequenceId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<SalesSequenceStep>(modelBuilder);

        modelBuilder.Entity<SalesSequenceEnrollment>(entity =>
        {
            entity.ToTable("sales_sequence_enrollments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.StoppedReason).HasMaxLength(300);
            entity.HasOne(x => x.Sequence).WithMany().HasForeignKey(x => x.SequenceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Opportunity).WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.Status, x.NextDueAt });
            entity.HasIndex(x => new { x.TenantId, x.OpportunityId });
        });
        ApplyTenantFilter<SalesSequenceEnrollment>(modelBuilder);

        modelBuilder.Entity<SalesSavedView>(entity =>
        {
            entity.ToTable("sales_saved_views");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Query).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.UserId });
        });
        ApplyTenantFilter<SalesSavedView>(modelBuilder);

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

        modelBuilder.Entity<SalesOpportunityContact>(entity =>
        {
            entity.ToTable("sales_opportunity_contacts");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Opportunity).WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contact).WithMany().HasForeignKey(x => x.ContactId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.OpportunityId, x.ContactId }).IsUnique();
        });
        ApplyTenantFilter<SalesOpportunityContact>(modelBuilder);

        modelBuilder.Entity<SalesAssignmentRule>(entity =>
        {
            entity.ToTable("sales_assignment_rules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(60);
            entity.Property(x => x.ServiceType).HasMaxLength(50);
            entity.Property(x => x.LocationContains).HasMaxLength(120);
            entity.Property(x => x.Method).HasMaxLength(20).IsRequired();
            entity.Property(x => x.OwnerUserIds).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.SortOrder });
        });
        ApplyTenantFilter<SalesAssignmentRule>(modelBuilder);

        modelBuilder.Entity<SalesStageChange>(entity =>
        {
            entity.ToTable("sales_stage_changes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FromStage).HasMaxLength(40);
            entity.Property(x => x.ToStage).HasMaxLength(40).IsRequired();
            entity.HasOne(x => x.Opportunity).WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.TenantId, x.ChangedAt });
            entity.HasIndex(x => new { x.OpportunityId, x.ChangedAt });
        });
        ApplyTenantFilter<SalesStageChange>(modelBuilder);

        modelBuilder.Entity<SalesTarget>(entity =>
        {
            entity.ToTable("sales_targets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            entity.HasIndex(x => new { x.TenantId, x.Month, x.OwnerUserId }).IsUnique();
        });
        ApplyTenantFilter<SalesTarget>(modelBuilder);

        modelBuilder.Entity<SalesPipeline>(entity =>
        {
            entity.ToTable("sales_pipelines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Module).HasMaxLength(40);
            entity.HasIndex(x => new { x.TenantId, x.SortOrder });
        });
        ApplyTenantFilter<SalesPipeline>(modelBuilder);

        modelBuilder.Entity<SalesPipelineStage>(entity =>
        {
            entity.ToTable("sales_pipeline_stages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(60).IsRequired();
            entity.Property(x => x.RequiredFields).HasMaxLength(2000);
            entity.HasOne(x => x.Pipeline).WithMany(x => x.Stages).HasForeignKey(x => x.PipelineId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.PipelineId, x.Key }).IsUnique();
        });
        ApplyTenantFilter<SalesPipelineStage>(modelBuilder);

        modelBuilder.Entity<SalesCustomField>(entity =>
        {
            entity.ToTable("sales_custom_fields");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(100).IsRequired();
            entity.Property(x => x.FieldType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Options).HasMaxLength(4000);
            entity.Property(x => x.HelpText).HasMaxLength(300);
            entity.HasOne<SalesPipeline>().WithMany().HasForeignKey(x => x.PipelineId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        });
        ApplyTenantFilter<SalesCustomField>(modelBuilder);

        modelBuilder.Entity<SalesCustomFieldValue>(entity =>
        {
            entity.ToTable("sales_custom_field_values");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Value).HasMaxLength(1000).IsRequired();
            entity.HasOne(x => x.Opportunity).WithMany(x => x.CustomFieldValues).HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Field).WithMany().HasForeignKey(x => x.FieldId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.OpportunityId, x.FieldId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.FieldId, x.Value });
        });
        ApplyTenantFilter<SalesCustomFieldValue>(modelBuilder);

        modelBuilder.Entity<SalesActivity>(entity =>
        {
            entity.ToTable("sales_activities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.LoggedByName).HasMaxLength(150);
            entity.HasOne(x => x.Opportunity).WithMany(x => x.Activities).HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.TenantId, x.OpportunityId, x.OccurredAt });
            entity.HasIndex(x => new { x.TenantId, x.DueAt });
        });
        ApplyTenantFilter<SalesActivity>(modelBuilder);

        modelBuilder.Entity<SalesTender>(entity =>
        {
            entity.ToTable("sales_tenders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(120);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.IssuerName).HasMaxLength(200);
            entity.Property(x => x.ContactName).HasMaxLength(150);
            entity.Property(x => x.ContactEmail).HasMaxLength(256);
            entity.Property(x => x.ContactPhone).HasMaxLength(50);
            entity.Property(x => x.SubmissionMethod).HasMaxLength(120);
            entity.Property(x => x.BidBondAmount).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.EstimatedValue).HasColumnType("numeric(18,2)");
            entity.Property(x => x.SubmittedValue).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            entity.Property(x => x.OutcomeNotes).HasMaxLength(1000);
            entity.Property(x => x.OwnerName).HasMaxLength(150);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Opportunity).WithMany(x => x.Tenders).HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.SubmissionDeadline });
            entity.HasIndex(x => x.OpportunityId);
        });
        ApplyTenantFilter<SalesTender>(modelBuilder);

        modelBuilder.Entity<TenantQuotationLine>(entity =>
        {
            entity.ToTable("tenant_quotation_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ItemType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Quantity).HasColumnType("numeric(18,4)");
            entity.Property(x => x.UnitRate).HasColumnType("numeric(18,4)");
            entity.Property(x => x.LineTotal).HasColumnType("numeric(18,2)");
            entity.Property(x => x.TaxRate).HasColumnType("numeric(8,4)");
            entity.Property(x => x.GrandTotal).HasColumnType("numeric(18,2)");
            entity.HasOne(x => x.Quotation).WithMany(x => x.Lines).HasForeignKey(x => x.QuotationId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RateCard).WithMany().HasForeignKey(x => x.RateCardId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.QuotationId);
        });

        // ── CreditTerm ───────────────────────────────────────────────────────
        modelBuilder.Entity<CreditTerm>(entity =>
        {
            entity.ToTable("credit_terms");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.Property(x => x.NetDays).HasDefaultValue(30);
            entity.Property(x => x.DepositPercent).HasColumnType("numeric(5,2)");
            entity.Property(x => x.CreditLimit).HasColumnType("numeric(18,2)");
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<CreditTerm>(modelBuilder);

        // ── ScheduledReport ──────────────────────────────────────────────────
        modelBuilder.Entity<ScheduledReport>(entity =>
        {
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

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("expenses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ClaimNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.SubmittedByName).HasMaxLength(200);
            entity.Property(x => x.Category).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired().HasDefaultValue("KES");
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired().HasDefaultValue("Submitted");
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.ApprovedByName).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<Expense>(modelBuilder);

        modelBuilder.Entity<SupplierBill>(entity =>
        {
            entity.ToTable("supplier_bills");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BillNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.AmountPaid).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired().HasDefaultValue("KES");
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired().HasDefaultValue("Open");
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });
        ApplyTenantFilter<SupplierBill>(modelBuilder);

        modelBuilder.Entity<SupplierBillPayment>(entity =>
        {
            entity.ToTable("supplier_bill_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Method).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Reference).HasMaxLength(120);
            entity.HasIndex(x => x.SupplierBillId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SupplierBill).WithMany(x => x.Payments).HasForeignKey(x => x.SupplierBillId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<SupplierBillPayment>(modelBuilder);

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

        ConfigureProjects(modelBuilder);
    }

    private void ConfigureProjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectType>(entity =>
        {
            entity.ToTable("project_types");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.DefaultTemplate).WithMany().HasForeignKey(x => x.DefaultTemplateId).OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<ProjectType>(modelBuilder);

        modelBuilder.Entity<ProjectStage>(entity =>
        {
            entity.ToTable("project_stages", tableBuilder =>
                tableBuilder.HasCheckConstraint("CK_project_stages_category",
                    "\"Category\" IN ('Planning', 'Active', 'OnHold', 'Handover', 'Completed', 'Closed', 'Cancelled')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ProjectStage>(modelBuilder);

        modelBuilder.Entity<ProjectTemplate>(entity =>
        {
            entity.ToTable("project_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.ItemsJson).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ProjectTemplate>(modelBuilder);

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(x => x.Id);
            entity.Ignore(x => x.RevisedValue);
            entity.Ignore(x => x.EffectiveProgressPercent);
            entity.Property(x => x.ProjectNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired().HasDefaultValue("KES");
            entity.Property(x => x.OriginalValue).HasPrecision(18, 2);
            entity.Property(x => x.ApprovedAdditions).HasPrecision(18, 2);
            entity.Property(x => x.ApprovedOmissions).HasPrecision(18, 2);
            entity.Property(x => x.ValueVersion).IsConcurrencyToken();
            entity.Property(x => x.ProgressPercent).HasPrecision(5, 2);
            entity.Property(x => x.ProgressOverridePercent).HasPrecision(5, 2);
            entity.Property(x => x.ProgressOverrideReason).HasMaxLength(1000);
            entity.Property(x => x.ClosureNotes).HasMaxLength(2000);
            entity.Property(x => x.VisibleInClientPortal).HasDefaultValue(true);
            entity.Property(x => x.RetentionPercent).HasPrecision(5, 2);
            entity.Property(x => x.RetentionCapAmount).HasPrecision(18, 2);
            entity.Property(x => x.RetentionReleaseAtCompletionPercent).HasPrecision(5, 2).HasDefaultValue(50m);
            entity.Property(x => x.AdvancePaymentAmount).HasPrecision(18, 2);
            entity.Property(x => x.AdvanceRecoveryPercent).HasPrecision(5, 2);
            entity.HasIndex(x => new { x.TenantId, x.ProjectNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ClientId });
            entity.HasIndex(x => new { x.TenantId, x.StageId });
            entity.HasIndex(x => new { x.TenantId, x.ProjectManagerUserId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ProjectType).WithMany().HasForeignKey(x => x.ProjectTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Stage).WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProjectManagerUser).WithMany().HasForeignKey(x => x.ProjectManagerUserId).OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<Project>(modelBuilder);

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.ToTable("project_members", tableBuilder =>
                tableBuilder.HasCheckConstraint("CK_project_members_person", "\"UserId\" IS NOT NULL OR \"TechnicianId\" IS NOT NULL"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Role).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Trade).HasMaxLength(100);
            entity.HasIndex(x => new { x.TenantId, x.ProjectId });
            entity.HasIndex(x => new { x.TenantId, x.UserId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany(x => x.Members).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ProjectMember>(modelBuilder);

        modelBuilder.Entity<Consultant>(entity =>
        {
            entity.ToTable("consultants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirmName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Discipline).HasMaxLength(100);
            entity.Property(x => x.ContactPerson).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.PhysicalAddress).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.FirmName }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<Consultant>(modelBuilder);

        modelBuilder.Entity<ProjectConsultant>(entity =>
        {
            entity.ToTable("project_consultants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RoleOnProject).HasMaxLength(100);
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.ConsultantId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ConsultantId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany(x => x.Consultants).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Consultant).WithMany().HasForeignKey(x => x.ConsultantId).OnDelete(DeleteBehavior.Restrict);
        });
        ApplyTenantFilter<ProjectConsultant>(modelBuilder);

        modelBuilder.Entity<ProjectMilestone>(entity =>
        {
            entity.ToTable("project_milestones", tableBuilder =>
                tableBuilder.HasCheckConstraint("CK_project_milestones_weight", "\"Weight\" >= 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Weight).HasPrecision(9, 2);
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.ProjectId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany(x => x.Milestones).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ProjectMilestone>(modelBuilder);

        modelBuilder.Entity<ProjectTask>(entity =>
        {
            entity.ToTable("project_tasks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.Priority).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.ProjectId });
            entity.HasIndex(x => new { x.TenantId, x.AssigneeUserId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany(x => x.Tasks).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Milestone).WithMany().HasForeignKey(x => x.MilestoneId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AssigneeUser).WithMany().HasForeignKey(x => x.AssigneeUserId).OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<ProjectTask>(modelBuilder);

        modelBuilder.Entity<ProjectVariation>(entity =>
        {
            entity.ToTable("project_variations", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("CK_project_variations_amount", "\"Amount\" > 0");
                tableBuilder.HasCheckConstraint("CK_project_variations_kind", "\"Kind\" IN ('Addition', 'Omission')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.VariationNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.ClientReference).HasMaxLength(100);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.DecisionNote).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.VariationNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany(x => x.Variations).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ProjectVariation>(modelBuilder);

        modelBuilder.Entity<ProjectWorker>(entity =>
        {
            entity.ToTable("project_workers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Trade).HasMaxLength(100);
            entity.Property(x => x.EngagementType).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.ProjectId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Technician).WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<ProjectWorker>(modelBuilder);

        modelBuilder.Entity<ProjectWorkerRate>(entity =>
        {
            entity.ToTable("project_worker_rates", tableBuilder =>
                tableBuilder.HasCheckConstraint("CK_project_worker_rates_rate", "\"DailyRate\" >= 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DailyRate).HasPrecision(18, 2);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.ProjectWorkerId, x.EffectiveFrom }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Worker).WithMany(x => x.Rates).HasForeignKey(x => x.ProjectWorkerId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ProjectWorkerRate>(modelBuilder);

        modelBuilder.Entity<ProjectMusterEntry>(entity =>
        {
            entity.ToTable("project_muster_entries", tableBuilder =>
                tableBuilder.HasCheckConstraint("CK_project_muster_entries_status", "\"Status\" IN ('Present', 'HalfDay', 'Absent')"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.DayFraction).HasPrecision(3, 2);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.ProjectWorkerId, x.WorkDate }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.WorkDate });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.ProjectWorkerId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ProjectMusterEntry>(modelBuilder);

        modelBuilder.Entity<ProjectDiaryEntry>(entity =>
        {
            entity.ToTable("project_diary_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WorkCompleted).HasMaxLength(4000);
            entity.Property(x => x.IssuesAndDelays).HasMaxLength(4000);
            entity.Property(x => x.InstructionsReceived).HasMaxLength(4000);
            entity.Property(x => x.Weather).HasMaxLength(100);
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.EntryDate });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AuthorUser).WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });
        ApplyTenantFilter<ProjectDiaryEntry>(modelBuilder);

        modelBuilder.Entity<ProjectBudgetLine>(entity =>
        {
            entity.ToTable("project_budget_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.ProjectId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ProjectBudgetLine>(modelBuilder);

        modelBuilder.Entity<ProjectCostEntry>(entity =>
        {
            entity.ToTable("project_cost_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.ProjectId });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        ApplyTenantFilter<ProjectCostEntry>(modelBuilder);

        modelBuilder.Entity<ProjectValuation>(entity =>
        {
            entity.ToTable("project_valuations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ValuationNumber).HasMaxLength(50).IsRequired();
            foreach (var amount in new[] { nameof(ProjectValuation.WorkDoneToDate), nameof(ProjectValuation.MaterialsOnSite), nameof(ProjectValuation.GrossValueToDate),
                         nameof(ProjectValuation.RetentionToDate), nameof(ProjectValuation.AdvanceRecoveredToDate), nameof(ProjectValuation.PreviouslyCertified),
                         nameof(ProjectValuation.AmountDue), nameof(ProjectValuation.CertifiedAmount) })
                entity.Property(amount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CertificateReference).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.ValuationNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.Status });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CertifiedByConsultant).WithMany().HasForeignKey(x => x.CertifiedByConsultantId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<ProjectValuation>(modelBuilder);

        modelBuilder.Entity<ProjectRetentionRelease>(entity =>
        {
            entity.ToTable("project_retention_releases", tableBuilder =>
                tableBuilder.HasCheckConstraint("CK_project_retention_releases_amount", "\"Amount\" > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Stage).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.ProjectId, x.Stage }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.SetNull);
        });
        ApplyTenantFilter<ProjectRetentionRelease>(modelBuilder);

        modelBuilder.Entity<ProjectReminderLog>(entity =>
        {
            entity.ToTable("project_reminder_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NotificationKey).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.EntityId, x.NotificationKey, x.ForDate }).IsUnique();
        });
        ApplyTenantFilter<ProjectReminderLog>(modelBuilder);

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
                    "(CASE WHEN \"UserId\" IS NULL THEN 0 ELSE 1 END) + (CASE WHEN \"ConsultantId\" IS NULL THEN 0 ELSE 1 END)"
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
            entity.HasOne(x => x.Consultant).WithMany().HasForeignKey(x => x.ConsultantId).OnDelete(DeleteBehavior.Cascade);
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

        modelBuilder.Entity<WithholdingCertificate>(entity =>
        {
            entity.ToTable("withholding_certificates", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("CK_withholding_certificates_amount", "\"Amount\" > 0");
                tableBuilder.HasCheckConstraint("CK_withholding_certificates_tax_type", "\"TaxType\" IN ('VAT', 'IncomeTax')");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TaxType).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RatePercent).HasPrecision(5, 2);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.CertificateNumber).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.TenantId, x.InvoiceId });
            entity.HasIndex(x => new { x.TenantId, x.ClientId, x.CertificateNumber });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });
        ApplyTenantFilter<WithholdingCertificate>(modelBuilder);

        // Project links on existing records: an optional foreign key and a (tenant, project) index each.
        ConfigureProjectLink<AttendanceEntry>(modelBuilder, x => x.ProjectId);
        ConfigureProjectLink<Expense>(modelBuilder, x => x.ProjectId);
        ConfigureProjectLink<SupplierBill>(modelBuilder, x => x.ProjectId);
        ConfigureProjectLink<PurchaseOrder>(modelBuilder, x => x.ProjectId);
        ConfigureProjectLink<TenantInvoice>(modelBuilder, x => x.ProjectId);
        ConfigureProjectLink<Contract>(modelBuilder, x => x.SourceProjectId);
        ConfigureProjectLink<TenantQuotation>(modelBuilder, x => x.ConvertedToProjectId);
        modelBuilder.Entity<TenantInvoice>().HasOne<ProjectValuation>().WithMany().HasForeignKey(x => x.ProjectValuationId).OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureProjectLink<TEntity>(ModelBuilder modelBuilder, Expression<Func<TEntity, Guid?>> projectId)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            var member = ((MemberExpression)projectId.Body).Member.Name;
            entity.HasOne<Project>().WithMany().HasForeignKey(member).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(nameof(ITenantEntity.TenantId), member);
        });
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

    /// <summary>
    /// Writes a <see cref="SalesStageChange"/> for every deal created or moved to another stage in this
    /// save, so the history is complete whichever code path moved it.
    /// </summary>
    private void RecordSalesStageChanges()
    {
        var utcNow = DateTime.UtcNow;
        var changes = new List<SalesStageChange>();
        foreach (var entry in ChangeTracker.Entries<SalesOpportunity>())
        {
            string? from;
            if (entry.State == EntityState.Added)
                from = null;
            else if (entry.State == EntityState.Modified && entry.Property(x => x.Stage).IsModified
                && !string.Equals(entry.Property(x => x.Stage).OriginalValue, entry.Entity.Stage, StringComparison.Ordinal))
                from = entry.Property(x => x.Stage).OriginalValue;
            else
                continue;

            // A deal recorded as won or lost on a past date (an import) keeps that date.
            var at = entry.Entity.Stage switch
            {
                "Won" when entry.Entity.WonAt < utcNow => entry.Entity.WonAt!.Value,
                "Lost" when entry.Entity.LostAt < utcNow => entry.Entity.LostAt!.Value,
                _ => utcNow,
            };
            entry.Entity.StageChangedAt = at;
            changes.Add(new SalesStageChange
            {
                TenantId = entry.Entity.TenantId,
                OpportunityId = entry.Entity.Id,
                FromStage = from,
                ToStage = entry.Entity.Stage,
                ChangedAt = at,
                ChangedByUserId = tenantContext?.UserId,
            });
        }
        if (changes.Count > 0)
            SalesStageChanges.AddRange(changes);
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

        foreach (var entry in ChangeTracker.Entries<KnowledgeDocument>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.UploadedAt = EnsureUtc(entry.Entity.UploadedAt);
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

        foreach (var entry in ChangeTracker.Entries<PlatformLead>())
        {
            if (entry.State is not EntityState.Added and not EntityState.Modified)
            {
                continue;
            }

            entry.Entity.CreatedAt = EnsureUtc(entry.Entity.CreatedAt);
            entry.Entity.UpdatedAt = EnsureUtc(entry.Entity.UpdatedAt);
            entry.Entity.ContactedAt = EnsureUtc(entry.Entity.ContactedAt);
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
