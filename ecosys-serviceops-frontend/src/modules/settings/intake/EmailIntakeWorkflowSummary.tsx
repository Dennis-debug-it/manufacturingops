import { Badge } from '../../../components/ui/Badge'
import type { EmailIntakeSectionId, EmailIntakeStep } from './emailIntakeModels'

function toneFromStatus(status: EmailIntakeStep['status']): 'success' | 'warning' | 'danger' | 'neutral' {
  if (status === 'Complete') return 'success'
  if (status === 'Warning') return 'warning'
  if (status === 'Needs Setup') return 'danger'
  return 'neutral'
}

export function EmailIntakeWorkflowSummary({
  steps,
  onConfigure,
}: {
  steps: EmailIntakeStep[]
  onConfigure: (section: EmailIntakeSectionId) => void
}) {
  return (
    <section data-testid="email-intake-workflow-summary" className="surface-card">
      <div>
        <p className="eyebrow-accent text-xs font-semibold uppercase tracking-[0.2em]">Workflow Overview</p>
        <h3 className="mt-2 text-xl font-semibold tracking-tight text-app">Email Intake Process</h3>
        <p className="mt-2 text-sm text-muted">Track setup and progress across all nine intake stages.</p>
      </div>

      <div className="mt-5 grid gap-3 lg:grid-cols-2">
        {steps.map((step) => (
          <article key={step.stepNumber} className="panel-subtle rounded-2xl border border-app/50 px-4 py-4">
            <div className="flex items-center justify-between gap-3">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Step {step.stepNumber}</p>
              <Badge tone={toneFromStatus(step.status)}>{step.status}</Badge>
            </div>
            <p className="mt-3 text-base font-semibold text-app">{step.title}</p>
            <p className="mt-2 text-sm text-muted">{step.description}</p>
            <button type="button" className="button-secondary mt-4" onClick={() => onConfigure(step.id)}>
              Configure
            </button>
          </article>
        ))}
      </div>
    </section>
  )
}
