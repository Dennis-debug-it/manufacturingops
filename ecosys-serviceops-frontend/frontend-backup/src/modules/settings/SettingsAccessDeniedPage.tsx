import { EmptyState } from '../../components/ui/EmptyState'
import { PageScaffold, SectionCard } from '../../components/ui/Workspace'

export function SettingsAccessDeniedPage() {
  return (
    <PageScaffold
      eyebrow="Settings"
      title="Permission Required"
      description="This settings area is not available in the tenant settings workspace."
    >
      <SectionCard title="Access restricted">
        <EmptyState
          title="You do not have permission to view this page."
          description="Branding and platform licensing controls are available only from the Platform Command Centre."
        />
      </SectionCard>
    </PageScaffold>
  )
}
