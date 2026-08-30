import { lazy, Suspense, useMemo } from 'react'
import {
  Navigate,
  Outlet,
  Route,
  Routes,
} from 'react-router-dom'
import { ProtectedRoute } from '../shared/components/ProtectedRoute'
import { Spinner } from '../shared/components/Spinner'
import { useAuth } from '../shared/hooks/useAuth'
import { AppShell } from '../shared/layout/AppShell'
import type { AppRole } from '../shared/types/auth'
const DashboardPage = lazy(() => import('../features/dashboard/DashboardPage').then((module) => ({ default: module.DashboardPage })))
const ExecutiveDashboardPage = lazy(() => import('../features/dashboard/ExecutiveDashboardPage').then((module) => ({ default: module.ExecutiveDashboardPage })))
const LoginPage = lazy(() => import('../features/auth/LoginPage').then((module) => ({ default: module.LoginPage })))
const ForgotPasswordPage = lazy(() => import('../features/auth/ForgotPasswordPage').then((module) => ({ default: module.ForgotPasswordPage })))
const ResetPasswordPage = lazy(() => import('../features/auth/ResetPasswordPage').then((module) => ({ default: module.ResetPasswordPage })))
const ImpersonationEntryPage = lazy(() => import('../features/auth/ImpersonationEntryPage').then((module) => ({ default: module.ImpersonationEntryPage })))
const RequestDemoPage = lazy(() => import('../features/public/RequestDemoPage').then((module) => ({ default: module.RequestDemoPage })))
const DemoConfirmationPage = lazy(() => import('../features/public/DemoConfirmationPage').then((module) => ({ default: module.DemoConfirmationPage })))
const CsatSurveyPage = lazy(() => import('../features/public/CsatSurveyPage').then((module) => ({ default: module.CsatSurveyPage })))
const TrackJobPage = lazy(() => import('../features/portal/TrackJobPage').then((module) => ({ default: module.TrackJobPage })))
const ForbiddenPage = lazy(() => import('../features/public/ForbiddenPage').then((module) => ({ default: module.ForbiddenPage })))
const NotFoundPage = lazy(() => import('../features/public/NotFoundPage').then((module) => ({ default: module.NotFoundPage })))
const WorkOrdersPage = lazy(() => import('../features/work-orders/WorkOrdersPage').then((module) => ({ default: module.WorkOrdersPage })))
const WorkOrderDetailPage = lazy(() => import('../features/work-orders/WorkOrderDetailPage').then((module) => ({ default: module.WorkOrderDetailPage })))
const MaintenanceCalendarPageReal = lazy(() => import('../features/maintenance/CalendarPage').then((module) => ({ default: module.CalendarPage })))
const MaintenanceSchedulesPageReal = lazy(() => import('../features/maintenance/SchedulesPage').then((module) => ({ default: module.SchedulesPage })))
const MaintenanceScheduleDetailPageReal = lazy(() => import('../features/maintenance/ScheduleDetailPage').then((module) => ({ default: module.ScheduleDetailPage })))
const MaintenanceCompliancePageReal = lazy(() => import('../features/maintenance/CompliancePage').then((module) => ({ default: module.CompliancePage })))
const MaintenanceEnginePageReal = lazy(() => import('../features/maintenance/EnginePage').then((module) => ({ default: module.EnginePage })))
const PartsPage = lazy(() => import('../features/inventory/PartsPage').then((module) => ({ default: module.PartsPage })))
const PartDetailPage = lazy(() => import('../features/inventory/PartDetailPage').then((module) => ({ default: module.PartDetailPage })))
const StockPage = lazy(() => import('../features/inventory/StockPage').then((module) => ({ default: module.StockPage })))
const LocationsPage = lazy(() => import('../features/inventory/LocationsPage').then((module) => ({ default: module.LocationsPage })))
const LocationDetailPage = lazy(() => import('../features/inventory/LocationDetailPage').then((module) => ({ default: module.LocationDetailPage })))
const MovementsPage = lazy(() => import('../features/inventory/MovementsPage').then((module) => ({ default: module.MovementsPage })))
const PurchaseOrdersPage = lazy(() => import('../features/inventory/PurchaseOrdersPage').then((module) => ({ default: module.PurchaseOrdersPage })))
const PurchaseOrderDetailPage = lazy(() => import('../features/inventory/PurchaseOrdersPage').then((module) => ({ default: module.PurchaseOrderDetailPage })))
const SuppliersPage = lazy(() => import('../features/inventory/SuppliersPage').then((module) => ({ default: module.SuppliersPage })))
const SupplierDetailPage = lazy(() => import('../features/inventory/SuppliersPage').then((module) => ({ default: module.SupplierDetailPage })))
const RestockRequestsPage = lazy(() => import('../features/inventory/RestockRequestsPage').then((module) => ({ default: module.RestockRequestsPage })))
const PartsRequestsPage = lazy(() => import('../features/inventory/PartsRequestsPage').then((module) => ({ default: module.PartsRequestsPage })))
const MaterialIssuesPage = lazy(() => import('../features/inventory/MaterialIssuesPage').then((module) => ({ default: module.MaterialIssuesPage })))
const VansPage = lazy(() => import('../features/inventory/VansPage').then((module) => ({ default: module.VansPage })))
const VanDetailPage = lazy(() => import('../features/inventory/VanDetailPage').then((module) => ({ default: module.VanDetailPage })))
const InventoryAlertsPageReal = lazy(() => import('../features/inventory/AlertsPage').then((module) => ({ default: module.AlertsPage })))
const InventoryReportsPageReal = lazy(() => import('../features/inventory/ReportsPage').then((module) => ({ default: module.ReportsPage })))
const TeamUsersPageReal = lazy(() => import('../features/team/UsersPage').then((module) => ({ default: module.UsersPage })))
const TeamUserDetailPageReal = lazy(() => import('../features/team/UserDetailPage').then((module) => ({ default: module.UserDetailPage })))
const TeamTechniciansPageReal = lazy(() => import('../features/team/TechniciansPage').then((module) => ({ default: module.TechniciansPage })))
const TechnicianDetailPage = lazy(() => import('../features/team/TechnicianDetailPage').then((module) => ({ default: module.TechnicianDetailPage })))
const TeamRolesPageReal = lazy(() => import('../features/team/RolesPage').then((module) => ({ default: module.RolesPage })))
const AccessSectionPage = lazy(() => import('../features/team/AccessSectionPage').then((module) => ({ default: module.AccessSectionPage })))
const TeamSkillsPageReal = lazy(() => import('../features/team/SkillsPage').then((module) => ({ default: module.SkillsPage })))
const TeamGroupsPageReal = lazy(() => import('../features/team/GroupsPage').then((module) => ({ default: module.GroupsPage })))
const LiveAttendancePage = lazy(() => import('../features/team/TimeAttendancePages').then((module) => ({ default: module.LiveAttendancePage })))
const TimesheetsPage = lazy(() => import('../features/team/TimeAttendancePages').then((module) => ({ default: module.TimesheetsPage })))
const TimesheetDetailPage = lazy(() => import('../features/team/TimeAttendancePages').then((module) => ({ default: module.TimesheetDetailPage })))
const LeavePage = lazy(() => import('../features/team/TimeAttendancePages').then((module) => ({ default: module.LeavePage })))
const TimeAttendanceReportsPage = lazy(() => import('../features/team/TimeAttendancePages').then((module) => ({ default: module.TimeAttendanceReportsPage })))
const MyTimePage = lazy(() => import('../features/team/MyTimePage').then((module) => ({ default: module.MyTimePage })))
const ContractsPageReal = lazy(() => import('../features/contracts/ContractsPage').then((module) => ({ default: module.ContractsPage })))
const ContractDetailPageReal = lazy(() => import('../features/contracts/ContractDetailPage').then((module) => ({ default: module.ContractDetailPage })))
const InvoicesPage = lazy(() => import('../features/finance/InvoicesPage').then((module) => ({ default: module.InvoicesPage })))
const DocumentsPage = lazy(() => import('../features/finance/DocumentsPage').then((module) => ({ default: module.DocumentsPage })))
const InvoiceDetailPage = lazy(() => import('../features/finance/InvoiceDetailPage').then((module) => ({ default: module.InvoiceDetailPage })))
const FinanceOverviewPage = lazy(() => import('../features/finance/FinanceOverviewPage').then((module) => ({ default: module.FinanceOverviewPage })))
const QuotesPage = lazy(() => import('../features/finance/QuotesPage').then((module) => ({ default: module.QuotesPage })))
const QuoteDetailPage = lazy(() => import('../features/finance/QuoteDetailPage').then((module) => ({ default: module.QuoteDetailPage })))
const FinanceAgingPageReal = lazy(() => import('../features/finance/AgingPage').then((module) => ({ default: module.AgingPage })))
const CreditControlPage = lazy(() => import('../features/finance/CreditControlPage').then((module) => ({ default: module.CreditControlPage })))
const ReceiptsPage = lazy(() => import('../features/finance/ReceiptsPage').then((module) => ({ default: module.ReceiptsPage })))
const CreditNotesPage = lazy(() => import('../features/finance/CreditNotesPage').then((module) => ({ default: module.CreditNotesPage })))
const CreditNoteDetailPage = lazy(() => import('../features/finance/CreditNoteDetailPage').then((module) => ({ default: module.CreditNoteDetailPage })))
const ExpensesPage = lazy(() => import('../features/finance/ExpensesPage').then((module) => ({ default: module.ExpensesPage })))
const ExpenseDetailPage = lazy(() => import('../features/finance/ExpensesPage').then((module) => ({ default: module.ExpenseDetailPage })))
const PayablesPage = lazy(() => import('../features/finance/PayablesPage').then((module) => ({ default: module.PayablesPage })))
const PayableDetailPage = lazy(() => import('../features/finance/PayablesPage').then((module) => ({ default: module.PayableDetailPage })))
const InvoiceApprovalsPage = lazy(() => import('../features/finance/InvoiceApprovalsPage').then((module) => ({ default: module.InvoiceApprovalsPage })))
const FinanceRateCardsPageReal = lazy(() => import('../features/finance/RateCardsPage').then((module) => ({ default: module.RateCardsPage })))
const FinancePreferentialRatesPageReal = lazy(() => import('../features/finance/PreferentialRatesPage').then((module) => ({ default: module.PreferentialRatesPage })))
const FinanceExchangeRatesPageReal = lazy(() => import('../features/finance/ExchangeRatesPage').then((module) => ({ default: module.ExchangeRatesPage })))
const FinanceReportsPageReal = lazy(() => import('../features/finance/FinanceReportsPage').then((module) => ({ default: module.FinanceReportsPage })))
const ReportsPageReal = lazy(() => import('../features/reports/ReportsPage').then((module) => ({ default: module.ReportsPage })))
const OnboardingPageReal = lazy(() => import('../features/onboarding/OnboardingPage').then((module) => ({ default: module.OnboardingPage })))
const ClientsPage = lazy(() => import('../features/clients/ClientsPage').then((module) => ({ default: module.ClientsPage })))
const SitesPage = lazy(() => import('../features/clients/SitesPage').then((module) => ({ default: module.SitesPage })))
const AssetsPage = lazy(() => import('../features/clients/AssetsPage').then((module) => ({ default: module.AssetsPage })))
const ClientDetailPage = lazy(() => import('../features/clients/ClientDetailPage').then((module) => ({ default: module.ClientDetailPage })))
const SiteDetailPage = lazy(() => import('../features/clients/SiteDetailPage').then((module) => ({ default: module.SiteDetailPage })))
const AssetDetailPage = lazy(() => import('../features/clients/AssetDetailPage').then((module) => ({ default: module.AssetDetailPage })))
const SettingsCompanyPageReal = lazy(() => import('../features/settings/CompanySettingsPage').then((module) => ({ default: module.CompanySettingsPage })))
const SettingsBrandingPageReal = lazy(() => import('../features/settings/BrandingSettingsPage').then((module) => ({ default: module.BrandingSettingsPage })))
const SettingsSecurityPageReal = lazy(() => import('../features/settings/SecuritySettingsPage').then((module) => ({ default: module.SecuritySettingsPage })))
const SettingsNotificationsPageReal = lazy(() => import('../features/settings/NotificationsSettingsPage').then((module) => ({ default: module.NotificationsSettingsPage })))
const SettingsNumberingPageReal = lazy(() => import('../features/settings/NumberingSettingsPage').then((module) => ({ default: module.NumberingSettingsPage })))
const SettingsPmTemplatesPageReal = lazy(() => import('../features/settings/PmTemplatesSettingsPage').then((module) => ({ default: module.PmTemplatesSettingsPage })))
const SettingsEmailTemplatesPageReal = lazy(() => import('../features/settings/EmailTemplatesSettingsPage').then((module) => ({ default: module.EmailTemplatesSettingsPage })))
const SettingsAuditLogsPageReal = lazy(() => import('../features/settings/AuditLogsSettingsPage').then((module) => ({ default: module.AuditLogsSettingsPage })))
const SettingsAccountPageReal = lazy(() => import('../features/settings/AccountSettingsPage').then((module) => ({ default: module.AccountSettingsPage })))
const SettingsTemplatesPageReal = lazy(() => import('../features/settings/DocumentTemplatesSettingsPage').then((module) => ({ default: module.DocumentTemplatesSettingsPage })))
const SettingsEmailIntakePageReal = lazy(() => import('../features/settings/EmailIntakeSettingsPage').then((module) => ({ default: module.EmailIntakeSettingsPage })))
const SettingsEmailDeliveryPageReal = lazy(() => import('../features/settings/EmailDeliverySettingsPage').then((module) => ({ default: module.EmailDeliverySettingsPage })))
const SettingsMonitoringIntakePageReal = lazy(() => import('../features/settings/MonitoringIntakeSettingsPage').then((module) => ({ default: module.MonitoringIntakeSettingsPage })))
const SettingsCreditControlPageReal = lazy(() => import('../features/settings/CreditControlSettingsPage').then((module) => ({ default: module.CreditControlSettingsPage })))
const SettingsOverviewPageReal = lazy(() => import('../features/settings/SettingsOverviewPage').then((module) => ({ default: module.SettingsOverviewPage })))
const SettingsTimeAttendancePageReal = lazy(() => import('../features/settings/TimeAttendanceSettingsPage').then((module) => ({ default: module.TimeAttendanceSettingsPage })))
const SettingsInventoryPageReal = lazy(() => import('../features/settings/InventorySettingsPage').then((module) => ({ default: module.InventorySettingsPage })))
const SettingsWhatsAppPageReal = lazy(() => import('../features/settings/WhatsAppSettingsPage').then((module) => ({ default: module.WhatsAppSettingsPage })))
const SettingsPaymentsPageReal = lazy(() => import('../features/settings/PaymentsSettingsPage').then((module) => ({ default: module.PaymentsSettingsPage })))
const SettingsPortalPageReal = lazy(() => import('../features/settings/PortalSettingsPage').then((module) => ({ default: module.PortalSettingsPage })))
const SettingsIntegrationsPageReal = lazy(() => import('../features/settings/IntegrationsSettingsPage').then((module) => ({ default: module.IntegrationsSettingsPage })))
const SettingsExecutiveDashboardPageReal = lazy(() => import('../features/settings/ExecutiveDashboardSettingsPage').then((module) => ({ default: module.ExecutiveDashboardSettingsPage })))
const ApprovalMatrixSettingsPage = lazy(() => import('../features/settings/ApprovalMatrixSettingsPage').then((module) => ({ default: module.ApprovalMatrixSettingsPage })))
const NotificationTemplatesPage = lazy(() => import('../features/settings/NotificationTemplatesPage').then((module) => ({ default: module.NotificationTemplatesPage })))
const SlaPoliciesPage = lazy(() => import('../features/settings/SlaPoliciesPage').then((module) => ({ default: module.SlaPoliciesPage })))
const WorkflowsSettingsPage = lazy(() => import('../features/settings/WorkflowsSettingsPage').then((module) => ({ default: module.WorkflowsSettingsPage })))
const EscalationRulesSettingsPage = lazy(() => import('../features/settings/EscalationRulesSettingsPage').then((module) => ({ default: module.EscalationRulesSettingsPage })))
const WOTemplatesSettingsPage = lazy(() => import('../features/settings/WOTemplatesSettingsPage').then((module) => ({ default: module.WOTemplatesSettingsPage })))
const MyApprovalsPage = lazy(() => import('../features/approvals/MyApprovalsPage').then((module) => ({ default: module.MyApprovalsPage })))
const WhatsAppInboxPage = lazy(() => import('../features/whatsapp/WhatsAppInboxPage').then((module) => ({ default: module.WhatsAppInboxPage })))
const KnowledgeBasePage = lazy(() => import('../features/knowledge/KnowledgeBasePage').then((module) => ({ default: module.KnowledgeBasePage })))
const LiveMapPage = lazy(() => import('../features/dispatch/LiveMapPage').then((module) => ({ default: module.LiveMapPage })))
const DailyRouteView = lazy(() => import('../features/dispatch/DailyRouteView').then((module) => ({ default: module.DailyRouteView })))
const SchedulingBoardPage = lazy(() => import('../features/dispatch/SchedulingBoardPage').then((module) => ({ default: module.SchedulingBoardPage })))
const PlatformApp = lazy(() => import('../platform/PlatformApp').then((module) => ({ default: module.PlatformApp })))
const PortalApp = lazy(() => import('../portal/PortalApp').then((module) => ({ default: module.PortalApp })))

