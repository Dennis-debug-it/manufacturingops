import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { EmptyState } from '../../../shared/components/EmptyState'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Spinner } from '../../../shared/components/Spinner'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatCurrency } from '../../../shared/utils/format'
import { useAddMaterialUsage, useExecutionBundle } from '../api'
import { useMaterialOptions } from '../pickerApi'
import type { WorkOrderMaterialUsage } from '../types'

export function MaterialsTab({ workOrderId }: { workOrderId: string }) {
  const canLog = usePermission('work-orders.materials.log')
  const { data, isLoading, isError } = useExecutionBundle(workOrderId)
  const addMaterial = useAddMaterialUsage(workOrderId)

  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 250)
  const { data: materials, isLoading: materialsLoading } = useMaterialOptions(debouncedSearch)
  const [materialItemId, setMaterialItemId] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [chargeable, setChargeable] = useState(true)
  const [notes, setNotes] = useState('')

  const selectedMaterial = (materials ?? []).find((material) => material.id === materialItemId)

  const closeDrawer = () => {
    setOpen(false)
    setMaterialItemId('')
    setQuantity('1')
    setChargeable(true)
    setNotes('')
    setSearch('')
  }

  const submit = async () => {
    const parsedQuantity = Number(quantity)
    if (!materialItemId || !Number.isFinite(parsedQuantity) || parsedQuantity <= 0) {
      toast.error('Select a material and a valid quantity')
      return
    }
    try {
      await addMaterial.mutateAsync({
        materialItemId,
        quantityUsed: parsedQuantity,
        chargeable,
        notes: notes.trim() || null,
      })
      toast.success('Material usage recorded')
      closeDrawer()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const totals = useMemo(() => {
    const usages = data?.materialUsages ?? []
    return usages.reduce((sum, usage) => sum + (usage.unitCost ?? 0) * usage.quantityUsed, 0)
  }, [data])

  const columns: DataTableColumn<WorkOrderMaterialUsage>[] = [
    { key: 'materialName', header: 'Material', cell: (row) => row.materialName ?? '—' },
    { key: 'quantityUsed', header: 'Qty', cell: (row) => `${row.quantityUsed} ${row.unitOfMeasure ?? ''}` },
    { key: 'unitCost', header: 'Unit Cost', cell: (row) => (row.unitCost != null ? formatCurrency(row.unitCost) : '—') },
    { key: 'total', header: 'Total', cell: (row) => (row.unitCost != null ? formatCurrency(row.unitCost * row.quantityUsed) : '—') },
    { key: 'chargeable', header: 'Chargeable', cell: (row) => (row.chargeable ? 'Yes' : 'No') },
    { key: 'usedByName', header: 'Logged By', cell: (row) => row.usedByName ?? '—' },
  ]

  if (isLoading) {
    return <Spinner label="Loading materials" />
  }

  if (isError || !data) {
    return <EmptyState title="Materials unavailable" description="Material usage could not be loaded." />
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="text-sm text-slate-600">Total material cost: <span className="font-semibold text-slate-900">{formatCurrency(totals)}</span></div>
        {canLog ? (
          <button
            type="button"
            onClick={() => setOpen(true)}
            className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800"
          >
            <Plus className="h-4 w-4" />
            Add Material
          </button>
        ) : null}
      </div>

      <DataTable
        columns={columns}
        data={data.materialUsages}
        emptyTitle="No materials logged"
        emptyDescription="Parts and materials used on this job will appear here."
      />

      <FormDrawer
        open={open}
        title="Add Material"
        onClose={closeDrawer}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={closeDrawer} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              Cancel
            </button>
            <button
              type="button"
              onClick={submit}
              disabled={addMaterial.isPending}
              className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {addMaterial.isPending ? 'Adding…' : 'Add Material'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="material-search">Search Parts</label>
            <input id="material-search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search by name or code…" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="material-select">Material</label>
            <select id="material-select" value={materialItemId} onChange={(event) => setMaterialItemId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
              <option value="">{materialsLoading ? 'Loading…' : 'Select a material'}</option>
              {(materials ?? []).map((material) => (
                <option key={material.id} value={material.id}>{material.itemName} — {material.quantityOnHand} {material.unitOfMeasure} in stock</option>
              ))}
            </select>
            {selectedMaterial ? (
              <p className="mt-1 text-xs text-slate-500">Van stock on hand: {selectedMaterial.quantityOnHand} {selectedMaterial.unitOfMeasure}</p>
            ) : null}
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="material-quantity">Quantity Used</label>
            <input id="material-quantity" type="number" min="0" step="0.01" value={quantity} onChange={(event) => setQuantity(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" checked={chargeable} onChange={(event) => setChargeable(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
            Chargeable to client
          </label>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="material-notes">Notes</label>
            <textarea id="material-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
        </div>
      </FormDrawer>
    </div>
  )
}
