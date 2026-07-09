import { SmartTextarea } from '../../../components/ui/SettingsWorkspace'
import type { AssignmentGroupRecord } from '../../../types/api'

export type WorkOrderCreationForm = {
  defaultType: string
  defaultStatus: string
  defaultPriority: string
  sourceFields: string
  attachmentHandling: string
  timelineHandling: string
  fallbackAssignmentGroupId: string
}

export function WorkOrderCreationPanel({
  form,
  groups,
  onChange,
  onSave,
}: {
  form: WorkOrderCreationForm
  groups: AssignmentGroupRecord[]
  onChange: (patch: Partial<WorkOrderCreationForm>) => void
  onSave: () => void
}) {
  return (
    <section data-testid="email-intake-workorder-panel" className="space-y-5">
      <article className="surface-card grid gap-4 xl:grid-cols-2">
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">Default work order type</span>
          <input className="field-input" value={form.defaultType} onChange={(event) => onChange({ defaultType: event.target.value })} />
        </label>
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">Default status</span>
          <input className="field-input" value={form.defaultStatus} onChange={(event) => onChange({ defaultStatus: event.target.value })} />
        </label>
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">Default priority</span>
          <select className="field-input" value={form.defaultPriority} onChange={(event) => onChange({ defaultPriority: event.target.value })}>
            <option>Low</option>
            <option>Medium</option>
            <option>High</option>
            <option>Critical</option>
          </select>
        </label>
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">Fallback assignment group</span>
          <select className="field-input" value={form.fallbackAssignmentGroupId} onChange={(event) => onChange({ fallbackAssignmentGroupId: event.target.value })}>
            <option value="">Select assignment group</option>
            {groups.map((group) => (
              <option key={group.id} value={group.id}>{group.name}</option>
            ))}
          </select>
        </label>
      </article>

      <article className="surface-card grid gap-4">
        <SmartTextarea label="Source fields" value={form.sourceFields} onChange={(value) => onChange({ sourceFields: value })} minRows={3} maxRows={5} description="Use subject for title and body for description." />
        <SmartTextarea label="Attachment handling" value={form.attachmentHandling} onChange={(value) => onChange({ attachmentHandling: value })} minRows={3} maxRows={5} description="Store attachments against the timeline when they are accepted." />
        <SmartTextarea label="Timeline handling" value={form.timelineHandling} onChange={(value) => onChange({ timelineHandling: value })} minRows={3} maxRows={5} description="Append parser and matching metadata to the timeline." />

        <div className="panel-subtle rounded-2xl p-4 text-sm text-app">
          <p className="font-semibold">Assignment logic</p>
          <p className="mt-1 text-muted">Work orders are routed to assignment groups first. Direct technician assignment is handled later in dispatch workflows.</p>
        </div>

        <button type="button" className="button-primary" onClick={onSave}>Save Work Order Defaults</button>
      </article>
    </section>
  )
}
