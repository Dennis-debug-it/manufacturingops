import { DataTable } from '../../../components/ui/DataTable'

export type IntakeRuleRow = {
  id: string
  type: string
  pattern: string
  effect: string
}

export type IntakeRulesForm = {
  allowedSenders: string
  blockedSenders: string
  subjectBodyFilters: IntakeRuleRow[]
  ignoreRules: IntakeRuleRow[]
  attachmentRules: IntakeRuleRow[]
  priorityKeywordRules: IntakeRuleRow[]
}

export function IntakeRulesPanel({
  form,
  onChange,
  onSave,
}: {
  form: IntakeRulesForm
  onChange: (patch: Partial<IntakeRulesForm>) => void
  onSave: () => void
}) {
  return (
    <section data-testid="email-intake-rules-panel" className="space-y-5">
      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Sender Controls</h3>
        <p className="mt-1 text-sm text-muted">Define who is allowed into intake and who is blocked.</p>
        <div className="mt-4 grid gap-4 lg:grid-cols-2">
          <label className="space-y-2">
            <span className="text-sm font-semibold text-app">Allowed senders/domains</span>
            <textarea className="field-input min-h-[120px]" value={form.allowedSenders} onChange={(event) => onChange({ allowedSenders: event.target.value })} placeholder="one domain or sender per line" />
          </label>
          <label className="space-y-2">
            <span className="text-sm font-semibold text-app">Blocked senders/domains</span>
            <textarea className="field-input min-h-[120px]" value={form.blockedSenders} onChange={(event) => onChange({ blockedSenders: event.target.value })} placeholder="one domain or sender per line" />
          </label>
        </div>
      </article>

      <article className="surface-card space-y-4">
        <h3 className="text-lg font-semibold text-app">Subject and Body Filters</h3>
        <DataTable
          rows={form.subjectBodyFilters}
          rowKey={(row) => row.id}
          pageSize={6}
          emptyTitle="No subject/body filters"
          emptyDescription="Add filters to include or ignore specific patterns."
          minTableWidth="min-w-[820px] w-full"
          columns={[
            { key: 'type', header: 'Type', cell: (row) => row.type },
            { key: 'pattern', header: 'Pattern', cell: (row) => row.pattern },
            { key: 'effect', header: 'Effect', cell: (row) => row.effect },
          ]}
        />
      </article>

      <article className="surface-card space-y-4">
        <h3 className="text-lg font-semibold text-app">Ignore and Attachment Rules</h3>
        <div className="grid gap-4 xl:grid-cols-2">
          <div>
            <p className="mb-3 text-sm font-semibold text-app">Auto-reply/newsletter/spam ignore rules</p>
            <DataTable
              rows={form.ignoreRules}
              rowKey={(row) => row.id}
              pageSize={5}
              emptyTitle="No ignore rules"
              emptyDescription="Use ignore rules to skip routine mailers."
              minTableWidth="min-w-[520px] w-full"
              columns={[
                { key: 'type', header: 'Type', cell: (row) => row.type },
                { key: 'pattern', header: 'Pattern', cell: (row) => row.pattern },
              ]}
            />
          </div>
          <div>
            <p className="mb-3 text-sm font-semibold text-app">Attachment rules</p>
            <DataTable
              rows={form.attachmentRules}
              rowKey={(row) => row.id}
              pageSize={5}
              emptyTitle="No attachment rules"
              emptyDescription="Control accepted attachments and limits."
              minTableWidth="min-w-[520px] w-full"
              columns={[
                { key: 'type', header: 'Type', cell: (row) => row.type },
                { key: 'pattern', header: 'Rule', cell: (row) => row.pattern },
                { key: 'effect', header: 'Effect', cell: (row) => row.effect },
              ]}
            />
          </div>
        </div>
      </article>

      <article className="surface-card space-y-4">
        <h3 className="text-lg font-semibold text-app">Priority Keyword Rules</h3>
        <DataTable
          rows={form.priorityKeywordRules}
          rowKey={(row) => row.id}
          pageSize={6}
          emptyTitle="No priority rules"
          emptyDescription="Map incoming keywords to work order priority."
          minTableWidth="min-w-[760px] w-full"
          columns={[
            { key: 'pattern', header: 'Keyword', cell: (row) => row.pattern },
            { key: 'effect', header: 'Priority', cell: (row) => row.effect },
          ]}
        />
        <button type="button" className="button-primary" onClick={onSave}>Save Intake Rules</button>
      </article>
    </section>
  )
}
