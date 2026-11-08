import { lazy, Suspense, useMemo } from 'react'
import { EMAIL_INTAKE_ENABLED, WHATSAPP_ENABLED } from '../shared/config/features'
import {
  Navigate,
  Outlet,
  Route,
  Routes,
  useLocation,
  useParams,
} from 'react-router-dom'
import { ProtectedRoute } from '../shared/components/ProtectedRoute'
import { Spinner } from '../shared/components/Spinner'
import { useAuth } from '../shared/hooks/useAuth'
import { isTechnicianWorkspaceUser, techPathFor } from '../shared/utils/technicianWorkspace'
import { TechLayout } from '../features/tech/TechLayout'
import { TechJobsPage } from '../features/tech/TechJobsPage'
import { TechJobPage } from '../features/tech/TechJobPage'
import { TechAccountPage } from '../features/tech/TechAccountPage'
import { NotificationsPage, TechNotificationsPage } from '../features/notifications/NotificationsPage'
import { MyNotificationSettingsPage } from '../features/notifications/NotificationPreferences'
import { MyTimePage } from '../features/team/MyTimePage'
import { AppShell } from '../shared/layout/AppShell'
import { createModulePage, type ModulePageConfig } from '../shared/pages/ModulePage'
import type { AppRole } from '../shared/types/auth'
import { DashboardPage } from '../features/dashboard/DashboardPage'
import { ExecutiveDashboardPage } from '../features/dashboard/ExecutiveDashboardPage'
import { LoginPage } from '../features/auth/LoginPage'
import { ForgotPasswordPage } from '../features/auth/ForgotPasswordPage'
import { ResetPasswordPage } from '../features/auth/ResetPasswordPage'
import { ChangePasswordPage } from '../features/auth/ChangePasswordPage'
import { ImpersonationEntryPage } from '../features/auth/ImpersonationEntryPage'
import { DemoSignedOutPage } from '../features/auth/DemoSignedOutPage'
import { RequestDemoPage } from '../features/public/RequestDemoPage'
import { SignupPage } from '../features/public/SignupPage'
import { SignupVerifyPage } from '../features/public/SignupVerifyPage'
import { DemoConfirmationPage } from '../features/public/DemoConfirmationPage'
import { EnquiryFormPage } from '../features/public/EnquiryFormPage'
import { TrackJobPage } from '../features/portal/TrackJobPage'
import { ForbiddenPage } from '../features/public/ForbiddenPage'
import { NotFoundPage } from '../features/public/NotFoundPage'
import { WorkOrdersPage } from '../features/work-orders/WorkOrdersPage'
import { WorkOrderDetailPage } from '../features/work-orders/WorkOrderDetailPage'
import { CalendarPage as MaintenanceCalendarPageReal } from '../features/maintenance/CalendarPage'
import { SchedulesPage as MaintenanceSchedulesPageReal } from '../features/maintenance/SchedulesPage'
import { ScheduleDetailPage as MaintenanceScheduleDetailPageReal } from '../features/maintenance/ScheduleDetailPage'
import { CompliancePage as MaintenanceCompliancePageReal } from '../features/maintenance/CompliancePage'
import { EnginePage as MaintenanceEnginePageReal } from '../features/maintenance/EnginePage'
import { PartsPage } from '../features/inventory/PartsPage'
import { PartDetailPage } from '../features/inventory/PartDetailPage'
import { StockPage } from '../features/inventory/StockPage'
import { LocationsPage } from '../features/inventory/LocationsPage'
import { LocationDetailPage } from '../features/inventory/LocationDetailPage'
import { MovementsPage } from '../features/inventory/MovementsPage'
import { PurchaseOrdersPage, PurchaseOrderDetailPage } from '../features/inventory/PurchaseOrdersPage'
import { SuppliersPage, SupplierDetailPage } from '../features/inventory/SuppliersPage'
import { RequestsPage as InventoryRequestsPage } from '../features/inventory/RequestsPage'
import { OverviewPage as InventoryOverviewPage } from '../features/inventory/OverviewPage'
import { InventoryShell } from '../features/inventory/components/InventoryShell'
import { FaultyPartsPage } from '../features/inventory/FaultyPartsPage'
import { DirectPurchasesPage } from '../features/inventory/DirectPurchasesPage'
import { PartsReturnsPage } from '../features/inventory/PartsReturnsPage'
import { StockCountDetailPage, StockCountsPage } from '../features/inventory/StockCountsPage'
import { ToolDetailPage, ToolsPage } from '../features/inventory/ToolsPage'
import { AlertsPage as InventoryAlertsPageReal } from '../features/inventory/AlertsPage'
import { ReportsPage as InventoryReportsPageReal } from '../features/inventory/ReportsPage'
import { UsersPage as TeamUsersPageReal } from '../features/team/UsersPage'
import { UserDetailPage as TeamUserDetailPageReal } from '../features/team/UserDetailPage'
import { TechniciansPage as TeamTechniciansPageReal } from '../features/team/TechniciansPage'
import { TechnicianDetailPage } from '../features/team/TechnicianDetailPage'
import { RolesPage as TeamRolesPageReal } from '../features/team/RolesPage'
import { AccessSectionPage } from '../features/team/AccessSectionPage'
import { SkillsPage as TeamSkillsPageReal } from '../features/team/SkillsPage'
import { GroupsPage as TeamGroupsPageReal } from '../features/team/GroupsPage'
import { LiveAttendancePage, TimesheetsPage, TimesheetDetailPage, LeavePage, TimeAttendanceReportsPage } from '../features/team/TimeAttendancePages'
import { ContractsPage as ContractsPageReal } from '../features/contracts/ContractsPage'
import { ContractDetailPage as ContractDetailPageReal } from '../features/contracts/ContractDetailPage'
import { CalendarPage as MyCalendarPage } from '../features/activities/CalendarPage'
import { CalendarFeedPage } from '../features/activities/CalendarFeedPage'
import { ActivityDetailPage } from '../features/activities/ActivityDetailPage'
import { ActionItemsPage } from '../features/activities/ActionItemsPage'
import { InvoicesPage } from '../features/finance/InvoicesPage'
import { DocumentsPage } from '../features/finance/DocumentsPage'
import { InvoiceDetailPage } from '../features/finance/InvoiceDetailPage'
import { FinanceOverviewPage } from '../features/finance/FinanceOverviewPage'
import { QuotesPage } from '../features/finance/QuotesPage'
import { QuoteDetailPage } from '../features/finance/QuoteDetailPage'
import { AgingPage as FinanceAgingPageReal } from '../features/finance/AgingPage'
import { CreditControlPage } from '../features/finance/CreditControlPage'
import { ReceiptsPage } from '../features/finance/ReceiptsPage'
import { CreditNotesPage } from '../features/finance/CreditNotesPage'
import { CreditNoteDetailPage } from '../features/finance/CreditNoteDetailPage'
import { ExpensesPage, ExpenseDetailPage } from '../features/finance/ExpensesPage'
import { PayablesPage, PayableDetailPage } from '../features/finance/PayablesPage'
import { InvoiceApprovalsPage } from '../features/finance/InvoiceApprovalsPage'
import { RateCardsPage as FinanceRateCardsPageReal } from '../features/finance/RateCardsPage'
import { PreferentialRatesPage as FinancePreferentialRatesPageReal } from '../features/finance/PreferentialRatesPage'
import { ExchangeRatesPage as FinanceExchangeRatesPageReal } from '../features/finance/ExchangeRatesPage'
import { FinanceReportsPage as FinanceReportsPageReal } from '../features/finance/FinanceReportsPage'
import { ReportsPage as ReportsPageReal } from '../features/reports/ReportsPage'
import { OnboardingPage as OnboardingPageReal } from '../features/onboarding/OnboardingPage'
import { ClientsPage } from '../features/clients/ClientsPage'
import { SitesPage } from '../features/clients/SitesPage'
// Factory pages load on first use so they don't grow the main bundle.
const FactoryAssetsPage = lazy(async () => ({ default: (await import('../features/factory/FactoryAssetsPage')).FactoryAssetsPage }))
const FactoryAssetDetailPage = lazy(async () => ({ default: (await import('../features/factory/FactoryAssetDetailPage')).FactoryAssetDetailPage }))
const ScanPage = lazy(async () => ({ default: (await import('../features/factory/ScanPage')).ScanPage }))
const FactoryJobsPage = lazy(async () => ({ default: (await import('../features/factory/jobs/FactoryJobsPage')).FactoryJobsPage }))
const FactoryJobDetailPage = lazy(async () => ({ default: (await import('../features/factory/jobs/FactoryJobDetailPage')).FactoryJobDetailPage }))
const RunningChecksPage = lazy(async () => ({ default: (await import('../features/factory/checks/RunningChecksPage')).RunningChecksPage }))
const CheckRunPage = lazy(async () => ({ default: (await import('../features/factory/checks/CheckRunPage')).CheckRunPage }))
const CheckSchedulesSettingsPage = lazy(async () => ({ default: (await import('../features/factory/checks/CheckSchedulesSettingsPage')).CheckSchedulesSettingsPage }))
const FactoryDashboardPage = lazy(async () => ({ default: (await import('../features/factory/FactoryDashboardPage')).FactoryDashboardPage }))
const PmPlansPage = lazy(async () => ({ default: (await import('../features/factory/planning/PmPlansPage')).PmPlansPage }))
const WindowsPage = lazy(async () => ({ default: (await import('../features/factory/planning/WindowsPage')).WindowsPage }))
const WindowDetailPage = lazy(async () => ({ default: (await import('../features/factory/planning/WindowDetailPage')).WindowDetailPage }))
const WindowPackPage = lazy(async () => ({ default: (await import('../features/factory/planning/WindowDetailPage')).WindowPackPage }))
const OverhaulPage = lazy(async () => ({ default: (await import('../features/factory/planning/OverhaulPage')).OverhaulPage }))
const OverhaulPlanPage = lazy(async () => ({ default: (await import('../features/factory/planning/OverhaulPage')).OverhaulPlanPage }))
const StatutoryPage = lazy(async () => ({ default: (await import('../features/factory/planning/StatutoryPage')).StatutoryPage }))
const PeoplePage = lazy(async () => ({ default: (await import('../features/factory/people/PeoplePage')).PeoplePage }))
const SafetyPage = lazy(async () => ({ default: (await import('../features/factory/people/SafetyPage')).SafetyPage }))
const StoresPage = lazy(async () => ({ default: (await import('../features/factory/stores/StoresPage')).StoresPage }))
const PerformancePage = lazy(async () => ({ default: (await import('../features/factory/performance/PerformancePage')).PerformancePage }))
const CostsPage = lazy(async () => ({ default: (await import('../features/factory/performance/CostsPage')).CostsPage }))
const CompliancePage = lazy(async () => ({ default: (await import('../features/factory/performance/CompliancePage')).CompliancePage }))
const ContractorsPage = lazy(async () => ({ default: (await import('../features/factory/stores/ContractorsPage')).ContractorsPage }))
const OperatorReportPage = lazy(async () => ({ default: (await import('../features/public/OperatorReportPage')).OperatorReportPage }))
const OrganisationSettingsPage = lazy(async () => ({ default: (await import('../features/factory/OrganisationSettingsPage')).OrganisationSettingsPage }))
import { ClientDetailPage } from '../features/clients/ClientDetailPage'
import { SiteDetailPage } from '../features/clients/SiteDetailPage'
import { CompanySettingsPage as SettingsCompanyPageReal } from '../features/settings/CompanySettingsPage'
import { BrandingSettingsPage as SettingsBrandingPageReal } from '../features/settings/BrandingSettingsPage'
import { SecuritySettingsPage as SettingsSecurityPageReal } from '../features/settings/SecuritySettingsPage'
import { NotificationsSettingsPage as SettingsNotificationsPageReal } from '../features/settings/NotificationsSettingsPage'
import { NumberingSettingsPage as SettingsNumberingPageReal } from '../features/settings/NumberingSettingsPage'
import { PmTemplatesSettingsPage as SettingsPmTemplatesPageReal } from '../features/settings/PmTemplatesSettingsPage'
import { AssetCategoriesSettingsPage } from '../features/settings/AssetCategoriesSettingsPage'
import { HelpPage } from '../features/help/HelpPage'
import { TechHelpPage } from '../features/help/TechHelpPage'
import { EmailIdentitySettingsPage } from '../features/settings/EmailIdentitySettingsPage'
import { EmailDeliveryLogsPage } from '../features/settings/EmailDeliveryLogsPage'
import { EmailTemplatesSettingsPage as SettingsEmailTemplatesPageReal } from '../features/settings/EmailTemplatesSettingsPage'
import { AuditLogsSettingsPage as SettingsAuditLogsPageReal } from '../features/settings/AuditLogsSettingsPage'
import { AccountSettingsPage as SettingsAccountPageReal } from '../features/settings/AccountSettingsPage'
import { DocumentTemplatesSettingsPage as SettingsTemplatesPageReal } from '../features/settings/DocumentTemplatesSettingsPage'
import { EmailIntakeSettingsPage as SettingsEmailIntakePageReal } from '../features/settings/EmailIntakeSettingsPage'
import { MonitoringIntakeSettingsPage as SettingsMonitoringIntakePageReal } from '../features/settings/MonitoringIntakeSettingsPage'
import { CreditControlSettingsPage as SettingsCreditControlPageReal } from '../features/settings/CreditControlSettingsPage'
import { SettingsOverviewPage as SettingsOverviewPageReal } from '../features/settings/SettingsOverviewPage'
import { TimeAttendanceSettingsPage as SettingsTimeAttendancePageReal } from '../features/settings/TimeAttendanceSettingsPage'
import { InventorySettingsPage as SettingsInventoryPageReal } from '../features/settings/InventorySettingsPage'
import { WhatsAppSettingsPage as SettingsWhatsAppPageReal } from '../features/settings/WhatsAppSettingsPage'
import { WhatsAppRemindersSettingsPage } from '../features/settings/WhatsAppRemindersSettingsPage'
import { PaymentsSettingsPage as SettingsPaymentsPageReal } from '../features/settings/PaymentsSettingsPage'
import { IntegrationsSettingsPage as SettingsIntegrationsPageReal } from '../features/settings/IntegrationsSettingsPage'
import { ExecutiveDashboardSettingsPage as SettingsExecutiveDashboardPageReal } from '../features/settings/ExecutiveDashboardSettingsPage'
import { SettingsDashboardsPage } from '../features/settings/SettingsDashboardsPage'
import { ApprovalMatrixSettingsPage } from '../features/settings/ApprovalMatrixSettingsPage'
import { NotificationTemplatesPage } from '../features/settings/NotificationTemplatesPage'
import { SlaPoliciesPage } from '../features/settings/SlaPoliciesPage'
import { WorkflowsSettingsPage } from '../features/settings/WorkflowsSettingsPage'
import { OperationalDefaultsSettingsPage } from '../features/settings/OperationalDefaultsSettingsPage'
import { FinanceDefaultsSettingsPage } from '../features/settings/FinanceDefaultsSettingsPage'
import { EscalationRulesSettingsPage } from '../features/settings/EscalationRulesSettingsPage'
import { WOTemplatesSettingsPage } from '../features/settings/WOTemplatesSettingsPage'
import { MyApprovalsPage } from '../features/approvals/MyApprovalsPage'
import { WhatsAppInboxPage } from '../features/whatsapp/WhatsAppInboxPage'
import { PlatformApp } from '../platform/PlatformApp'