const tenantRoles: AppRole[] = ['admin', 'manager', 'dispatcher', 'technician', 'finance', 'viewer']
const platformRoles: AppRole[] = ['super_admin', 'platform_sales']

const MaintenanceCalendarPage = MaintenanceCalendarPageReal
const MaintenanceSchedulesPage = MaintenanceSchedulesPageReal
const MaintenanceScheduleDetailPage = MaintenanceScheduleDetailPageReal
const MaintenanceCompliancePage = MaintenanceCompliancePageReal
const MaintenanceEnginePage = MaintenanceEnginePageReal

const InventoryPartsPage = PartsPage
const InventoryPartDetailPage = PartDetailPage
const InventoryStockPage = StockPage
const InventoryLocationsPage = LocationsPage
const InventoryLocationDetailPage = LocationDetailPage
const InventoryMovementsPage = MovementsPage
const InventoryPurchaseOrdersPage = PurchaseOrdersPage
const InventoryPurchaseOrderDetailPage = PurchaseOrderDetailPage
const InventorySuppliersPage = SuppliersPage
const InventorySupplierDetailPage = SupplierDetailPage
const InventoryRestockRequestsPage = RestockRequestsPage
const InventoryPartsRequestsPage = PartsRequestsPage
const InventoryAlertsPage = InventoryAlertsPageReal
const InventoryReportsPage = InventoryReportsPageReal

