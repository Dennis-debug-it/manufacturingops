export type SimulationInput = {
  from: string
  to: string
  subject: string
  body: string
  attachmentsMock: string
}

export type SimulationStage = {
  key: string
  title: string
  value: string
}

export function SimulationPanel({
  input,
  running,
  stages,
  onChange,
  onRun,
}: {
  input: SimulationInput
  running: boolean
  stages: SimulationStage[]
  onChange: (patch: Partial<SimulationInput>) => void
  onRun: () => void
}) {
  return (
    <section data-testid="email-intake-simulation-panel" className="space-y-5">
      <article className="surface-card grid gap-4 xl:grid-cols-2">
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">From</span>
          <input className="field-input" value={input.from} onChange={(event) => onChange({ from: event.target.value })} />
        </label>
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">To</span>
          <input className="field-input" value={input.to} onChange={(event) => onChange({ to: event.target.value })} />
        </label>
        <label className="space-y-2 xl:col-span-2">
          <span className="text-sm font-semibold text-app">Subject</span>
          <input className="field-input" value={input.subject} onChange={(event) => onChange({ subject: event.target.value })} />
        </label>
        <label className="space-y-2 xl:col-span-2">
          <span className="text-sm font-semibold text-app">Body</span>
          <textarea className="field-input min-h-[140px]" value={input.body} onChange={(event) => onChange({ body: event.target.value })} />
        </label>
        <label className="space-y-2 xl:col-span-2">
          <span className="text-sm font-semibold text-app">Optional attachments mock</span>
          <textarea className="field-input min-h-[80px]" value={input.attachmentsMock} onChange={(event) => onChange({ attachmentsMock: event.target.value })} placeholder="filename.pdf, screenshot.png" />
        </label>
      </article>

      <button type="button" className="button-primary" onClick={onRun} disabled={running}>{running ? 'Running Simulation...' : 'Run Simulation'}</button>

      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Simulation Stages</h3>
        <p className="mt-1 text-sm text-muted">Review each stage before enabling automation in production.</p>
        <div className="mt-4 space-y-3">
          {stages.map((stage, index) => (
            <div key={stage.key} className="panel-subtle rounded-2xl p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{index + 1}. {stage.title}</p>
              <p className="mt-2 text-sm text-app">{stage.value}</p>
            </div>
          ))}
        </div>
      </article>
    </section>
  )
}
