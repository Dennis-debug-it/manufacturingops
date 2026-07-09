import { lazy, Suspense, useCallback, useEffect, useMemo } from 'react'
import { BrowserRouter, Link, Navigate, Outlet, Route, Routes, useLocation, useOutletContext, useParams } from 'react-router-dom'
import {
  Activity,
  BookOpen,
  Building2,
  ClipboardList,
  FileText,
  LayoutDashboard,
  Layers,
  Package,
  Radio,
  Settings2,
  ShieldCheck,
  Users,
  Wallet,
  Wrench,
} from 'lucide-react'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { RequireAuth } from './auth/RequireAuth'
import { ErrorBoundary } from './components/ErrorBoundary'
import { UnsupportedRolePage } from './components/auth/UnsupportedRolePage'
import { AppShell } from './components/layout/AppShell'
import type { ShellOutletContext } from './components/layout/AppShell'
import { LoadingState } from './components/ui/LoadingState'
import { ToastProvider } from './components/ui/ToastProvider'
import { ThemeProvider } from './context/ThemeContext'
import { GuestRoute } from './routes/GuestRoute'
import type { NotificationItem, SearchItem } from './types/app'
import { SettingsLayout } from './pages/settings/SettingsLayout'
import { settingsPageRoutes } from './pages/settings/routes'
import { SettingsHubPage } from './modules/settings/SettingsPage'
import { installInteractionDebugLogger } from './utils/appCleanup'
import { isPlatformRole, isTenantAdminRole, isTenantWorkspaceRole, PLATFORM_ROLES, TENANT_USER_ROLES } from './utils/constants'
import { isKnownRole, isTechnicianOnlyWorkspaceUser, roleHomePath } from './utils/roles'

