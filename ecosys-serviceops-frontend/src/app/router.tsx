import { lazy, Suspense, useMemo } from 'react'
import { WHATSAPP_ENABLED } from '../shared/config/features'
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
import { TechGuidePage, TechGuidesPage } from '../features/tech/TechGuides'
import { TechAccountPage } from '../features/tech/TechAccountPage'
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
import { CsatSurveyPage } from '../features/public/CsatSurveyPage'
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
import { RestockRequestsPage } from '../features/inventory/RestockRequestsPage'
import { PartsRequestsPage } from '../features/inventory/PartsRequestsPage'
import { PartsReturnsPage } from '../features/inventory/PartsReturnsPage'
import { MaterialIssuesPage } from '../features/inventory/MaterialIssuesPage'
import { VansPage } from '../features/inventory/VansPage'
import { VanDetailPage } from '../features/inventory/VanDetailPage'
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
import { InvoicesPage } from '../features/finance/InvoicesPage'
import { DocumentsPage } from '../features/finance/DocumentsPage'
import { InvoiceDetailPage } from '../features/finance/InvoiceDetailPage'
import { FinanceOverviewPage } from '../features/finance/FinanceOverviewPage'
import { QuotesPage } from '../features/finance/QuotesPage'
import { PipelinePage } from '../features/crm/PipelinePage'
import { OpportunityDetailPage } from '../features/crm/OpportunityDetailPage'
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
import { AssetsPage } from '../features/clients/AssetsPage'
import { ClientDetailPage } from '../features/clients/ClientDetailPage'
import { SiteDetailPage } from '../features/clients/SiteDetailPage'
import { AssetDetailPage } from '../features/clients/AssetDetailPage'
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
import { PaymentsSettingsPage as SettingsPaymentsPageReal } from '../features/settings/PaymentsSettingsPage'
import { PortalSettingsPage as SettingsPortalPageReal } from '../features/settings/PortalSettingsPage'
import { IntegrationsSettingsPage as SettingsIntegrationsPageReal } from '../features/settings/IntegrationsSettingsPage'
import { ExecutiveDashboardSettingsPage as SettingsExecutiveDashboardPageReal } from '../features/settings/ExecutiveDashboardSettingsPage'
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
import { KnowledgeBasePage } from '../features/knowledge/KnowledgeBasePage'
import { LiveMapPage } from '../features/dispatch/LiveMapPage'
import { DailyRouteView } from '../features/dispatch/DailyRouteView'
import { SchedulingBoardPage } from '../features/dispatch/SchedulingBoardPage'
import { PlatformApp } from '../platform/PlatformApp'
import { PortalApp } from '../portal/PortalApp'

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
      <Outlet />
    </AppShell>
  )
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
      ['/maintenance/calendar', MaintenanceCalendarPage],
      ['/maintenance/schedules', MaintenanceSchedulesPage],
      ['/maintenance/schedules/new', MaintenanceSchedulesPage],
      ['/maintenance/schedules/:id', MaintenanceScheduleDetailPage],
      ['/maintenance/compliance', MaintenanceCompliancePage],
      ['/maintenance/engine', MaintenanceEnginePage],
      ['/crm', PipelinePage],
      ['/crm/opportunities/:id', OpportunityDetailPage],
      ['/clients', ClientsPage],
      ['/clients/:id', ClientDetailPage],
      ['/clients/:id/sites/new', ClientWizardPage],
      ['/sites', SitesPage],
      ['/sites/:id', SiteDetailPage],
      ['/sites/:id/assets/new', SiteAssetsNewRedirect],
      ['/assets', AssetsPage],
      ['/assets/:id', AssetDetailPage],
      ['/inventory', InventoryStockPage],
      ['/inventory/parts', InventoryPartsPage],
      ['/inventory/parts/new', InventoryPartsPage],
      ['/inventory/parts/:id', InventoryPartDetailPage],
      ['/inventory/stock', InventoryStockPage],
      ['/inventory/locations', InventoryLocationsPage],
      ['/inventory/locations/:id', InventoryLocationDetailPage],
      ['/inventory/movements', InventoryMovementsPage],
      ['/inventory/vans', VansPage],
      ['/inventory/vans/:id', VanDetailPage],
      ['/inventory/purchase-orders', InventoryPurchaseOrdersPage],
      ['/inventory/purchase-orders/new', InventoryPurchaseOrdersPage],
      ['/inventory/purchase-orders/:id', InventoryPurchaseOrderDetailPage],
      ['/inventory/suppliers', InventorySuppliersPage],
      ['/inventory/suppliers/:id', InventorySupplierDetailPage],
      ['/inventory/restock-requests', InventoryRestockRequestsPage],
      ['/inventory/parts-requests', InventoryPartsRequestsPage],
      ['/inventory/parts-returns', PartsReturnsPage],
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
      ['/knowledge', KnowledgeBasePage],
      ['/help', HelpPage],
      ['/help/:guideId', HelpPage],
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
      ['/settings/email', EmailIdentitySettingsPage],
      // Merged into the Email page; old links and bookmarks land there.
      ['/settings/email-delivery', () => <Navigate to="/settings/email" replace />],
      ['/settings/email/delivery-logs', EmailDeliveryLogsPage],
      ['/settings/email-templates', SettingsEmailTemplatesPage],
      ['/settings/email-templates/:id', SettingsEmailTemplatesPage],
      ['/settings/numbering', SettingsNumberingPage],
      ['/settings/pm-templates', SettingsPmTemplatesPage],
      ['/settings/asset-categories', AssetCategoriesSettingsPage],
      ['/settings/pm-templates/:id', SettingsPmTemplatesPage],
      ['/settings/inventory', SettingsInventoryPage],
      ['/settings/time-attendance', SettingsTimeAttendancePage],
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
      ['/settings/operational-defaults', OperationalDefaultsSettingsPage],
      ['/settings/finance-defaults', FinanceDefaultsSettingsPage],
      ['/settings/escalation-rules', EscalationRulesSettingsPage],
      ['/settings/wo-templates', WOTemplatesSettingsPage],
      ['/approvals', MyApprovalsPage],
      ['/my-time', MyTimePage],
      ['/me', TechAccountPage],
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
        <Route path="/change-password" element={<ChangePasswordPage />} />
        <Route path="/impersonate-entry" element={<ImpersonationEntryPage />} />
        <Route path="/demo/signed-out" element={<DemoSignedOutPage />} />
        <Route path="/request-demo" element={<RequestDemoPage />} />
        <Route path="/signup" element={<SignupPage />} />
        <Route path="/signup/verify" element={<SignupVerifyPage />} />
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

        <Route path="/tech" element={<ProtectedRoute allowedRoles={tenantRoles}><TechLayout /></ProtectedRoute>}>
          <Route index element={<TechJobsPage />} />
          <Route path="jobs/:id" element={<TechJobPage />} />
          <Route path="jobs/:id/:section" element={<TechJobPage />} />
          <Route path="time" element={<MyTimePage />} />
          <Route path="guides" element={<TechGuidesPage />} />
          <Route path="guides/:id" element={<TechGuidePage />} />
          <Route path="help" element={<TechHelpPage />} />
          <Route path="account" element={<TechAccountPage />} />
        </Route>

        <Route element={<ProtectedRoute allowedRoles={tenantRoles}><TenantShellRoute /></ProtectedRoute>}>
          {tenantRouteComponents.filter(([path]) => WHATSAPP_ENABLED || !path.includes('whatsapp')).map(([path, Component]) => (
            <Route key={path} path={path} element={<Component />} />
          ))}
        </Route>

        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  )
}