const tenantRoles: AppRole[] = ['admin', 'manager', 'dispatcher', 'technician', 'materials_controller', 'finance', 'viewer']
const platformRoles: AppRole[] = ['super_admin', 'platform_sales']

const lazyModulePage = (config: ModulePageConfig) =>
  lazy(async () => ({ default: createModulePage(config) }))

const MaintenanceCalendarPage = MaintenanceCalendarPageReal
const MaintenanceSchedulesPage = MaintenanceSchedulesPageReal
const MaintenanceScheduleDetailPage = MaintenanceScheduleDetailPageReal
const MaintenanceCompliancePage = MaintenanceCompliancePageReal
const MaintenanceEnginePage = MaintenanceEnginePageReal

const ClientWizardPage = lazyModulePage({ title: 'Client Setup Wizard', subtitle: 'Sites, assets, and contract setup flow.' })

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
const SettingsMonitoringIntakePage = SettingsMonitoringIntakePageReal
const SettingsWhatsAppPage = SettingsWhatsAppPageReal
const SettingsPaymentsPage = SettingsPaymentsPageReal
const SettingsCreditControlPage = SettingsCreditControlPageReal
const SettingsNotificationsPage = SettingsNotificationsPageReal
const SettingsIntegrationsPage = SettingsIntegrationsPageReal
const SettingsSecurityPage = SettingsSecurityPageReal
const SettingsExecutiveDashboardPage = SettingsExecutiveDashboardPageReal
const SettingsAccountPage = SettingsAccountPageReal
const SettingsAuditLogsPage = SettingsAuditLogsPageReal
const SettingsOverviewPage = SettingsOverviewPageReal
const OnboardingPage = OnboardingPageReal

