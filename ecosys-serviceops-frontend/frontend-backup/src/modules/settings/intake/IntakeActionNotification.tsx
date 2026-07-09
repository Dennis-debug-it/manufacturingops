import type { ReactNode } from 'react'
import type { NotificationActionConfig } from './types'

const notificationTokens = ['{{workOrderNumber}}', '{{title}}', '{{priority}}', '{{status}}', '{{clientName}}', '{{siteName}}']

export function IntakeActionNotification({
  config,
  onChange,
}: {
  config: NotificationActionConfig
  onChange: (value: NotificationActionConfig) => void
}) {
  return (
    <div className="mt-4 space-y-4 rounded-[28px] border border-app bg-white/5 p-4">
      <div>
        <p className="text-sm font-semibold text-app">Notification rules</p>
        <p className="mt-1 text-sm text-muted">Choose who gets notified and define the message template used after a protocol match.</p>
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        <ToggleRow label="Notify via Email" checked={config.notifyViaEmail} onChange={(value) => onChange({ ...config, notifyViaEmail: value })} />
        <ToggleRow label="Notify via In-app" checked={config.notifyViaInApp} onChange={(value) => onChange({ ...config, notifyViaInApp: value })} />
        <ToggleRow label="Notify group" checked={config.notifyGroup} onChange={(value) => onChange({ ...config, notifyGroup: value })} />
        <ToggleRow label="Notify assigned user" checked={config.notifyAssignedUser} onChange={(value) => onChange({ ...config, notifyAssignedUser: value })} />
        <ToggleRow label="Notify branch manager" checked={config.notifyBranchManager} onChange={(value) => onChange({ ...config, notifyBranchManager: value })} />
      </div>
      <Field label="Custom email recipients">
        <input value={config.customEmailRecipients} onChange={(event) => onChange({ ...config, customEmailRecipients: event.target.value })} className="field-input" placeholder="ops@example.com, manager@example.com" />
      </Field>
      <Field label="Message template">
        <textarea value={config.messageTemplate} onChange={(event) => onChange({ ...config, messageTemplate: event.target.value })} className="field-input min-h-[120px]" />
      </Field>
      <div className="panel-subtle rounded-[24px] p-4">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Allowed template variables</p>
        <div className="mt-3 flex flex-wrap gap-2">
          {notificationTokens.map((token) => (
            <span key={token} className="rounded-full border border-app bg-white/5 px-3 py-1 text-xs font-semibold text-accent">
              {token}
            </span>
          ))}
        </div>
      </div>
    </div>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
    </label>
  )
}

function ToggleRow({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (value: boolean) => void
}) {
  return (
    <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
      <span className="text-sm text-app">{label}</span>
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
    </label>
  )
}