const LoginPage = lazy(async () => ({ default: (await import('./modules/auth/LoginPage')).LoginPage }))
const ChangePasswordPage = lazy(async () => ({ default: (await import('./modules/auth/ChangePasswordPage')).ChangePasswordPage }))
const MyNotificationsPage = lazy(async () => ({ default: (await import('./modules/profile/MyNotificationsPage')).MyNotificationsPage }))
const ForgotPasswordPage = lazy(async () => ({ default: (await import('./modules/auth/ForgotPasswordPage')).ForgotPasswordPage }))
const ResetPasswordPage = lazy(async () => ({ default: (await import('./modules/auth/ResetPasswordPage')).ResetPasswordPage }))
const SignupPage = lazy(async () => ({ default: (await import('./modules/auth/SignupPage')).SignupPage }))
const PlatformLeadsPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformLeadsPage')).PlatformLeadsPage }))
const DashboardPage = lazy(async () => ({ default: (await import('./modules/dashboard/DashboardPage')).DashboardPage }))
const ContractsPage = lazy(async () => ({ default: (await import('./modules/contracts/ContractsPage')).ContractsPage }))
const ContractCreatePage = lazy(async () => ({ default: (await import('./modules/contracts/ContractCreatePage')).ContractCreatePage }))
const ContractDetailPage = lazy(async () => ({ default: (await import('./modules/contracts/ContractDetailPage')).ContractDetailPage }))
const WorkOrdersPage = lazy(async () => ({ default: (await import('./modules/work-orders/WorkOrdersPage')).WorkOrdersPage }))
const WorkOrderDetailPage = lazy(async () => ({ default: (await import('./modules/work-orders/WorkOrderDetailPage')).WorkOrderDetailPage }))
const ClientsPage = lazy(async () => ({ default: (await import('./modules/clients/ClientsPage')).ClientsPage }))
const ClientDetailPage = lazy(async () => ({ default: (await import('./modules/clients/ClientDetailPage')).ClientDetailPage }))
const SiteDetailPage = lazy(async () => ({ default: (await import('./modules/clients/SiteDetailPage')).SiteDetailPage }))
const SitesPage = lazy(async () => ({ default: (await import('./modules/sites/SitesPage')).SitesPage }))
const SystemDetailPage = lazy(async () => ({ default: (await import('./modules/clients/SystemDetailPage')).SystemDetailPage }))
const AssetsPage = lazy(async () => ({ default: (await import('./modules/assets/AssetsPage')).AssetsPage }))
const MaterialsPage = lazy(async () => ({ default: (await import('./modules/materials/MaterialsPage')).MaterialsPage }))
const PreventiveMaintenancePage = lazy(async () => ({ default: (await import('./modules/preventive-maintenance/PreventiveMaintenancePage')).PreventiveMaintenancePage }))
const DispatchBoardPage = lazy(async () => ({ default: (await import('./modules/dispatch/DispatchBoardPage')).DispatchBoardPage }))
const TemplatesPage = lazy(async () => ({ default: (await import('./modules/templates/TemplatesPage')).TemplatesPage }))
const ReportsPage = lazy(async () => ({ default: (await import('./modules/reports/ReportsPage')).ReportsPage }))
const TeamManagementPage = lazy(async () => ({ default: (await import('./modules/team-management/TeamManagementPage')).TeamManagementPage }))
const KnowledgeCentrePage = lazy(async () => ({ default: (await import('./modules/knowledge/KnowledgeCentrePage')).KnowledgeCentrePage }))
const KnowledgeArticleEditorPage = lazy(async () => ({ default: (await import('./modules/knowledge/KnowledgeArticleEditorPage')).KnowledgeArticleEditorPage }))
const KnowledgeArticleDetailPage = lazy(async () => ({ default: (await import('./modules/knowledge/KnowledgeArticleDetailPage')).KnowledgeArticleDetailPage }))
const TenantFinancePage = lazy(async () => ({ default: (await import('./modules/tenant-finance/TenantFinancePage')).TenantFinancePage }))
const TenantFinanceOverviewPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/TenantFinanceOverviewPage')).TenantFinanceOverviewPage }))
const TenantPreferentialRatesPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/TenantPreferentialRatesPage')).TenantPreferentialRatesPage }))
const TenantFinanceApprovalsPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/TenantFinanceApprovalsPage')).TenantFinanceApprovalsPage }))
const TenantBillableWorkPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/TenantBillableWorkPage')).TenantBillableWorkPage }))
const TenantRateCardsPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/TenantRateCardsPage')).TenantRateCardsPage }))
const InvoicesPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/InvoicesPage')).InvoicesPage }))
const InvoiceDetailPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/InvoiceDetailPage')).InvoiceDetailPage }))
const InvoiceCreatePage = lazy(async () => ({ default: (await import('./modules/tenant-finance/InvoiceCreatePage')).InvoiceCreatePage }))
const QuotationsPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/QuotationsPage')).QuotationsPage }))
const FinanceDocumentsHubPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/FinanceModulePages')).FinanceDocumentsHubPage }))
const FinanceCostingRatesHubPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/FinanceModulePages')).FinanceCostingRatesHubPage }))
const FinanceTaxReceivablesHubPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/FinanceModulePages')).FinanceTaxReceivablesHubPage }))
const FinanceAnalyticsPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/FinanceModulePages')).FinanceAnalyticsPage }))
const FinanceSettingsHubPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/FinanceModulePages')).FinanceSettingsHubPage }))
const FinanceComingSoonPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/FinanceModulePages')).FinanceComingSoonPage }))
const FinanceAgingAnalysisPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/FinanceModulePages')).FinanceAgingAnalysisPage }))
const FinanceClientStatementPage = lazy(async () => ({ default: (await import('./modules/tenant-finance/FinanceModulePages')).FinanceClientStatementPage }))
const TechLayout = lazy(async () => ({ default: (await import('./modules/tech/TechLayout')).TechLayout }))
const TechJobsPage = lazy(async () => ({ default: (await import('./modules/tech/TechJobsPage')).TechJobsPage }))
const TechJobPage = lazy(async () => ({ default: (await import('./modules/tech/TechJobPage')).TechJobPage }))
const PlatformLayout = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformLayout')).PlatformLayout }))
const PlatformOverviewPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformOverviewAndLicensesPages')).PlatformOverviewPage }))
const PlatformLicensesPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformOverviewAndLicensesPages')).PlatformLicensesPage }))
const PlatformTenantsPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformTenantsPage')).PlatformTenantsPage }))
const PlatformUsersPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformOperationsPages')).PlatformUsersPage }))
const PlatformReportsPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformOperationsPages')).PlatformReportsPage }))
const PlatformAuditLogsPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformOperationsPages')).PlatformAuditLogsPage }))
const PlatformSettingsPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformOperationsPages')).PlatformSettingsPage }))
const PlatformFinancePage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformFinancePages')).PlatformFinancePage }))
const FinanceSubscriptionsPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformFinancePages')).FinanceSubscriptionsPage }))
const FinanceInvoicesPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformFinancePages')).FinanceInvoicesPage }))
const FinancePaymentsPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformFinancePages')).FinancePaymentsPage }))
const FinanceExpensesPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformFinancePages')).FinanceExpensesPage }))
const FinanceReportsPage = lazy(async () => ({ default: (await import('./modules/platform-v2/PlatformFinancePages')).FinanceReportsPage }))

