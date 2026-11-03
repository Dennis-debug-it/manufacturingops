import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ErrorState } from '../../shared/components/ErrorState'
import { api } from '../../shared/api/apiClient'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useBranches } from './api'
import { useSuppliers } from './purchaseOrdersApi'

export type HeldPartStatus = 'Held' | 'ReturnedToSupplier' | 'WrittenOff' | 'Restocked'
type HeldPartAction = 'ReturnToSupplier' | 'WriteOff' | 'Restock'

export interface HeldPart {
  id: string
  materialItemId: string
  itemCode: string | null
  itemName: string | null
  unitOfMeasure: string | null
  quantity: number
  unitCost: number | null
  status: HeldPartStatus
  branchId: string | null
  branchName: string | null
  sourceReference: string | null
  workOrderId: string | null
  workOrderNumber: string | null
  notes: string | null
  supplierId: string | null
  supplierName: string | null
  resolutionReference: string | null
  resolutionNote: string | null
  resolvedByName: string | null
  resolvedAt: string | null
  createdAt: string
}

const statusLabels: Record<HeldPartStatus, string> = {
  Held: 'Held',
  ReturnedToSupplier: 'Returned to supplier',
  WrittenOff: 'Written off',
  Restocked: 'Back in stock',
}

export function useHeldParts(status: HeldPartStatus | 'All') {
  return useQuery({
    queryKey: ['held-parts', status],
    queryFn: () => api.get<HeldPart[]>('/api/held-parts', status === 'All' ? undefined : { status }),
    staleTime: 30_000,
  })
}

function useResolveHeldPart() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { id: string; action: HeldPartAction; quantity: number; supplierId?: string | null; reference?: string | null; note?: string | null; branchId?: string | null }) =>
      api.post<HeldPart>(`/api/held-parts/${payload.id}/resolve`, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['held-parts'] })
      void queryClient.invalidateQueries({ queryKey: ['materials'] })
    },
  })
}

const filters: Array<HeldPartStatus | 'All'> = ['Held', 'ReturnedToSupplier', 'WrittenOff', 'Restocked', 'All']

/**
 * Faulty parts received back from jobs. They come off the job but not back into stock, so they wait
 * here until they go back to the supplier, are written off, or are found sound and restocked.
 */
export function FaultyPartsPage() {
  const canIssue = usePermission('inventory.requests.issue')
  const canManage = usePermission('inventory.materials.manage')
  const canResolve = canIssue || canManage
  const [status, setStatus] = useState<HeldPartStatus | 'All'>('Held')
  const [selected, setSelected] = useState<HeldPart | null>(null)
  const { data, isLoading, isError, refetch } = useHeldParts(status)
  const heldValue = (data ?? []).filter((row) => row.status === 'Held').reduce((sum, row) => sum + row.quantity * (row.unitCost ?? 0), 0)

  const columns: DataTableColumn<HeldPart>[] = [
    { key: 'part', header: 'Part', cell: (row) => <Link to={`/inventory/parts/${row.materialItemId}`} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{row.itemName ?? 'Part'}</Link>, sortValue: (row) => row.itemName },
    { key: 'qty', header: 'Qty', cell: (row) => <span className="tabular-nums">{row.quantity} {row.unitOfMeasure ?? ''}</span>, sortValue: (row) => row.quantity },
    { key: 'value', header: 'Value', cell: (row) => (row.unitCost != null ? formatCurrency(row.quantity * row.unitCost) : '—'), sortValue: (row) => row.quantity * (row.unitCost ?? 0) },
    { key: 'from', header: 'From', cell: (row) => <span>{row.sourceReference ?? '—'}{row.workOrderId ? <> · <Link to={`/work-orders/${row.workOrderId}`} className="underline decoration-slate-300 underline-offset-4">{row.workOrderNumber}</Link></> : null}</span> },
    { key: 'branch', header: 'Held at', cell: (row) => row.branchName ?? '—', sortValue: (row) => row.branchName },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={statusLabels[row.status]} />, sortValue: (row) => row.status },
    {
      key: 'outcome',
      header: 'Outcome',
      cell: (row) => (row.status === 'Held' ? <span className="text-slate-500">Since {formatDate(row.createdAt)}</span> : (
        <span className="text-slate-600">
          {row.status === 'ReturnedToSupplier' ? `${row.supplierName ?? 'Supplier'}${row.resolutionReference ? ` · ${row.resolutionReference}` : ''}` : row.resolutionNote ?? row.resolutionReference ?? ''}
          {row.resolvedAt ? <span className="block text-xs text-slate-400">{formatDate(row.resolvedAt)}{row.resolvedByName ? ` · ${row.resolvedByName}` : ''}</span> : null}
        </span>
      )),
    },
    {
      key: 'act',
      header: '',
      cell: (row) => (canResolve && row.status === 'Held' ? (
        <button type="button" onClick={() => setSelected(row)} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Deal with it</button>
      ) : null),
    },
  ]

  return (
    <div className="space-y-4">
      <PageHeader title="Faulty parts" subtitle="Faulty parts returned from jobs and held in stores: send them back to the supplier, write them off, or restock them if they turn out fine." />
      <div className="flex flex-wrap items-center gap-2">
        {filters.map((value) => (
          <button key={value} type="button" aria-pressed={status === value} onClick={() => setStatus(value)} className={cn('inline-flex h-9 items-center rounded-lg border px-3 text-sm font-medium transition', status === value ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}>
            {value === 'All' ? 'All' : statusLabels[value]}
          </button>
        ))}
        {status === 'Held' && heldValue > 0 ? <span className="ml-auto text-sm text-slate-600">Held value <strong className="tabular-nums text-slate-900">{formatCurrency(heldValue)}</strong></span> : null}
      </div>
      {isError ? (
        <ErrorState title="Faulty parts unavailable" description="Held parts could not be loaded." onRetry={() => void refetch()} />
      ) : (
        <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle={status === 'Held' ? 'Nothing held' : 'None'} emptyDescription="Parts received back from a job as Faulty appear here." />
      )}
      {selected ? <ResolveDrawer part={selected} onClose={() => setSelected(null)} /> : null}
    </div>
  )
}

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

