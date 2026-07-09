import { SmartTextarea } from '../../../components/ui/SettingsWorkspace'

export type MatchingForm = {
  clientMatching: string
  contactMatching: string
  siteMatching: string
  assetMatching: string
  confidenceThreshold: number
  manualReviewFallback: boolean
}

export function MatchingPanel({
  form,
  onChange,
  onSave,
}: {
  form: MatchingForm
  onChange: (patch: Partial<MatchingForm>) => void
  onSave: () => void
}) {
  return (
    <section data-testid="email-intake-matching-panel" className="space-y-5">
      <article className="surface-card grid gap-4 xl:grid-cols-2">
        <SmartTextarea label="Client matching" value={form.clientMatching} onChange={(value) => onChange({ clientMatching: value })} minRows={3} maxRows={6} description="Match sender domains before falling back to manual review." />
        <SmartTextarea label="Contact matching" value={form.contactMatching} onChange={(value) => onChange({ contactMatching: value })} minRows={3} maxRows={6} description="Match sender email to known client contacts." />
        <SmartTextarea label="Site / branch matching" value={form.siteMatching} onChange={(value) => onChange({ siteMatching: value })} minRows={3} maxRows={6} description="Use aliases from the email subject or body." />
        <SmartTextarea label="Asset matching" value={form.assetMatching} onChange={(value) => onChange({ assetMatching: value })} minRows={3} maxRows={6} description="Match serial numbers, tags, or other asset keywords." />
      </article>

      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Confidence and Fallback</h3>
        <div className="mt-4 grid gap-4 md:grid-cols-2">
          <label className="space-y-2">
            <span className="text-sm font-semibold text-app">Confidence threshold (%)</span>
            <input className="field-input" type="number" min={1} max={100} value={form.confidenceThreshold} onChange={(event) => onChange({ confidenceThreshold: Number(event.target.value) || 0 })} />
          </label>
          <label className="panel-subtle inline-flex items-center gap-2 rounded-2xl px-3 py-2 text-sm text-app">
            <input type="checkbox" checked={form.manualReviewFallback} onChange={(event) => onChange({ manualReviewFallback: event.target.checked })} />
            Route low-confidence matches to manual review
          </label>
        </div>

        <button type="button" className="button-primary mt-4" onClick={onSave}>Save Matching Rules</button>
      </article>
    </section>
  )
}
