import { useMemo, useState } from 'react'
import { EmptyState } from '../../../../components/ui/EmptyState'
import { ErrorState } from '../../../../components/ui/ErrorState'
import { LoadingState } from '../../../../components/ui/LoadingState'
import { useAsyncData } from '../../../../hooks/useAsyncData'
import { platformSettingsService } from '../../../../services/platformSettingsService'

const statusTone: Record<string, string> = {
  Sent: 'bg-emerald-500/15 text-emerald-700',
  Failed: 'bg-rose-500/15 text-rose-700',
  Skipped: 'bg-amber-500/15 text-amber-700',
  Pending: 'bg-slate-500/15 text-slate-700',
  Sending: 'bg-sky-500/15 text-sky-700',
  Retrying: 'bg-orange-500/15 text-orange-700',
}

export function EmailDeliveryLogsPanel() {
  const [status, setStatus] = useState('')
  const [search, setSearch] = useState('')
  const { data, loading, error, reload } = useAsyncData(
    () => platformSettingsService.listDeliveryLogs({ status: status || undefined, recipientEmail: search || undefined }),
    [],
    [status, search],
  )

  const rows = useMemo(() => data, [data])

  if (loading) return <LoadingState label="Loading delivery logs" />
  if (error) return <ErrorState title="Unable to load delivery logs" description={error} />

  return (
    <section className="space-y-4">
      <div className="grid gap-3 md:grid-cols-[220px_minmax(0,1fr)_auto]">
        <label className="block space-y-2">
          <span className="text-sm font-medium text-app">Status</span>
          <select value={status} onChange={(event) => setStatus(event.target.value)} className="field-input">
            <option value="">All statuses</option>
            <option value="Sent">Sent</option>
            <option value="Failed">Failed</option>
            <option value="Skipped">Skipped</option>
            <option value="Pending">Pending</option>
            <option value="Sending">Sending</option>
            <option value="Retrying">Retrying</option>
          </select>
        </label>
        <label className="block space-y-2">
          <span className="text-sm font-medium text-app">Search recipient or subject</span>
          <input value={search} onChange={(event) => setSearch(event.target.value)} className="field-input" placeholder="name@example.com or subject" />
        </label>
        <button type="button" className="button-secondary self-end" onClick={() => void reload()}>Refresh</button>
      </div>

      {rows.length === 0 ? (
        <EmptyState title="No delivery logs yet" description="Test emails, credential emails, and template tests will appear here as they are attempted." />
      ) : (
        <div className="space-y-3">
          {rows.map((row) => (
            <article key={row.id} className="rounded-[28px] border border-app bg-[var(--app-card)] p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="text-sm font-semibold text-app">{row.subject}</p>
                  <p className="mt-1 text-sm text-muted">{row.recipientEmail}</p>
                </div>
                <span className={`rounded-full px-3 py-1 text-xs font-semibold ${statusTone[row.status] || statusTone.Pending}`}>{row.status}</span>
              </div>
              <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <LogMeta label="Date / Time" value={new Date(row.createdAt).toLocaleString()} />
                <LogMeta label="Event" value={row.eventKey} mono />
                <LogMeta label="Template" value={row.templateKey} mono />
                <LogMeta label="Triggered By" value={row.triggeredByUserId || 'System'} mono={!row.triggeredByUserId} />
                <LogMeta label="Attempts" value={String(row.attemptCount ?? 0)} />
                <LogMeta label="Last Attempt" value={row.lastAttemptAt ? new Date(row.lastAttemptAt).toLocaleString() : 'Not yet attempted'} />
                <LogMeta label="Next Attempt" value={row.nextAttemptAt ? new Date(row.nextAttemptAt).toLocaleString() : 'None scheduled'} />
                <LogMeta label="Sent Time" value={row.sentAt ? new Date(row.sentAt).toLocaleString() : 'Not sent'} />
              </div>
              {row.errorMessage ? <p className="mt-4 text-sm text-rose-700">{row.errorMessage}</p> : null}
            </article>
          ))}
        </div>
      )}
    </section>
  )
}

function LogMeta({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="rounded-2xl border border-app bg-app/40 px-3 py-3">
      <p className="text-[11px] uppercase tracking-[0.16em] text-muted">{label}</p>
      <p className={`mt-2 text-sm text-app ${mono ? 'font-mono' : ''}`}>{value}</p>
    </div>
  )
}