const routeCatalog = [
  { label: 'Dashboard', path: '/dashboard', roles: TENANT_USER_ROLES, permission: null, icon: LayoutDashboard },
  { label: 'Work Orders', path: '/work-orders', roles: TENANT_USER_ROLES, permission: 'canViewWorkOrders' as const, icon: ClipboardList },
  { label: 'Dispatch', path: '/dispatch', roles: TENANT_USER_ROLES, permission: 'canViewWorkOrders' as const, icon: Radio },
  { label: 'Clients', path: '/clients', roles: TENANT_USER_ROLES, permission: null, icon: Building2 },
  { label: 'Contracts', path: '/contracts', roles: TENANT_USER_ROLES, permission: null, icon: FileText },
  { label: 'Materials', path: '/materials', roles: TENANT_USER_ROLES, permission: 'canViewMaterials' as const, icon: Package },
  { label: 'Preventive Maintenance', path: '/preventive-maintenance', roles: ['tenantadmin', 'admin'] as const, permission: 'canManageSettings' as const, icon: Wrench },
  { label: 'Templates', path: '/templates', roles: ['tenantadmin', 'admin'] as const, permission: 'canManageSettings' as const, icon: Activity },
  { label: 'Knowledge', path: '/knowledge', roles: TENANT_USER_ROLES, permission: 'canViewKnowledge' as const, icon: BookOpen },
  { label: 'Finance', path: '/finance', roles: TENANT_USER_ROLES, permission: 'canViewFinance' as const, icon: Wallet },
  { label: 'Reports', path: '/reports', roles: TENANT_USER_ROLES, permission: 'canViewReports' as const, icon: Activity },
  { label: 'Team Management', path: '/team-management', roles: ['tenantadmin', 'admin'] as const, permission: 'canManageSettings' as const, icon: Users },
  { label: 'Settings', path: '/settings', roles: ['tenantadmin', 'admin'] as const, permission: 'canManageSettings' as const, icon: Settings2 },
  { label: 'Sites & Assets', path: '/sites', roles: TENANT_USER_ROLES, permission: null, icon: Layers },
  { label: 'Assets', path: '/assets', roles: TENANT_USER_ROLES, permission: null, icon: Package },
  { label: 'Overview', path: '/platform', roles: PLATFORM_ROLES, permission: 'canViewPlatformTenants' as const, icon: ShieldCheck },
  { label: 'Tenants', path: '/platform/tenants', roles: PLATFORM_ROLES, permission: 'canViewPlatformTenants' as const, icon: Building2 },
  { label: 'Leads & Enquiries', path: '/platform/leads', roles: PLATFORM_ROLES, permission: 'canViewPlatformTenants' as const, icon: Users },
  { label: 'Licenses & Subscriptions', path: '/platform/licenses', roles: PLATFORM_ROLES, permission: 'canViewPlatformTenants' as const, icon: FileText },
  { label: 'Platform Users', path: '/platform/users', roles: PLATFORM_ROLES, permission: 'canViewPlatformTenants' as const, icon: Users },
  { label: 'Finance', path: '/platform/finance', roles: PLATFORM_ROLES, permission: 'canViewPlatformTenants' as const, icon: Wallet },
  { label: 'Reports', path: '/platform/reports', roles: PLATFORM_ROLES, permission: 'canViewPlatformTenants' as const, icon: Activity },
  { label: 'Audit Logs', path: '/platform/audit-logs', roles: PLATFORM_ROLES, permission: 'canViewPlatformTenants' as const, icon: FileText },
  { label: 'Settings', path: '/platform/settings', roles: PLATFORM_ROLES, permission: 'canViewPlatformTenants' as const, icon: Settings2 },
] as const

