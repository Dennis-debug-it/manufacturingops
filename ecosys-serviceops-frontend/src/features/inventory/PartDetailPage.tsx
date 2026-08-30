import { useEffect, useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate, useParams } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDateTime } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useAdjustMaterial, useDeleteMaterial, useMaterial, useMaterialMovements, useMaterials, useReplenishMaterial, useUpdateMaterial } from './api'
import type { Material, StockMovement } from './types'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

function EditPartDrawer({ open, material, onClose }: { open: boolean; material: Material; onClose: () => void }) {
  const updateMaterial = useUpdateMaterial(material.id)
  const [itemCode, setItemCode] = useState(material.itemCode)
  const [itemName, setItemName] = useState(material.itemName)
  const [category, setCategory] = useState(material.category ?? '')
  const [unitOfMeasure, setUnitOfMeasure] = useState(material.unitOfMeasure)
  const [reorderLevel, setReorderLevel] = useState(String(material.reorderLevel))
  const [unitCost, setUnitCost] = useState(material.unitCost != null ? String(material.unitCost) : '')

  useEffect(() => {
    if (open) {
      setItemCode(material.itemCode)
      setItemName(material.itemName)
      setCategory(material.category ?? '')
      setUnitOfMeasure(material.unitOfMeasure)
      setReorderLevel(String(material.reorderLevel))
      setUnitCost(material.unitCost != null ? String(material.unitCost) : '')
    }
  }, [open, material])

  const submit = async () => {
    if (!itemCode.trim() || !itemName.trim() || !unitOfMeasure.trim()) {
      toast.error('Code, name, and unit of measure are required')
      return
    }
    try {
      await updateMaterial.mutateAsync({
        itemCode: itemCode.trim(),
        itemName: itemName.trim(),
        category: category.trim() || null,
        unitOfMeasure: unitOfMeasure.trim(),
        quantityOnHand: material.quantityOnHand,
        reorderLevel: Number(reorderLevel) || 0,
        unitCost: unitCost.trim() ? Number(unitCost) : null,
        branchId: material.branchId ?? null,
      })
      toast.success('Part updated')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title={`Edit ${material.itemName}`}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={updateMaterial.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {updateMaterial.isPending ? 'Saving…' : 'Save Changes'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="edit-part-code">Item Code</label>
            <input id="edit-part-code" value={itemCode} onChange={(event) => setItemCode(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="edit-part-uom">Unit of Measure</label>
            <input id="edit-part-uom" value={unitOfMeasure} onChange={(event) => setUnitOfMeasure(event.target.value)} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="edit-part-name">Item Name</label>
          <input id="edit-part-name" value={itemName} onChange={(event) => setItemName(event.target.value)} className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="edit-part-category">Category</label>
          <input id="edit-part-category" value={category} onChange={(event) => setCategory(event.target.value)} className={fieldClass} />
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="edit-part-reorder">Reorder Level</label>
            <input id="edit-part-reorder" type="number" min="0" value={reorderLevel} onChange={(event) => setReorderLevel(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="edit-part-cost">Unit Cost</label>
            <input id="edit-part-cost" type="number" min="0" step="0.01" value={unitCost} onChange={(event) => setUnitCost(event.target.value)} className={fieldClass} />
          </div>
        </div>
        <p className="text-xs text-slate-500">Stock on hand is not edited here — use Replenish for receipts or Adjust for corrections so the movement ledger stays accurate.</p>
      </div>
    </FormDrawer>
  )
}

export function PartDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const canManage = usePermission('inventory.materials.manage')
  const { data: material, isLoading, isError } = useMaterial(id)
  const { data: siblings } = useMaterials()
  const { data: movements, isLoading: movementsLoading } = useMaterialMovements(id)
  const replenish = useReplenishMaterial(id ?? '')
  const adjust = useAdjustMaterial(id ?? '')
  const deleteMaterial = useDeleteMaterial()

  const [showReplenish, setShowReplenish] = useState(false)
  const [showAdjust, setShowAdjust] = useState(false)
  const [showEdit, setShowEdit] = useState(false)
  const [confirmDeactivate, setConfirmDeactivate] = useState(false)
  const [quantity, setQuantity] = useState('1')
  const [reason, setReason] = useState('')
  const [adjustDirection, setAdjustDirection] = useState<'increase' | 'decrease'>('decrease')
  const [adjustQuantity, setAdjustQuantity] = useState('1')
  const [adjustReason, setAdjustReason] = useState('')

  const stockByLocation = useMemo(
    () => (siblings ?? []).filter((item) => item.itemCode === material?.itemCode),
    [siblings, material],
  )

  const submitReplenish = async () => {
    const qty = Number(quantity)
    if (!Number.isFinite(qty) || qty <= 0) {
      toast.error('Enter a valid quantity')
      return
    }
    try {
      await replenish.mutateAsync({ quantity: qty, reason: reason.trim() || null, branchId: material?.branchId ?? null })
      toast.success('Stock replenished')
      setShowReplenish(false)
      setQuantity('1')
      setReason('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const submitAdjust = async () => {
    const qty = Number(adjustQuantity)
    if (!Number.isFinite(qty) || qty <= 0) {
      toast.error('Enter a valid quantity')
      return
    }
    if (!adjustReason.trim()) {
      toast.error('A reason is required for stock adjustments')
      return
    }
    try {
      await adjust.mutateAsync({
        quantityChange: adjustDirection === 'increase' ? qty : -qty,
        reason: adjustReason.trim(),
        branchId: material?.branchId ?? null,
      })
      toast.success('Stock adjusted')
      setShowAdjust(false)
      setAdjustQuantity('1')
      setAdjustReason('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading part" />
  }

  if (isError || !material || !id) {
    return <EmptyState title="Part unavailable" description="This part could not be loaded." />
  }

  const movementColumns: DataTableColumn<StockMovement>[] = [
    { key: 'movementType', header: 'Type', cell: (row) => <StatusBadge status={row.movementType} /> },
    { key: 'quantity', header: 'Qty', cell: (row) => row.quantity },
    { key: 'balanceAfter', header: 'Balance After', cell: (row) => row.balanceAfter },
    { key: 'reason', header: 'Reason', cell: (row) => row.reason ?? '—' },
    { key: 'createdAt', header: 'Date', cell: (row) => formatDateTime(row.createdAt) },
  ]

  const header = (
    <PageHeader
      title={material.itemName}
      subtitle={material.itemCode}
      breadcrumbs={[{ label: 'Parts', to: '/inventory/parts' }, { label: material.itemName }]}
      secondaryActions={<StatusBadge status={material.isLowStock ? 'Low Stock' : 'Active'} />}
      primaryAction={
        canManage ? (
          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={() => setShowEdit(true)} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              Edit
            </button>
            <button type="button" onClick={() => setShowAdjust(true)} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              Adjust Stock
            </button>
            <button type="button" onClick={() => setShowReplenish(true)} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              Replenish Stock
            </button>
            <button type="button" onClick={() => setConfirmDeactivate(true)} className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-600 transition hover:bg-rose-50">
              Deactivate
            </button>
          </div>
        ) : undefined
      }
    />
  )

  return (
    <>
      <DetailLayout
        header={header}
        tabs={[
          {
            id: 'details',
            label: 'Details',
            content: (
              <SummarySection title="Part Details" description="Identity and stock configuration.">
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
                  {[
                    ['Category', material.category ?? '—'],
                    ['Unit of Measure', material.unitOfMeasure],
                    ['Branch', material.branchName ?? 'All Branches'],
                    ['On Hand', String(material.quantityOnHand)],
                    ['Reorder Level', String(material.reorderLevel)],
                    ['Unit Cost', material.unitCost != null ? formatCurrency(material.unitCost) : '—'],
                  ].map(([label, value]) => (
                    <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
                      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                      <div className="mt-2 text-sm text-slate-900">{value}</div>
                    </div>
                  ))}
                </div>
              </SummarySection>
            ),
          },
          {
            id: 'stock-by-location',
            label: 'Stock by Location',
            content: (
              <DataTable
                columns={[
                  { key: 'branchName', header: 'Branch', cell: (row) => row.branchName ?? 'Unassigned' },
                  { key: 'quantityOnHand', header: 'On Hand', cell: (row) => `${row.quantityOnHand} ${row.unitOfMeasure}` },
                  { key: 'reorderLevel', header: 'Reorder Level', cell: (row) => row.reorderLevel },
                  { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.isLowStock ? 'Low Stock' : 'OK'} /> },
                ]}
                data={stockByLocation}
                emptyTitle="No branch-level stock records"
                emptyDescription="This part has no other branch-scoped stock records with the same item code."
              />
            ),
          },
          {
            id: 'movements',
            label: 'Movement History',
            content: <DataTable columns={movementColumns} data={movements ?? []} loading={movementsLoading} emptyTitle="No movements" emptyDescription="Stock receipts, issues, and adjustments will appear here." />,
          },
        ]}
      />

      <EditPartDrawer open={showEdit} material={material} onClose={() => setShowEdit(false)} />

      <ConfirmDialog
        open={confirmDeactivate}
        title="Deactivate Part"
        message={`Deactivate "${material.itemName}"? It will no longer appear in parts lists or be available for requests. Movement history is preserved.`}
        confirmLabel="Deactivate"
        danger
        onCancel={() => setConfirmDeactivate(false)}
        onConfirm={async () => {
          try {
            await deleteMaterial.mutateAsync(id)
            toast.success('Part deactivated')
            navigate('/inventory/parts')
          } catch {
            // interceptor surfaces the error toast
          } finally {
            setConfirmDeactivate(false)
          }
        }}
      />

      <FormDrawer
        open={showAdjust}
        title="Adjust Stock"
        onClose={() => setShowAdjust(false)}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => setShowAdjust(false)} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={() => void submitAdjust()} disabled={adjust.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {adjust.isPending ? 'Saving…' : 'Adjust Stock'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
            Current on hand: <span className="font-semibold text-slate-900">{material.quantityOnHand} {material.unitOfMeasure}</span>. Adjustments record a ledger entry — use them for stock takes, damage, or corrections.
          </div>
          <div>
            <label className={labelClass}>Direction</label>
            <div className="flex gap-2">
              {(['decrease', 'increase'] as const).map((direction) => (
                <button
                  key={direction}
                  type="button"
                  onClick={() => setAdjustDirection(direction)}
                  className={`inline-flex h-11 flex-1 items-center justify-center rounded-lg border text-sm font-medium transition ${adjustDirection === direction ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50'}`}
                >
                  {direction === 'decrease' ? 'Decrease (−)' : 'Increase (+)'}
                </button>
              ))}
            </div>
          </div>
          <div>
            <label className={labelClass} htmlFor="adjust-qty">Quantity</label>
            <input id="adjust-qty" type="number" min="0" step="0.01" value={adjustQuantity} onChange={(event) => setAdjustQuantity(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="adjust-reason">Reason (required)</label>
            <input id="adjust-reason" value={adjustReason} onChange={(event) => setAdjustReason(event.target.value)} placeholder="e.g. Stock take variance, damaged items" className={fieldClass} />
          </div>
        </div>
      </FormDrawer>

      <FormDrawer
        open={showReplenish}
        title="Replenish Stock"
        onClose={() => setShowReplenish(false)}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => setShowReplenish(false)} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={submitReplenish} disabled={replenish.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {replenish.isPending ? 'Saving…' : 'Replenish'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className={labelClass} htmlFor="replenish-qty">Quantity</label>
            <input id="replenish-qty" type="number" min="0" step="0.01" value={quantity} onChange={(event) => setQuantity(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="replenish-reason">Reason / Reference</label>
            <input id="replenish-reason" value={reason} onChange={(event) => setReason(event.target.value)} className={fieldClass} />
          </div>
        </div>
      </FormDrawer>
    </>
  )
}
