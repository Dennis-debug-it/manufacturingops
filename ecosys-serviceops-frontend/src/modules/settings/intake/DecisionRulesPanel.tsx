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
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">New work order rules</span>
          <textarea className="field-input min-h-[120px]" value={form.newWorkOrderRules} onChange={(event) => onChange({ newWorkOrderRules: event.target.value })} />
        </label>
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">Existing work order update rules</span>
          <textarea className="field-input min-h-[120px]" value={form.existingWorkOrderRules} onChange={(event) => onChange({ existingWorkOrderRules: event.target.value })} />
        </label>
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">Thread matching rules</span>
          <textarea className="field-input min-h-[120px]" value={form.threadMatchingRules} onChange={(event) => onChange({ threadMatchingRules: event.target.value })} />
        </label>
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">Low-confidence handling</span>
          <textarea className="field-input min-h-[120px]" value={form.lowConfidenceHandling} onChange={(event) => onChange({ lowConfidenceHandling: event.target.value })} />
        </label>
        <label className="space-y-2 xl:col-span-2">
          <span className="text-sm font-semibold text-app">Ignore/reject rules</span>
          <textarea className="field-input min-h-[120px]" value={form.ignoreRejectRules} onChange={(event) => onChange({ ignoreRejectRules: event.target.value })} />
        </label>
      </article>

      <button type="button" className="button-primary" onClick={onSave}>Save Decision Rules</button>
    </section>
  )
}
