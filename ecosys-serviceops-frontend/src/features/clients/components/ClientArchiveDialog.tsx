import { useMemo } from 'react'
import { Archive, X } from 'lucide-react'
import type { Client } from '../types'

interface ClientArchiveDialogProps {
  open: boolean
  client: Client | null
  onCancel: () => void
  onConfirm: () => void
}

export function ClientArchiveDialog({ open, client, onCancel, onConfirm }: ClientArchiveDialogProps) {
  const impacts = useMemo(() => {
    const siteCount = client?.siteCount ?? 0
    const assetCount = client?.assetCount ?? 0
    const openWorkOrderCount = client?.openWorkOrderCount ?? 0

    return {
      hasImpact: siteCount > 0 || assetCount > 0 || openWorkOrderCount > 0,
      siteCount,
      assetCount,
      openWorkOrderCount,
    }
  }, [client])

  if (!open) {
    return null
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-6">
      <button
        type="button"
        aria-label="Close dialog"
        className="absolute inset-0 bg-[var(--app-primary)]/30 backdrop-blur-[2px]"
        onClick={onCancel}
      />
      <div className="relative w-full max-w-lg rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white-strong)] p-6 shadow-xl backdrop-blur-[var(--glass-blur-lg)]">
        <div className="flex items-start justify-between gap-4">
          <div className="flex items-start gap-3">
            <Archive className="mt-1 h-5 w-5 text-amber-600" />
            <div>
              <h3 className="text-lg font-semibold text-slate-900">Archive client</h3>
              <p className="mt-1 text-sm leading-6 text-slate-600">
                Archive "{client?.clientName}"? The client will be hidden from active lists but their history is retained.
              </p>
            </div>
          </div>
          <button type="button" onClick={onCancel} className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900">
            <X className="h-4 w-4" />
          </button>
        </div>

        {impacts.hasImpact && (
          <div className="mt-4 space-y-2 rounded-lg bg-amber-50 p-3 text-sm">
            <p className="font-medium text-amber-900">This will also archive:</p>
            <ul className="space-y-1 text-amber-800">
              {impacts.siteCount > 0 && <li>• {impacts.siteCount} site(s)</li>}
              {impacts.assetCount > 0 && <li>• {impacts.assetCount} asset(s)</li>}
              {impacts.openWorkOrderCount > 0 && (
                <li className="text-amber-900">
                  ⚠ {impacts.openWorkOrderCount} open work order(s) will be affected
                </li>
              )}
            </ul>
          </div>
        )}

        <div className="mt-6 flex items-center justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="inline-flex h-11 items-center justify-center rounded-lg border border-[var(--app-border)] bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-[var(--app-primary-soft)]"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={onConfirm}
            className="inline-flex h-11 items-center gap-2 justify-center rounded-lg bg-amber-600 px-4 text-sm font-medium text-white transition hover:bg-amber-700"
          >
            <Archive className="h-4 w-4" />
            Archive
          </button>
        </div>
      </div>
    </div>
  )
}
