using Ecosys.Shared.Auth;

namespace Ecosys.Infrastructure.Services;

public static class DashboardWidgetSizes
{
    public const string Small = "small";
    public const string Wide = "wide";

    public static bool IsSupported(string? value) => value is Small or Wide;
}

/// <summary>What a widget's figures depend on, and so what its cached result is keyed by.</summary>
public enum DashboardWidgetScope
{
    /// <summary>The same for everyone in the tenant (finance figures, contracts, purchase orders).</summary>
    Tenant,
    /// <summary>Filtered by the requested branch and the caller's branch access.</summary>
    Branch,
    /// <summary>Also depends on who is asking (project visibility, "my projects").</summary>
    User,
}

/// <param name="Modules">Every module the widget needs; the tenant must have all of them.</param>
/// <param name="AnyPermissions">The caller needs one of these (admins always pass). Empty means any staff member.</param>
/// <param name="Areas">The <see cref="DashboardDataAreas"/> the widget's query reads; a save touching one recomputes it.</param>
public sealed record DashboardWidgetDefinition(
    string Key,
    string Title,
    string Description,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> AnyPermissions,
    string DefaultSize,
    DashboardWidgetScope Scope,
    IReadOnlyList<string> Areas);

/// <summary>
/// The widget catalogue (Release 3, 5.1). Users arrange these; nobody adds queries. Each key has exactly
/// one query in <see cref="RoleDashboardQueries"/>.
/// </summary>
public static class DashboardWidgetCatalog
{
    public const string WorkOrdersOpenOverdue = "work-orders.open-overdue";
    public const string WorkOrdersSlaAtRisk = "work-orders.sla-at-risk";
    public const string WorkOrdersUnassigned = "work-orders.unassigned";
    public const string DispatchTodaysVisits = "dispatch.todays-visits";
    public const string TechniciansWorkload = "technicians.workload";
    public const string TechniciansOnSite = "technicians.on-site";
    public const string MaintenancePmCompliance = "maintenance.pm-compliance";
    public const string ContractsExpiring = "contracts.expiring";
    public const string MaterialsLowStock = "materials.low-stock";
    public const string MaterialsPendingIssues = "materials.pending-issues";
    public const string PurchaseOrdersOpen = "purchase-orders.open";
    public const string FinanceReceivablesOverdue = "finance.receivables-overdue";
    public const string FinanceReceivablesAgeing = "finance.receivables-ageing";
    public const string FinanceCashReceived = "finance.cash-received";
    public const string FinanceTaxWithheld = "finance.tax-withheld";
    public const string FinanceInvoicesToSend = "finance.invoices-to-send";
    public const string ProjectsBillingWaiting = "projects.billing-waiting";
    public const string ProjectsBehindSchedule = "projects.behind-schedule";
    public const string ProjectsMine = "projects.mine";
    public const string ProjectsPendingVariations = "projects.pending-variations";
    public const string ProjectsValuationsAwaiting = "projects.valuations-awaiting";
    public const string ProjectsMusterToday = "projects.muster-today";
    public const string ProjectsUncostedLabour = "projects.uncosted-labour";
    public const string ActionItemsDue = "action-items.due";

    private static readonly string[] Ops = [ServiceOpsPlans.Operations];
    private static readonly string[] Mnt = [ServiceOpsPlans.Maintenance];
    private static readonly string[] Inv = [ServiceOpsPlans.Inventory];
    private static readonly string[] Fin = [ServiceOpsPlans.Finance];
    private static readonly string[] Prj = [ServiceOpsPlans.Projects];
    private static readonly string[] PrjFin = [ServiceOpsPlans.Projects, ServiceOpsPlans.Finance];

