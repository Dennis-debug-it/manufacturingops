namespace Ecosys.Shared.Auth;

public static class PermissionNames
{
    public const string CanViewWorkOrders = "can_view_workorders";
    public const string CanCreateWorkOrders = "can_create_workorders";
    public const string CanAssignWorkOrders = "can_assign_workorders";
    public const string CanCompleteWorkOrders = "can_complete_workorders";
    public const string CanViewMaterials = "can_view_materials";
    public const string CanRequestMaterials = "can_request_materials";
    public const string CanManageMaterials = "can_manage_materials";
    public const string CanApproveMaterials = "can_approve_materials";
    public const string CanIssueMaterials = "can_issue_materials";
    public const string CanManageAssets = "can_manage_assets";
    public const string CanManageSettings = "can_manage_settings";
    public const string CanViewReports = "can_view_reports";
    public const string FinanceView = "finance.view";
    public const string FinanceManage = "finance.manage";
    public const string FinanceViewRates = "finance.view_rates";
    public const string FinanceManageRates = "finance.manage_rates";
    public const string FinanceViewApprovals = "finance.view_approvals";
    public const string FinanceManageApprovals = "finance.manage_approvals";
    public const string MaterialsOverrideStock = "materials.override_stock";
    public const string AttendanceView = "attendance.view";
    public const string AttendanceManage = "attendance.manage";
    /// <summary>See meetings and action items, and organise your own (Release 3 design 3.6).</summary>
    public const string ActivitiesView = "activities.view";
    /// <summary>Organise and change any meeting, not only your own.</summary>
    public const string ActivitiesManage = "activities.manage";
    /// <summary>The team view of the calendar (other people's items).</summary>
    public const string CalendarTeam = "calendar.team";
    /// <summary>Gate desk: register visitors, check them in and out, see who is on site, run a roll call.</summary>
    public const string VisitorsGate = "visitors.gate";
    /// <summary>Set up access zones, block and unblock visitors, approve any visit, and override a failed gate check.</summary>
    public const string VisitorsManage = "visitors.manage";
    /// <summary>Walk patrols: scan checkpoints, report security incidents, use the panic button, hand over a shift.</summary>
    public const string SecurityPatrol = "security.patrol";
    /// <summary>Set up checkpoints, routes and schedules; watch the patrol board; manage security incidents.</summary>
    public const string SecurityManage = "security.manage";

    /// <summary>
    /// Capabilities with no column in the legacy role/user permission tables. Their baseline comes
    /// from role defaults in code; permission groups and per-user overrides adjust them, and login
    /// carries the granted ones in the token as ordinary permission claims.
    /// </summary>
    public static readonly string[] CapabilityOnly =
    [
        ActivitiesView,
        ActivitiesManage,
        CalendarTeam,
        VisitorsGate,
        VisitorsManage,
        SecurityPatrol,
        SecurityManage
    ];

    public static readonly string[] All =
    [
        CanViewWorkOrders,
        CanCreateWorkOrders,
        CanAssignWorkOrders,
        CanCompleteWorkOrders,
        CanViewMaterials,
        CanRequestMaterials,
        CanManageMaterials,
        CanApproveMaterials,
        CanIssueMaterials,
        CanManageAssets,
        CanManageSettings,
        CanViewReports,
        FinanceView,
        FinanceManage,
        FinanceViewRates,
        FinanceManageRates,
        FinanceViewApprovals,
        FinanceManageApprovals,
        MaterialsOverrideStock,
        AttendanceView,
        AttendanceManage,
        ActivitiesView,
        ActivitiesManage,
        CalendarTeam,
        VisitorsGate,
        VisitorsManage,
        SecurityPatrol,
        SecurityManage
    ];
}
