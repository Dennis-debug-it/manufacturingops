export type IntakeNotificationsForm = {
  notifyDispatchGroup: boolean
  notifyAssignedGroup: boolean
  notifyTenantAdmin: boolean
  notifyOperationsRecipients: string
  notifySlaEscalationRecipients: string
  notifySenderReceived: boolean
}

export function IntakeNotificationsPanel({
  form,
  onChange,
  onSave,
}: {
  form: IntakeNotificationsForm
  onChange: (patch: Partial<IntakeNotificationsForm>) => void
  onSave: () => void
}) {
  return (
    <section data-testid="email-intake-notifications-panel" className="space-y-5">
      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Intake Notifications</h3>
        <p className="mt-1 text-sm text-muted">Choose who receives operational alerts after intake actions complete.</p>

        <div className="mt-4 grid gap-3">
          <label className="panel-subtle inline-flex items-center gap-2 rounded-2xl px-3 py-2 text-sm text-app">
            <input type="checkbox" checked={form.notifyDispatchGroup} onChange={(event) => onChange({ notifyDispatchGroup: event.target.checked })} />
            Notify dispatch group
          </label>
          <label className="panel-subtle inline-flex items-center gap-2 rounded-2xl px-3 py-2 text-sm text-app">
            <input type="checkbox" checked={form.notifyAssignedGroup} onChange={(event) => onChange({ notifyAssignedGroup: event.target.checked })} />
            Notify assigned group
          </label>
          <label className="panel-subtle inline-flex items-center gap-2 rounded-2xl px-3 py-2 text-sm text-app">
            <input type="checkbox" checked={form.notifyTenantAdmin} onChange={(event) => onChange({ notifyTenantAdmin: event.target.checked })} />
            Notify tenant admin
          </label>
          <label className="panel-subtle inline-flex items-center gap-2 rounded-2xl px-3 py-2 text-sm text-app">
            <input type="checkbox" checked={form.notifySenderReceived} onChange={(event) => onChange({ notifySenderReceived: event.target.checked })} />
            Notify sender request was received
          </label>
        </div>
      </article>

      <article className="surface-card grid gap-4 md:grid-cols-2">
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">Operations recipients</span>
          <textarea className="field-input min-h-[100px]" value={form.notifyOperationsRecipients} onChange={(event) => onChange({ notifyOperationsRecipients: event.target.value })} placeholder="one email per line" />
        </label>
        <label className="space-y-2">
          <span className="text-sm font-semibold text-app">SLA escalation recipients (high priority)</span>
          <textarea className="field-input min-h-[100px]" value={form.notifySlaEscalationRecipients} onChange={(event) => onChange({ notifySlaEscalationRecipients: event.target.value })} placeholder="one email per line" />
        </label>
      </article>

      <button type="button" className="button-primary" onClick={onSave}>Save Notification Preferences</button>
    </section>
  )
}