    private static readonly string[] ViewWorkOrders = [PermissionNames.CanViewWorkOrders];
    private static readonly string[] Dispatch = [PermissionNames.CanAssignWorkOrders];
    private static readonly string[] ViewMaterials =
    [
        PermissionNames.CanViewMaterials, PermissionNames.CanRequestMaterials, PermissionNames.CanManageMaterials,
        PermissionNames.CanApproveMaterials, PermissionNames.CanIssueMaterials,
    ];
    private static readonly string[] ViewFinance = [PermissionNames.FinanceView];

    private static readonly string[] WorkOrderData = [DashboardDataAreas.WorkOrders];
    private static readonly string[] PmData = [DashboardDataAreas.Maintenance, DashboardDataAreas.WorkOrders];
    private static readonly string[] ContractData = [DashboardDataAreas.Contracts];
    private static readonly string[] MaterialData = [DashboardDataAreas.Materials];
    private static readonly string[] FinanceData = [DashboardDataAreas.Finance];
    private static readonly string[] ProjectData = [DashboardDataAreas.Projects];
    private static readonly string[] ProjectFinanceData = [DashboardDataAreas.Projects, DashboardDataAreas.Finance];
    private static readonly string[] ActivityData = [DashboardDataAreas.Activities];

    public static readonly IReadOnlyList<DashboardWidgetDefinition> All =
    [
        new(WorkOrdersOpenOverdue, "Open and overdue work orders", "Work orders still open, and those past their due date.",
            Ops, ViewWorkOrders, DashboardWidgetSizes.Small, DashboardWidgetScope.Branch, WorkOrderData),
        new(WorkOrdersSlaAtRisk, "SLA at risk", "Open work orders within two hours of their resolution deadline, and those already breached.",
            Ops, ViewWorkOrders, DashboardWidgetSizes.Small, DashboardWidgetScope.Branch, WorkOrderData),
        new(WorkOrdersUnassigned, "Unassigned work orders", "Open work orders with no technician and no assignment group.",
            Ops, Dispatch, DashboardWidgetSizes.Small, DashboardWidgetScope.Branch, WorkOrderData),
        new(DispatchTodaysVisits, "Today's visits by technician", "Visits scheduled to start today, per technician.",
            Ops, Dispatch, DashboardWidgetSizes.Wide, DashboardWidgetScope.Branch, WorkOrderData),
        new(TechniciansWorkload, "Technician workload", "Open work orders per technician, with responses still pending.",
            Ops, Dispatch, DashboardWidgetSizes.Wide, DashboardWidgetScope.Branch, WorkOrderData),
        new(TechniciansOnSite, "Technicians on site", "Technicians who have arrived on a job and not yet left.",
            Ops, Dispatch, DashboardWidgetSizes.Small, DashboardWidgetScope.Branch, WorkOrderData),
        new(MaintenancePmCompliance, "PM compliance", "Preventive maintenance done on time over the last 30 days, and plans overdue.",
            Mnt, [PermissionNames.CanViewWorkOrders, PermissionNames.CanManageSettings], DashboardWidgetSizes.Small, DashboardWidgetScope.Branch, PmData),
        new(ContractsExpiring, "Contracts expiring", "Contracts ending in the next 30 and 90 days.",
            Mnt, [], DashboardWidgetSizes.Small, DashboardWidgetScope.Tenant, ContractData),
        new(MaterialsLowStock, "Low stock", "Materials at or below their reorder level.",
            Inv, ViewMaterials, DashboardWidgetSizes.Small, DashboardWidgetScope.Branch, MaterialData),
        new(MaterialsPendingIssues, "Pending material issues", "Issues waiting for approval, and approved issues not yet handed out.",
            Inv, [PermissionNames.CanIssueMaterials, PermissionNames.CanApproveMaterials, PermissionNames.CanManageMaterials], DashboardWidgetSizes.Small, DashboardWidgetScope.Branch, MaterialData),
        new(PurchaseOrdersOpen, "Open purchase orders", "Purchase orders not yet fully received, and those past their expected date.",
            Inv, [PermissionNames.CanViewMaterials, PermissionNames.CanManageMaterials], DashboardWidgetSizes.Small, DashboardWidgetScope.Tenant, MaterialData),
        new(FinanceReceivablesOverdue, "Receivables overdue", "Unpaid invoice balances past their due date.",
            Fin, ViewFinance, DashboardWidgetSizes.Small, DashboardWidgetScope.Tenant, FinanceData),
        new(FinanceReceivablesAgeing, "Receivables ageing", "Unpaid invoice balances by days past due.",
            Fin, ViewFinance, DashboardWidgetSizes.Wide, DashboardWidgetScope.Tenant, FinanceData),
        new(FinanceCashReceived, "Cash received this month", "Confirmed receipts since the first of the month.",
            Fin, ViewFinance, DashboardWidgetSizes.Small, DashboardWidgetScope.Tenant, FinanceData),
        new(FinanceTaxWithheld, "Tax withheld this month", "Withholding certificates dated this month.",
            Fin, ViewFinance, DashboardWidgetSizes.Small, DashboardWidgetScope.Tenant, FinanceData),
        new(FinanceInvoicesToSend, "Invoices to send", "Draft invoices not yet issued.",
            Fin, ViewFinance, DashboardWidgetSizes.Small, DashboardWidgetScope.Tenant, FinanceData),
        new(ProjectsBillingWaiting, "Project billing waiting", "Certified valuations not yet invoiced, and retention that can be released.",
            PrjFin, ViewFinance, DashboardWidgetSizes.Wide, DashboardWidgetScope.Branch, ProjectFinanceData),
        new(ProjectsBehindSchedule, "Projects behind schedule", "Open projects whose progress is behind their planned milestones.",
            Prj, [PermissionNames.ProjectsView], DashboardWidgetSizes.Wide, DashboardWidgetScope.User, ProjectData),
        new(ProjectsMine, "My projects", "Projects you manage or are on, with progress against plan.",
            Prj, [PermissionNames.ProjectsView], DashboardWidgetSizes.Wide, DashboardWidgetScope.User, ProjectData),
        new(ProjectsPendingVariations, "Pending variations", "Variations submitted and waiting for a decision.",
            Prj, [PermissionNames.ProjectsVariationsApprove, PermissionNames.ProjectsManage], DashboardWidgetSizes.Small, DashboardWidgetScope.User, ProjectData),
        new(ProjectsValuationsAwaiting, "Valuations awaiting certificate or invoice", "Submitted valuations not yet certified, and certified ones not yet invoiced.",
            PrjFin, [PermissionNames.ProjectsFinancialsView], DashboardWidgetSizes.Wide, DashboardWidgetScope.User, ProjectData),
        new(ProjectsMusterToday, "Today's muster roll", "Active projects with workers, and whether today's roll has been marked.",
            Prj, [PermissionNames.ProjectsView], DashboardWidgetSizes.Wide, DashboardWidgetScope.User, ProjectData),
        new(ProjectsUncostedLabour, "Uncosted labour days", "Days on the muster roll with no daily rate set for the worker.",
            PrjFin, [PermissionNames.ProjectsFinancialsView], DashboardWidgetSizes.Wide, DashboardWidgetScope.User, ProjectData),
        new(ActionItemsDue, "My action items", "Your open action items: overdue, and due in the next seven days.",
            [], [PermissionNames.ActivitiesView], DashboardWidgetSizes.Small, DashboardWidgetScope.User, ActivityData),
    ];

