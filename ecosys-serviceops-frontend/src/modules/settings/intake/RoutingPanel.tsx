import { DataTable } from '../../../components/ui/DataTable'

export type RoutingRule = {
  id: string
  order: number
  condition: string
  assignmentGroup: string
  outcome: string
}

export type RoutingForm = {
  rules: RoutingRule[]
  fallbackAssignmentGroup: string
  useManualDispatchQueue: boolean
}

export function RoutingPanel({
  form,
  onChange,
  onSave,
}: {
  form: RoutingForm
  onChange: (patch: Partial<RoutingForm>) => void
  onSave: () => void
}) {
  return (
    <section data-testid="email-intake-routing-panel" className="space-y-5">
      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Routing Rules</h3>
        <p className="mt-1 text-sm text-muted">Set routing priority and destination groups after work order creation.</p>
        <div className="mt-4">
          <DataTable
            rows={form.rules}
            rowKey={(row) => row.id}
            pageSize={8}
            emptyTitle="No routing rules"
            emptyDescription="Add at least one routing rule for email intake automation."
            minTableWidth="min-w-[920px] w-full"
            columns={[
              { key: 'order', header: 'Priority', cell: (row) => row.order },
              { key: 'condition', header: 'Condition', cell: (row) => row.condition },
              { key: 'group', header: 'Assignment Group', cell: (row) => row.assignmentGroup },
              { key: 'outcome', header: 'Outcome', cell: (row) => row.outcome },
            ]}
          />
        </div>
      </article>

      <article className="surface-card grid gap-4 md:grid-cols-2">
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">Fallback assignment group</span>
          <input className="field-input" value={form.fallbackAssignmentGroup} onChange={(event) => onChange({ fallbackAssignmentGroup: event.target.value })} />
        </label>
        <label className="panel-subtle inline-flex items-center gap-2 rounded-2xl px-3 py-2 text-sm text-app">
          <input type="checkbox" checked={form.useManualDispatchQueue} onChange={(event) => onChange({ useManualDispatchQueue: event.target.checked })} />
          Route unmatched work to manual dispatch queue
        </label>
      </article>

      <button type="button" className="button-primary" onClick={onSave}>Save Routing Rules</button>
    </section>
  )
}
