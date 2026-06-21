export type ParserKeywordsForm = {
  extractedFields: string
  keywordMappings: string
  priorityKeywords: string
  serviceCategoryKeywords: string
  sampleOutput: string
}

export function ParserKeywordsPanel({
  form,
  onChange,
  onSave,
}: {
  form: ParserKeywordsForm
  onChange: (patch: Partial<ParserKeywordsForm>) => void
  onSave: () => void
}) {
  return (
    <section data-testid="email-intake-parser-panel" className="space-y-5">
      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Extracted Fields</h3>
        <p className="mt-1 text-sm text-muted">Define which fields are pulled from each incoming message.</p>
        <textarea className="field-input mt-4 min-h-[120px]" value={form.extractedFields} onChange={(event) => onChange({ extractedFields: event.target.value })} />
      </article>

      <article className="surface-card grid gap-4 xl:grid-cols-2">
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">Keyword mappings</span>
          <textarea className="field-input min-h-[140px]" value={form.keywordMappings} onChange={(event) => onChange({ keywordMappings: event.target.value })} />
        </label>
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">Priority keywords</span>
          <textarea className="field-input min-h-[140px]" value={form.priorityKeywords} onChange={(event) => onChange({ priorityKeywords: event.target.value })} />
        </label>
        <label className="space-y-2 xl:col-span-2">
          <span className="text-sm font-semibold text-app">Service category keywords</span>
          <textarea className="field-input min-h-[120px]" value={form.serviceCategoryKeywords} onChange={(event) => onChange({ serviceCategoryKeywords: event.target.value })} />
        </label>
      </article>

      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Sample Parser Output</h3>
        <p className="mt-1 text-sm text-muted">Use a sample output block to validate parser expectations.</p>
        <textarea className="field-input mt-4 min-h-[180px] font-mono text-xs" value={form.sampleOutput} onChange={(event) => onChange({ sampleOutput: event.target.value })} />
        <button type="button" className="button-primary mt-4" onClick={onSave}>Save Parser Settings</button>
      </article>
    </section>
  )
}