    private static readonly Dictionary<string, DashboardWidgetDefinition> ByKey =
        All.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

    public static DashboardWidgetDefinition? Find(string? key) =>
        key is not null && ByKey.TryGetValue(key, out var widget) ? widget : null;
}

/// <summary>Default layouts per role (5.2). Admins hide widgets per role; users rearrange their own.</summary>
public static class DashboardRoleDefaults
{
    /// <summary>
    /// Not a login role: anyone who manages an open project (or is on one as its project manager).
    /// Its widgets are added to their own role's default, and an admin can hide widgets for it too.
    /// </summary>
    public const string ProjectManager = "ProjectManager";

    private static readonly string[] Manager =
    [
        DashboardWidgetCatalog.WorkOrdersOpenOverdue,
        DashboardWidgetCatalog.WorkOrdersSlaAtRisk,
        DashboardWidgetCatalog.TechniciansWorkload,
        DashboardWidgetCatalog.MaintenancePmCompliance,
        DashboardWidgetCatalog.ContractsExpiring,
        DashboardWidgetCatalog.ProjectsBehindSchedule,
        DashboardWidgetCatalog.FinanceReceivablesOverdue,
    ];

    private static readonly string[] Dispatcher =
    [
        DashboardWidgetCatalog.WorkOrdersUnassigned,
        DashboardWidgetCatalog.DispatchTodaysVisits,
        DashboardWidgetCatalog.WorkOrdersSlaAtRisk,
        DashboardWidgetCatalog.TechniciansOnSite,
    ];