function ResolveDrawer({ part, onClose }: { part: HeldPart; onClose: () => void }) {
  const resolve = useResolveHeldPart()
  const { data: suppliers } = useSuppliers()
  const { data: branches } = useBranches()
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)
  const [action, setAction] = useState<HeldPartAction>('ReturnToSupplier')
  const [quantity, setQuantity] = useState(String(part.quantity))
  const [supplierId, setSupplierId] = useState('')
  const [reference, setReference] = useState('')
  const [note, setNote] = useState('')
  const [branchId, setBranchId] = useState(part.branchId ?? '')

  const submit = async () => {
    const qty = Number(quantity)
    if (!Number.isFinite(qty) || qty <= 0 || qty > part.quantity) return void toast.error(`Quantity must be more than 0 and at most ${part.quantity}`)
    if (action === 'ReturnToSupplier' && !supplierId) return void toast.error('Choose the supplier')
    if (action === 'WriteOff' && !note.trim()) return void toast.error('Give a reason for writing it off')
    try {
      await resolve.mutateAsync({ id: part.id, action, quantity: qty, supplierId: supplierId || null, reference: reference.trim() || null, note: note.trim() || null, branchId: branchId || null })
      toast.success(action === 'ReturnToSupplier' ? 'Recorded as returned to the supplier' : action === 'WriteOff' ? 'Written off' : 'Back in stock')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={`${part.itemName ?? 'Part'} · ${part.quantity} ${part.unitOfMeasure ?? ''}`}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={resolve.isPending} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">{resolve.isPending ? 'Saving…' : 'Save'}</button>
        </div>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-slate-600">From {part.sourceReference ?? 'a parts return'}{part.workOrderNumber ? ` on ${part.workOrderNumber}` : ''}.{part.notes ? ` Technician's note: ${part.notes}` : ''}</p>
        <div className="grid gap-2" role="radiogroup" aria-label="What happened">
          {([
            ['ReturnToSupplier', 'Return to supplier', 'Going back under warranty or for credit.'],
            ['WriteOff', 'Write off', 'Scrapped; no stock changes.'],
            ['Restock', 'Back into stock', 'Tested or repaired and fit to use.'],
          ] as const).map(([value, label, hint]) => (
            <label key={value} className={cn('flex cursor-pointer items-start gap-2 rounded-lg border p-3 text-sm', action === value ? 'border-[var(--app-primary)] bg-slate-50' : 'border-slate-200')}>
              <input type="radio" name="held-action" checked={action === value} onChange={() => setAction(value)} className="mt-0.5" />
              <span><span className="font-medium text-slate-900">{label}</span><span className="block text-xs text-slate-500">{hint}</span></span>
            </label>
          ))}
        </div>
        <div>
          <label className={labelClass} htmlFor="held-qty">Quantity</label>
          <input id="held-qty" type="number" min="0" max={part.quantity} step="any" value={quantity} onChange={(event) => setQuantity(event.target.value)} className={fieldClass} />
          <p className="mt-1 text-xs text-slate-500">Less than {part.quantity} leaves the rest held.</p>
        </div>
        {action === 'ReturnToSupplier' ? (
          <>
            <div>
              <label className={labelClass} htmlFor="held-supplier">Supplier</label>
              <select id="held-supplier" value={supplierId} onChange={(event) => setSupplierId(event.target.value)} className={fieldClass}>
                <option value="">Select a supplier…</option>
                {(suppliers ?? []).map((supplier) => <option key={supplier.id} value={supplier.id}>{supplier.supplierName}</option>)}
              </select>
            </div>
            <div>
              <label className={labelClass} htmlFor="held-ref">RMA / return reference (optional)</label>
              <input id="held-ref" value={reference} onChange={(event) => setReference(event.target.value)} className={fieldClass} />
            </div>
          </>
        ) : null}
        {action === 'Restock' && activeBranches.length > 1 ? (
          <div>
            <label className={labelClass} htmlFor="held-branch">Back into</label>
            <select id="held-branch" value={branchId} onChange={(event) => setBranchId(event.target.value)} className={fieldClass}>
              {activeBranches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
            </select>
          </div>
        ) : null}
        <div>
          <label className={labelClass} htmlFor="held-note">{action === 'WriteOff' ? 'Reason (required)' : 'Note (optional)'}</label>
          <input id="held-note" value={note} onChange={(event) => setNote(event.target.value)} placeholder={action === 'WriteOff' ? 'e.g. Burnt out, not covered by warranty' : ''} className={fieldClass} />
        </div>
      </div>
    </FormDrawer>
  )
}
