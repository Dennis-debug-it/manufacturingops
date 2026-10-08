import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, X } from 'lucide-react'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { cn } from '../../shared/utils/cn'
import { formatDateTime } from '../../shared/utils/dates'
import { useMaterialOptions } from '../work-orders/pickerApi'
import { useCreatePartsRequest, useDeliverPartsRequest, usePartsRequestsForWorkOrder, type PartsRequestStatus } from '../work-orders/partsRequestsApi'
import {
  useCreatePartsReturn,
  usePartsReturnsForWorkOrder,
  useReturnableParts,
  type PartsReturnCondition,
  type PartsReturnStatus,
} from '../work-orders/partsReturnsApi'

const inputClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const STATUS_TONE: Record<PartsRequestStatus | PartsReturnStatus, string> = {
  Pending: 'bg-amber-50 text-amber-700 ring-amber-200',
  Approved: 'bg-sky-50 text-sky-700 ring-sky-200',
  Dispatched: 'bg-indigo-50 text-indigo-700 ring-indigo-200',
  Delivered: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  Received: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  Rejected: 'bg-rose-50 text-rose-700 ring-rose-200',
}

function StatusPill({ status }: { status: PartsRequestStatus | PartsReturnStatus }) {
  return <span className={cn('shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset', STATUS_TONE[status] ?? 'bg-slate-50 text-slate-700 ring-slate-200')}>{status}</span>
}

type DraftLine = { key: number; inventoryItemId: string | null; label: string; quantity: number }