    private static readonly string[] Finance =
    [
        DashboardWidgetCatalog.FinanceReceivablesAgeing,
        DashboardWidgetCatalog.FinanceCashReceived,
        DashboardWidgetCatalog.FinanceTaxWithheld,
        DashboardWidgetCatalog.FinanceInvoicesToSend,
        DashboardWidgetCatalog.ProjectsBillingWaiting,
    ];

    private static readonly string[] ProjectManagerWidgets =
    [
        DashboardWidgetCatalog.ProjectsMine,
        DashboardWidgetCatalog.ActionItemsDue,
        DashboardWidgetCatalog.ProjectsPendingVariations,
        DashboardWidgetCatalog.ProjectsValuationsAwaiting,
        DashboardWidgetCatalog.ProjectsMusterToday,
        DashboardWidgetCatalog.ProjectsUncostedLabour,
    ];

    private static readonly string[] Materials =
    [
        DashboardWidgetCatalog.MaterialsLowStock,
        DashboardWidgetCatalog.MaterialsPendingIssues,
        DashboardWidgetCatalog.PurchaseOrdersOpen,
    ];

    /// <summary>The roles an admin can hide widgets for, in the order the settings screen lists them.</summary>
    public static readonly IReadOnlyList<string> ConfigurableRoles =
    [
        AppRoles.Admin, AppRoles.Manager, AppRoles.Dispatcher, AppRoles.FinanceUser, ProjectManager,
        AppRoles.MaterialsController, AppRoles.Viewer, AppRoles.Technician,
    ];

    /// <summary>Maps a login role onto the role its dashboard follows (legacy "User" is a Viewer).</summary>
    public static string Normalize(string? role)
    {
        var match = ConfigurableRoles.FirstOrDefault(x => string.Equals(x, role, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match;
        return string.Equals(role, AppRoles.User, StringComparison.OrdinalIgnoreCase) ? AppRoles.Viewer : role ?? AppRoles.Viewer;
    }

    public static IReadOnlyList<string> For(string role) => Normalize(role) switch
    {
        AppRoles.Admin or AppRoles.Manager => Manager,
        AppRoles.Dispatcher => Dispatcher,
        AppRoles.FinanceUser => Finance,
        AppRoles.MaterialsController => Materials,
        ProjectManager => ProjectManagerWidgets,
        // Viewers get read-only versions of the manager widgets they have permission for.
        AppRoles.Viewer => Manager,
        _ => [],
    };

    /// <summary>The role default for this person: their role's widgets, then the project manager's if they run a project.</summary>
    public static IReadOnlyList<string> For(string role, bool isProjectManager) =>
        isProjectManager ? [.. For(role).Concat(ProjectManagerWidgets).Distinct(StringComparer.OrdinalIgnoreCase)] : For(role);
}
