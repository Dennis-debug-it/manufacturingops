export type MailboxConnectionForm = {
  intakeEnabled: boolean
  mailboxDisplayName: string
  intakeEmailAddress: string
  connectionType: 'IMAP' | 'Microsoft Graph' | 'Gmail API' | 'Generic API'
  serverHost: string
  port: number
  username: string
  password: string
  useSslTls: boolean
  folderToMonitor: string
  pollingIntervalSeconds: number
  markProcessedAsRead: boolean
  moveProcessedEmails: boolean
  processedFolderName: string
  failedFolderName: string
}

function Field({
  label,
  helper,
  children,
}: {
  label: string
  helper?: string
  children: ReactNode
}) {
  return (
    <label className="space-y-2">
      <span className="text-sm font-semibold text-app">{label}</span>
      {children}
      {helper ? <p className="text-xs text-muted">{helper}</p> : null}
    </label>
  )
}

export function MailboxConnectionPanel({
  form,
  saving,
  testing,
  syncing,
  onChange,
  onSave,
  onTest,
  onSync,
}: {
  form: MailboxConnectionForm
  saving: boolean
  testing: boolean
  syncing: boolean
  onChange: (patch: Partial<MailboxConnectionForm>) => void
  onSave: () => void
  onTest: () => void
  onSync: () => void
}) {
  return (
    <section data-testid="email-intake-mailbox-panel" className="space-y-5">
      <article className="surface-card">
        <div className="flex items-center justify-between gap-3">
          <div>
            <h3 className="text-lg font-semibold text-app">Connection Details</h3>
            <p className="mt-1 text-sm text-muted">Define where incoming intake emails are monitored.</p>
          </div>
          <label className="panel-subtle inline-flex items-center gap-2 rounded-2xl px-3 py-2 text-sm text-app">
            <input
              data-testid="email-intake-enable-toggle"
              type="checkbox"
              checked={form.intakeEnabled}
              onChange={(event) => onChange({ intakeEnabled: event.target.checked })}
            />
            Intake enabled
          </label>
        </div>

        <div className="mt-4 grid gap-4 lg:grid-cols-2">
          <Field label="Mailbox display name">
            <input className="field-input" value={form.mailboxDisplayName} onChange={(event) => onChange({ mailboxDisplayName: event.target.value })} />
          </Field>
          <Field label="Intake email address">
            <input className="field-input" type="email" value={form.intakeEmailAddress} onChange={(event) => onChange({ intakeEmailAddress: event.target.value })} />
          </Field>
          <Field label="Connection type">
            <select className="field-input" value={form.connectionType} onChange={(event) => onChange({ connectionType: event.target.value as MailboxConnectionForm['connectionType'] })}>
              <option value="IMAP">IMAP</option>
              <option value="Microsoft Graph">Microsoft Graph (Coming Soon)</option>
              <option value="Gmail API">Gmail API (Coming Soon)</option>
              <option value="Generic API">Generic API (Coming Soon)</option>
            </select>
          </Field>
          <Field label="Folder to monitor">
            <input className="field-input" value={form.folderToMonitor} onChange={(event) => onChange({ folderToMonitor: event.target.value })} placeholder="INBOX" />
          </Field>
        </div>
      </article>

      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Authentication</h3>
        <p className="mt-1 text-sm text-muted">Use credentials for the monitored mailbox connection.</p>

        <div className="mt-4 grid gap-4 lg:grid-cols-2">
          <Field label="Server host">
            <input className="field-input" value={form.serverHost} onChange={(event) => onChange({ serverHost: event.target.value })} placeholder="imap.example.com" />
          </Field>
          <Field label="Port">
            <input className="field-input" type="number" value={form.port} onChange={(event) => onChange({ port: Number(event.target.value) || 0 })} />
          </Field>
          <Field label="Username">
            <input className="field-input" value={form.username} onChange={(event) => onChange({ username: event.target.value })} />
          </Field>
          <Field label="Password / secret" helper="Stored as a masked secret once saved.">
            <input className="field-input" type="password" value={form.password} onChange={(event) => onChange({ password: event.target.value })} />
          </Field>
          <label className="panel-subtle inline-flex items-center gap-2 rounded-2xl px-3 py-2 text-sm text-app lg:col-span-2">
            <input type="checkbox" checked={form.useSslTls} onChange={(event) => onChange({ useSslTls: event.target.checked })} />
            Use SSL/TLS
          </label>
        </div>
      </article>

      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Processing Behaviour</h3>
        <p className="mt-1 text-sm text-muted">Control how processed and failed messages are handled.</p>

        <div className="mt-4 grid gap-4 lg:grid-cols-2">
          <Field label="Polling interval (seconds)">
            <input className="field-input" type="number" value={form.pollingIntervalSeconds} onChange={(event) => onChange({ pollingIntervalSeconds: Number(event.target.value) || 60 })} />
          </Field>
          <Field label="Processed folder name">
            <input className="field-input" value={form.processedFolderName} onChange={(event) => onChange({ processedFolderName: event.target.value })} />
          </Field>
          <Field label="Failed folder name">
            <input className="field-input" value={form.failedFolderName} onChange={(event) => onChange({ failedFolderName: event.target.value })} />
          </Field>
          <div className="space-y-2">
            <label className="panel-subtle inline-flex w-full items-center gap-2 rounded-2xl px-3 py-2 text-sm text-app">
              <input type="checkbox" checked={form.markProcessedAsRead} onChange={(event) => onChange({ markProcessedAsRead: event.target.checked })} />
              Mark processed as read
            </label>
            <label className="panel-subtle inline-flex w-full items-center gap-2 rounded-2xl px-3 py-2 text-sm text-app">
              <input type="checkbox" checked={form.moveProcessedEmails} onChange={(event) => onChange({ moveProcessedEmails: event.target.checked })} />
              Move processed emails
            </label>
          </div>
        </div>
      </article>

      <article className="surface-card">
        <h3 className="text-lg font-semibold text-app">Actions</h3>
        <p className="mt-1 text-sm text-muted">Save settings, validate credentials, and manually trigger intake sync.</p>

        <div className="mt-4 flex flex-wrap gap-3">
          <button type="button" className="button-primary" onClick={onSave} disabled={saving}>{saving ? 'Saving...' : 'Save Connection'}</button>
          <button data-testid="email-intake-test-button" type="button" className="button-secondary" onClick={onTest} disabled={testing}>{testing ? 'Testing...' : 'Test Connection'}</button>
          <button type="button" className="button-secondary" onClick={onSync} disabled={syncing}>{syncing ? 'Syncing...' : 'Run Manual Sync'}</button>
        </div>
      </article>
    </section>
  )
}
import type { ReactNode } from 'react'