function RouteLoading({ label }: { label: string }) {
  return (
    <div className="min-h-dvh bg-app p-4 sm:p-6 lg:p-8">
      <LoadingState label={label} />
    </div>
  )
}

function HomeRedirect() {
  const { isReady, session } = useAuth()

  if (!isReady) {
    return <RouteLoading label="Opening ServiceOps" />
  }

  if (!session) {
    return <Navigate to="/login" replace />
  }

  if (session.mustChangePassword) {
    return <Navigate to="/change-password" replace />
  }

  return <Navigate to={roleHomePath(session.role, { title: session.title, permissions: session.permissions })} replace />
}

function NotFoundPage() {
  const location = useLocation()
  const { session } = useAuth()
  const fallbackPath = session
    ? roleHomePath(session.role, { title: session.title, permissions: session.permissions })
    : '/login'

  return (
    <div className="min-h-dvh bg-app p-4 sm:p-6 lg:p-8">
      <div className="surface-card mx-auto max-w-2xl">
        <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Not Found</p>
        <h1 className="mt-2 text-3xl font-semibold tracking-tight text-app">That route does not exist</h1>
        <p className="mt-3 text-sm text-muted">
          <span className="font-medium text-app">{location.pathname}</span> is not a valid ServiceOps route.
        </p>
        <div className="mt-6">
          <Link to={fallbackPath} className="button-primary">
            Go back
          </Link>
        </div>
      </div>
    </div>
  )
}

function LegacyClientSiteRedirect() {
  const { clientId, siteId } = useParams<{ clientId: string; siteId: string }>()
  if (!clientId) {
    return <Navigate to="/clients" replace />
  }
  void siteId
  return <Navigate to={`/clients/${clientId}`} replace />
}

function TenantContractRedirect() {
  const { id } = useParams<{ id: string }>()
  return <Navigate to={id ? `/contracts/${id}` : '/contracts'} replace />
}

function TenantOnlyRoute() {
  const { isReady, session } = useAuth()
  const location = useLocation()
  const shellContext = useOutletContext<ShellOutletContext>()

  if (!isReady) {
    return <RouteLoading label="Checking tenant access" />
  }

  if (!session) {
    return <Navigate to="/login" replace />
  }

  if (isPlatformRole(session.role)) {
    return <Navigate to="/platform" replace />
  }

  if (!isKnownRole(session.role)) {
    return <Navigate to="/unsupported-role" replace />
  }

  if (
    isTechnicianOnlyWorkspaceUser(session.role, session.title, session.permissions)
    && !location.pathname.startsWith('/dashboard')
    && !location.pathname.startsWith('/tech')
  ) {
    return <Navigate to="/tech" replace />
  }

  return <Outlet context={shellContext} />
}

function AdminOnlyRoute() {
  const { isReady, session } = useAuth()
  const shellContext = useOutletContext<ShellOutletContext>()

  if (!isReady) {
    return <RouteLoading label="Checking admin access" />
  }

  if (!session) {
    return <Navigate to="/login" replace />
  }

  if (!isTenantAdminRole(session.role) || !session.permissions?.canManageSettings) {
    return <Navigate to={roleHomePath(session.role, { title: session.title, permissions: session.permissions })} replace />
  }

  return <Outlet context={shellContext} />
}

