namespace Ecosys.Shared.Auth;

public static class PermissionNames
{
    public const string CanViewWorkOrders = "can_view_workorders";
    public const string CanCreateWorkOrders = "can_create_workorders";
    public const string CanAssignWorkOrders = "can_assign_workorders";
    public const string CanCompleteWorkOrders = "can_complete_workorders";
    public const string CanViewKnowledge = "can_view_knowledge";
    public const string CanManageKnowledge = "can_manage_knowledge";
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
    public const string FinanceViewBillableWork = "finance.view_billable_work";
    public const string FinanceManageBillableWork = "finance.manage_billable_work";
    public const string MaterialsOverrideStock = "materials.override_stock";

    public static readonly string[] All =
    [
        CanViewWorkOrders,
        CanCreateWorkOrders,
        CanAssignWorkOrders,
        CanCompleteWorkOrders,
        CanViewKnowledge,
        CanManageKnowledge,
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
        FinanceViewBillableWork,
        FinanceManageBillableWork,
        MaterialsOverrideStock
    ];
}
