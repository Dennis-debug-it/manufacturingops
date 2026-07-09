import { SmartTextarea } from '../../../components/ui/SettingsWorkspace'

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
        <SmartTextarea label="Extracted fields" value={form.extractedFields} onChange={(value) => onChange({ extractedFields: value })} minRows={3} maxRows={6} description="One field per line keeps parser output readable." className="mt-4" />
      </article>

      <article className="surface-card grid gap-4 xl:grid-cols-2">
        <SmartTextarea label="Keyword mappings" value={form.keywordMappings} onChange={(value) => onChange({ keywordMappings: value })} minRows={3} maxRows={6} description="Map phrases to parser outcomes." />
        <SmartTextarea label="Priority keywords" value={form.priorityKeywords} onChange={(value) => onChange({ priorityKeywords: value })} minRows={3} maxRows={6} description="Examples: critical, outage, urgent." />
        <SmartTextarea label="Service category keywords" value={form.serviceCategoryKeywords} onChange={(value) => onChange({ serviceCategoryKeywords: value })} minRows={3} maxRows={6} description="Examples: electrical, HVAC, plumbing, fire safety." className="xl:col-span-2" />
      </article>

      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Sample Parser Output</h3>
        <p className="mt-1 text-sm text-muted">Use a sample output block to validate parser expectations.</p>
        <SmartTextarea label="Sample output" value={form.sampleOutput} onChange={(value) => onChange({ sampleOutput: value })} minRows={4} maxRows={8} description="Keep the example close to the real payload you expect." className="mt-4 font-mono text-xs" />
        <button type="button" className="button-primary mt-4" onClick={onSave}>Save Parser Settings</button>
      </article>
    </section>
  )
}
