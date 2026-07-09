import { SmartTextarea } from '../../../components/ui/SettingsWorkspace'

export type DecisionRulesForm = {
  newWorkOrderRules: string
  existingWorkOrderRules: string
  threadMatchingRules: string
  lowConfidenceHandling: string
  ignoreRejectRules: string
}

export function DecisionRulesPanel({
  form,
  onChange,
  onSave,
}: {
  form: DecisionRulesForm
  onChange: (patch: Partial<DecisionRulesForm>) => void
  onSave: () => void
}) {
  return (
    <section data-testid="email-intake-decisions-panel" className="space-y-5">
      <article className="surface-card grid gap-4 xl:grid-cols-2">
        <SmartTextarea label="New work order rules" value={form.newWorkOrderRules} onChange={(value) => onChange({ newWorkOrderRules: value })} minRows={3} maxRows={6} description="Create new work orders when no thread match is found." />
        <SmartTextarea label="Existing work order update rules" value={form.existingWorkOrderRules} onChange={(value) => onChange({ existingWorkOrderRules: value })} minRows={3} maxRows={6} description="Update an open work order when the same thread or reference is found." />
        <SmartTextarea label="Thread matching rules" value={form.threadMatchingRules} onChange={(value) => onChange({ threadMatchingRules: value })} minRows={3} maxRows={6} description="Match by message-id, in-reply-to, and ticket references." />
        <SmartTextarea label="Low-confidence handling" value={form.lowConfidenceHandling} onChange={(value) => onChange({ lowConfidenceHandling: value })} minRows={3} maxRows={6} description="Route to manual review when confidence drops below threshold." />
        <SmartTextarea label="Ignore/reject rules" value={form.ignoreRejectRules} onChange={(value) => onChange({ ignoreRejectRules: value })} minRows={3} maxRows={6} description="Skip auto-replies, newsletters, and blocked senders." className="xl:col-span-2" />
      </article>

      <button type="button" className="button-primary" onClick={onSave}>Save Decision Rules</button>
    </section>
  )
}
