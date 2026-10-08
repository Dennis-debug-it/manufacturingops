import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { cn } from '../../shared/utils/cn'
import { useMaterialOptions } from '../work-orders/pickerApi'
import { savedMessage, useTechActions } from './offline/actions'
import { useTechBundle } from './offline/hooks'
import type { WorkOrder } from '../work-orders/types'

const inputClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

/** Parts and consumables used on the job; logging one issues it from the branch's stock. */
export function TechMaterials({ job, editable }: { job: WorkOrder; editable: boolean }) {
  const { data: bundle } = useTechBundle(job.id)
  const actions = useTechActions()
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 250)
  const { data: options, isLoading } = useMaterialOptions(debouncedSearch)
  const [materialId, setMaterialId] = useState('')
  const [quantity, setQuantity] = useState('1')
  const used = bundle?.materialUsages ?? []
  const selected = (options ?? []).find((option) => option.id === materialId)
  const qty = Number(quantity)
  const validQty = Number.isFinite(qty) && qty > 0

  const log = async (materialItemId: string, quantityUsed: number, label: string, unitOfMeasure: string | null = null) => {
    try {
      const result = await actions.logMaterial(job, { materialItemId, quantityUsed, assetId: job.assetId, materialName: label, unitOfMeasure })
      toast.success(savedMessage(`Logged ${quantityUsed} × ${label}`, result))
      setMaterialId('')
      setQuantity('1')
      setSearch('')
    } catch {
      // interceptor surfaces the error toast (e.g. not enough stock)
    }
  }

  // Planned items not yet logged become one-tap shortcuts.
  const loggedIds = new Set(used.map((usage) => usage.materialItemId))
  const plannedPending = (job.plannedMaterials ?? []).filter((planned) => !loggedIds.has(planned.materialId))

  return (
    <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
      <h2 className="text-sm font-semibold text-slate-900">Materials used</h2>

      {used.length > 0 ? (
        <ul className="divide-y divide-slate-100 text-sm">
          {used.map((usage) => (
            <li key={usage.id} className="flex justify-between gap-3 py-1.5">
              <span className="text-slate-900">{usage.materialName ?? 'Material'}</span>
              <span className="shrink-0 text-slate-500">{usage.quantityUsed} {usage.unitOfMeasure ?? ''}</span>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-slate-500">Nothing logged yet.</p>
      )}

      {editable && plannedPending.length > 0 ? (
        <div className="space-y-1.5">
          <p className="text-xs font-medium text-slate-500">Planned for this job</p>
          {plannedPending.map((planned) => (
            <button
              key={planned.materialId}
              type="button"
              disabled={actions.busy}
              onClick={() => void log(planned.materialId, planned.quantity, planned.materialName)}
              className="flex h-10 w-full items-center justify-between rounded-lg border border-dashed border-slate-300 px-3 text-sm text-slate-700 disabled:opacity-60"
            >
              <span>{planned.materialName} × {planned.quantity}</span>
              <span className="inline-flex items-center gap-1 text-xs font-medium text-[var(--app-primary)]"><Plus className="h-3.5 w-3.5" />Log</span>
            </button>
          ))}
        </div>
      ) : null}

      {editable ? (
        <div className="space-y-2 border-t border-slate-100 pt-3">
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search parts and materials" aria-label="Search materials" className={inputClass} />
          <select value={materialId} onChange={(event) => setMaterialId(event.target.value)} aria-label="Material used" className={inputClass}>
            <option value="">{isLoading ? 'Loading…' : (options ?? []).length === 0 ? 'No matching items' : 'Select an item'}</option>
            {(options ?? []).map((option) => (
              <option key={option.id} value={option.id}>{option.itemName} ({option.quantityOnHand} {option.unitOfMeasure} in stock)</option>
            ))}
          </select>
          <div className="flex gap-2">
            <input type="number" min={0.01} step="any" inputMode="decimal" value={quantity} onChange={(event) => setQuantity(event.target.value)} aria-label="Quantity used" className={cn(inputClass, 'w-28')} />
            <span className="flex items-center text-sm text-slate-500">{selected?.unitOfMeasure ?? ''}</span>
            <button
              type="button"
              disabled={!selected || !validQty || actions.busy}
              onClick={() => selected && void log(selected.id, qty, selected.itemName, selected.unitOfMeasure)}
              className="ml-auto inline-flex h-10 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white disabled:opacity-50"
            >
              {actions.busy ? 'Logging…' : 'Log material'}
            </button>
          </div>
          {selected && validQty && qty > selected.quantityOnHand ? (
            <p className="text-xs text-amber-700">Only {selected.quantityOnHand} {selected.unitOfMeasure} in stock at your branch.</p>
          ) : null}
        </div>
      ) : null}
    </section>
  )
}
