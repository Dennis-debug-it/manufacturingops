import { DataTable } from '../../../components/ui/DataTable'
import type { EmailIntakeSectionId, EmailIntakeStep, IntakeActivityRecord, ManualReviewRecord } from './emailIntakeModels'
import { EmailIntakeWorkflowSummary } from './EmailIntakeWorkflowSummary'

export function EmailIntakeOverview({
  steps,
  activity,
  manualReview,
  latestSimulationSummary,
  draftRuleName,
  onRuleNameChange,
  onSaveRule,
  onConfigureStep,
  onOpenManualReview,
  onOpenSimulation,
}: {
  steps: EmailIntakeStep[]
  activity: IntakeActivityRecord[]
  manualReview: ManualReviewRecord[]
  latestSimulationSummary: string
  draftRuleName: string
  onRuleNameChange: (value: string) => void
  onSaveRule: () => void
  onConfigureStep: (section: EmailIntakeSectionId) => void
  onOpenManualReview: () => void
  onOpenSimulation: () => void
}) {
  return (
    <div data-testid="email-intake-overview-panel" className="space-y-5">
      <EmailIntakeWorkflowSummary steps={steps} onConfigure={onConfigureStep} />

      <section className="surface-card">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="text-sm font-semibold text-app">Default Intake Rule</p>
            <p className="mt-1 text-sm text-muted">Set a clear rule name before adding detailed filters and mappings.</p>
          </div>
          <button type="button" className="button-secondary" onClick={onOpenSimulation}>Go to Simulation</button>
        </div>
        <div className="mt-4 grid gap-3 md:grid-cols-[1fr_auto]">
          <input
            data-testid="email-intake-rule-name-input"
            className="field-input"
            value={draftRuleName}
            onChange={(event) => onRuleNameChange(event.target.value)}
            placeholder="e.g. Generator failure escalation"
          />
          <button data-testid="email-intake-save-button" type="button" className="button-primary" onClick={onSaveRule}>
            Save Rule
          </button>
        </div>
        <div data-testid="email-intake-preview-result" className="panel-subtle mt-4 rounded-2xl p-4 text-sm text-app">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Latest Simulation Preview</p>
          <p className="mt-2">{latestSimulationSummary}</p>
        </div>
      </section>

      <section className="surface-card">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="text-sm font-semibold text-app">Recent Activity</p>
            <p className="mt-1 text-sm text-muted">Latest inbound email intake lifecycle events.</p>
          </div>
          <button type="button" className="button-secondary" onClick={onOpenManualReview}>View Manual Review</button>
        </div>
        <div className="mt-4">
          <DataTable
            rows={activity.slice(0, 8)}
            rowKey={(row) => row.id}
            pageSize={8}
            emptyTitle="No intake activity yet"
            emptyDescription="Incoming email events will appear here once intake starts processing messages."
            minTableWidth="min-w-[980px] w-full"
            columns={[
              { key: 'time', header: 'Time', cell: (row) => new Date(row.occurredAt).toLocaleString() },
              { key: 'sender', header: 'Sender', cell: (row) => row.sender },
              { key: 'subject', header: 'Subject', cell: (row) => row.subject },
              { key: 'action', header: 'Action', cell: (row) => row.actionType },
              { key: 'status', header: 'Status', cell: (row) => row.status },
            ]}
          />
        </div>
      </section>

      <section className="surface-card">
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="text-sm font-semibold text-app">Manual Review Preview</p>
            <p className="mt-1 text-sm text-muted">Top unresolved items that need dispatcher validation.</p>
          </div>
          <button type="button" className="button-secondary" onClick={onOpenManualReview}>Open Full Queue</button>
        </div>

        <div className="mt-4">
          <DataTable
            rows={manualReview.slice(0, 5)}
            rowKey={(row) => row.id}
            pageSize={5}
            emptyTitle="Manual review queue is clear"
            emptyDescription="Low-confidence intake records will appear here for review."
            minTableWidth="min-w-[980px] w-full"
            columns={[
              { key: 'received', header: 'Received', cell: (row) => new Date(row.receivedDate).toLocaleString() },
              { key: 'sender', header: 'Sender', cell: (row) => row.sender },
              { key: 'subject', header: 'Subject', cell: (row) => row.subject },
              { key: 'client', header: 'Suggested Client', cell: (row) => row.suggestedClient },
              { key: 'confidence', header: 'Confidence', cell: (row) => `${row.confidence}%` },
              { key: 'reason', header: 'Reason', cell: (row) => row.reviewReason },
            ]}
          />
        </div>
      </section>
    </div>
  )
}
