import { EmptyState } from '../../components/ui/EmptyState'
import { PageHeader } from '../../components/ui/PageHeader'

export function SettingsAccessDeniedPage() {
  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Settings"
        title="Permission Required"
        description="This settings area is not available in the tenant settings workspace."
      />
      <section className="surface-card">
        <EmptyState
          title="You do not have permission to view this page."
          description="Branding and platform licensing controls are available only from the Platform Command Centre."
        />
      </section>
    </div>
  )
}
