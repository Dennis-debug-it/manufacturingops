import { useEffect, useState } from 'react'
import { format } from 'date-fns'
import { Archive } from 'lucide-react'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import type { RetireAssetPayload } from '../api'

const REASONS = [
  'No longer under support',
  'End of life',
  'Replaced',
  'Disposed or sold',
  'Removed from client site',
]

const fieldClass = 'w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'

export interface RetireAssetDialogProps {
  open: boolean
  /** Number of assets being retired; drives the wording. */
  count: number
  assetName?: string
  /** Open work orders on the asset(s), shown as a warning. They are left open. */
  openWorkOrders?: number
  defaultReason?: string
  submitting?: boolean
  onCancel: () => void
  onConfirm: (payload: RetireAssetPayload) => void
}

export function RetireAssetDialog({ open, count, assetName, openWorkOrders = 0, defaultReason = '', submitting = false, onCancel, onConfirm }: RetireAssetDialogProps) {
  const today = format(new Date(), 'yyyy-MM-dd')
  const [reason, setReason] = useState(defaultReason)
  const [date, setDate] = useState(today)

  useEffect(() => {
    if (open) {
      setReason(defaultReason)
      setDate(format(new Date(), 'yyyy-MM-dd'))
    }
  }, [open, defaultReason])

  const subject = count === 1 ? (assetName ? `"${assetName}"` : 'this asset') : `${count} assets`
  const trimmedReason = reason.trim()
  const dateValid = Boolean(date) && date <= today

  return (
    <ConfirmDialog
      open={open}
      title={count === 1 ? 'Retire asset' : `Retire ${count} assets`}
      message={`Retiring removes ${subject} from the active register and stops automatic PM scheduling. History is kept, and you can reactivate it later.`}
      confirmLabel={submitting ? 'Retiring…' : count === 1 ? 'Retire Asset' : `Retire ${count} Assets`}
      confirmDisabled={submitting || !trimmedReason || !dateValid}
      danger
      icon={<Archive className="mt-1 h-5 w-5 text-rose-600" />}
      onCancel={onCancel}
      onConfirm={() => onConfirm({ reason: trimmedReason, decommissionDate: new Date(date).toISOString() })}
    >
      <div className="mt-4 space-y-3">
        {openWorkOrders > 0 ? (
          <p className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
            {openWorkOrders} open work order{openWorkOrders === 1 ? '' : 's'} will stay open. Close or cancel {openWorkOrders === 1 ? 'it' : 'them'} separately if no longer needed.
          </p>
        ) : null}
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="retire-reason">Reason</label>
          <div className="mb-2 flex flex-wrap gap-1.5">
            {REASONS.map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => setReason(option)}
                className={`rounded-full border px-2.5 py-1 text-xs transition ${reason === option ? 'border-[var(--app-primary)] bg-[var(--app-primary-soft)] text-slate-900' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'}`}
              >
                {option}
              </button>
            ))}
          </div>
          <textarea id="retire-reason" value={reason} maxLength={500} rows={2} onChange={(event) => setReason(event.target.value)} className={`${fieldClass} py-2`} />
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="retire-date">Decommission Date</label>
          <input id="retire-date" type="date" max={today} value={date} onChange={(event) => setDate(event.target.value)} className={`${fieldClass} h-11`} />
          {!dateValid ? <p className="mt-1 text-xs text-rose-600">Choose a date that is not in the future.</p> : null}
        </div>
      </div>
    </ConfirmDialog>
  )
}
