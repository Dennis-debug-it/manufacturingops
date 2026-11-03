import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Search, Undo2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ErrorState } from '../../shared/components/ErrorState'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { formatDateTime } from '../../shared/utils/dates'
import { useWorkOrders } from '../work-orders/api'
import { usePartsReturns, useReceivePartsReturn, useRejectPartsReturn, type PartsReturn } from '../work-orders/partsReturnsApi'
import { conditionLabels } from '../work-orders/components/PartsReturnCard'
import { ReturnPartsDrawer } from '../work-orders/components/PartsReturnsSection'

const statuses = ['Pending', 'Received', 'Rejected', 'All'] as const
const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

/**
 * Stores' queue of parts coming back from jobs, as a table. Pending first, since those are waiting
 * at the counter; stores can also record a return a technician hands in without raising one.
 */
export function PartsReturnsPage() {
  const canProcess = usePermission('parts-returns.receive')
  const canCreate = usePermission('parts-returns.create')
  const [status, setStatus] = useState<(typeof statuses)[number]>('Pending')
  const [search, setSearch] = useState('')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [recording, setRecording] = useState(false)
  const { data, isLoading, isError, refetch } = usePartsReturns(status)

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase()
    return (data ?? []).filter((row) =>
      !term || [row.returnNumber, row.workOrderNumber, row.requestedByName, ...row.lines.map((line) => line.itemName)].some((value) => value?.toLowerCase().includes(term)),
    )
  }, [data, search])
  const selected = (data ?? []).find((row) => row.id === selectedId) ?? null

  const columns: DataTableColumn<PartsReturn>[] = [
    {
      key: 'returnNumber',
      header: 'Return',
      cell: (row) => <button type="button" onClick={() => setSelectedId(row.id)} className="font-mono text-xs font-semibold text-slate-900 underline decoration-slate-300 underline-offset-4">{row.returnNumber}</button>,
      sortValue: (row) => row.returnNumber,
    },
    { key: 'workOrder', header: 'Work order', cell: (row) => (row.workOrderNumber ? <Link to={`/work-orders/${row.workOrderId}`} className="underline decoration-slate-300 underline-offset-4">{row.workOrderNumber}</Link> : '—'), sortValue: (row) => row.workOrderNumber },
    { key: 'parts', header: 'Parts', cell: (row) => <span className="text-slate-700">{row.lines.map((line) => `${line.itemName} × ${line.quantity}`).join(', ')}</span> },
    {
      key: 'condition',
      header: 'Condition',
      cell: (row) => (
        <span className="flex flex-wrap gap-1">
          {Array.from(new Set(row.lines.map((line) => line.condition))).map((condition) => (
            <span key={condition} className={cn('rounded-full px-2 py-0.5 text-xs font-medium', conditionLabels[condition].tone)}>{conditionLabels[condition].label}</span>
          ))}
        </span>
      ),
    },
    { key: 'requestedBy', header: 'Raised by', cell: (row) => row.requestedByName ?? '—', sortValue: (row) => row.requestedByName },
    { key: 'requestedAt', header: 'Raised', cell: (row) => formatDateTime(row.requestedAt), sortValue: (row) => row.requestedAt },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} />, sortValue: (row) => row.status },
    {
      key: 'open',
      header: '',
      cell: (row) => (
        <button type="button" onClick={() => setSelectedId(row.id)} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
          {canProcess && row.status === 'Pending' ? 'Receive' : 'View'}
        </button>
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Returns"
        subtitle="Unused and faulty parts coming back from jobs, and old parts removed from client equipment."
        primaryAction={
          canCreate ? (
            <button type="button" onClick={() => setRecording(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Undo2 className="h-4 w-4" />Record return
            </button>
          ) : undefined
        }
      />

      <div className="flex flex-wrap items-center gap-2">
        {statuses.map((value) => (
          <button
            key={value}
            type="button"
            aria-pressed={status === value}
            onClick={() => setStatus(value)}
            className={cn('inline-flex h-9 items-center rounded-lg border px-3 text-sm font-medium transition', status === value ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}
          >
            {value === 'Pending' ? 'Waiting to receive' : value}
          </button>
        ))}
        <label className="ml-auto flex h-10 min-w-[14rem] items-center gap-2 rounded-lg border border-slate-300 bg-white px-3">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search return, job or part" aria-label="Search returns" className="w-full border-0 bg-transparent p-0 text-sm outline-none" />
        </label>
      </div>

      {isError ? (
        <ErrorState title="Returns unavailable" description="Parts returns could not be loaded." onRetry={() => void refetch()} />
      ) : (
        <DataTable
          columns={columns}
          data={rows}
          loading={isLoading}
          emptyTitle={status === 'Pending' ? 'Nothing waiting to be received' : 'No returns'}
          emptyDescription="Technicians raise returns from a work order's Parts tab. Stores can record one with Record return."
        />
      )}

      {selected ? <ReturnPanel key={selected.id} partsReturn={selected} canProcess={canProcess} onClose={() => setSelectedId(null)} /> : null}
      {recording ? <RecordReturn onClose={() => setRecording(false)} /> : null}
    </div>
  )
}

function ReturnPanel({ partsReturn, canProcess, onClose }: { partsReturn: PartsReturn; canProcess: boolean; onClose: () => void }) {
  const receive = useReceivePartsReturn()
  const reject = useRejectPartsReturn()
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const pending = partsReturn.status === 'Pending'
  const busy = receive.isPending || reject.isPending

  const doReceive = async () => {
    try {
      await receive.mutateAsync(partsReturn.id)
      toast.success(`${partsReturn.returnNumber} received`)
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const doReject = async () => {
    if (!reason.trim()) return void toast.error('Give the technician a reason')
    try {
      await reject.mutateAsync({ id: partsReturn.id, reason: reason.trim() })
      toast.success(`${partsReturn.returnNumber} rejected`)
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={`Return ${partsReturn.returnNumber}`}
      onClose={onClose}
      footer={
        <div className="flex flex-wrap items-center justify-end gap-2">
          {rejecting ? (
            <>
              <button type="button" onClick={() => setRejecting(false)} disabled={busy} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">Back</button>
              <button type="button" onClick={() => void doReject()} disabled={busy} className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-700 hover:bg-rose-50 disabled:opacity-60">Reject return</button>
            </>
          ) : (
            <>
              <button type="button" onClick={onClose} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">Close</button>
              {canProcess && pending ? (
                <>
                  <button type="button" onClick={() => setRejecting(true)} disabled={busy} className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-700 hover:bg-rose-50">Reject</button>
                  <button type="button" onClick={() => void doReceive()} disabled={busy} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">{receive.isPending ? 'Receiving…' : 'Receive into stores'}</button>
                </>
              ) : null}
            </>
          )}
        </div>
      }
    >
      <div className="space-y-4">
        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
          <dt className="text-slate-500">Work order</dt>
          <dd>{partsReturn.workOrderNumber ? <Link to={`/work-orders/${partsReturn.workOrderId}`} className="underline decoration-slate-300 underline-offset-4">{partsReturn.workOrderNumber}</Link> : '—'}</dd>
          <dt className="text-slate-500">Raised by</dt><dd>{partsReturn.requestedByName ?? '—'}</dd>
          <dt className="text-slate-500">Raised</dt><dd>{formatDateTime(partsReturn.requestedAt)}</dd>
          <dt className="text-slate-500">Status</dt><dd><StatusBadge status={partsReturn.status} /></dd>
          {partsReturn.processedAt ? (<><dt className="text-slate-500">{partsReturn.status} by</dt><dd>{partsReturn.processedByName ?? 'stores'} · {formatDateTime(partsReturn.processedAt)}</dd></>) : null}
        </dl>
        {partsReturn.notes ? <p className="rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-700">{partsReturn.notes}</p> : null}
        {partsReturn.rejectionReason ? <p className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">Rejected: {partsReturn.rejectionReason}</p> : null}

        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500">
              <tr><th className="px-3 py-2">Part</th><th className="px-3 py-2 text-right">Qty</th><th className="px-3 py-2">Condition</th><th className="px-3 py-2">What happens</th></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {partsReturn.lines.map((line) => (
                <tr key={line.id}>
                  <td className="px-3 py-2">{line.itemName}{line.notes ? <span className="block text-xs text-slate-500">{line.notes}</span> : null}</td>
                  <td className="px-3 py-2 text-right tabular-nums">{line.quantity}</td>
                  <td className="px-3 py-2"><span className={cn('rounded-full px-2 py-0.5 text-xs font-medium', conditionLabels[line.condition].tone)}>{conditionLabels[line.condition].label}</span></td>
                  <td className="px-3 py-2 text-xs text-slate-500">{conditionLabels[line.condition].hint}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {rejecting ? (
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="return-reject-reason">Reason (the technician sees this)</label>
            <textarea id="return-reject-reason" value={reason} onChange={(event) => setReason(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
            <p className="mt-1 text-xs text-slate-500">Nothing changes on the job or in stock. The technician can raise a corrected return.</p>
          </div>
        ) : pending && canProcess ? (
          <p className="text-xs text-slate-500">Receive only once the parts are physically back in stores.</p>
        ) : null}
      </div>
    </FormDrawer>
  )
}

/** Stores records a return at the counter: pick the job, then the same form technicians use. */
function RecordReturn({ onClose }: { onClose: () => void }) {
  const { data: workOrders } = useWorkOrders()
  const [workOrderId, setWorkOrderId] = useState('')

  return (
    <ReturnPartsDrawer
      key={workOrderId || 'none'}
      open
      workOrderId={workOrderId}
      onClose={onClose}
      workOrderPicker={
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="return-wo">Work order</label>
          <select id="return-wo" value={workOrderId} onChange={(event) => setWorkOrderId(event.target.value)} className={fieldClass}>
            <option value="">Select the job the parts came from…</option>
            {(workOrders ?? []).map((wo) => <option key={wo.id} value={wo.id}>{wo.workOrderNumber} — {wo.title}</option>)}
          </select>
        </div>
      }
    />
  )
}