const TeamUsersPage = TeamUsersPageReal
const TeamUserDetailPage = TeamUserDetailPageReal
const TeamTechniciansPage = TeamTechniciansPageReal
const TeamTechnicianDetailPage = TechnicianDetailPage
const TeamRolesPage = TeamRolesPageReal
const TeamSkillsPage = TeamSkillsPageReal
const TeamGroupsPage = TeamGroupsPageReal
const TimeAttendanceLivePage = LiveAttendancePage
const TimeAttendanceTimesheetsPage = TimesheetsPage
const TimeAttendanceTimesheetDetailPage = TimesheetDetailPage
const TimeAttendanceLeavePage = LeavePage

const ContractsPage = ContractsPageReal
const ContractDetailPage = ContractDetailPageReal

const FinanceQuotesPage = QuotesPage
const FinanceQuoteDetailPage = QuoteDetailPage
const FinanceInvoicesPage = InvoicesPage
const FinanceInvoiceDetailPage = InvoiceDetailPage
const FinanceInvoiceApprovalsPage = InvoiceApprovalsPage
const FinanceReceiptsPage = ReceiptsPage
const FinanceExpensesPage = ExpensesPage
const FinanceExpenseDetailPage = ExpenseDetailPage
const FinancePayablesPage = PayablesPage
const FinancePayableDetailPage = PayableDetailPage
const FinanceRateCardsPage = FinanceRateCardsPageReal
const FinancePreferentialRatesPage = FinancePreferentialRatesPageReal
const FinanceExchangeRatesPage = FinanceExchangeRatesPageReal
const FinanceCreditControlPage = CreditControlPage
const FinanceAgingPage = FinanceAgingPageReal
const FinanceReportsPage = FinanceReportsPageReal