function TemplateManagerOnlyRoute() {
  const { isReady, session } = useAuth()
  const shellContext = useOutletContext<ShellOutletContext>()

  if (!isReady) {
    return <RouteLoading label="Checking template access" />
  }

  if (!session) {
    return <Navigate to="/login" replace />
  }

  if (!isTenantAdminRole(session.role) || !session.permissions?.canManageSettings) {
    return <Navigate to={roleHomePath(session.role, { title: session.title, permissions: session.permissions })} replace />
  }

  return <Outlet context={shellContext} />
}

function PlatformOnlyRoute() {
  const { isReady, session } = useAuth()
  const shellContext = useOutletContext<ShellOutletContext>()

  if (!isReady) {
    return <RouteLoading label="Checking platform access" />
  }

  if (!session) {
    return <Navigate to="/login" replace />
  }

  if (!isPlatformRole(session.role) || !session.permissions?.canViewPlatformTenants) {
    return <Navigate to={roleHomePath(session.role, { title: session.title, permissions: session.permissions })} replace />
  }

  return <Outlet context={shellContext} />
}

function AuthenticatedRouteBoundary() {
  return (
    <ErrorBoundary>
      <Outlet />
    </ErrorBoundary>
  )
}

function UnsupportedRoleRoute() {
  const { session } = useAuth()
  return <UnsupportedRolePage role={session?.role} />
}

function AuthenticatedShell() {
  const { isReady, session } = useAuth()

  const canAccessRoute = useCallback((roles: readonly string[], permission: string | null) => {
    if (!session) return false
    if (roles === PLATFORM_ROLES) {
      if (!isPlatformRole(session.role)) return false
    } else if (roles === TENANT_USER_ROLES) {
      if (!isTenantWorkspaceRole(session.role)) return false
    } else if (!roles.includes(session.role)) {
      return false
    }
    if (!permission) return true
    if (permission === 'canViewFinance') {
      return Boolean(
        session.permissions?.canViewFinance
        || session.permissions?.canManageFinance
        || session.permissions?.canViewFinanceRates
        || session.permissions?.canManageFinanceRates
        || session.permissions?.canViewFinanceApprovals
        || session.permissions?.canManageFinanceApprovals
        || session.permissions?.canViewBillableWork
        || session.permissions?.canManageBillableWork,
      )
    }
    if (permission === 'canViewKnowledge') {
      return Boolean(
        session.permissions?.canViewKnowledge
        || session.permissions?.canManageKnowledge,
      )
    }
    if (permission === 'canViewMaterials') {
      return Boolean(
        session.permissions?.canViewMaterials
        || session.permissions?.canRequestMaterials
        || session.permissions?.canManageMaterials
        || session.permissions?.canApproveMaterials
        || session.permissions?.canIssueMaterials,
      )
    }
    return Boolean(session.permissions?.[permission as keyof NonNullable<typeof session.permissions>])
  }, [session])

  const searchItems = useMemo<SearchItem[]>(() => {
    if (!session) return []

    return routeCatalog
      .filter((item) => canAccessRoute(item.roles, item.permission))
      .map((item) => ({
        id: item.path,
        title: item.label,
        subtitle: item.path,
        path: item.path,
        roles: [session.role],
      }))
  }, [canAccessRoute, session])

  const navItems = useMemo(() => {
    if (!session) return []

    return routeCatalog
      .filter((item) => canAccessRoute(item.roles, item.permission))
      .map(({ label, path, icon }) => ({ label, path, icon }))
  }, [canAccessRoute, session])

  const sidebarSections = useMemo(() => {
    if (!session) return []

    const visibleRoutes = routeCatalog.filter((item) => canAccessRoute(item.roles, item.permission))
    const getRoute = (path: string) => visibleRoutes.find((item) => item.path === path)
    const mapRoutes = (paths: string[]) =>
      paths
        .map((path) => getRoute(path))
        .filter((item): item is NonNullable<typeof item> => Boolean(item))
        .map(({ label, path, icon }) => ({ label, path, icon }))

    if (isPlatformRole(session.role)) {
      return [{ label: 'PLATFORM', items: navItems }]
    }

    const sections = [
      { label: 'MAIN', items: mapRoutes(['/dashboard', '/work-orders', '/dispatch']) },
      { label: 'CUSTOMERS & ASSETS', items: mapRoutes(['/clients', '/sites', '/contracts']) },
      { label: 'OPERATIONS', items: mapRoutes(['/preventive-maintenance', '/templates', '/materials']) },
      { label: 'FINANCE', items: mapRoutes(['/finance', '/reports']) },
      { label: 'ADMIN', items: mapRoutes(['/team-management', '/settings']) },
    ]

    return sections.filter((section) => section.items.length > 0)
  }, [canAccessRoute, navItems, session])

  const notifications = useMemo<NotificationItem[]>(() => [], [])

  const branches = useMemo(() => {
    if (!session || isPlatformRole(session.role)) return []
    const mapped = (session.branches ?? []).map((branch) => ({
      id: branch.id,
      name: branch.name,
      code: branch.code,
      region: branch.location ?? '',
      city: branch.location ?? '',
      active: branch.isActive,
      siteCount: 0,
    }))
    return [
      { id: 'all', name: 'All Branches', code: 'ALL', region: '', city: '', active: true, siteCount: 0 },
      ...mapped,
    ]
  }, [session])

  if (!isReady || !session) {
    return <RouteLoading label="Loading workspace shell" />
  }

  if (!isKnownRole(session.role)) {
    return <UnsupportedRolePage role={session.role} />
  }

  return (
    <AppShell
      mode={isPlatformRole(session.role) ? 'platform' : 'tenant'}
      navItems={navItems}
      sidebarSections={sidebarSections}
      session={session}
      tenantName={isPlatformRole(session.role) ? 'Ecosys Platform' : session.tenantName}
      branches={branches}
      notifications={notifications}
      searchItems={searchItems}
    />
  )
}

