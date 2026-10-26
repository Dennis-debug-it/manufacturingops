import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { ShoppingCart } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { formatCurrency } from '../../shared/utils/format'
import { useBranches, useLowStockMaterials, useReplenishMaterial } from './api'
import { useCreateOrdersFromSuggestions, useReorderSuggestions, useSuppliers, type ReorderSuggestion } from './purchaseOrdersApi'
import type { Material } from './types'
import { InventoryTabNav } from './components/InventoryTabNav'

// The critical cut-off is Settings → Inventory's threshold, applied by the server.
function severity(material: Material): 'Critical' | 'Low' {
  return material.isCriticalStock ? 'Critical' : 'Low'
}

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

function ReplenishForm({ material, onClose }: { material: Material; onClose: () => void }) {
  const [quantity, setQuantity] = useState(String(Math.max(material.reorderLevel - material.quantityOnHand, 1)))
  const [branchId, setBranchId] = useState(material.branchId ?? '')
  const { data: branches } = useBranches()
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)
  const replenish = useReplenishMaterial(material.id)

  const submit = async () => {
    const qty = Number(quantity)
    if (!Number.isFinite(qty) || qty <= 0) {
      toast.error('Enter a valid quantity')
      return
    }
    try {
      await replenish.mutateAsync({ quantity: qty, branchId: branchId || null, reason: 'Alert quick replenish' })
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
      <div className="space-y-4">
        {activeBranches.length > 0 ? (
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="alert-branch">Branch</label>
            <select id="alert-branch" value={branchId} onChange={(event) => setBranchId(event.target.value)} className={fieldClass}>
              <option value="">My default branch</option>
              {activeBranches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
            </select>
          </div>
        ) : null}
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="alert-qty">Quantity to add</label>
          <input id="alert-qty" type="number" min="0" step="0.01" value={quantity} onChange={(event) => setQuantity(event.target.value)} className={fieldClass} />
          <p className="mt-2 text-xs text-slate-500">Currently {material.quantityOnHand} {material.unitOfMeasure} on hand · reorder level {material.reorderLevel}. For stock you are buying, a purchase order keeps the supplier and cost on record.</p>
        </div>
      </div>
    </FormDrawer>
  )
}

interface ReorderDraft {
  selected: boolean
  quantity: string
  supplierId: string
}

function ReorderView({ branchId }: { branchId: string }) {
  const canManage = usePermission('inventory.materials.manage')
  const { data, isLoading, isError, refetch } = useReorderSuggestions(branchId)
  const { data: suppliers } = useSuppliers()
  const activeSuppliers = (suppliers ?? []).filter((supplier) => supplier.status === 'Active')
  const createOrders = useCreateOrdersFromSuggestions()
  const [drafts, setDrafts] = useState<Record<string, ReorderDraft>>({})

  const draftOf = (row: ReorderSuggestion): ReorderDraft =>
    drafts[row.materialItemId] ?? {
      selected: !row.coveredByOpenOrders && row.preferredSupplierActive,
      quantity: String(row.suggestedQuantity || ''),
      supplierId: row.preferredSupplierActive && row.preferredSupplierId ? row.preferredSupplierId : '',
    }
  const update = (row: ReorderSuggestion, patch: Partial<ReorderDraft>) => setDrafts((prev) => ({ ...prev, [row.materialItemId]: { ...draftOf(row), ...patch } }))

  const chosen = (data ?? []).filter((row) => draftOf(row).selected)
  const estimate = chosen.reduce((sum, row) => sum + (Number(draftOf(row).quantity) || 0) * (row.unitCost ?? 0), 0)
  const supplierCount = new Set(chosen.map((row) => draftOf(row).supplierId).filter(Boolean)).size

  const submit = async () => {
    const items = chosen.map((row) => ({ materialItemId: row.materialItemId, quantity: Number(draftOf(row).quantity), supplierId: draftOf(row).supplierId || null }))
    if (items.some((item) => !Number.isFinite(item.quantity) || item.quantity <= 0)) return toast.error('Every chosen part needs a quantity')
    if (items.some((item) => !item.supplierId)) return toast.error('Every chosen part needs a supplier')
    try {
      const orders = await createOrders.mutateAsync({ items })
      toast.success(`Drafted ${orders.map((order) => order.poNumber).join(', ')}. Review and send them from Purchase Orders.`)
      setDrafts({})
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const columns: DataTableColumn<ReorderSuggestion & { id: string }>[] = [
    {
      key: 'select',
      header: '',
      cell: (row) => (
        <input type="checkbox" aria-label={`Reorder ${row.itemName}`} disabled={!canManage} checked={draftOf(row).selected} onChange={(event) => update(row, { selected: event.target.checked })} />
      ),
    },
    {
      key: 'item',
      header: 'Part',
      cell: (row) => (
        <Link className="text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${row.materialItemId}`}>
          {row.itemName} <span className="font-mono text-xs text-slate-500">{row.itemCode}</span>
        </Link>
      ),
    },
    { key: 'severity', header: 'Severity', cell: (row) => <StatusBadge status={row.isCritical ? 'Critical' : 'Low'} /> },
    { key: 'onHand', header: 'On Hand / Reorder', cell: (row) => <span className="tabular-nums">{row.quantityOnHand} / {row.reorderLevel} {row.unitOfMeasure}</span> },
    { key: 'onOrder', header: 'On Order', cell: (row) => (row.onOrder > 0 ? <span className="tabular-nums">{row.onOrder}{row.coveredByOpenOrders ? <span className="ml-1 text-xs text-emerald-700">covered</span> : null}</span> : '—') },
    {
      key: 'quantity',
      header: 'Order Qty',
      cell: (row) => (
        <input type="number" min="0" step="any" aria-label={`Quantity of ${row.itemName}`} disabled={!canManage} value={draftOf(row).quantity} onChange={(event) => update(row, { quantity: event.target.value, selected: true })} className="h-9 w-24 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
      ),
    },
    {
      key: 'supplier',
      header: 'Supplier',
      cell: (row) => (
        <select aria-label={`Supplier for ${row.itemName}`} disabled={!canManage} value={draftOf(row).supplierId} onChange={(event) => update(row, { supplierId: event.target.value })} className="h-9 max-w-[12rem] rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]">
          <option value="">{row.preferredSupplierId && !row.preferredSupplierActive ? `${row.preferredSupplierName ?? 'Preferred'} (inactive)` : 'Choose…'}</option>
          {activeSuppliers.map((supplier) => <option key={supplier.id} value={supplier.id}>{supplier.supplierName}</option>)}
        </select>
      ),
    },
    { key: 'lead', header: 'Lead Time', cell: (row) => `${row.leadTimeDays} d` },
    { key: 'cost', header: 'Est. Cost', cell: (row) => (row.unitCost != null ? formatCurrency((Number(draftOf(row).quantity) || 0) * row.unitCost) : '—') },
  ]

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-3 shadow-sm backdrop-blur-[var(--glass-blur-md)]">
        <p className="text-sm text-slate-600">
          {chosen.length > 0
            ? `${chosen.length} part${chosen.length === 1 ? '' : 's'} · ${supplierCount} supplier${supplierCount === 1 ? '' : 's'} · about ${formatCurrency(estimate)}`
            : 'Tick the parts to order. Suggested quantities use each part’s reorder quantity, else top up to twice the reorder level.'}
        </p>
        {canManage ? (
          <button type="button" disabled={chosen.length === 0 || createOrders.isPending} onClick={() => void submit()} className="inline-flex h-10 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-50">
            <ShoppingCart className="h-4 w-4" />{createOrders.isPending ? 'Drafting…' : 'Draft Purchase Orders'}
          </button>
        ) : null}
      </div>
      <DataTable
        columns={columns}
        data={(data ?? []).map((row) => ({ ...row, id: row.materialItemId }))}
        loading={isLoading}
        emptyTitle={isError ? 'Reorder suggestions unavailable' : 'Nothing to reorder'}
        emptyDescription={isError ? 'Suggestions could not be loaded — this is not a confirmation that stock is fine.' : 'Every part with a reorder level is above it.'}
        emptyActionLabel={isError ? 'Retry' : undefined}
        onEmptyAction={isError ? () => void refetch() : undefined}
      />
    </div>
  )
}

function LowStockView({ branchId }: { branchId: string }) {
  const canManage = usePermission('inventory.materials.manage')
  const { data, isLoading, isError } = useLowStockMaterials(branchId || undefined)
  const [selected, setSelected] = useState<Material | null>(null)

  const rows = useMemo(() => (data ?? []).map((material) => ({ ...material, severity: severity(material) })), [data])

  const columns: DataTableColumn<(typeof rows)[number]>[] = [
    { key: 'itemName', header: 'Part', cell: (row) => <Link className="underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${row.id}`}>{row.itemName}</Link> },
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
    <>
      <DataTable columns={columns} data={rows} loading={isLoading} emptyTitle={isError ? 'Alerts unavailable' : 'No low stock items'} emptyDescription={isError ? 'Low-stock materials could not be loaded.' : 'All parts are above their reorder level.'} />
      {selected ? <ReplenishForm material={selected} onClose={() => setSelected(null)} /> : null}
    </>
  )
}

export function AlertsPage() {
  const [view, setView] = useState<'reorder' | 'low'>('reorder')
  const [branchId, setBranchId] = useState('')
  const { data: branches } = useBranches()
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)

  return (
    <div className="space-y-4">
      <PageHeader
        title="Stock Alerts"
        subtitle="Parts at or below their reorder level, what is already on order, and what to buy."
        secondaryActions={
          <div className="flex flex-wrap items-center gap-2">
            {activeBranches.length > 0 ? (
              <select aria-label="Branch" value={branchId} onChange={(event) => setBranchId(event.target.value)} className="h-10 rounded-full border border-slate-200 bg-white px-3 text-sm">
                <option value="">All branches</option>
                {activeBranches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
              </select>
            ) : null}
            {(['reorder', 'low'] as const).map((option) => (
              <button key={option} type="button" onClick={() => setView(option)} className={cn('inline-flex h-10 items-center rounded-full border px-3 text-sm font-medium transition', view === option ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}>
                {option === 'reorder' ? 'Reorder' : 'Low Stock'}
              </button>
            ))}
          </div>
        }
      />
      <InventoryTabNav />
      {view === 'reorder' ? <ReorderView branchId={branchId} /> : <LowStockView branchId={branchId} />}
    </div>
  )
}
