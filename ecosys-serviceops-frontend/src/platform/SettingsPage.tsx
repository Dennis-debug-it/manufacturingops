import { Outlet } from 'react-router-dom'
import { PageHeader } from '../shared/components/PageHeader'
import { MiniSidebar } from './common'

const items = [
  { label: 'Platform Profile', to: '/platform/settings' },
  { label: 'Branding', to: '/platform/settings/branding' },
  { label: 'Email & Notifications', to: '/platform/settings/email-delivery' },
  { label: 'Security', to: '/platform/settings/security' },
  { label: 'Roles & Permissions', to: '/platform/settings/identity' },
  { label: 'Modules', to: '/platform/settings/features' },
  { label: 'CRM', to: '/platform/settings/crm' },
  { label: 'Announcements', to: '/platform/settings/announcements' },
  { label: 'Support', to: '/platform/settings/support' },
  { label: 'System Health', to: '/platform/settings/health' },
  { label: 'Document Templates', to: '/platform/settings/email-templates' },
  { label: 'Finance & Tax', to: '/platform/settings/plans' },
  { label: 'Audit & Logs', to: '/platform/settings/audit' },
]

export function SettingsPage() {
  return (
    <div className="space-y-5">
      <PageHeader title="Platform Settings" subtitle="Platform-wide defaults, security, branding, and operational preferences." />
      <div className="grid grid-cols-1 gap-5 xl:grid-cols-[260px_minmax(0,1fr)]">
        <MiniSidebar items={items} />
        <div className="min-w-0">
          <Outlet />
        </div>
      </div>
    </div>
  )
}

