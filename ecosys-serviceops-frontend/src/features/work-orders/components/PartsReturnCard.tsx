import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDateTime } from '../../../shared/utils/dates'
import { useReceivePartsReturn, useRejectPartsReturn, type PartsReturn, type PartsReturnCondition } from '../partsReturnsApi'

export const conditionLabels: Record<PartsReturnCondition, { label: string; hint: string; tone: string }> = {
  Unused: { label: 'Unused', hint: 'Back to stock, not billed', tone: 'bg-emerald-50 text-emerald-700' },
  Faulty: { label: 'Faulty', hint: 'Not billed, not restocked', tone: 'bg-amber-50 text-amber-700' },
  Removed: { label: 'Removed old part', hint: 'Recorded only', tone: 'bg-slate-100 text-slate-600' },
}

/** A parts return with its lines; stores (materials.issue) can receive or reject it while Pending. */
export function PartsReturnCard({ partsReturn, showWorkOrder = false }: { partsReturn: PartsReturn; showWorkOrder?: boolean }) {
  const canProcess = usePermission('parts-returns.receive')
  const receive = useReceivePartsReturn()
  const reject = useRejectPartsReturn()
  const [action, setAction] = useState<'receive' | 'reject' | null>(null)
  const [reason, setReason] = useState('')

  const close = () => {
    setAction(null)
    setReason('')
  }

  const confirm = async () => {
    try {
      if (action === 'receive') {
        await receive.mutateAsync(partsReturn.id)
        toast.success(`${partsReturn.returnNumber} received`)
      } else if (action === 'reject') {
        if (!reason.trim()) {
          toast.error('A reason is required')
          return
        }
        await reject.mutateAsync({ id: partsReturn.id, reason: reason.trim() })
        toast.success(`${partsReturn.returnNumber} rejected`)
      }
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)]">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <div className="flex items-center gap-2">
            <span className="font-mono text-xs font-semibold text-slate-700">{partsReturn.returnNumber}</span>
            <StatusBadge status={partsReturn.status} />
          </div>
          <p className="mt-1 text-xs text-slate-500">
            {showWorkOrder && partsReturn.workOrderNumber ? (
              <>
                <Link to={`/work-orders/${partsReturn.workOrderId}`} className="font-medium text-[var(--app-primary)] hover:underline">{partsReturn.workOrderNumber}</Link>
                {' · '}
              </>
            ) : null}
            Raised by {partsReturn.requestedByName ?? 'technician'} · {formatDateTime(partsReturn.requestedAt)}
          </p>
        </div>
        {canProcess && partsReturn.status === 'Pending' ? (
          <div className="flex gap-2">
            <button type="button" onClick={() => setAction('receive')} className="inline-flex h-9 items-center rounded-lg bg-[var(--app-primary)] px-3 text-xs font-medium text-white transition hover:bg-[var(--app-secondary)]">Receive</button>
            <button type="button" onClick={() => setAction('reject')} className="inline-flex h-9 items-center rounded-lg border border-rose-200 bg-white px-3 text-xs font-medium text-rose-600 hover:bg-rose-50">Reject</button>
          </div>
        ) : null}
      </div>

      <div className="mt-3 space-y-1.5">
        {partsReturn.lines.map((line) => (
          <div key={line.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-slate-50 px-3 py-2 text-sm">
            <span className="text-slate-700">
              {line.itemName}
              {line.notes ? <span className="ml-1 text-xs text-slate-500">— {line.notes}</span> : null}
            </span>
            <span className="flex items-center gap-2 text-xs text-slate-500">
              <span className={`rounded-full px-2 py-0.5 font-medium ${conditionLabels[line.condition].tone}`}>{conditionLabels[line.condition].label}</span>
              × {line.quantity}
            </span>
          </div>
        ))}
      </div>

      {partsReturn.notes ? <p className="mt-3 text-xs text-slate-500">Notes: {partsReturn.notes}</p> : null}
      {partsReturn.processedAt ? (
        <p className="mt-2 text-xs text-slate-500">{partsReturn.status} by {partsReturn.processedByName ?? 'stores'} · {formatDateTime(partsReturn.processedAt)}</p>
      ) : null}
      {partsReturn.status === 'Rejected' && partsReturn.rejectionReason ? <p className="mt-1 text-xs text-rose-600">Rejected: {partsReturn.rejectionReason}</p> : null}

      <ConfirmDialog
        open={action !== null}
        title={action === 'receive' ? `Receive ${partsReturn.returnNumber}` : `Reject ${partsReturn.returnNumber}`}
        message={action === 'receive'
          ? 'Confirm the parts are physically back in stores. Unused parts go back into stock and come off the job (not billed); faulty parts come off the job but are not restocked.'
          : 'Nothing changes on the job or in stock. The technician can raise a corrected return.'}
        confirmLabel={action === 'receive' ? 'Receive' : 'Reject'}
        danger={action === 'reject'}
        confirmDisabled={receive.isPending || reject.isPending}
        onCancel={close}
        onConfirm={() => void confirm()}
      >
        {action === 'reject' ? (
          <textarea value={reason} onChange={(event) => setReason(event.target.value)} rows={3} placeholder="Reason" className="mt-3 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        ) : null}
      </ConfirmDialog>
    </div>
  )
}