/** Old link target: assets are added from the site page, so send it there rather than to a "not found" asset. */
function SiteAssetsNewRedirect() {
  const { id } = useParams()
  return <Navigate to={id ? `/sites/${id}` : '/sites'} replace />
}

function TenantShellRoute() {
  const { user } = useAuth()
  const location = useLocation()
  // Field technicians are refused the office screens by the API; send them to the /tech equivalent.
  if (isTechnicianWorkspaceUser(user)) {
    return <Navigate to={techPathFor(location.pathname)} replace />
  }
  return (
    <AppShell>
      {/* Inventory pages share a grouped side menu instead of each repeating a row of tabs. */}
      {location.pathname === '/inventory' || location.pathname.startsWith('/inventory/') ? (
        <InventoryShell>
          <Outlet />
        </InventoryShell>
      ) : (
        <Outlet />
      )}
    </AppShell>
  )
}

/**
 * Issue slips, job parts requests and restock requests became one queue. Old links (bookmarks,
 * notifications) land on it, keeping an issue-slip status filter as the matching stage.
 */
function InventoryRequestsRedirect() {
  const location = useLocation()
  const status = new URLSearchParams(location.search).get('status')
  const stage = status === 'pending_approval' || status === 'draft' ? 'Requested' : status === 'approved' ? 'Approved' : status === 'issued' ? 'Issued' : null
  return <Navigate to={stage ? `/inventory/requests?stage=${stage}` : '/inventory/requests'} replace />
}