const ReportsPage = ReportsPageReal

const SettingsCompanyPage = SettingsCompanyPageReal
const SettingsBrandingPage = SettingsBrandingPageReal
const SettingsTemplatesPage = SettingsTemplatesPageReal
const SettingsEmailTemplatesPage = SettingsEmailTemplatesPageReal
const SettingsNumberingPage = SettingsNumberingPageReal
const SettingsPmTemplatesPage = SettingsPmTemplatesPageReal
const SettingsInventoryPage = SettingsInventoryPageReal
const SettingsTimeAttendancePage = SettingsTimeAttendancePageReal
const SettingsEmailIntakePage = SettingsEmailIntakePageReal
const SettingsEmailDeliveryPage = SettingsEmailDeliveryPageReal
const SettingsMonitoringIntakePage = SettingsMonitoringIntakePageReal
const SettingsWhatsAppPage = SettingsWhatsAppPageReal
const SettingsPaymentsPage = SettingsPaymentsPageReal
const SettingsCreditControlPage = SettingsCreditControlPageReal
const SettingsPortalPage = SettingsPortalPageReal
const SettingsNotificationsPage = SettingsNotificationsPageReal
const SettingsIntegrationsPage = SettingsIntegrationsPageReal
const SettingsSecurityPage = SettingsSecurityPageReal
const SettingsExecutiveDashboardPage = SettingsExecutiveDashboardPageReal
const SettingsAccountPage = SettingsAccountPageReal
const SettingsAuditLogsPage = SettingsAuditLogsPageReal
const SettingsOverviewPage = SettingsOverviewPageReal
const OnboardingPage = OnboardingPageReal

