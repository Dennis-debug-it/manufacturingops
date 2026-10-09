import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Download, Upload, Trash2, MinusCircle } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useAuth } from '../../shared/hooks/useAuth'
import { usePermission } from '../../shared/hooks/usePermission'
import { useMaterials, useBranches } from './api'
import { useVan, useVanStock, useLoadVan, useReturnVanStock, useConsumeVanStock, useDeactivateVan, type VanStockLine } from './vansApi'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

type MoveMode = 'load' | 'return' | 'consume' | null
type VanStockRow = VanStockLine & { id: string }

export function VanDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const canIssue = usePermission('inventory.requests.issue')
  const canManage = usePermission('inventory.materials.manage')

  const { data: van, isLoading, isError } = useVan(id)
  const { data: stock, isLoading: stockLoading } = useVanStock(id)
  const { data: materials } = useMaterials({ status: 'active' })
  // Only admins can list branches (the API refuses others); everyone else sees just "None".
  const { data: branches } = useBranches({ enabled: useAuth().user?.role === 'admin' })

  const loadVan = useLoadVan(id ?? '')
  const returnVan = useReturnVanStock(id ?? '')
  const consumeVan = useConsumeVanStock(id ?? '')
  const deactivateVan = useDeactivateVan()

  const [mode, setMode] = useState<MoveMode>(null)
  const [materialId, setMaterialId] = useState('')
  const [branchId, setBranchId] = useState('')
  const [quantity, setQuantity] = useState('')
  const [reason, setReason] = useState('')
  const [showDeactivate, setShowDeactivate] = useState(false)

  if (isLoading) {
    return <Spinner label="Loading van" />
  }
  if (isError || !van || !id) {
    return <EmptyState title="Van unavailable" description="This van could not be loaded." />
  }

  const resetForm = () => {
    setMode(null)
    setMaterialId('')
    setBranchId('')
    setQuantity('')
    setReason('')
  }

  const submitMove = async () => {
    const qty = Number(quantity)
    if (!materialId || !(qty > 0)) {
      toast.error('Select a material and a quantity greater than zero.')
      return
    }
    if ((mode === 'load' || mode === 'return') && !branchId) {
      toast.error('Select a branch.')
      return
    }

    try {
      if (mode === 'load') {
        await loadVan.mutateAsync({ materialId, branchId, quantity: qty, reason: reason.trim() || undefined })
        toast.success('Stock loaded onto van')
      } else if (mode === 'return') {
        await returnVan.mutateAsync({ materialId, branchId, quantity: qty, reason: reason.trim() || undefined })
        toast.success('Stock returned to branch')
      } else if (mode === 'consume') {
        await consumeVan.mutateAsync({ materialId, quantity: qty, reason: reason.trim() || undefined })
        toast.success('Stock consumed')
      }
      resetForm()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const stockColumns: DataTableColumn<VanStockRow>[] = [
    { key: 'materialName', header: 'Part', cell: (row) => <span className="font-medium text-slate-900">{row.materialName}</span> },
    { key: 'materialCode', header: 'Code', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.materialCode}</span> },
    { key: 'quantityOnHand', header: 'On Van', cell: (row) => `${row.quantityOnHand} ${row.unitOfMeasure}` },
    { key: 'status', header: '', cell: (row) => (row.quantityOnHand <= row.reorderLevel && row.quantityOnHand > 0 ? <StatusBadge status="Low" /> : null) },
  ]

  // return/consume should only offer materials the van actually holds
  const heldMaterials: VanStockRow[] = (stock ?? []).filter((line) => line.quantityOnHand > 0).map((line) => ({ ...line, id: line.materialId }))
  const materialOptions = mode === 'load'
    ? (materials ?? []).map((m) => ({ id: m.id, label: `${m.itemName} (${m.itemCode})` }))
    : heldMaterials.map((m) => ({ id: m.materialId, label: `${m.materialName} (${m.materialCode}) — ${m.quantityOnHand} on van` }))

  const drawerTitle = mode === 'load' ? 'Load Stock onto Van' : mode === 'return' ? 'Return Stock to Branch' : 'Consume Stock'

  return (
    <div className="space-y-4">
      <PageHeader
        title={van.name}
        subtitle={[van.registrationNumber, van.technicianName, van.homeBranchName].filter(Boolean).join(' · ') || 'Mobile stock location'}
        breadcrumbs={[{ label: 'Vans', to: '/inventory/vans' }, { label: van.name }]}
        secondaryActions={<StatusBadge status={van.isActive ? 'Active' : 'Inactive'} />}
        primaryAction={
          <div className="flex flex-wrap gap-2">
            {canIssue ? (
              <>
                <button type="button" onClick={() => setMode('load')} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
                  <Upload className="h-4 w-4" />Load
                </button>
                <button type="button" onClick={() => setMode('return')} className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
                  <Download className="h-4 w-4" />Return
                </button>
              </>
            ) : null}
            <button type="button" onClick={() => setMode('consume')} className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              <MinusCircle className="h-4 w-4" />Consume
            </button>
            {canManage ? (
              <button type="button" onClick={() => setShowDeactivate(true)} className="inline-flex h-11 items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-4 text-sm font-medium text-rose-700 transition hover:bg-rose-100">
                <Trash2 className="h-4 w-4" />Deactivate
              </button>
            ) : null}
          </div>
        }
      />

      <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <h3 className="mb-4 text-sm font-semibold text-slate-900">Stock on this van</h3>
        {stockLoading ? (
          <Spinner label="Loading van stock" />
        ) : heldMaterials.length === 0 ? (
          <EmptyState title="Van is empty" description="Load parts from a branch to stock this van." />
        ) : (
          <DataTable columns={stockColumns} data={heldMaterials} />
        )}
      </section>

      <FormDrawer open={mode !== null} title={drawerTitle} onClose={resetForm}>
        <div className="space-y-4">
          <div>
            <label className={labelClass}>Material</label>
            <select value={materialId} onChange={(event) => setMaterialId(event.target.value)} className={fieldClass}>
              <option value="">Select a material</option>
              {materialOptions.map((option) => (
                <option key={option.id} value={option.id}>{option.label}</option>
              ))}
            </select>
          </div>
          {mode === 'load' || mode === 'return' ? (
            <div>
              <label className={labelClass}>{mode === 'load' ? 'Source Branch' : 'Destination Branch'}</label>
              <select value={branchId} onChange={(event) => setBranchId(event.target.value)} className={fieldClass}>
                <option value="">Select a branch</option>
                {(branches ?? []).map((branch) => (
                  <option key={branch.id} value={branch.id}>{branch.name}</option>
                ))}
              </select>
            </div>
          ) : null}
          <div>
            <label className={labelClass}>Quantity</label>
            <input type="number" min="0" step="any" value={quantity} onChange={(event) => setQuantity(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass}>Reason (optional)</label>
            <input value={reason} onChange={(event) => setReason(event.target.value)} className={fieldClass} />
          </div>
          <button
            type="button"
            disabled={loadVan.isPending || returnVan.isPending || consumeVan.isPending}
            onClick={() => void submitMove()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            {mode === 'load' ? 'Load onto Van' : mode === 'return' ? 'Return to Branch' : 'Confirm Consume'}
          </button>
        </div>
      </FormDrawer>

      <ConfirmDialog
        open={showDeactivate}
        title="Deactivate van"
        message={`Deactivate "${van.name}"? The van must be empty (all stock returned) first.`}
        confirmLabel="Deactivate"
        danger
        onCancel={() => setShowDeactivate(false)}
        onConfirm={() => {
          setShowDeactivate(false)
          void deactivateVan.mutateAsync(van.id)
            .then(() => {
              toast.success('Van deactivated')
              navigate('/inventory/vans')
            })
            .catch(() => {
              // interceptor surfaces the error toast
            })
        }}
      />
    </div>
  )
}
