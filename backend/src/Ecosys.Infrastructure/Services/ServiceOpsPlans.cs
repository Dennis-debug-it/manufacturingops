using Ecosys.Domain.Entities;
using Ecosys.Shared.Errors;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Editions and the modules each one includes, independent of names and RBAC. An edition is a fixed list
/// of modules; the WhatsApp add-on adds its own. Everything else in the app asks "does this tenant have
/// module X", so it never needs to know about editions.
/// </summary>
public static class ServiceOpsPlans
{
    // Editions (LicensePlan.PlanCode).
    public const string Essentials = "essentials";
    public const string Professional = "professional";
    public const string Enterprise = "enterprise";

    // Modules. Essentials: Operations + Finance.
    public const string Operations = "operations";
    public const string Finance = "finance";
    // Professional adds these.
    public const string Maintenance = "maintenance";
    public const string Inventory = "inventory";
    public const string Projects = "projects";
    public const string Advanced = "advanced";
    // Enterprise adds this: multiple branches, API keys and webhooks, monitoring (settings routes and limits).
    public const string EnterpriseModule = "enterprise_tools";
    // WhatsApp Business add-on: the tenant's own number and the two-way inbox.
    public const string WhatsAppBusiness = "whatsapp_business";

    // WhatsApp add-ons (TenantLicense.WhatsAppAddOn).
    public const string WhatsAppNotificationsAddOn = "whatsapp_notifications";
    public const string WhatsAppBusinessAddOn = "whatsapp_business";

    public static readonly IReadOnlyList<string> Editions = [Essentials, Professional, Enterprise];

    public static bool IsEdition(string? code) => code is Essentials or Professional or Enterprise;

    public static string DisplayName(string code) => code switch
    {
        Essentials => "Ecosys ServiceOps Essentials",
        Professional => "Ecosys ServiceOps Professional",
        Enterprise => "Ecosys ServiceOps Enterprise",
        _ => throw new BusinessRuleException("Select Essentials, Professional, or Enterprise.")
    };

    public static IReadOnlyList<string> EditionModules(string? planCode) => planCode switch
    {
        Essentials => [Operations, Finance],
        Professional => [Operations, Finance, Maintenance, Inventory, Projects, Advanced],
        Enterprise => [Operations, Finance, Maintenance, Inventory, Projects, Advanced, EnterpriseModule],
        _ => []
    };

    /// <summary>The tenant's modules: its edition's, plus the WhatsApp Business add-on's.</summary>
    public static IReadOnlyList<string> Modules(LicensePlan plan, string? whatsAppAddOn = null) =>
        whatsAppAddOn == WhatsAppBusinessAddOn ? [.. EditionModules(plan.PlanCode), WhatsAppBusiness] : EditionModules(plan.PlanCode);

    /// <summary>Validates a WhatsApp add-on code; blank means none.</summary>
    public static string? NormalizeWhatsAppAddOn(string? value)
    {
        var code = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
        return code is null or WhatsAppNotificationsAddOn or WhatsAppBusinessAddOn
            ? code
            : throw new BusinessRuleException("WhatsApp add-on must be none, whatsapp_notifications, or whatsapp_business.");
    }