function DashboardRoute() {
  const { user } = useAuth()
  if (user?.role === 'super_admin' || user?.role === 'platform_sales') {
    return <Navigate to="/platform/overview" replace />
  }
  if (isTechnicianWorkspaceUser(user)) {
    return <Navigate to="/tech" replace />
  }

  return (
    <AppShell>
      <DashboardPage />
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
  if (isTechnicianWorkspaceUser(user)) {
    return <Navigate to="/tech" replace />
  }
  return <Navigate to="/dashboard" replace />
}

export function AppRouter() {
  const tenantRouteComponents = useMemo(
    () => [
      ['/work-orders', WorkOrdersPage],
      ['/work-orders/:id', WorkOrderDetailPage],
      ['/dashboard/executive', ExecutiveDashboardPage],
      ['/maintenance/overview', FactoryDashboardPage],
      ['/maintenance/jobs', FactoryJobsPage],
      ['/maintenance/jobs/:id', FactoryJobDetailPage],
      ['/maintenance/checks', RunningChecksPage],
      ['/maintenance/checks/:id', CheckRunPage],
      ['/maintenance/pm', PmPlansPage],
      ['/maintenance/windows', WindowsPage],
      ['/maintenance/windows/:id', WindowDetailPage],
      ['/maintenance/windows/:id/pack', WindowPackPage],
      ['/maintenance/overhaul', OverhaulPage],
      ['/maintenance/overhaul/:id', OverhaulPlanPage],
      ['/maintenance/statutory', StatutoryPage],
      ['/maintenance/people', PeoplePage],
      ['/maintenance/safety', SafetyPage],
      ['/maintenance/stores', StoresPage],
      ['/maintenance/contractors', ContractorsPage],
      ['/maintenance/performance', PerformancePage],
      ['/maintenance/costs', CostsPage],
      ['/maintenance/compliance', CompliancePage],
      ['/maintenance/calendar', MaintenanceCalendarPage],
      ['/maintenance/schedules', MaintenanceSchedulesPage],
      ['/maintenance/schedules/new', MaintenanceSchedulesPage],
      ['/maintenance/schedules/:id', MaintenanceScheduleDetailPage],
      ['/maintenance/compliance', MaintenanceCompliancePage],
      ['/maintenance/engine', MaintenanceEnginePage],
      ['/clients', ClientsPage],
      ['/clients/:id', ClientDetailPage],
      ['/clients/:id/sites/new', ClientWizardPage],
      ['/sites', SitesPage],
      ['/sites/:id', SiteDetailPage],
      ['/sites/:id/assets/new', SiteAssetsNewRedirect],
      ['/assets', FactoryAssetsPage],
      ['/assets/:id', FactoryAssetDetailPage],
      ['/scan', ScanPage],
      ['/scan/:code', ScanPage],
      ['/inventory', InventoryOverviewPage],
      ['/inventory/parts', InventoryPartsPage],
      ['/inventory/parts/new', InventoryPartsPage],
      ['/inventory/parts/:id', InventoryPartDetailPage],
      ['/inventory/stock', InventoryStockPage],
      ['/inventory/locations', InventoryLocationsPage],
      ['/inventory/locations/:id', InventoryLocationDetailPage],
      ['/inventory/movements', InventoryMovementsPage],
      ['/inventory/purchase-orders', InventoryPurchaseOrdersPage],
      ['/inventory/purchase-orders/new', InventoryPurchaseOrdersPage],
      ['/inventory/purchase-orders/:id', InventoryPurchaseOrderDetailPage],
      ['/inventory/suppliers', InventorySuppliersPage],
      ['/inventory/suppliers/:id', InventorySupplierDetailPage],
      ['/inventory/requests', InventoryRequestsPage],
      ['/inventory/restock-requests', InventoryRequestsRedirect],
      ['/inventory/parts-requests', InventoryRequestsRedirect],
      ['/inventory/parts-returns', PartsReturnsPage],
      ['/inventory/faulty-parts', FaultyPartsPage],
      ['/inventory/direct-purchases', DirectPurchasesPage],
      ['/inventory/issues', InventoryRequestsRedirect],
      ['/inventory/alerts', InventoryAlertsPage],
      ['/inventory/stock-counts', StockCountsPage],
      ['/inventory/stock-counts/:id', StockCountDetailPage],
      ['/inventory/tools', ToolsPage],
      ['/inventory/tools/:id', ToolDetailPage],
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
      ['/help', HelpPage],
      ['/help/:guideId', HelpPage],
      ['/contracts', ContractsPage],
      ['/contracts/:id', ContractDetailPage],
      ['/calendar', MyCalendarPage],
      ['/calendar/feed', CalendarFeedPage],
      ['/activities/:id', ActivityDetailPage],
      ['/action-items', ActionItemsPage],
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
      ['/settings/email', EmailIdentitySettingsPage],
      // Merged into the Email page; old links and bookmarks land there.
      ['/settings/email-delivery', () => <Navigate to="/settings/email" replace />],
      ['/settings/email/delivery-logs', EmailDeliveryLogsPage],
      ['/settings/email-templates', SettingsEmailTemplatesPage],
      ['/settings/email-templates/:id', SettingsEmailTemplatesPage],
      ['/settings/numbering', SettingsNumberingPage],
      ['/settings/pm-templates', SettingsPmTemplatesPage],
      ['/settings/running-checks', CheckSchedulesSettingsPage],
      ['/settings/asset-categories', AssetCategoriesSettingsPage],
      ['/settings/organisation', OrganisationSettingsPage],
      ['/settings/pm-templates/:id', SettingsPmTemplatesPage],
      ['/settings/inventory', SettingsInventoryPage],
      ['/settings/time-attendance', SettingsTimeAttendancePage],
      ['/settings/email-intake', SettingsEmailIntakePage],
      ['/settings/monitoring', SettingsMonitoringIntakePage],
      ['/settings/whatsapp', SettingsWhatsAppPage],
      ['/settings/whatsapp-reminders', WhatsAppRemindersSettingsPage],
      ['/settings/payments', SettingsPaymentsPage],
      ['/settings/credit-control', SettingsCreditControlPage],
      ['/settings/notifications', MyNotificationSettingsPage],
      ['/settings/workspace-notifications', SettingsNotificationsPage],
      ['/settings/integrations', SettingsIntegrationsPage],
      ['/settings/security', SettingsSecurityPage],
      ['/settings/executive-dashboard', SettingsExecutiveDashboardPage],
      ['/settings/dashboards', SettingsDashboardsPage],
      ['/settings/account', SettingsAccountPage],
      ['/settings/audit-logs', SettingsAuditLogsPage],
      ['/settings/approval-matrix', ApprovalMatrixSettingsPage],
      ['/settings/notification-templates', NotificationTemplatesPage],
      ['/settings/sla-policies', SlaPoliciesPage],
      ['/settings/workflows', WorkflowsSettingsPage],
      ['/settings/operational-defaults', OperationalDefaultsSettingsPage],
      ['/settings/finance-defaults', FinanceDefaultsSettingsPage],
      ['/settings/escalation-rules', EscalationRulesSettingsPage],
      ['/settings/wo-templates', WOTemplatesSettingsPage],
      ['/approvals', MyApprovalsPage],
      ['/my-time', MyTimePage],
      ['/me', TechAccountPage],
      ['/notifications', NotificationsPage],
      ['/onboarding', OnboardingPage],
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
        <Route path="/change-password" element={<ChangePasswordPage />} />
        <Route path="/impersonate-entry" element={<ImpersonationEntryPage />} />
        <Route path="/demo/signed-out" element={<DemoSignedOutPage />} />
        <Route path="/request-demo" element={<RequestDemoPage />} />
        <Route path="/signup" element={<SignupPage />} />
        <Route path="/signup/verify" element={<SignupVerifyPage />} />
        <Route path="/demo-confirmation" element={<DemoConfirmationPage />} />
        <Route path="/enquiry/:key" element={<EnquiryFormPage />} />
        <Route path="/track/:token" element={<TrackJobPage />} />
        <Route path="/t/:tag" element={<OperatorReportPage />} />
        <Route path="/403" element={<ForbiddenPage />} />
        <Route path="/404" element={<NotFoundPage />} />


        <Route path="/platform/*" element={<ProtectedRoute allowedRoles={platformRoles}><PlatformApp /></ProtectedRoute>} />
        <Route path="/command-centre/*" element={<Navigate to="/platform/overview" replace />} />
        <Route path="/command-centre" element={<Navigate to="/platform/overview" replace />} />
        <Route path="/platform/dashboard" element={<Navigate to="/platform/overview" replace />} />

        <Route path="/dashboard" element={<ProtectedRoute><DashboardRoute /></ProtectedRoute>} />

        <Route path="/tech" element={<ProtectedRoute allowedRoles={tenantRoles}><TechLayout /></ProtectedRoute>}>
          <Route index element={<TechJobsPage />} />
          <Route path="jobs/:id" element={<TechJobPage />} />
          <Route path="jobs/:id/:section" element={<TechJobPage />} />
          <Route path="time" element={<MyTimePage />} />
          <Route path="scan" element={<ScanPage />} />
          <Route path="scan/:code" element={<ScanPage />} />
          <Route path="assets/:id" element={<FactoryAssetDetailPage />} />
          <Route path="maintenance/jobs" element={<FactoryJobsPage />} />
          <Route path="maintenance/jobs/:id" element={<FactoryJobDetailPage />} />
          <Route path="maintenance/checks" element={<RunningChecksPage />} />
          <Route path="maintenance/checks/:id" element={<CheckRunPage />} />
          <Route path="maintenance/windows" element={<WindowsPage />} />
          <Route path="maintenance/safety" element={<SafetyPage />} />
          <Route path="maintenance/windows/:id" element={<WindowDetailPage />} />
          <Route path="maintenance/windows/:id/pack" element={<WindowPackPage />} />
          <Route path="help" element={<TechHelpPage />} />
          <Route path="account" element={<TechAccountPage />} />
          <Route path="notifications" element={<TechNotificationsPage />} />
        </Route>

        <Route element={<ProtectedRoute allowedRoles={tenantRoles}><TenantShellRoute /></ProtectedRoute>}>
          {/* Features switched off in shared/config/features.ts have no routes. */}
          {tenantRouteComponents.filter(([path]) => (WHATSAPP_ENABLED || !(path.startsWith('/whatsapp') || path.startsWith('/settings/whatsapp')))
            && (EMAIL_INTAKE_ENABLED || path !== '/settings/email-intake')).map(([path, Component]) => (
            <Route key={path} path={path} element={<Component />} />
          ))}
        </Route>

        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  )
}
