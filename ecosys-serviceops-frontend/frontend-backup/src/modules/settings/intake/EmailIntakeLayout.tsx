import type { ReactNode } from 'react'
import { Badge } from '../../../components/ui/Badge'
import type { EmailIntakeMetric, IntakeStatusBadge } from './emailIntakeModels'
import { EmailIntakeMetricCards } from './EmailIntakeMetricCards'
import { EmailIntakeSubNav } from './EmailIntakeSubNav'
import type { EmailIntakeSectionId } from './emailIntakeModels'

function toneFromStatus(status: IntakeStatusBadge): 'success' | 'danger' | 'warning' {
  if (status === 'Enabled') return 'success'
  if (status === 'Disabled') return 'danger'
  return 'warning'
}

export function EmailIntakeLayout({
  status,
  metrics,
  activeSection,
  onSectionChange,
  onRunSync,
  onOpenSimulation,
  onOpenManualReview,
  onOpenMailbox,
  children,
}: {
  status: IntakeStatusBadge
  metrics: EmailIntakeMetric[]
  activeSection: EmailIntakeSectionId
  onSectionChange: (section: EmailIntakeSectionId) => void
  onRunSync: () => void
  onOpenSimulation: () => void
  onOpenManualReview: () => void
  onOpenMailbox: () => void
  children: ReactNode
}) {
  return (
    <div data-testid="email-intake-page" className="space-y-5">
      <section data-testid="email-intake-header" className="surface-card">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h1 className="text-3xl font-semibold tracking-tight text-app">Email-to-Work Order</h1>
            <p className="mt-2 max-w-3xl text-sm leading-6 text-muted">Connect a service mailbox so Ecosys can read customer service requests and turn them into work orders automatically. The setup follows a simple six-step flow: connect, decide, match, fallback, test, and enable.</p>
            <div className="mt-3" data-testid="email-intake-status-badge">
              <Badge tone={toneFromStatus(status)}>{status}</Badge>
            </div>
          </div>

          <div className="flex w-full flex-wrap gap-2 lg:w-auto">
            <button type="button" className="button-secondary flex-1 sm:flex-none" onClick={onRunSync}>Run Sync</button>
            <button data-testid="email-intake-simulate-button" type="button" className="button-secondary flex-1 sm:flex-none" onClick={onOpenSimulation}>Send test email</button>
            <button type="button" className="button-secondary flex-1 sm:flex-none" onClick={onOpenManualReview}>Review queue</button>
            <button type="button" className="button-secondary flex-1 sm:flex-none" onClick={onOpenMailbox}>Mailbox</button>
          </div>
        </div>
      </section>

      <EmailIntakeMetricCards metrics={metrics} />

      <div className="grid gap-5 lg:grid-cols-[280px_minmax(0,1fr)]">
        <EmailIntakeSubNav activeSection={activeSection} onChange={onSectionChange} />
        <div className="min-w-0">{children}</div>
      </div>
    </div>
  )
}