    private static readonly HashSet<string> OperationsControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "AssignmentGroups", "ContractReference", "Dashboard", "Dispatch", "EtaNotificationSettings", "Reports", "Sites",
        "TeamManagement", "Technicians", "TechnicianJobs", "WorkOrderAssignments", "WorkOrderExecution",
        "WorkOrderTemplates", "WorkOrders"
    };
    private static readonly HashSet<string> MaintenanceControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Assets", "AssetCategories", "Contracts", "EscalationRules", "PreventiveMaintenance", "PmTemplates",
        "SlaDefinitions", "SlaPolicies", "Systems"
    };
    private static readonly HashSet<string> InventoryControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "MaterialIssues", "MaterialRequests", "Materials", "PartsRequests", "PartsReturns", "PurchaseOrders",
        "StockCounts", "StockMovements", "StockTransfers", "Vans", "InventoryReports", "Tools", "InventoryRequests",
        "PurchaseOrderDocuments"
    };
    private static readonly HashSet<string> AdvancedControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApprovalMatrix", "CreditTerms", "Csat", "ExpensesPayables", "Intake", "IntakeProtocols", "ScheduledReports",
        "TimeAttendance", "Workflows"
    };
    private static readonly HashSet<string> WhatsAppBusinessControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "WhatsApp"
    };
    private static readonly HashSet<string> FinanceControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "ExchangeRates", "TenantCreditNote", "TenantFinance", "TenantInvoice", "TenantQuotation", "WithholdingCertificates"
    };

    private static readonly HashSet<string> ProjectsControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Projects", "ProjectSettings", "Consultants", "PortalProjects", "ProjectSite", "ProjectCommercial"
    };

    /// <summary>Each module's controllers, so classification and gating read from one table.</summary>
    private static readonly (string Module, HashSet<string> Controllers)[] ModuleControllers =
    [
        (Operations, OperationsControllers),
        (Finance, FinanceControllers),
        (Maintenance, MaintenanceControllers),
        (Inventory, InventoryControllers),
        (Projects, ProjectsControllers),
        (Advanced, AdvancedControllers),
        (WhatsAppBusiness, WhatsAppBusinessControllers),
    ];

    /// <summary>
    /// Tenant controllers deliberately available to every ServiceOps plan (identity, workspace settings,
    /// clients, knowledge base, audit, attachments…). Listed explicitly so that a new controller is never
    /// ungated by omission: <c>ServiceOpsModuleClassificationTests</c> fails for any controller that is in
    /// none of the Operations/Finance/Shared/route-classified sets (audit TM-P2-03).
    /// </summary>
    public static readonly IReadOnlySet<string> SharedControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AccessControl", "ActionItems", "Activities", "AdvancedSettings", "Attachments", "AuditLogs", "Auth", "Branches", "Calendar", "CalendarFeed", "ClientPortalContacts",
        "Clients", "Dev", "Integrations", "Kip", "Knowledge", "KnowledgeDocuments", "Notifications", "PortalAuth", "PortalKnowledge",
        "PublicWorkOrder", "PushSubscriptions", "RoleDashboard", "RolePermissions", "StarterPacks", "Tenant", "TenantEmailDelivery", "TenantEmailDeliveryLogs",
        "TenantEmailTemplates", "TenantLicense", "UserNotificationPreferences", "Users",
        // WhatsApp reminders (Release 3 design 6): every edition, gated by the WhatsApp add-on.
        // The two-way inbox (WhatsApp controller) needs the WhatsApp Business add-on.
        "MyWhatsApp", "WhatsAppNotifications", "ClientWhatsAppConsent", "PortalWhatsApp", "WhatsAppWebhook"
    };

    /// <summary>
    /// The sales CRM (pipeline, RFPs/RFQs). Its point is the whole chain from an RFQ to a quote (Finance)
    /// to the job (Operations) to the invoice, so it needs both modules. Every edition includes both, so
    /// the CRM is on every edition (docs/design/LICENSING_REMODEL_PLAN.md).
    /// </summary>
    public static readonly IReadOnlySet<string> CrmControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SalesCrm", "SalesTenders"
    };

    /// <summary>Controllers whose module requirement is decided per route inside <see cref="RequiredModules"/>.</summary>
    public static readonly IReadOnlySet<string> RouteClassifiedControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Portal", "WorkOrderBilling", "Import", "Settings"
    };

    public static bool IsClassified(string controller) =>
        ModuleControllers.Any(x => x.Controllers.Contains(controller))
        || SharedControllers.Contains(controller)
        || CrmControllers.Contains(controller)
        || RouteClassifiedControllers.Contains(controller);

    public static IReadOnlyList<string> RequiredModules(string? controller, string path)
    {
        var route = path.ToLowerInvariant();
        if (controller == "Portal")
        {
            if (route.StartsWith("/api/portal/projects")) return [Projects];
            if (route.StartsWith("/api/portal/invoices") || route.StartsWith("/api/portal/quotations")) return [Finance];
            if (route.StartsWith("/api/portal/assets") || route.StartsWith("/api/portal/maintenance") || route.StartsWith("/api/portal/contracts")) return [Maintenance];
            if (!route.StartsWith("/api/portal/profile")) return [Operations];
        }
        if (controller == "WorkOrderBilling" || route.Contains("/generate-invoice") || route.Contains("/convert-to-work-order") || route.EndsWith("/from-quote"))
            return [Operations, Finance];
        if ((controller is not null && CrmControllers.Contains(controller)) || route.EndsWith("/from-opportunity"))
            return [Operations, Finance];
        // Every Projects route needs only Projects: the editions that include it include everything its
        // tabs touch (finance, inventory, maintenance contracts).
        if (controller is not null && ModuleControllers.FirstOrDefault(x => x.Controllers.Contains(controller)) is { Module: { } module })
            return [module];
        if (route.StartsWith("/api/settings/"))
        {
            var setting = route.Split('/')[3];
            if (setting is "operations-policy" or "assignment-groups") return [Operations];
            if (setting is "inventory") return [Inventory];
            if (setting is "email-intake" or "time-attendance" or "credit-control") return [Advanced];
            if (setting is "monitoring-webhooks" or "api-keys" or "webhooks") return [EnterpriseModule];
            if (setting is "whatsapp") return [WhatsAppBusiness];
            if (setting is "payments" or "tax" or "finance") return [Finance];
        }
        if (controller == "Import" && route.Contains("/assets")) return [Maintenance];
        if (controller == "Import" && (route.Contains("/sites") || route.Contains("/technicians"))) return [Operations];
        return [];
    }

    public static void EnsureModules(IEnumerable<string> enabled, IEnumerable<string> required)
    {
        foreach (var module in required)
            if (!enabled.Contains(module, StringComparer.OrdinalIgnoreCase))
                throw new LicenseException($"Your ServiceOps edition does not include {module}. Contact your account manager to upgrade.", $"license_module_{module}", 403);
    }
}
