import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Trash2, Undo2 } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Spinner } from '../../../shared/components/Spinner'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { usePermission } from '../../../shared/hooks/usePermission'
import { useMaterialOptions } from '../pickerApi'
import {
  useCreatePartsReturn,
  usePartsReturnsForWorkOrder,
  useReturnableParts,
  type CreatePartsReturnPayload,
} from '../partsReturnsApi'
import { PartsReturnCard, conditionLabels } from './PartsReturnCard'

const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

interface UsedDraft {
  quantity: string
  condition: 'Unused' | 'Faulty'
}

interface RemovedDraft {
  key: string
  materialItemId: string
  quantity: string
  notes: string
}

const emptyRemoved = (): RemovedDraft => ({ key: crypto.randomUUID(), materialItemId: '', quantity: '1', notes: '' })

/**
 * Returns from the job back to stores: unused or faulty parts that were booked on this work order,
 * and old parts taken out of the client's equipment. Stores receives or rejects each return.
 */
export function PartsReturnsSection({ workOrderId }: { workOrderId: string }) {
  const canCreate = usePermission('parts-returns.create')
  const { data: returns, isLoading } = usePartsReturnsForWorkOrder(workOrderId)
  const createReturn = useCreatePartsReturn(workOrderId)

  const [open, setOpen] = useState(false)
  const { data: returnable, isLoading: returnableLoading } = useReturnableParts(workOrderId, open)
  const [used, setUsed] = useState<Record<string, UsedDraft>>({})
  const [removed, setRemoved] = useState<RemovedDraft[]>([])
  const [notes, setNotes] = useState('')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 250)
  const { data: materials } = useMaterialOptions(debouncedSearch)

  const close = () => {
    setOpen(false)
    setUsed({})
    setRemoved([])
    setNotes('')
    setSearch('')
  }

  const updateUsed = (usageId: string, patch: Partial<UsedDraft>) =>
    setUsed((current) => ({ ...current, [usageId]: { ...(current[usageId] ?? { quantity: '', condition: 'Unused' }), ...patch } }))
  const updateRemoved = (key: string, patch: Partial<RemovedDraft>) =>
    setRemoved((current) => current.map((row) => (row.key === key ? { ...row, ...patch } : row)))

  const submit = async () => {
    const lines: CreatePartsReturnPayload['lines'] = []
    for (const part of returnable ?? []) {
      const draft = used[part.workOrderMaterialUsageId]
      const quantity = Number(draft?.quantity)
      if (!draft || !Number.isFinite(quantity) || quantity <= 0) continue
      if (quantity > part.returnableQuantity) {
        toast.error(`Only ${part.returnableQuantity} ${part.itemName} can be returned`)
        return
      }
      lines.push({ workOrderMaterialUsageId: part.workOrderMaterialUsageId, materialItemId: null, quantity, condition: draft.condition, notes: null })
    }
    for (const row of removed) {
      const quantity = Number(row.quantity)
      if (!row.materialItemId || !Number.isFinite(quantity) || quantity <= 0) continue
      lines.push({ workOrderMaterialUsageId: null, materialItemId: row.materialItemId, quantity, condition: 'Removed', notes: row.notes.trim() || null })
    }

    if (lines.length === 0) {
      toast.error('Enter a quantity for at least one part')
      return
    }

    try {
      const created = await createReturn.mutateAsync({ notes: notes.trim() || null, lines })
      toast.success(`Return ${created.returnNumber} sent to stores`)
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <section className="space-y-3 border-t border-slate-200 pt-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold text-slate-900">Parts Returns</h3>
          <p className="text-sm text-slate-600">Send unused or faulty parts back to stores, and log old parts removed from the equipment.</p>
        </div>
        {canCreate ? (
          <button type="button" onClick={() => setOpen(true)} className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
            <Undo2 className="h-4 w-4" />
            Return Parts
          </button>
        ) : null}
      </div>

      {isLoading ? <Spinner label="Loading returns" /> : (returns ?? []).length === 0 ? (
        <p className="text-sm text-slate-500">No parts returned from this job.</p>
      ) : (
        <div className="space-y-3">
          {(returns ?? []).map((item) => <PartsReturnCard key={item.id} partsReturn={item} />)}
        </div>
      )}

      <FormDrawer
        open={open}
        title="Return Parts"
        onClose={close}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={() => void submit()} disabled={createReturn.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60">
              {createReturn.isPending ? 'Submitting…' : 'Send to Stores'}
            </button>
          </div>
        }
      >
        <div className="space-y-5">
          <div className="space-y-2">
            <p className="text-xs font-semibold uppercase tracking-[0.14em] text-slate-500">Parts used on this job</p>
            {returnableLoading ? <Spinner label="Loading parts" /> : (returnable ?? []).length === 0 ? (
              <p className="text-sm text-slate-500">No parts booked on this job can be returned.</p>
            ) : (returnable ?? []).map((part) => {
              const draft = used[part.workOrderMaterialUsageId]
              return (
                <div key={part.workOrderMaterialUsageId} className="rounded-xl border border-slate-200 p-3">
                  <div className="flex items-baseline justify-between gap-2">
                    <span className="text-sm font-medium text-slate-800">{part.itemName}</span>
                    <span className="text-xs text-slate-500">
                      Used {part.quantityUsed}{part.pendingReturnQuantity > 0 ? ` · ${part.pendingReturnQuantity} pending` : ''} · up to {part.returnableQuantity} {part.unitOfMeasure ?? ''}
                    </span>
                  </div>
                  <div className="mt-2 grid grid-cols-2 gap-2">
                    <input type="number" min="0" max={part.returnableQuantity} step="0.01" placeholder="Qty to return" value={draft?.quantity ?? ''} onChange={(event) => updateUsed(part.workOrderMaterialUsageId, { quantity: event.target.value })} className={fieldClass} />
                    <select value={draft?.condition ?? 'Unused'} onChange={(event) => updateUsed(part.workOrderMaterialUsageId, { condition: event.target.value as UsedDraft['condition'] })} className={fieldClass}>
                      <option value="Unused">Unused — {conditionLabels.Unused.hint}</option>
                      <option value="Faulty">Faulty — {conditionLabels.Faulty.hint}</option>
                    </select>
                  </div>
                </div>
              )
            })}
          </div>

          <div className="space-y-2">
            <p className="text-xs font-semibold uppercase tracking-[0.14em] text-slate-500">Old parts removed from the equipment</p>
            {removed.length > 0 ? (
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search inventory by name or code…" className={fieldClass} />
            ) : null}
            {removed.map((row) => (
              <div key={row.key} className="space-y-2 rounded-xl border border-slate-200 p-3">
                <div className="flex gap-2">
                  <select value={row.materialItemId} onChange={(event) => updateRemoved(row.key, { materialItemId: event.target.value })} className={fieldClass}>
                    <option value="">Select part</option>
                    {(materials ?? []).map((material) => <option key={material.id} value={material.id}>{material.itemName}</option>)}
                  </select>
                  <button type="button" onClick={() => setRemoved((current) => current.filter((item) => item.key !== row.key))} className="rounded p-2 text-slate-400 hover:bg-slate-100 hover:text-rose-600" aria-label="Remove row">
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
                <div className="grid grid-cols-3 gap-2">
                  <input type="number" min="0" step="0.01" value={row.quantity} onChange={(event) => updateRemoved(row.key, { quantity: event.target.value })} className={fieldClass} />
                  <input value={row.notes} onChange={(event) => updateRemoved(row.key, { notes: event.target.value })} placeholder="Serial / fault noted" className={`${fieldClass} col-span-2`} />
                </div>
              </div>
            ))}
            <button type="button" onClick={() => setRemoved((current) => [...current, emptyRemoved()])} className="inline-flex h-10 items-center gap-1.5 rounded-lg border border-dashed border-slate-300 px-3 text-xs font-medium text-slate-600 hover:bg-slate-50">
              <Plus className="h-3.5 w-3.5" />
              Add removed part
            </button>
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="return-notes">Notes</label>
            <textarea id="return-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
        </div>
      </FormDrawer>
    </section>
  )
}
