import { EmptyState } from '../../shared/components/EmptyState'
import { PageHeader } from '../../shared/components/PageHeader'
import { Megaphone, MessageCircleQuestion, Activity, Users2 } from 'lucide-react'

export function CrmSettingsPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="CRM Settings" subtitle="Lead pipeline stages, scoring rules, and demo-request routing." />
      <EmptyState icon={Users2} title="CRM configuration is not yet backend-managed" description="Pipeline stages and lead routing are currently fixed in application code. No PlatformCrmSettingsController exists in the current backend to persist tenant-configurable CRM rules." />
    </div>
  )
}

export function AnnouncementsSettingsPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Announcements" subtitle="Platform-wide banners and release notes shown to tenant users." />
      <EmptyState icon={Megaphone} title="Announcements are not yet backend-managed" description="No PlatformAnnouncementsController exists in the current backend to store and publish announcement banners." />
    </div>
  )
}

export function SupportSettingsPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Support" subtitle="Support contact routing and escalation configuration." />
      <EmptyState icon={MessageCircleQuestion} title="Support routing is not yet backend-managed" description="No PlatformSupportSettingsController exists in the current backend. The support email shown to tenants comes from Platform Identity settings." />
    </div>
  )
}

export function HealthSettingsPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="System Health" subtitle="Background job status, queue depth, and service health checks." />
      <EmptyState icon={Activity} title="System health monitoring is not yet exposed" description="No platform health/status endpoint exists in the current backend. Consider the Platform Dashboard for tenant and subscription KPIs in the meantime." />
    </div>
  )
}