/** Ask stores for parts the job needs; the office approves and dispatches, the technician confirms delivery. */
function PartsRequests({ workOrderId, editable }: { workOrderId: string; editable: boolean }) {
  const { data: requests, isLoading } = usePartsRequestsForWorkOrder(workOrderId)
  const create = useCreatePartsRequest(workOrderId)
  const deliver = useDeliverPartsRequest(workOrderId)
  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 250)
  const { data: options } = useMaterialOptions(debouncedSearch)
  const [materialId, setMaterialId] = useState('')
  const [freeText, setFreeText] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [lines, setLines] = useState<DraftLine[]>([])
  const [notes, setNotes] = useState('')
  const qty = Number(quantity)
  const validQty = Number.isFinite(qty) && qty > 0
  const selected = (options ?? []).find((option) => option.id === materialId)

  const addLine = () => {
    if (!validQty) return
    if (selected) {
      setLines((current) => [...current, { key: Date.now(), inventoryItemId: selected.id, label: selected.itemName, quantity: qty }])
    } else if (freeText.trim()) {
      setLines((current) => [...current, { key: Date.now(), inventoryItemId: null, label: freeText.trim(), quantity: qty }])
    } else {
      return
    }
    setMaterialId('')
    setFreeText('')
    setSearch('')
    setQuantity('1')
  }

  const submit = async () => {
    try {
      await create.mutateAsync({
        notes: notes.trim() || null,
        items: lines.map((line) => ({ inventoryItemId: line.inventoryItemId, description: line.inventoryItemId ? null : line.label, quantityRequested: line.quantity })),
      })
      toast.success('Parts request sent to stores')
      setLines([])
      setNotes('')
      setOpen(false)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const confirmDelivery = async (id: string) => {
    try {
      await deliver.mutateAsync(id)
      toast.success('Delivery confirmed')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold text-slate-900">Parts requests</h2>
        {editable && !open ? (
          <button type="button" onClick={() => setOpen(true)} className="inline-flex h-9 items-center gap-1 rounded-lg border border-slate-300 px-3 text-xs font-medium text-slate-700">
            <Plus className="h-3.5 w-3.5" />Request parts
          </button>
        ) : null}
      </div>

      {isLoading ? <p className="text-sm text-slate-500">Loading…</p> : null}
      {requests && requests.length === 0 && !open ? <p className="text-sm text-slate-500">No parts requested for this job.</p> : null}
      {requests && requests.length > 0 ? (
        <ul className="space-y-2">
          {requests.map((request) => (
            <li key={request.id} className="rounded-lg border border-slate-200 p-3 text-sm">
              <div className="flex items-center justify-between gap-2">
                <span className="text-xs text-slate-500">{formatDateTime(request.requestedAt)}</span>
                <StatusPill status={request.status} />
              </div>
              <ul className="mt-1.5 space-y-0.5">
                {request.items.map((item) => (
                  <li key={item.id} className="flex justify-between gap-3">
                    <span className="text-slate-900">{item.itemName ?? item.description ?? 'Part'}</span>
                    <span className="shrink-0 text-slate-500">
                      {item.quantityApproved != null && item.quantityApproved !== item.quantityRequested ? `${item.quantityApproved} of ${item.quantityRequested}` : item.quantityRequested}
                    </span>
                  </li>
                ))}
              </ul>
              {request.status === 'Rejected' && request.rejectionReason ? <p className="mt-1.5 text-xs text-rose-700">{request.rejectionReason}</p> : null}
              {request.status === 'Dispatched' ? (
                <button type="button" disabled={deliver.isPending} onClick={() => void confirmDelivery(request.id)} className="mt-2 inline-flex h-9 w-full items-center justify-center rounded-lg bg-slate-900 text-xs font-semibold text-white disabled:opacity-60">
                  I've received these parts
                </button>
              ) : null}
            </li>
          ))}
        </ul>
      ) : null}

      {open ? (
        <div className="space-y-2 border-t border-slate-100 pt-3">
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search stock items" aria-label="Search stock items" className={inputClass} />
          <select value={materialId} onChange={(event) => setMaterialId(event.target.value)} aria-label="Stock item" className={inputClass}>
            <option value="">Select a stock item…</option>
            {(options ?? []).map((option) => (
              <option key={option.id} value={option.id}>{option.itemName} ({option.quantityOnHand} {option.unitOfMeasure} in stock)</option>
            ))}
          </select>
          {!selected ? (
            <input value={freeText} onChange={(event) => setFreeText(event.target.value)} placeholder="…or describe a part not in stock" aria-label="Part description" className={inputClass} />
          ) : null}
          <div className="flex gap-2">
            <input type="number" min={0.01} step="any" inputMode="decimal" value={quantity} onChange={(event) => setQuantity(event.target.value)} aria-label="Quantity requested" className={cn(inputClass, 'w-28')} />
            <button type="button" onClick={addLine} disabled={!validQty || (!selected && !freeText.trim())} className="ml-auto inline-flex h-10 items-center rounded-lg border border-slate-300 px-4 text-sm font-medium text-slate-700 disabled:opacity-50">
              Add to request
            </button>
          </div>
          {lines.length > 0 ? (
            <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200 text-sm">
              {lines.map((line) => (
                <li key={line.key} className="flex items-center justify-between gap-3 px-3 py-2">
                  <span className="text-slate-900">{line.label} × {line.quantity}</span>
                  <button type="button" aria-label={`Remove ${line.label}`} onClick={() => setLines((current) => current.filter((x) => x.key !== line.key))} className="text-slate-400"><X className="h-4 w-4" /></button>
                </li>
              ))}
            </ul>
          ) : null}
          <textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={2} placeholder="Notes for stores (optional)" aria-label="Parts request notes" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          <div className="flex gap-2">
            <button type="button" onClick={() => { setOpen(false); setLines([]) }} className="inline-flex h-10 flex-1 items-center justify-center rounded-lg border border-slate-300 text-sm font-medium text-slate-700">Cancel</button>
            <button type="button" onClick={() => void submit()} disabled={lines.length === 0 || create.isPending} className="inline-flex h-10 flex-1 items-center justify-center rounded-lg bg-[var(--app-primary)] text-sm font-semibold text-white disabled:opacity-50">
              {create.isPending ? 'Sending…' : 'Send request'}
            </button>
          </div>
        </div>
      ) : null}
    </section>
  )
}

const CONDITIONS: { value: PartsReturnCondition; label: string }[] = [
  { value: 'Unused', label: 'Unused' },
  { value: 'Faulty', label: 'Faulty' },
]

/** Hand back parts logged on the job but not fitted (or found faulty); stores receive them in the office. */
function PartsReturns({ workOrderId }: { workOrderId: string }) {
  const { data: returns } = usePartsReturnsForWorkOrder(workOrderId)
  const [open, setOpen] = useState(false)
  const { data: returnable, isLoading } = useReturnableParts(workOrderId, open)
  const create = useCreatePartsReturn(workOrderId)
  const [picked, setPicked] = useState<Record<string, { quantity: string; condition: PartsReturnCondition }>>({})
  const [notes, setNotes] = useState('')

  const lines = Object.entries(picked)
    .map(([usageId, line]) => ({ usageId, quantity: Number(line.quantity), condition: line.condition }))
    .filter((line) => Number.isFinite(line.quantity) && line.quantity > 0)

  const submit = async () => {
    const parts = returnable ?? []
    const tooMany = lines.find((line) => line.quantity > (parts.find((part) => part.workOrderMaterialUsageId === line.usageId)?.returnableQuantity ?? 0))
    if (tooMany) {
      toast.error('You cannot return more than was used')
      return
    }
    try {
      await create.mutateAsync({
        notes: notes.trim() || null,
        lines: lines.map((line) => ({ workOrderMaterialUsageId: line.usageId, materialItemId: null, quantity: line.quantity, condition: line.condition, notes: null })),
      })
      toast.success('Return sent to stores')
      setPicked({})
      setNotes('')
      setOpen(false)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold text-slate-900">Parts returns</h2>
        {!open ? (
          <button type="button" onClick={() => setOpen(true)} className="inline-flex h-9 items-center gap-1 rounded-lg border border-slate-300 px-3 text-xs font-medium text-slate-700">
            <Plus className="h-3.5 w-3.5" />Return parts
          </button>
        ) : null}
      </div>

      {returns && returns.length === 0 && !open ? <p className="text-sm text-slate-500">No parts returned from this job.</p> : null}
      {returns && returns.length > 0 ? (
        <ul className="space-y-2">
          {returns.map((item) => (
            <li key={item.id} className="rounded-lg border border-slate-200 p-3 text-sm">
              <div className="flex items-center justify-between gap-2">
                <span className="font-mono text-xs text-slate-500">{item.returnNumber}</span>
                <StatusPill status={item.status} />
              </div>
              <ul className="mt-1.5 space-y-0.5">
                {item.lines.map((line) => (
                  <li key={line.id} className="flex justify-between gap-3">
                    <span className="text-slate-900">{line.itemName}</span>
                    <span className="shrink-0 text-slate-500">{line.quantity} · {line.condition}</span>
                  </li>
                ))}
              </ul>
              {item.status === 'Rejected' && item.rejectionReason ? <p className="mt-1.5 text-xs text-rose-700">{item.rejectionReason}</p> : null}
            </li>
          ))}
        </ul>
      ) : null}

      {open ? (
        <div className="space-y-2 border-t border-slate-100 pt-3">
          {isLoading ? <p className="text-sm text-slate-500">Loading parts used…</p> : null}
          {returnable && returnable.filter((part) => part.returnableQuantity > 0).length === 0 ? (
            <p className="text-sm text-slate-500">Nothing to return: log the parts you used on the Finish tab first.</p>
          ) : null}
          {(returnable ?? []).filter((part) => part.returnableQuantity > 0).map((part) => {
            const line = picked[part.workOrderMaterialUsageId]
            return (
              <div key={part.workOrderMaterialUsageId} className="space-y-2 rounded-lg border border-slate-200 p-3 text-sm">
                <div className="flex justify-between gap-3">
                  <span className="text-slate-900">{part.itemName}</span>
                  <span className="shrink-0 text-xs text-slate-500">up to {part.returnableQuantity} {part.unitOfMeasure ?? ''}</span>
                </div>
                <div className="flex gap-2">
                  <input
                    type="number"
                    min={0}
                    max={part.returnableQuantity}
                    step="any"
                    inputMode="decimal"
                    placeholder="Qty"
                    value={line?.quantity ?? ''}
                    onChange={(event) => setPicked((current) => ({ ...current, [part.workOrderMaterialUsageId]: { quantity: event.target.value, condition: current[part.workOrderMaterialUsageId]?.condition ?? 'Unused' } }))}
                    aria-label={`Quantity of ${part.itemName} to return`}
                    className={cn(inputClass, 'w-24')}
                  />
                  <div className="grid flex-1 grid-cols-2 gap-1 rounded-lg border border-slate-200 p-1" role="radiogroup" aria-label={`Condition of ${part.itemName}`}>
                    {CONDITIONS.map((condition) => {
                      const active = (line?.condition ?? 'Unused') === condition.value
                      return (
                        <button
                          key={condition.value}
                          type="button"
                          role="radio"
                          aria-checked={active}
                          onClick={() => setPicked((current) => ({ ...current, [part.workOrderMaterialUsageId]: { quantity: current[part.workOrderMaterialUsageId]?.quantity ?? '', condition: condition.value } }))}
                          className={cn('h-8 rounded-md text-xs font-medium', active ? 'bg-slate-900 text-white' : 'text-slate-600')}
                        >
                          {condition.label}
                        </button>
                      )
                    })}
                  </div>
                </div>
              </div>
            )
          })}
          <textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={2} placeholder="Notes (optional)" aria-label="Parts return notes" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          <div className="flex gap-2">
            <button type="button" onClick={() => { setOpen(false); setPicked({}) }} className="inline-flex h-10 flex-1 items-center justify-center rounded-lg border border-slate-300 text-sm font-medium text-slate-700">Cancel</button>
            <button type="button" onClick={() => void submit()} disabled={lines.length === 0 || create.isPending} className="inline-flex h-10 flex-1 items-center justify-center rounded-lg bg-[var(--app-primary)] text-sm font-semibold text-white disabled:opacity-50">
              {create.isPending ? 'Sending…' : 'Send return'}
            </button>
          </div>
        </div>
      ) : null}
    </section>
  )
}

export function TechParts({ workOrderId, editable }: { workOrderId: string; editable: boolean }) {
  return (
    <div className="space-y-3">
      <PartsRequests workOrderId={workOrderId} editable={editable} />
      <PartsReturns workOrderId={workOrderId} />
    </div>
  )
}
