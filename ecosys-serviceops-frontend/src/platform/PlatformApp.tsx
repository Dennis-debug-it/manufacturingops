import type { ReactNode } from 'react'
import { Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Spinner } from '../shared/components/Spinner'
import { useAuth } from '../shared/hooks/useAuth'
import { platformNavItems } from '../shared/layout/navigation'
import type { AppRole } from '../shared/types/auth'
import { PlatformShell } from './layout/PlatformShell'
import { PlatformDashboardPage } from './PlatformDashboardPage'
import { TenantsPage } from './tenants/TenantsPage'
import { TenantDetailPage } from './tenants/TenantDetailPage'
import { LeadsPage } from './crm/LeadsPage'
import { ContactsPage } from './crm/ContactsPage'
import { ActivitiesPage } from './crm/ActivitiesPage'
import { TasksPage } from './crm/TasksPage'
import { PipelinePage } from './crm/PipelinePage'
import { LeadDetailPage } from './crm/LeadDetailPage'
import { UsersPage } from './UsersPage'
import { LicensingPage } from './LicensingPage'
import { FinancePage } from './FinancePage'
import { ReportsPage } from './ReportsPage'
import { AuditPage } from './AuditPage'
import { SettingsPage } from './SettingsPage'
import { PlatformSettingsOverviewPage } from './settings/PlatformSettingsOverviewPage'
import { IdentitySettingsPage } from './settings/IdentitySettingsPage'
import { PlansSettingsPage } from './settings/PlansSettingsPage'
import { BrandingSettingsPage } from './settings/BrandingSettingsPage'
import { FeaturesSettingsPage } from './settings/FeaturesSettingsPage'
import { EmailTemplatesSettingsPage } from './settings/EmailTemplatesSettingsPage'
import { EmailDeliverySettingsPage } from './settings/EmailDeliverySettingsPage'
import { SecuritySettingsPage } from './settings/SecuritySettingsPage'
import { AuditSettingsPage } from './settings/AuditSettingsPage'
import { CrmSettingsPage } from './settings/CrmSettingsPage'
import { AnnouncementsSettingsPage } from './settings/AnnouncementsSettingsPage'
import { SupportSettingsPage } from './settings/SupportSettingsPage'
import { HealthSettingsPage } from './settings/HealthSettingsPage'
import { DemoWorkspacePage } from './DemoWorkspacePage'

/**
 * The roles allowed to open each platform section, keyed by the section's nav `to` path.
 * Read from `platformNavItems` so the sidebar and the router cannot drift apart: whatever the
 * nav declares (and therefore whatever it renders) is exactly what the route will admit.
 */
function rolesFor(navPath: string): AppRole[] {
  const item = platformNavItems.find((candidate) => candidate.to === navPath)
  // A section with no nav entry has no declared audience; treat it as the most restrictive
  // platform role rather than silently opening it to everyone.
  return item ? item.roles : ['super_admin']
}

/** The first section this role may open — where a refused navigation lands. */
function landingPathFor(role: AppRole | undefined): string {
  const item = platformNavItems.find((candidate) => candidate.roles.includes(role ?? 'viewer'))
  return item ? item.to : '/403'
}

/**
 * Route-level enforcement of the per-section roles. `PlatformShell` hides links the role may not
 * use; this refuses the URL itself, so typing or bookmarking one is not a way in. The server
 * enforces the same boundary (`AddPlatformPolicy`), so this exists to stop the UI attempting a
 * request that would 403 and leaving the operator on a broken screen.
 */
function RequirePlatformSection({ navPath, children }: { navPath: string; children: ReactNode }) {
  const { user } = useAuth()

  if (!user) {
    // `/platform/*` sits behind ProtectedRoute, so an absent user means the session is still
    // resolving; ProtectedRoute owns the redirect.
    return <Spinner label="Loading platform" />
  }

  if (rolesFor(navPath).includes(user.role)) {
    return <>{children}</>
  }

  return <Navigate to={landingPathFor(user.role)} replace />
}

