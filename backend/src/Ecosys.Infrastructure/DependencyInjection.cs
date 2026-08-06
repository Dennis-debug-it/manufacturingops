using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Integrations;
using Ecosys.Infrastructure.Options;
using Ecosys.Infrastructure.Security;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ecosys.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        services.AddHttpContextAccessor();
        services.AddDataProtection();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddScoped<IPortalContext, HttpPortalContext>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("postgres");

        services.AddScoped<IMvpAuthService, MvpAuthService>();
        services.AddScoped<IPortalAuthService, PortalAuthService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IDocumentNumberingService, DocumentNumberingService>();
        services.AddScoped<IKnowledgeDocumentProcessingService, KnowledgeDocumentProcessingService>();
        services.AddScoped<IPreventiveMaintenancePlanService, PreventiveMaintenancePlanService>();
        services.AddScoped<IStarterPackService, StarterPackService>();
        services.AddScoped<IPmTemplateService, PmTemplateService>();
        services.AddScoped<IUserPermissionTemplateService, UserPermissionTemplateService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<IDynamicAccessService, DynamicAccessService>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<IBranchAccessService, BranchAccessService>();
        services.AddScoped<IStockLedgerService, StockLedgerService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<IPlatformBootstrapService, PlatformBootstrapService>();
        services.AddScoped<ITestDataSeeder, TestDataSeeder>();
        services.AddScoped<ILicenseGuardService, LicenseGuardService>();
        services.AddScoped<ISecretEncryptionService, SecretEncryptionService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IEmailNotificationRegistry, EmailNotificationRegistry>();
        services.AddScoped<IEmailDeliveryLogService, EmailDeliveryLogService>();
        services.AddScoped<IEmailSubjectRuleService, EmailSubjectRuleService>();
        services.AddScoped<IEmailOutboxService, EmailOutboxService>();
        services.AddScoped<IEmailOutboxProcessor, EmailOutboxProcessor>();
        services.AddScoped<IUserCredentialDeliveryService, UserCredentialDeliveryService>();
        services.AddScoped<ITemporaryPasswordService, TemporaryPasswordService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<ITenantSecurityPolicyService, TenantSecurityPolicyService>();
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
        services.AddSingleton<IPdfRenderer, QuestPdfRenderer>();
        services.AddScoped<IScheduledReportService, ScheduledReportService>();
        services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
        services.AddHostedService<EmailOutboxWorker>();
        services.AddHostedService<TrialLifecycleWorker>();
        services.AddHostedService<PmSchedulerWorker>();
        services.AddHostedService<MrrSnapshotWorker>();
        services.AddHostedService<SlaEnforcementWorker>();
        services.AddHostedService<ContractReminderWorker>();
        services.AddHostedService<ScheduledReportWorker>();
        services.AddHostedService<ApprovalReminderWorker>();
        services.AddHostedService<EscalationRuleWorker>();

        return services;
    }
}
