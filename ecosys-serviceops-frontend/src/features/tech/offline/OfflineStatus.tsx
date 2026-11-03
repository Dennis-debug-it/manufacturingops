import { useState } from 'react'
import { createPortal } from 'react-dom'
import { useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, CheckCircle2, CloudOff, RefreshCw, X } from 'lucide-react'
import { useOnlineStatus } from '../../../shared/hooks/useOnlineStatus'
import { cn } from '../../../shared/utils/cn'
import { formatRelative } from '../../../shared/utils/dates'
import { refreshAfterSync, useOutbox, useSyncNow } from './hooks'
import { removeOutboxEntry, retryOutboxEntry } from './outbox'

/**
 * Header pill for the technician app: offline / syncing / N waiting / N didn't sync. Tapping it
 * lists what is waiting on this phone, and lets the technician retry or discard what the server
 * refused (e.g. a job reassigned while they were offline).
 */
export function OfflineStatus() {
  const online = useOnlineStatus()
  const { entries, syncState } = useOutbox()
  const syncNow = useSyncNow()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const pending = entries.filter((entry) => entry.status === 'pending')
  const failed = entries.filter((entry) => entry.status === 'failed')

  if (online && entries.length === 0) return null

  const label = failed.length > 0
    ? `${failed.length} didn't sync`
    : !online
      ? pending.length > 0 ? `Offline · ${pending.length} waiting` : 'Offline'
      : syncState === 'syncing' ? 'Syncing…' : `${pending.length} waiting to sync`

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        data-testid="offline-status"
        className={cn(
          'inline-flex h-9 items-center gap-1.5 rounded-lg px-2.5 text-xs font-semibold',
          failed.length > 0 ? 'bg-rose-50 text-rose-700 ring-1 ring-rose-200' : !online ? 'bg-amber-50 text-amber-800 ring-1 ring-amber-200' : 'bg-sky-50 text-sky-700 ring-1 ring-sky-200',
        )}
      >
        {failed.length > 0 ? <AlertTriangle className="h-3.5 w-3.5" /> : !online ? <CloudOff className="h-3.5 w-3.5" /> : <RefreshCw className={cn('h-3.5 w-3.5', syncState === 'syncing' && 'animate-spin')} />}
        {label}
      </button>

      {/* Portalled: the sticky header's backdrop blur would otherwise trap this fixed sheet inside it. */}
      {open ? createPortal(
        <div className="fixed inset-0 z-[60] flex items-end bg-slate-900/40" onClick={() => setOpen(false)}>
          <div role="dialog" aria-label="Changes on this phone" className="mx-auto max-h-[80vh] w-full max-w-xl overflow-y-auto rounded-t-2xl bg-white p-4 pb-[max(env(safe-area-inset-bottom),1rem)]" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-slate-900">Changes on this phone</h2>
              <button type="button" aria-label="Close" onClick={() => setOpen(false)} className="rounded-lg p-1.5 text-slate-500"><X className="h-4 w-4" /></button>
            </div>
            <p className="mt-1 text-xs text-slate-500">
              {online
                ? 'They are sent in the order you made them, with the time you made them.'
                : 'You are offline. Keep working; these are sent automatically when the signal is back.'}
            </p>

            {entries.length === 0 ? (
              <p className="mt-4 flex items-center gap-2 text-sm text-emerald-700"><CheckCircle2 className="h-4 w-4" />Everything is synced.</p>
            ) : (
              <ul className="mt-3 divide-y divide-slate-100">
                {entries.map((entry) => (
                  <li key={entry.id} className="py-2.5" data-testid="outbox-entry">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-900">{entry.label}</p>
                        <p className="text-xs text-slate-500">{formatRelative(entry.occurredAt)}</p>
                      </div>
                      <span className={cn('shrink-0 rounded-full px-2 py-0.5 text-[11px] font-medium', entry.status === 'failed' ? 'bg-rose-50 text-rose-700' : 'bg-slate-100 text-slate-600')}>
                        {entry.status === 'failed' ? "Didn't sync" : 'Waiting'}
                      </span>
                    </div>
                    {entry.status === 'failed' ? (
                      <div className="mt-1.5 space-y-1.5">
                        <p className="text-xs text-rose-700">{entry.error}</p>
                        <div className="flex gap-2">
                          <button type="button" onClick={() => void retryOutboxEntry(entry.id, () => refreshAfterSync(queryClient))} className="h-8 rounded-lg border border-slate-300 px-3 text-xs font-medium text-slate-700">Try again</button>
                          <button type="button" onClick={() => void removeOutboxEntry(entry.id).then(() => refreshAfterSync(queryClient))} className="h-8 rounded-lg border border-rose-200 px-3 text-xs font-medium text-rose-700">Discard</button>
                        </div>
                      </div>
                    ) : null}
                  </li>
                ))}
              </ul>
            )}

            {online && pending.length > 0 ? (
              <button type="button" onClick={() => void syncNow()} disabled={syncState === 'syncing'} className="mt-3 inline-flex h-11 w-full items-center justify-center gap-2 rounded-lg bg-[var(--app-primary)] text-sm font-semibold text-white disabled:opacity-60">
                <RefreshCw className={cn('h-4 w-4', syncState === 'syncing' && 'animate-spin')} />{syncState === 'syncing' ? 'Syncing…' : 'Sync now'}
              </button>
            ) : null}
          </div>
        </div>,
        document.body,
      ) : null}
    </>
  )
}