function TenantShellRoute() {
  return (
    <AppShell>
      {/*
        The suspense boundary sits inside the shell, not above it. Every page is a `lazy()` chunk
        now, and a boundary above `AppShell` would tear down the sidebar and topbar on each
        navigation and rebuild them when the chunk arrived.
      */}
      <Suspense fallback={<Spinner label="Loading page" />}>
        <Outlet />
      </Suspense>
    </AppShell>
  )
}

function DashboardRoute() {
  const { user } = useAuth()
  if (user?.role === 'super_admin' || user?.role === 'platform_sales') {
    return <Navigate to="/platform/overview" replace />
  }

  return (
    <AppShell>
      <Suspense fallback={<Spinner label="Loading page" />}>
        <DashboardPage />
      </Suspense>
    </AppShell>
  )
}

function HomeRedirect() {
  const { isReady, isAuthenticated, user } = useAuth()
  if (!isReady) {
    return <Spinner label="Opening ECOSYS" />
  }
  if (!isAuthenticated || !user) {
    return <Navigate to="/login" replace />
  }
  if (user.role === 'super_admin' || user.role === 'platform_sales') {
    return <Navigate to="/platform/overview" replace />
  }
  return <Navigate to="/dashboard" replace />
}

export function AppRouter() {
  const tenantRouteComponents = useMemo(
    () => [
      ['/work-orders', WorkOrdersPage],
      ['/work-orders/:id', WorkOrderDetailPage],
      ['/dashboard/executive', ExecutiveDashboardPage],
      ['/maintenance/calendar', MaintenanceCalendarPage],
      ['/maintenance/schedules', MaintenanceSchedulesPage],
      ['/maintenance/schedules/:id', MaintenanceScheduleDetailPage],
      ['/maintenance/compliance', MaintenanceCompliancePage],
      ['/maintenance/engine', MaintenanceEnginePage],
      ['/clients', ClientsPage],
      ['/clients/:id', ClientDetailPage],
      ['/sites', SitesPage],
      ['/sites/:id', SiteDetailPage],
      ['/sites/:id/assets/new', AssetDetailPage],
      ['/assets', AssetsPage],
      ['/assets/:id', AssetDetailPage],
      ['/inventory', InventoryStockPage],
      ['/inventory/parts', InventoryPartsPage],
      ['/inventory/parts/:id', InventoryPartDetailPage],
      ['/inventory/stock', InventoryStockPage],
      ['/inventory/locations', InventoryLocationsPage],
      ['/inventory/locations/:id', InventoryLocationDetailPage],
      ['/inventory/movements', InventoryMovementsPage],
      ['/inventory/vans', VansPage],
      ['/inventory/vans/:id', VanDetailPage],
      ['/inventory/purchase-orders', InventoryPurchaseOrdersPage],
      ['/inventory/purchase-orders/:id', InventoryPurchaseOrderDetailPage],
      ['/inventory/suppliers', InventorySuppliersPage],
      ['/inventory/suppliers/:id', InventorySupplierDetailPage],
      ['/inventory/restock-requests', InventoryRestockRequestsPage],
      ['/inventory/parts-requests', InventoryPartsRequestsPage],
      ['/inventory/issues', MaterialIssuesPage],
      ['/inventory/alerts', InventoryAlertsPage],
      ['/inventory/reports', InventoryReportsPage],
      ['/team', TeamUsersPage],
      ['/team/users', TeamUsersPage],
      ['/team/users/:id', TeamUserDetailPage],
      ['/team/technicians', TeamTechniciansPage],
      ['/team/technicians/:id', TeamTechnicianDetailPage],
      ['/team/roles', TeamRolesPage],
      ['/team/capabilities', TeamRolesPage],
      ['/team/permission-groups', () => <AccessSectionPage section="groups" />],
      ['/team/access-scopes', () => <AccessSectionPage section="scopes" />],
      ['/team/approval-limits', () => <AccessSectionPage section="limits" />],
      ['/team/audit-history', () => <AccessSectionPage section="audit" />],
      ['/team/skills', TeamSkillsPage],
      ['/team/groups', TeamGroupsPage],
      ['/team/time-attendance', TimeAttendanceLivePage],
      ['/team/time-attendance/live', TimeAttendanceLivePage],
      ['/team/time-attendance/timesheets', TimeAttendanceTimesheetsPage],
      ['/team/time-attendance/timesheets/:id', TimeAttendanceTimesheetDetailPage],
      ['/team/time-attendance/leave', TimeAttendanceLeavePage],
      ['/team/time-attendance/reports', TimeAttendanceReportsPage],
      ['/me/time', MyTimePage],
      ['/knowledge', KnowledgeBasePage],
      ['/contracts', ContractsPage],
      ['/contracts/:id', ContractDetailPage],
      ['/finance', FinanceOverviewPage],
      ['/finance/documents', DocumentsPage],
      ['/finance/quotes', FinanceQuotesPage],
      ['/finance/quotes/:id', FinanceQuoteDetailPage],
      ['/finance/invoices', FinanceInvoicesPage],
      ['/finance/invoices/:id', FinanceInvoiceDetailPage],
      ['/finance/invoice-approvals', FinanceInvoiceApprovalsPage],
      ['/finance/receipts', FinanceReceiptsPage],
      ['/finance/credit-notes', CreditNotesPage],
      ['/finance/credit-notes/:id', CreditNoteDetailPage],
      ['/finance/expenses', FinanceExpensesPage],
      ['/finance/expenses/:id', FinanceExpenseDetailPage],
      ['/finance/payables', FinancePayablesPage],
      ['/finance/payables/:id', FinancePayableDetailPage],
      ['/finance/rate-cards', FinanceRateCardsPage],
      ['/finance/preferential-rates', FinancePreferentialRatesPage],
      ['/finance/exchange-rates', FinanceExchangeRatesPage],
      ['/finance/credit-control', FinanceCreditControlPage],
      ['/finance/aging', FinanceAgingPage],
      ['/finance/reports', FinanceReportsPage],
      ['/reports', ReportsPage],
      ['/whatsapp', WhatsAppInboxPage],
      ['/whatsapp/inbox', WhatsAppInboxPage],
      ['/settings', SettingsOverviewPage],
      ['/settings/company', SettingsCompanyPage],
      ['/settings/branding', SettingsBrandingPage],
      ['/settings/templates', SettingsTemplatesPage],
      ['/settings/email-templates', SettingsEmailTemplatesPage],
      ['/settings/numbering', SettingsNumberingPage],
      ['/settings/pm-templates', SettingsPmTemplatesPage],
      ['/settings/inventory', SettingsInventoryPage],
      ['/settings/time-attendance', SettingsTimeAttendancePage],
      ['/settings/email-delivery', SettingsEmailDeliveryPage],
      ['/settings/email-intake', SettingsEmailIntakePage],
      ['/settings/monitoring', SettingsMonitoringIntakePage],
      ['/settings/whatsapp', SettingsWhatsAppPage],
      ['/settings/payments', SettingsPaymentsPage],
      ['/settings/credit-control', SettingsCreditControlPage],
      ['/settings/portal', SettingsPortalPage],
      ['/settings/notifications', SettingsNotificationsPage],
      ['/settings/integrations', SettingsIntegrationsPage],
      ['/settings/security', SettingsSecurityPage],
      ['/settings/executive-dashboard', SettingsExecutiveDashboardPage],
      ['/settings/account', SettingsAccountPage],
      ['/settings/audit-logs', SettingsAuditLogsPage],
      ['/settings/approval-matrix', ApprovalMatrixSettingsPage],
      ['/settings/notification-templates', NotificationTemplatesPage],
      ['/settings/sla-policies', SlaPoliciesPage],
      ['/settings/workflows', WorkflowsSettingsPage],
      ['/settings/escalation-rules', EscalationRulesSettingsPage],
      ['/settings/wo-templates', WOTemplatesSettingsPage],
      ['/approvals', MyApprovalsPage],
      ['/onboarding', OnboardingPage],
      ['/dispatch/map', LiveMapPage],
      ['/dispatch/route/:technicianId', DailyRouteView],
      ['/dispatch/board', SchedulingBoardPage],
    ] as const,
    [],
  )

  return (
    <Suspense fallback={<Spinner label="Loading page" />}>
      <Routes>
        <Route path="/" element={<HomeRedirect />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="/impersonate-entry" element={<ImpersonationEntryPage />} />
        <Route path="/request-demo" element={<RequestDemoPage />} />
        <Route path="/demo-confirmation" element={<DemoConfirmationPage />} />
        <Route path="/csat/:token" element={<CsatSurveyPage />} />
        <Route path="/track/:token" element={<TrackJobPage />} />
        <Route path="/403" element={<ForbiddenPage />} />
        <Route path="/404" element={<NotFoundPage />} />

        <Route path="/portal/*" element={<PortalApp />} />

        <Route path="/platform/*" element={<ProtectedRoute allowedRoles={platformRoles}><PlatformApp /></ProtectedRoute>} />
        <Route path="/command-centre/*" element={<Navigate to="/platform/overview" replace />} />
        <Route path="/command-centre" element={<Navigate to="/platform/overview" replace />} />
        <Route path="/platform/dashboard" element={<Navigate to="/platform/overview" replace />} />

        <Route path="/dashboard" element={<ProtectedRoute><DashboardRoute /></ProtectedRoute>} />

        <Route element={<ProtectedRoute allowedRoles={tenantRoles}><TenantShellRoute /></ProtectedRoute>}>
          {tenantRouteComponents.map(([path, Component]) => (
            <Route key={path} path={path} element={<Component />} />
          ))}
        </Route>

        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  )
}
