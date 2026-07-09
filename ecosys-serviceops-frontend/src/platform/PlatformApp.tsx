import { Suspense } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { Spinner } from '../shared/components/Spinner'
import { PlatformShell } from './layout/PlatformShell'
import { PlatformDashboardPage } from './PlatformDashboardPage'
import { TenantsPage } from './tenants/TenantsPage'
import { TenantDetailPage } from './tenants/TenantDetailPage'
import { LeadsPage } from './crm/LeadsPage'
import { ContactsPage } from './crm/ContactsPage'
import { ActivitiesPage } from './crm/ActivitiesPage'
import { TasksPage } from './crm/TasksPage'
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

export function PlatformApp() {
  return (
    <Suspense fallback={<Spinner label="Loading platform" />}>
      <PlatformShell>
        <Routes>
          <Route index element={<Navigate to="overview" replace />} />
          <Route path="overview" element={<PlatformDashboardPage />} />
          <Route path="dashboard" element={<Navigate to="/platform/overview" replace />} />

          <Route path="tenants" element={<TenantsPage />} />
          <Route path="tenants/:id" element={<TenantDetailPage />} />

          <Route path="leads" element={<LeadsPage />} />
          <Route path="crm/contacts" element={<ContactsPage />} />
          <Route path="crm/activities" element={<ActivitiesPage />} />
          <Route path="crm/tasks" element={<TasksPage />} />
          <Route path="crm/*" element={<Navigate to="/platform/leads" replace />} />

          <Route path="users" element={<UsersPage />} />
          <Route path="licensing" element={<LicensingPage />} />
          <Route path="finance" element={<FinancePage />} />
          <Route path="reports" element={<ReportsPage />} />
          <Route path="audit" element={<AuditPage />} />

          <Route path="settings" element={<SettingsPage />}>
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

          <Route path="*" element={<Navigate to="/platform/overview" replace />} />
        </Routes>
      </PlatformShell>
    </Suspense>
  )
}
