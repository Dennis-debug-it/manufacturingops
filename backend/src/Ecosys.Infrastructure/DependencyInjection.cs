using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Integrations;
using Ecosys.Infrastructure.Options;
using Ecosys.Infrastructure.Security;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Observability;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Ecosys.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<WebPushOptions>(configuration.GetSection(WebPushOptions.SectionName));
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.Configure<PlatformFeatureOptions>(configuration.GetSection(PlatformFeatureOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddDataProtection();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddScoped<IPortalContext, HttpPortalContext>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddSingleton<IRuntimeMetrics, RuntimeMetrics>();

        // Dashboard data versions (Release 3 part 4): every save bumps the areas it touches, so cached
        // widgets recompute and open dashboards hear about it. The Api replaces the no-op notifier.
        services.TryAddSingleton<IDashboardNotifier, NullDashboardNotifier>();
        services.AddSingleton<IDashboardDataVersions, DashboardDataVersions>();
        services.AddSingleton<IDashboardChangeBroadcaster, DashboardChangeBroadcaster>();
        services.AddSingleton<DashboardChangeInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(serviceProvider.GetRequiredService<DashboardChangeInterceptor>()));
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("postgres", tags: ["ready"]);

        services.AddScoped<IMvpAuthService, MvpAuthService>();
        services.AddScoped<IPortalAuthService, PortalAuthService>();
        services.AddScoped<IDemoWorkspaceService, DemoWorkspaceService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IDocumentNumberingService, DocumentNumberingService>();
        services.AddScoped<IKnowledgeDocumentProcessingService, KnowledgeDocumentProcessingService>();
        services.AddScoped<IPreventiveMaintenancePlanService, PreventiveMaintenancePlanService>();
        services.AddScoped<IStarterPackService, StarterPackService>();
        services.AddScoped<IPmTemplateService, PmTemplateService>();
        services.AddScoped<Reports.IClientTemplateInstaller, Reports.ClientTemplateInstaller>();
        services.AddScoped<IUserPermissionTemplateService, UserPermissionTemplateService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<IDynamicAccessService, DynamicAccessService>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<IBranchAccessService, BranchAccessService>();
        services.AddScoped<Services.Factory.IOrgScopeService, Services.Factory.OrgScopeService>();
        services.AddScoped<Services.Factory.IFactorySettingsService, Services.Factory.FactorySettingsService>();
        services.AddScoped<Services.Factory.IAssetLifecycleService, Services.Factory.AssetLifecycleService>();
        services.AddScoped<Services.Factory.IMeterReadingService, Services.Factory.MeterReadingService>();
        services.AddScoped<Services.Factory.IAssetTagNumberService, Services.Factory.AssetTagNumberService>();
        services.AddScoped<Services.Factory.IAssetClassService, Services.Factory.AssetClassService>();
        services.AddScoped<Services.Factory.IAssetAlertRunner, Services.Factory.AssetAlertRunner>();
        services.AddScoped<Services.Factory.IFactoryAssetImportService, Services.Factory.FactoryAssetImportService>();
        services.AddSingleton<Services.Factory.IAssetLabelPdfRenderer, Services.Factory.AssetLabelPdfRenderer>();
        services.AddScoped<IStockLedgerService, StockLedgerService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<IPlatformBootstrapService, PlatformBootstrapService>();
        services.AddScoped<ITestDataSeeder, TestDataSeeder>();
        services.AddScoped<ILicenseGuardService, LicenseGuardService>();
        services.AddScoped<ISecretEncryptionService, SecretEncryptionService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IEmailSettingsResolver, EmailSettingsResolver>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IEmailNotificationRegistry, EmailNotificationRegistry>();
        services.AddScoped<IEmailDeliveryLogService, EmailDeliveryLogService>();
        services.AddScoped<IEmailSubjectRuleService, EmailSubjectRuleService>();
        services.AddScoped<IEmailOutboxService, EmailOutboxService>();
        services.AddScoped<IEmailOutboxProcessor, EmailOutboxProcessor>();
        services.AddScoped<IFinancialRecipientResolver, FinancialRecipientResolver>();
        services.AddScoped<IServiceReportDeliveryService, ServiceReportDeliveryService>();
        services.AddScoped<IUserCredentialDeliveryService, UserCredentialDeliveryService>();
        services.AddScoped<ITemporaryPasswordService, TemporaryPasswordService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IPlatformLeadNotifier, PlatformLeadNotifier>();
        services.AddScoped<ISelfSignupService, SelfSignupService>();
        services.AddScoped<IEmailIntakeMailbox, ImapEmailIntakeMailbox>();
        services.AddScoped<IEmailIntakeService, EmailIntakeService>();
        services.AddScoped<ITenantSecurityPolicyService, TenantSecurityPolicyService>();
        services.AddScoped<ITenantTimeZoneService, TenantTimeZoneService>();
        services.AddScoped<IWorkOrderLifecycleService, WorkOrderLifecycleService>();
        services.AddScoped<IWorkOrderAssignmentWorkflowService, WorkOrderAssignmentWorkflowService>();
        services.AddScoped<IPmWorkOrderChecklistService, PmWorkOrderChecklistService>();
        services.AddScoped<ISlaService, SlaService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IEscalationRuleService, EscalationRuleService>();
        services.AddScoped<IEtaNotificationService, EtaNotificationService>();
        services.AddScoped<IContractAccessService, ContractAccessService>();
        services.AddScoped<IContractCoverageService, ContractCoverageService>();
        services.AddScoped<IContractPricingService, ContractPricingService>();
        services.AddScoped<IContractLifecycleService, ContractLifecycleService>();
        services.AddScoped<IContractOperationalService, ContractOperationalService>();
        services.AddScoped<IProjectSettingsService, ProjectSettingsService>();
        services.AddScoped<IProjectSetupService, ProjectSetupService>();
        services.AddScoped<IProjectAccessService, ProjectAccessService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectNotificationService, ProjectNotificationService>();
        // One way to notify (Release 3 design 2.1). The Api replaces the no-op live notifier with
        // SignalR. Further channels register as INotificationChannel (WhatsApp, below).
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.TryAddSingleton<IInboxNotifier, NullInboxNotifier>();
        services.AddScoped<INotificationPushProcessor, NotificationPushProcessor>();
        services.AddScoped<INotificationHousekeeping, NotificationHousekeeping>();
        // WhatsApp reminders from the shared Ecosys number (Release 3 design 4).
        services.AddScoped<INotificationChannel, WhatsAppNotificationChannel>();
        services.AddSingleton<IWhatsAppAccountResolver, SharedWhatsAppAccountResolver>();
        services.AddHttpClient(MetaWhatsAppSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddScoped<IWhatsAppSender, MetaWhatsAppSender>();
        services.AddScoped<IWhatsAppOutbox, WhatsAppOutbox>();
        services.AddScoped<IWhatsAppOutboxProcessor, WhatsAppOutboxProcessor>();
        services.AddScoped<IWhatsAppVerificationService, WhatsAppVerificationService>();
        services.AddScoped<IWhatsAppWebhookProcessor, WhatsAppWebhookProcessor>();
        services.AddScoped<IClientWhatsAppNotifier, ClientWhatsAppNotifier>();
        services.AddScoped<IProjectFinancialsService, ProjectFinancialsService>();
        // Role dashboards (Release 3 design part 4).
        services.AddScoped<ITenantModuleResolver, LicenseTenantModuleResolver>();
        services.AddScoped<IDashboardSettingsService, DashboardSettingsService>();
        services.AddScoped<RoleDashboardQueries>();
        services.AddScoped<IRoleDashboardService, RoleDashboardService>();
        services.AddScoped<IProjectInvoiceService, ProjectInvoiceService>();
        services.AddScoped<IProjectReminderRunner, ProjectReminderRunner>();
        // Calendar & Activities (Release 3 step 3b).
        services.AddScoped<IAccessViewerFactory, AccessViewerFactory>();
        services.AddScoped<ILinkedRecordService, LinkedRecordService>();
        services.AddScoped<ActivityNotifier>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<ICalendarFeedService, CalendarFeedService>();
        services.AddScoped<IActivityReminderRunner, ActivityReminderRunner>();
        services.AddSingleton<IPdfRenderer, QuestPdfRenderer>();
        services.AddScoped<IScheduledReportService, ScheduledReportService>();
        services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
        services.AddScoped<IPlatformBillingLifecycleService, PlatformBillingLifecycleService>();
        // Every one of these ten workers generates work orders, sends customer email or writes
        // snapshots on a timer. Registered plainly they ran on every replica, so `--scale api=2`
        // produced duplicate preventive-maintenance work orders and duplicate customer emails. The
        // lease is applied here, once, for all of them rather than inside each worker — a worker
        // added later cannot silently miss the guard (EC-P2-10).
        services.AddSingleton<IBackgroundWorkLease>(serviceProvider => new PostgresAdvisoryLockLease(
            connectionString,
            serviceProvider.GetRequiredService<ILogger<PostgresAdvisoryLockLease>>()));
        // Push notifications. Inbox pushes are rows, sent by the leased NotificationPushWorker below;
        // the in-memory queue (test pushes only) is per instance, so its sender runs everywhere.
        services.AddHttpClient();
        services.AddSingleton<IVapidKeyProvider, VapidKeyProvider>();
        services.AddSingleton<IWebPushSender, WebPushSender>();
        services.AddSingleton<PushNotificationQueue>();
        services.AddSingleton<IPushNotificationQueue>(sp => sp.GetRequiredService<PushNotificationQueue>());
        services.AddHostedService<PushNotificationWorker>();
        services.AddSingleInstanceHostedService<EmailOutboxWorker>();
        services.AddSingleInstanceHostedService<EmailIntakeWorker>();
        services.AddSingleInstanceHostedService<TrialLifecycleWorker>();
        services.AddSingleInstanceHostedService<PmSchedulerWorker>();
        services.AddSingleInstanceHostedService<MrrSnapshotWorker>();
        services.AddSingleInstanceHostedService<SlaEnforcementWorker>();
        services.AddSingleInstanceHostedService<ContractReminderWorker>();
        services.AddSingleInstanceHostedService<ScheduledReportWorker>();
        services.AddSingleInstanceHostedService<ApprovalReminderWorker>();
        services.AddSingleInstanceHostedService<ProjectReminderWorker>();
        services.AddScoped<ISalesReminderService, SalesReminderService>();
        services.AddScoped<ISalesOwnerResolver, SalesOwnerResolver>();
        services.AddSingleInstanceHostedService<SalesReminderWorker>();
        services.AddSingleInstanceHostedService<AttendanceAutoClockOutWorker>();
        services.AddSingleInstanceHostedService<EscalationRuleWorker>();
        services.AddSingleInstanceHostedService<SubscriptionLifecycleWorker>();
        services.AddSingleInstanceHostedService<NotificationPushWorker>();
        services.AddSingleInstanceHostedService<NotificationHousekeepingWorker>();
        services.AddSingleInstanceHostedService<ActivityReminderWorker>();
        services.AddSingleInstanceHostedService<Services.Factory.AssetAlertWorker>();
        services.AddSingleInstanceHostedService<WhatsAppOutboxWorker>();

        return services;
    }
}
