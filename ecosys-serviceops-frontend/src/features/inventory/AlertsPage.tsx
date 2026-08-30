import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { useLowStockMaterials, useReplenishMaterial } from './api'
import type { Material } from './types'
import { InventoryTabNav } from './components/InventoryTabNav'

function severity(material: Material): 'Critical' | 'Low' {
  return material.reorderLevel > 0 && material.quantityOnHand <= material.reorderLevel * 0.5 ? 'Critical' : 'Low'
}

function ReplenishForm({ material, onClose }: { material: Material; onClose: () => void }) {
  const [quantity, setQuantity] = useState(String(Math.max(material.reorderLevel - material.quantityOnHand, 1)))
  const replenish = useReplenishMaterial(material.id)

  const submit = async () => {
    const qty = Number(quantity)
    if (!Number.isFinite(qty) || qty <= 0) {
      toast.error('Enter a valid quantity')
      return
    }
    try {
      await replenish.mutateAsync({ quantity: qty, branchId: material.branchId, reason: 'Alert quick replenish' })
      toast.success('Stock replenished')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={`Replenish ${material.itemName}`}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={submit} disabled={replenish.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {replenish.isPending ? 'Saving…' : 'Replenish'}
          </button>
        </div>
      }
    >
      <div>
        <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="alert-qty">Quantity to add</label>
        <input id="alert-qty" type="number" min="0" step="0.01" value={quantity} onChange={(event) => setQuantity(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        <p className="mt-2 text-xs text-slate-500">Currently {material.quantityOnHand} {material.unitOfMeasure} on hand · reorder level {material.reorderLevel}</p>
      </div>
    </FormDrawer>
  )
}

export function AlertsPage() {
  const canManage = usePermission('inventory.materials.manage')
  const { data, isLoading, isError } = useLowStockMaterials()
  const [selected, setSelected] = useState<Material | null>(null)

  const rows = useMemo(() => (data ?? []).map((material) => ({ ...material, severity: severity(material) })), [data])

  const columns: DataTableColumn<(typeof rows)[number]>[] = [
    { key: 'itemName', header: 'Part', cell: (row) => row.itemName },
    { key: 'branchName', header: 'Branch', cell: (row) => row.branchName ?? 'All Branches' },
    { key: 'quantityOnHand', header: 'On Hand', cell: (row) => `${row.quantityOnHand} ${row.unitOfMeasure}` },
    { key: 'reorderLevel', header: 'Reorder Level', cell: (row) => row.reorderLevel },
    { key: 'severity', header: 'Severity', cell: (row) => <StatusBadge status={row.severity} /> },
    {
      key: 'action',
      header: '',
      cell: (row) => (canManage ? <button type="button" onClick={() => setSelected(row)} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Replenish</button> : null),
    },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Stock Alerts" subtitle="Critical and low stock items with suggested replenishment." />
      <InventoryTabNav />
      <DataTable columns={columns} data={rows} loading={isLoading} emptyTitle={isError ? 'Alerts unavailable' : 'No low stock items'} emptyDescription={isError ? 'Low-stock materials could not be loaded.' : 'All parts are above their reorder level.'} />
      {selected ? <ReplenishForm material={selected} onClose={() => setSelected(null)} /> : null}
    </div>
  )
}