function PlatformIndexRedirect() {
  const { user } = useAuth()
  return <Navigate to={landingPathFor(user?.role)} replace />
}

export function PlatformApp() {
  return (
    <Suspense fallback={<Spinner label="Loading platform" />}>
      <PlatformShell>
        <Routes>
          <Route index element={<PlatformIndexRedirect />} />
          <Route path="overview" element={<RequirePlatformSection navPath="/platform/overview"><PlatformDashboardPage /></RequirePlatformSection>} />
          <Route path="dashboard" element={<Navigate to="/platform/overview" replace />} />

          <Route path="tenants" element={<RequirePlatformSection navPath="/platform/tenants"><TenantsPage /></RequirePlatformSection>} />
          <Route path="tenants/:id" element={<RequirePlatformSection navPath="/platform/tenants"><TenantDetailPage /></RequirePlatformSection>} />

          <Route path="leads" element={<RequirePlatformSection navPath="/platform/leads"><LeadsPage /></RequirePlatformSection>} />
          <Route path="crm/pipeline" element={<RequirePlatformSection navPath="/platform/leads"><PipelinePage /></RequirePlatformSection>} />
          <Route path="crm/leads/:id" element={<RequirePlatformSection navPath="/platform/leads"><LeadDetailPage /></RequirePlatformSection>} />
          <Route path="crm/contacts" element={<RequirePlatformSection navPath="/platform/leads"><ContactsPage /></RequirePlatformSection>} />
          <Route path="crm/activities" element={<RequirePlatformSection navPath="/platform/leads"><ActivitiesPage /></RequirePlatformSection>} />
          <Route path="crm/tasks" element={<RequirePlatformSection navPath="/platform/leads"><TasksPage /></RequirePlatformSection>} />
          <Route path="crm/*" element={<Navigate to="/platform/leads" replace />} />

          <Route path="users" element={<RequirePlatformSection navPath="/platform/users"><UsersPage /></RequirePlatformSection>} />
          <Route path="licensing" element={<RequirePlatformSection navPath="/platform/licensing"><LicensingPage /></RequirePlatformSection>} />
          <Route path="finance" element={<RequirePlatformSection navPath="/platform/finance"><FinancePage /></RequirePlatformSection>} />
          <Route path="reports" element={<RequirePlatformSection navPath="/platform/reports"><ReportsPage /></RequirePlatformSection>} />
          <Route path="audit" element={<RequirePlatformSection navPath="/platform/audit"><AuditPage /></RequirePlatformSection>} />
          <Route path="health" element={<RequirePlatformSection navPath="/platform/health"><HealthSettingsPage /></RequirePlatformSection>} />
          <Route path="demo" element={<RequirePlatformSection navPath="/platform/demo"><DemoWorkspacePage /></RequirePlatformSection>} />

          <Route path="settings" element={<RequirePlatformSection navPath="/platform/settings"><SettingsPage /></RequirePlatformSection>}>
            <Route index element={<PlatformSettingsOverviewPage />} />
            <Route path="branding" element={<BrandingSettingsPage />} />
            <Route path="email-delivery" element={<EmailDeliverySettingsPage />} />
            <Route path="email-templates" element={<EmailTemplatesSettingsPage />} />
            <Route path="plans" element={<PlansSettingsPage />} />
            <Route path="features" element={<FeaturesSettingsPage />} />
            <Route path="identity" element={<IdentitySettingsPage />} />
            <Route path="security" element={<SecuritySettingsPage />} />
            <Route path="audit" element={<AuditSettingsPage />} />
            <Route path="templates" element={<EmailTemplatesSettingsPage />} />
            <Route path="crm" element={<CrmSettingsPage />} />
            <Route path="announcements" element={<AnnouncementsSettingsPage />} />
            <Route path="support" element={<SupportSettingsPage />} />
            <Route path="health" element={<HealthSettingsPage />} />
          </Route>

          <Route path="*" element={<PlatformIndexRedirect />} />
        </Routes>
      </PlatformShell>
    </Suspense>
  )
}
