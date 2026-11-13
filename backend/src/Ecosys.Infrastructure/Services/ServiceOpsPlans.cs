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

    // Modules. Essentials: Operations, Maintenance and Finance (the core maintenance system).
    public const string Operations = "operations";
    public const string Finance = "finance";
    public const string Maintenance = "maintenance";
    // Professional adds these.
    public const string Inventory = "inventory";
    public const string Advanced = "advanced";
    // Enterprise adds this: multiple branches, API keys and webhooks, monitoring (settings routes and limits).
    public const string EnterpriseModule = "enterprise_tools";
    // WhatsApp Business add-on: the tenant's own number and the two-way inbox.
    public const string WhatsAppBusiness = "whatsapp_business";

    // WhatsApp add-ons (TenantLicense.WhatsAppAddOn).
    public const string WhatsAppNotificationsAddOn = "whatsapp_notifications";
    public const string WhatsAppBusinessAddOn = "whatsapp_business";

    // Factory Operations add-ons (TenantLicense.OperationsAddOns), taken with any edition.
    public const string Visitors = "visitors";
    public const string Security = "security";
    public static readonly IReadOnlyList<string> OperationsAddOnModules = [Visitors, Security];

    public static readonly IReadOnlyList<string> Editions = [Essentials, Professional, Enterprise];

    /// <summary>Every module a sales pipeline can be tied to.</summary>
    public static readonly IReadOnlyList<string> AllModules = [Operations, Finance, Maintenance, Inventory, Advanced, EnterpriseModule, Visitors, Security];

    public static bool IsEdition(string? code) => code is Essentials or Professional or Enterprise;

    public static string DisplayName(string code) => code switch
    {
        Essentials => "Ecosys Factory Maintenance Essentials",
        Professional => "Ecosys Factory Maintenance Professional",
        Enterprise => "Ecosys Factory Maintenance Enterprise",
        _ => throw new BusinessRuleException("Select Essentials, Professional, or Enterprise.")
    };

    public static IReadOnlyList<string> EditionModules(string? planCode) => planCode switch
    {
        Essentials => [Operations, Finance, Maintenance],
        Professional => [Operations, Finance, Maintenance, Inventory, Advanced],
        Enterprise => [Operations, Finance, Maintenance, Inventory, Advanced, EnterpriseModule],
        _ => []
    };

    /// <summary>The tenant's modules: its edition's, plus the WhatsApp Business add-on's and any Factory Operations add-ons.</summary>
    public static IReadOnlyList<string> Modules(LicensePlan plan, string? whatsAppAddOn = null, string? operationsAddOns = null)
    {
        IReadOnlyList<string> modules = whatsAppAddOn == WhatsAppBusinessAddOn ? [.. EditionModules(plan.PlanCode), WhatsAppBusiness] : EditionModules(plan.PlanCode);
        var extra = ParseOperationsAddOns(operationsAddOns);
        return extra.Count == 0 ? modules : [.. modules, .. extra];
    }

    /// <summary>The Factory Operations add-on modules in a stored list, ignoring anything unknown.</summary>
    public static IReadOnlyList<string> ParseOperationsAddOns(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : OperationsAddOnModules.Where(module => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Contains(module, StringComparer.OrdinalIgnoreCase)).ToList();

    /// <summary>Validates and stores a set of Factory Operations add-ons; null for none.</summary>
    public static string? NormalizeOperationsAddOns(IEnumerable<string>? modules)
    {
        var requested = (modules ?? []).Select(x => x?.Trim().ToLowerInvariant()).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var unknown = requested.Where(x => !OperationsAddOnModules.Contains(x!)).ToList();
        if (unknown.Count > 0)
            throw new BusinessRuleException($"Factory Operations add-ons must be among: {string.Join(", ", OperationsAddOnModules)}.");
        var ordered = OperationsAddOnModules.Where(requested.Contains).ToList();
        return ordered.Count == 0 ? null : string.Join(",", ordered);
    }

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
        "AssignmentGroups", "ContractReference", "Dashboard", "EtaNotificationSettings", "Reports", "Sites",
        "TeamManagement", "Technicians", "TechnicianJobs", "WorkOrderAssignments", "WorkOrderExecution",
        "WorkOrderTemplates", "WorkOrders"
    };
    private static readonly HashSet<string> MaintenanceControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Assets", "AssetCategories", "AssetContracts", "AssetLifecycle", "AssetTags", "AssetDocuments", "Meters", "ConditionMonitoring", "SensorGateways", "Reliability", "Fleet", "FactoryAssetImport", "FactoryWorkOrders", "JobPlans", "RunningChecks", "FactoryDashboard", "FactoryPm", "MaintenanceWindows", "Overhaul", "Compliance", "FactoryPeople", "Safety", "Contractors", "FactoryCosts", "FactoryKpis", "ComplianceFramework", "DataProtection", "Contracts", "EscalationRules", "PreventiveMaintenance", "PmTemplates",
        "SlaDefinitions", "SlaPolicies", "Systems"
    };
    private static readonly HashSet<string> InventoryControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "MaterialIssues", "MaterialRequests", "Materials", "PartsRequests", "PartsReturns", "PurchaseOrders",
        "StockCounts", "StockMovements", "StockTransfers", "InventoryReports", "Tools", "InventoryRequests",
        "PurchaseOrderDocuments", "HeldParts", "DirectPurchases", "FactoryStores"
    };
    private static readonly HashSet<string> AdvancedControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApprovalMatrix", "Intake", "IntakeProtocols", "ScheduledReports",
        "TimeAttendance", "Workflows"
    };
    private static readonly HashSet<string> WhatsAppBusinessControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "WhatsApp"
    };
    private static readonly HashSet<string> VisitorsControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Visits", "VisitorGate"
    };

    private static readonly HashSet<string> FinanceControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "ExchangeRates", "TenantFinance"
    };

    /// <summary>Each module's controllers, so classification and gating read from one table.</summary>
    private static readonly (string Module, HashSet<string> Controllers)[] ModuleControllers =
    [
        (Operations, OperationsControllers),
        (Finance, FinanceControllers),
        (Maintenance, MaintenanceControllers),
        (Inventory, InventoryControllers),
        (Advanced, AdvancedControllers),
        (WhatsAppBusiness, WhatsAppBusinessControllers),
        (Visitors, VisitorsControllers),
    ];

    /// <summary>
    /// Tenant controllers deliberately available to every ServiceOps plan (identity, workspace settings,
    /// clients, audit, attachments…). Listed explicitly so that a new controller is never
    /// ungated by omission: <c>ServiceOpsModuleClassificationTests</c> fails for any controller that is in
    /// none of the Operations/Finance/Shared/route-classified sets (audit TM-P2-03).
    /// </summary>
    public static readonly IReadOnlySet<string> SharedControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AccessControl", "ActionItems", "Activities", "AdvancedSettings", "Attachments", "AuditLogs", "Auth", "Branches", "Calendar", "CalendarFeed", "ClientPortalContacts",
        "Clients", "ClientContacts", "Dev", "Integrations", "Notifications",
        "PublicWorkOrder", "OperatorFaultReport", "Sensors", "FleetPositions", "PushSubscriptions", "RoleDashboard", "RolePermissions", "StarterPacks", "Tenant", "TenantEmailDelivery", "TenantEmailDeliveryLogs",
        "TenantEmailTemplates", "TenantLicense", "UserNotificationPreferences", "Users",
        // Factory hierarchy and settings: the backbone every factory module hangs off.
        "OrgNodes", "FactorySettings",
        // Access zones of a factory: used by Visitor Management and Security Patrol alike.
        "AccessZones",
        // WhatsApp reminders (Release 3 design 6): every edition, gated by the WhatsApp add-on.
        // The two-way inbox (WhatsApp controller) needs the WhatsApp Business add-on.
        "MyWhatsApp", "WhatsAppNotifications", "ClientWhatsAppConsent", "WhatsAppWebhook"
    };

    /// <summary>Controllers whose module requirement is decided per route inside <see cref="RequiredModules"/>.</summary>
    public static readonly IReadOnlySet<string> RouteClassifiedControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Import", "Settings"
    };

    public static bool IsClassified(string controller) =>
        ModuleControllers.Any(x => x.Controllers.Contains(controller))
        || SharedControllers.Contains(controller)
        || RouteClassifiedControllers.Contains(controller);

    public static IReadOnlyList<string> RequiredModules(string? controller, string path)
    {
        var route = path.ToLowerInvariant();
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
            if (setting is "tax" or "finance") return [Finance];
        }
        if (controller == "Import" && route.Contains("/assets")) return [Maintenance];
        if (controller == "Import" && (route.Contains("/sites") || route.Contains("/technicians"))) return [Operations];
        return [];
    }

    public static void EnsureModules(IEnumerable<string> enabled, IEnumerable<string> required)
    {
        foreach (var module in required)
            if (!enabled.Contains(module, StringComparer.OrdinalIgnoreCase))
                throw new LicenseException($"Your Ecosys edition does not include {module}. Contact your account manager to upgrade.", $"license_module_{module}", 403);
    }
}