function AppRoutes() {
  useEffect(() => installInteractionDebugLogger(), [])
  useEffect(() => {
    document.title = 'Ecosys'
  }, [])

  return (
    <Suspense fallback={<RouteLoading label="Loading page" />}>
      <Routes>
        <Route path="/" element={<HomeRedirect />} />

        <Route element={<GuestRoute />}>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route path="/get-started" element={<SignupPage />} />
          <Route path="/signup" element={<Navigate to="/get-started" replace />} />
        </Route>

        <Route element={<RequireAuth />}>
          <Route element={<AuthenticatedRouteBoundary />}>
            <Route path="/change-password" element={<ChangePasswordPage />} />
            <Route path="/my-notifications" element={<MyNotificationsPage />} />
            <Route path="/unsupported-role" element={<UnsupportedRoleRoute />} />
            <Route path="/tech" element={<TechLayout />}>
              <Route index element={<TechJobsPage />} />
              <Route path="jobs/:id" element={<TechJobPage />} />
              <Route path="jobs/:id/checklist" element={<TechJobPage />} />
              <Route path="jobs/:id/photos" element={<TechJobPage />} />
              <Route path="jobs/:id/complete" element={<TechJobPage />} />
            </Route>
            <Route element={<AuthenticatedShell />}>
              <Route element={<TenantOnlyRoute />}>
                <Route path="/dashboard" element={<DashboardPage />} />
                <Route path="/contracts" element={<ContractsPage />} />
                <Route path="/contracts/new" element={<ContractCreatePage />} />
                <Route path="/contracts/:id" element={<ContractDetailPage />} />
                <Route path="/tenant/contracts" element={<Navigate to="/contracts" replace />} />
                <Route path="/tenant/contracts/new" element={<Navigate to="/contracts/new" replace />} />
                <Route path="/tenant/contracts/:id" element={<TenantContractRedirect />} />
                <Route path="/work-orders" element={<WorkOrdersPage />} />
                <Route path="/work-orders/:id" element={<WorkOrderDetailPage />} />
                <Route path="/dispatch" element={<DispatchBoardPage />} />
                <Route path="/clients" element={<ClientsPage />} />
                <Route path="/clients/:clientId" element={<ClientDetailPage />} />
                <Route path="/clients/:clientId/sites/:siteId" element={<SiteDetailPage />} />
                <Route path="/clients/:clientId/sites/:siteId/legacy" element={<LegacyClientSiteRedirect />} />
                <Route path="/clients/:clientId/sites/:siteId/systems/:systemId" element={<SystemDetailPage />} />
                <Route path="/sites" element={<SitesPage />} />
                <Route path="/sites/:id" element={<Navigate to="/sites" replace />} />
                <Route path="/assets" element={<AssetsPage />} />
                <Route path="/assets/:id" element={<Navigate to="/assets" replace />} />
                <Route path="/materials" element={<MaterialsPage />} />
                <Route path="/preventive-maintenance" element={<PreventiveMaintenancePage />} />
                <Route element={<TemplateManagerOnlyRoute />}>
                  <Route path="/templates" element={<TemplatesPage />} />
                </Route>
                <Route path="/knowledge" element={<KnowledgeCentrePage />} />
                <Route path="/knowledge/new" element={<KnowledgeArticleEditorPage />} />
                <Route path="/knowledge/:id" element={<KnowledgeArticleDetailPage />} />
                <Route path="/knowledge/:id/edit" element={<KnowledgeArticleEditorPage />} />
                <Route path="/finance" element={<TenantFinancePage />}>
                  <Route index element={<TenantFinanceOverviewPage />} />
                  <Route path="overview" element={<TenantFinanceOverviewPage />} />
                  <Route path="documents" element={<FinanceDocumentsHubPage />} />
                  <Route path="documents/quotes" element={<QuotationsPage />} />
                  <Route path="documents/invoices" element={<InvoicesPage />} />
                  <Route path="documents/invoices/new" element={<InvoiceCreatePage />} />
                  <Route path="documents/invoices/:id" element={<InvoiceDetailPage />} />
                  <Route path="documents/proforma-invoices" element={<FinanceComingSoonPage title="Proforma Invoices" description="No proforma invoices yet. Create a proforma from an accepted quote or for advance billing before issuing a tax invoice." />} />
                  <Route path="documents/receipts-payments" element={<FinanceComingSoonPage title="Receipts / Payments" description="No payments recorded yet. Record client payments and allocate them to invoices." />} />
                  <Route path="documents/credit-notes" element={<FinanceComingSoonPage title="Credit Notes" description="No credit notes yet. Use credit notes to reduce or correct issued invoices." />} />
                  <Route path="documents/debit-notes" element={<FinanceComingSoonPage title="Debit Notes" description="No debit notes yet. Use debit notes to add charges after an invoice has been issued." />} />

                  <Route path="costing-rates" element={<FinanceCostingRatesHubPage />} />
                  <Route path="costing-rates/billable-work" element={<TenantBillableWorkPage />} />
                  <Route path="costing-rates/expenses" element={<FinanceComingSoonPage title="Expenses / Costs" description="No expenses recorded yet. Track subcontractor, transport, labour, and job-related costs." />} />
                  <Route path="costing-rates/material-costing" element={<FinanceComingSoonPage title="Material Costing" description="No material costing records yet. Material costs will appear here when issued materials are linked to billable work orders." />} />
                  <Route path="costing-rates/rate-cards" element={<TenantRateCardsPage />} />
                  <Route path="costing-rates/preferential-rates" element={<TenantPreferentialRatesPage />} />

                  <Route path="tax-receivables" element={<FinanceTaxReceivablesHubPage />} />
                  <Route path="tax-receivables/tax-vat" element={<FinanceComingSoonPage title="Tax / VAT" description="Tax settings are not configured yet. Configure VAT/tax rules before issuing compliant tax documents." />} />
                  <Route path="tax-receivables/statements" element={<FinanceClientStatementPage />} />
                  <Route path="tax-receivables/aging" element={<FinanceAgingAnalysisPage />} />
                  <Route path="tax-receivables/approvals" element={<TenantFinanceApprovalsPage />} />

                  <Route path="analytics" element={<FinanceAnalyticsPage />} />

                  <Route path="settings" element={<FinanceSettingsHubPage />} />

                  <Route path="quotes" element={<QuotationsPage />} />
                  <Route path="invoices" element={<InvoicesPage />} />
                  <Route path="invoices/new" element={<InvoiceCreatePage />} />
                  <Route path="invoices/:id" element={<InvoiceDetailPage />} />
                  <Route path="rate-cards" element={<TenantRateCardsPage />} />
                  <Route path="preferential-rates" element={<TenantPreferentialRatesPage />} />
                  <Route path="approvals" element={<TenantFinanceApprovalsPage />} />
                  <Route path="billable-work" element={<TenantBillableWorkPage />} />
                  <Route path="reports" element={<FinanceAnalyticsPage />} />
                </Route>
                <Route path="/reports" element={<ReportsPage />} />
              </Route>

              <Route element={<AdminOnlyRoute />}>
                <Route path="/team-management" element={<TeamManagementPage />} />
                <Route path="/settings" element={<SettingsLayout />}>
                  <Route index element={<SettingsHubPage />} />
                  {settingsPageRoutes.map((route) => (
                    <Route key={route.path} path={route.path} element={route.element} />
                  ))}
                </Route>
              </Route>

              <Route element={<PlatformOnlyRoute />}>
                <Route path="/platform" element={<PlatformLayout />}>
                  <Route index element={<PlatformOverviewPage />} />
                  <Route path="tenants" element={<PlatformTenantsPage />} />
                  <Route path="leads" element={<PlatformLeadsPage />} />
                  <Route path="licenses" element={<PlatformLicensesPage />} />
                  <Route path="users" element={<PlatformUsersPage />} />
                  <Route path="finance" element={<PlatformFinancePage />}>
                    <Route path="overview" element={<Navigate to="/platform/finance" replace />} />
                    <Route path="subscriptions" element={<FinanceSubscriptionsPage />} />
                    <Route path="invoices" element={<FinanceInvoicesPage />} />
                    <Route path="payments" element={<FinancePaymentsPage />} />
                    <Route path="expenses" element={<FinanceExpensesPage />} />
                    <Route path="reports" element={<FinanceReportsPage />} />
                  </Route>
                  <Route path="reports" element={<PlatformReportsPage />} />
                  <Route path="audit-logs" element={<PlatformAuditLogsPage />} />
                  <Route path="settings" element={<PlatformSettingsPage />} />
                </Route>

                <Route path="/command-centre" element={<Navigate to="/platform" replace />} />
                <Route path="/command-centre/tenants" element={<Navigate to="/platform/tenants" replace />} />
                <Route path="/command-centre/leads" element={<Navigate to="/platform/leads" replace />} />
                <Route path="/command-centre/licenses" element={<Navigate to="/platform/licenses" replace />} />
                <Route path="/command-centre/reports" element={<Navigate to="/platform/reports" replace />} />
                <Route path="/command-centre/platform-users" element={<Navigate to="/platform/users" replace />} />
                <Route path="/command-centre/audit-logs" element={<Navigate to="/platform/audit-logs" replace />} />
                <Route path="/command-centre/settings" element={<Navigate to="/platform/settings" replace />} />
                <Route path="/command-centre/finance" element={<Navigate to="/platform/finance" replace />} />
                <Route path="/command-centre/invoices" element={<Navigate to="/platform/finance/invoices" replace />} />
              </Route>

            </Route>
          </Route>
        </Route>

        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </Suspense>
  )
}

function App() {
  return (
    <ThemeProvider>
      <ToastProvider>
        <AuthProvider>
          <BrowserRouter>
            <AppRoutes />
          </BrowserRouter>
        </AuthProvider>
      </ToastProvider>
    </ThemeProvider>
  )
}

export default App
