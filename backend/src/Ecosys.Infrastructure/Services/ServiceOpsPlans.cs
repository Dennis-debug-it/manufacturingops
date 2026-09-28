using Ecosys.Domain.Entities;
using Ecosys.Shared.Errors;

namespace Ecosys.Infrastructure.Services;

/// <summary>Commercial modules on the existing license plan, independent of names and RBAC.</summary>
public static class ServiceOpsPlans
{
    public const string Operations = "operations";
    public const string Finance = "finance";
    public const string Suite = "suite";
    public static bool IsCommercial(string code) => code is Operations or Finance or Suite;
    public static string DisplayName(string code) => code switch
    {
        Operations => "Ecosys ServiceOps Operations",
        Finance => "Ecosys ServiceOps Finance",
        Suite => "Ecosys ServiceOps Suite",
        _ => throw new BusinessRuleException("Select ServiceOps Operations, Finance, or Suite.")
    };

    public static IReadOnlyList<string> Modules(LicensePlan plan)
    {
        // Canonical offerings have fixed module boundaries. Feature flags and capacity
        // overrides continue to use the existing licensing mechanisms.
        if (plan.PlanCode == Operations) return [Operations];
        if (plan.PlanCode == Finance) return [Finance];
        if (plan.PlanCode == Suite) return [Operations, Finance];
        var explicitModules = (plan.ModulesIncluded ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.ToLowerInvariant()).Where(x => x is Operations or Finance).Distinct().ToArray();
        // Existing plans had no module restrictions. Preserve their paid/demo access.
        return explicitModules.Length > 0 ? explicitModules : [Operations, Finance];
    }

    private static readonly HashSet<string> OperationsControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "AssignmentGroups", "Assets", "AssetCategories", "Contracts", "ContractReference", "Csat",
        "Dashboard", "Dispatch", "EscalationRules", "EtaNotificationSettings", "Intake", "IntakeProtocols",
        "MaterialIssues", "MaterialRequests", "Materials", "PartsRequests", "PreventiveMaintenance", "PmTemplates",
        "PurchaseOrders", "Reports", "ScheduledReports", "Sites", "Systems", "SlaDefinitions", "SlaPolicies",
        "StockTransfers", "TeamManagement", "Technicians", "TechnicianJobs", "TimeAttendance", "Vans", "WhatsApp",
        "Workflows", "WorkOrderAssignments", "WorkOrderExecution", "WorkOrderTemplates", "WorkOrders"
    };
    private static readonly HashSet<string> FinanceControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "ApprovalMatrix", "CreditTerms", "ExchangeRates", "ExpensesPayables", "TenantCreditNote",
        "TenantFinance", "TenantInvoice", "TenantQuotation"
    };

    /// <summary>
    /// Tenant controllers deliberately available to every ServiceOps plan (identity, workspace settings,
    /// clients, knowledge base, audit, attachments…). Listed explicitly so that a new controller is never
    /// ungated by omission: <c>ServiceOpsModuleClassificationTests</c> fails for any controller that is in
    /// none of the Operations/Finance/Shared/route-classified sets (audit TM-P2-03).
    /// </summary>
    public static readonly IReadOnlySet<string> SharedControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AccessControl", "AdvancedSettings", "Attachments", "AuditLogs", "Auth", "Branches", "ClientPortalContacts",
        "Clients", "Dev", "Integrations", "Kip", "Knowledge", "KnowledgeDocuments", "PortalAuth", "PortalKnowledge",
        "PublicWorkOrder", "RolePermissions", "StarterPacks", "Tenant", "TenantEmailDeliveryLogs",
        "TenantEmailTemplates", "TenantLicense", "UserNotificationPreferences", "Users"
    };

    /// <summary>Controllers whose module requirement is decided per route inside <see cref="RequiredModules"/>.</summary>
    public static readonly IReadOnlySet<string> RouteClassifiedControllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Portal", "WorkOrderBilling", "Import", "Settings"
    };

    public static bool IsClassified(string controller) =>
        OperationsControllers.Contains(controller)
        || FinanceControllers.Contains(controller)
        || SharedControllers.Contains(controller)
        || RouteClassifiedControllers.Contains(controller);

    public static IReadOnlyList<string> RequiredModules(string? controller, string path)
    {
        var route = path.ToLowerInvariant();
        if (controller == "Portal")
        {
            if (route.StartsWith("/api/portal/invoices") || route.StartsWith("/api/portal/quotations")) return [Finance];
            if (!route.StartsWith("/api/portal/profile")) return [Operations];
        }
        if (controller == "WorkOrderBilling" || route.Contains("/generate-invoice") || route.Contains("/convert-to-work-order") || route.EndsWith("/from-quote"))
            return [Operations, Finance];
        if (controller is not null && OperationsControllers.Contains(controller)) return [Operations];
        if (controller is not null && FinanceControllers.Contains(controller)) return [Finance];
        if (route.StartsWith("/api/settings/"))
        {
            var setting = route.Split('/')[3];
            if (setting is "email-intake" or "monitoring-webhooks" or "inventory" or "time-attendance" or "operations-policy" or "assignment-groups" or "whatsapp") return [Operations];
            if (setting is "payments" or "credit-control" or "tax" or "finance") return [Finance];
        }
        if (controller == "Import" && (route.Contains("/assets") || route.Contains("/sites") || route.Contains("/technicians"))) return [Operations];
        return [];
    }

    public static void EnsureModules(IEnumerable<string> enabled, IEnumerable<string> required)
    {
        foreach (var module in required)
            if (!enabled.Contains(module, StringComparer.OrdinalIgnoreCase))
                throw new LicenseException($"Your ServiceOps plan does not include {module}. Contact your account manager to upgrade.", $"license_module_{module}", 403);
    }
}
