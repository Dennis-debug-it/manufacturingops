import { useState } from 'react'
import toast from 'react-hot-toast'
import { Lightbulb } from 'lucide-react'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import type { WorkOrder } from '../work-orders/types'
import { savedMessage, useTechActions } from './offline/actions'

/**
 * "Spotted more work?" A technician on site is often the first to see a failing unit or an upgrade
 * the client needs; this passes it to the sales team as a lead without leaving the job. Works offline.
 */
export function TechSalesLead({ job }: { job: Pick<WorkOrder, 'id' | 'workOrderNumber'> }) {
  const { canAccessPath } = useEntitlements()
  const actions = useTechActions()
  const [open, setOpen] = useState(false)
  const [note, setNote] = useState('')

  if (!canAccessPath('/crm')) return null

  const send = async () => {
    const text = note.trim()
    if (!text) {
      toast.error('Say what the client could use')
      return
    }
    try {
      toast.success(savedMessage('Sent to the sales team', await actions.flagSalesLead(job, text)))
      setNote('')
      setOpen(false)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4" data-testid="tech-sales-lead">
      {open ? (
        <div className="space-y-3">
          <label htmlFor="sales-lead-note" className="block text-sm font-medium text-slate-900">What could this client use?</label>
          <textarea
            id="sales-lead-note"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            rows={3}
            maxLength={1000}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-base outline-none focus:border-[var(--app-primary)]"
            placeholder="e.g. Chiller 2 compressor is near end of life; they'll need a replacement"
          />
          <div className="flex gap-2">
            <button type="button" disabled={actions.busy} onClick={() => void send()} className="inline-flex h-11 flex-1 items-center justify-center rounded-lg bg-[var(--app-primary)] text-sm font-semibold text-white disabled:opacity-60">
              Send to sales
            </button>
            <button type="button" onClick={() => setOpen(false)} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-300 px-4 text-sm font-medium text-slate-700">
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <button type="button" onClick={() => setOpen(true)} className="flex w-full items-center gap-3 text-left">
          <Lightbulb className="h-5 w-5 shrink-0 text-amber-500" />
          <span>
            <span className="block text-sm font-medium text-slate-900">Spotted more work for this client?</span>
            <span className="block text-xs text-slate-500">Flag it and the sales team will follow it up.</span>
          </span>
        </button>
      )}
    </section>
  )
}
