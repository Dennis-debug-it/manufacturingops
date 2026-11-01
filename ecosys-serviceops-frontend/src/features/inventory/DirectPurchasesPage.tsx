import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ErrorState } from '../../shared/components/ErrorState'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useWorkOrders } from '../work-orders/api'
import { useMaterials } from './api'
import { useSuppliers } from './purchaseOrdersApi'

export type DirectPurchaseStatus = 'Pending' | 'Reconciled'

export interface DirectPurchase {
  id: string
  materialItemId: string
  itemCode: string | null
  itemName: string | null
  unitOfMeasure: string | null
  quantity: number
  unitCost: number | null
  status: DirectPurchaseStatus
  branchId: string | null
  branchName: string | null
  workOrderId: string | null
  workOrderNumber: string | null
  supplierId: string | null
  supplierName: string | null
  supplierReference: string | null
  purchasedByName: string | null
  invoiceReference: string | null
  reconciledByName: string | null
  reconciledAt: string | null
  createdAt: string
}

export function useDirectPurchases(status: DirectPurchaseStatus | 'All') {
  return useQuery({
    queryKey: ['direct-purchases', status],
    queryFn: () => api.get<DirectPurchase[]>('/api/direct-purchases', status === 'All' ? undefined : { status }),
    staleTime: 30_000,
  })
}

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const primaryButton = 'inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60'
const cancelButton = 'inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50'

/**
 * Parts bought straight from a supplier for a job. Their use took stores below what it held (even
 * below zero); reconciling them with the supplier's invoice books them back so the balance is right.
 */
export function DirectPurchasesPage() {
  const canIssue = usePermission('inventory.requests.issue')
  const canManage = usePermission('inventory.materials.manage')
  const canReconcile = canIssue || canManage
  const [status, setStatus] = useState<DirectPurchaseStatus | 'All'>('Pending')
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [reconciling, setReconciling] = useState(false)
  const [logging, setLogging] = useState(false)
  const { data, isLoading, isError, refetch } = useDirectPurchases(status)
  const rows = useMemo(() => data ?? [], [data])
  const pending = rows.filter((row) => row.status === 'Pending')
  const selected = pending.filter((row) => selectedIds.includes(row.id))
  const toggle = (id: string) => setSelectedIds((current) => (current.includes(id) ? current.filter((x) => x !== id) : [...current, id]))

  const columns: DataTableColumn<DirectPurchase>[] = [
    ...(canReconcile && status !== 'Reconciled'
      ? [{
          key: 'pick',
          header: '',
          cell: (row: DirectPurchase) => (row.status === 'Pending' ? (
            <input type="checkbox" aria-label={`Select ${row.itemName ?? 'purchase'}`} checked={selectedIds.includes(row.id)} onChange={() => toggle(row.id)} className="h-4 w-4 rounded border-slate-300" />
          ) : null),
        }]
      : []),
    { key: 'part', header: 'Part', cell: (row) => <Link to={`/inventory/parts/${row.materialItemId}`} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{row.itemName ?? 'Part'}</Link>, sortValue: (row) => row.itemName },
    { key: 'qty', header: 'Qty', cell: (row) => <span className="tabular-nums">{row.quantity} {row.unitOfMeasure ?? ''}</span>, sortValue: (row) => row.quantity },
    { key: 'cost', header: 'Paid', cell: (row) => (row.unitCost != null ? formatCurrency(row.unitCost * row.quantity) : '—'), sortValue: (row) => (row.unitCost ?? 0) * row.quantity },
    { key: 'supplier', header: 'Supplier', cell: (row) => <span>{row.supplierName ?? '—'}{row.supplierReference ? <span className="block text-xs text-slate-500">Receipt {row.supplierReference}</span> : null}</span>, sortValue: (row) => row.supplierName },
    { key: 'job', header: 'Job', cell: (row) => (row.workOrderId ? <Link to={`/work-orders/${row.workOrderId}`} className="underline decoration-slate-300 underline-offset-4">{row.workOrderNumber}</Link> : '—'), sortValue: (row) => row.workOrderNumber },
    { key: 'by', header: 'Bought by', cell: (row) => <span>{row.purchasedByName ?? '—'}<span className="block text-xs text-slate-500">{formatDate(row.createdAt)}</span></span>, sortValue: (row) => row.createdAt },
    { key: 'branch', header: 'Branch', cell: (row) => row.branchName ?? '—', sortValue: (row) => row.branchName },
    {
      key: 'status',
      header: 'Status',
      cell: (row) => (row.status === 'Pending' ? <StatusBadge status="Pending" /> : (
        <span><StatusBadge status="Reconciled" /><span className="block text-xs text-slate-500">Invoice {row.invoiceReference}</span></span>
      )),
      sortValue: (row) => row.status,
    },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Direct purchases"
        subtitle="Parts bought straight from a supplier for a job. Using them can take stock below zero until you reconcile them with the supplier's invoice."
        primaryAction={canReconcile ? (
          <button type="button" onClick={() => setLogging(true)} className={primaryButton}><Plus className="h-4 w-4" />Record a direct purchase</button>
        ) : undefined}
      />
      <div className="flex flex-wrap items-center gap-2">
        {(['Pending', 'Reconciled', 'All'] as const).map((value) => (
          <button key={value} type="button" aria-pressed={status === value} onClick={() => { setStatus(value); setSelectedIds([]) }} className={cn('inline-flex h-9 items-center rounded-lg border px-3 text-sm font-medium transition', status === value ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}>
            {value === 'Pending' ? 'To reconcile' : value}
          </button>
        ))}
        {canReconcile && selected.length > 0 ? (
          <button type="button" onClick={() => setReconciling(true)} className={cn(primaryButton, 'ml-auto h-9')}>
            Reconcile {selected.length} with an invoice
          </button>
        ) : status === 'Pending' && pending.length > 0 && canReconcile ? (
          <span className="ml-auto text-sm text-slate-500">Tick the purchases on one supplier invoice, then reconcile them together.</span>
        ) : null}
      </div>
      {isError ? (
        <ErrorState title="Direct purchases unavailable" description="Direct purchases could not be loaded." onRetry={() => void refetch()} />
      ) : (
        <DataTable columns={columns} data={rows} loading={isLoading} emptyTitle={status === 'Pending' ? 'Nothing to reconcile' : 'None'} emptyDescription="When a technician logs a part as bought directly from a supplier, it waits here for the supplier's invoice." />
      )}
      {reconciling ? <ReconcileDrawer purchases={selected} onClose={() => setReconciling(false)} onDone={() => { setReconciling(false); setSelectedIds([]) }} /> : null}
      {logging ? <RecordDirectPurchaseDrawer onClose={() => setLogging(false)} /> : null}
    </div>
  )
}

function ReconcileDrawer({ purchases, onClose, onDone }: { purchases: DirectPurchase[]; onClose: () => void; onDone: () => void }) {
  const queryClient = useQueryClient()
  const { data: suppliers } = useSuppliers()
  const [invoice, setInvoice] = useState('')
  const [supplierId, setSupplierId] = useState(purchases.find((row) => row.supplierId)?.supplierId ?? '')
  const [costs, setCosts] = useState<Record<string, string>>(Object.fromEntries(purchases.map((row) => [row.id, row.unitCost != null ? String(row.unitCost) : ''])))
  const reconcile = useMutation({
    mutationFn: (payload: { ids: string[]; invoiceReference: string; unitCosts: Record<string, number>; supplierId: string | null }) =>
      api.post<DirectPurchase[]>('/api/direct-purchases/reconcile', payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['direct-purchases'] })
      void queryClient.invalidateQueries({ queryKey: ['materials'] })
    },
  })

  const submit = async () => {
    if (!invoice.trim()) return void toast.error("Enter the supplier's invoice number")
    const unitCosts: Record<string, number> = {}
    for (const row of purchases) {
      const raw = costs[row.id]?.trim() ?? ''
      if (raw === '') continue
      const value = Number(raw)
      if (!Number.isFinite(value) || value < 0) return void toast.error(`${row.itemName ?? 'A part'}: enter a cost of 0 or more`)
      unitCosts[row.id] = value
    }
    try {
      await reconcile.mutateAsync({ ids: purchases.map((row) => row.id), invoiceReference: invoice.trim(), unitCosts, supplierId: supplierId || null })
      toast.success(`Reconciled ${purchases.length} purchase${purchases.length === 1 ? '' : 's'} with ${invoice.trim()}`)
      onDone()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title="Reconcile with supplier invoice"
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className={cancelButton}>Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={reconcile.isPending} className={primaryButton}>{reconcile.isPending ? 'Saving…' : 'Reconcile'}</button>
        </div>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-slate-600">Each part is booked back into its branch as a receipt against this invoice, which brings the stock balance back to what stores really holds.</p>
        <div>
          <label className={labelClass} htmlFor="dp-invoice">Supplier invoice number</label>
          <input id="dp-invoice" value={invoice} onChange={(event) => setInvoice(event.target.value)} className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="dp-supplier">Supplier (optional)</label>
          <select id="dp-supplier" value={supplierId} onChange={(event) => setSupplierId(event.target.value)} className={fieldClass}>
            <option value="">As logged</option>
            {(suppliers ?? []).map((supplier) => <option key={supplier.id} value={supplier.id}>{supplier.supplierName}</option>)}
          </select>
          <p className="mt-1 text-xs text-slate-500">Links purchases logged with only a typed supplier name to this supplier.</p>
        </div>
        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500">
              <tr><th className="px-3 py-2">Part</th><th className="px-3 py-2 text-right">Qty</th><th className="px-3 py-2">Unit cost on invoice</th></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {purchases.map((row) => (
                <tr key={row.id}>
                  <td className="px-3 py-2">{row.itemName}<span className="block text-xs text-slate-500">{row.workOrderNumber ?? ''}{row.supplierReference ? ` · receipt ${row.supplierReference}` : ''}</span></td>
                  <td className="px-3 py-2 text-right tabular-nums">{row.quantity}</td>
                  <td className="px-3 py-2">
                    <input type="number" min="0" step="any" aria-label={`Unit cost for ${row.itemName ?? 'part'}`} value={costs[row.id] ?? ''} onChange={(event) => setCosts((current) => ({ ...current, [row.id]: event.target.value }))} className="h-9 w-28 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </FormDrawer>
  )
}

/** Stores records a counter purchase from a receipt a technician handed in. */
function RecordDirectPurchaseDrawer({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient()
  const { data: workOrders } = useWorkOrders()
  const { data: materials } = useMaterials({ status: 'active' })
  const { data: suppliers } = useSuppliers()
  const [workOrderId, setWorkOrderId] = useState('')
  const [materialId, setMaterialId] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [supplierId, setSupplierId] = useState('')
  const [supplierName, setSupplierName] = useState('')
  const [reference, setReference] = useState('')
  const [unitCost, setUnitCost] = useState('')
  const record = useMutation({
    mutationFn: (payload: Record<string, unknown>) => api.post(endpoints.workOrders.materialsUsed(workOrderId), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['direct-purchases'] })
      void queryClient.invalidateQueries({ queryKey: ['materials'] })
      void queryClient.invalidateQueries({ queryKey: ['work-orders'] })
    },
  })

  const submit = async () => {
    const qty = Number(quantity)
    const cost = unitCost.trim() === '' ? null : Number(unitCost)
    if (!workOrderId) return void toast.error('Choose the job it was bought for')
    if (!materialId) return void toast.error('Choose the part')
    if (!Number.isFinite(qty) || qty <= 0) return void toast.error('Enter a quantity greater than zero')
    if (cost != null && (!Number.isFinite(cost) || cost < 0)) return void toast.error('Price must be 0 or more')
    const supplier = (suppliers ?? []).find((item) => item.id === supplierId)
    try {
      await record.mutateAsync({
        materialItemId: materialId, assetId: null, quantityUsed: qty, unitCost: cost, chargeable: true, notes: null, usedAt: null,
        boughtDirect: true, supplierId: supplierId || null, supplierName: supplier?.supplierName ?? (supplierName.trim() || null), supplierReference: reference.trim() || null,
      })
      toast.success('Direct purchase recorded on the job')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title="Record a direct purchase"
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className={cancelButton}>Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={record.isPending} className={primaryButton}>{record.isPending ? 'Saving…' : 'Record'}</button>
        </div>
      }
    >
      <div className="space-y-4">
        <p className="text-sm text-slate-600">For a part a technician bought at a supplier's counter and used on a job. It is booked as used on the job — taking stock below zero if needed — and waits here until you reconcile the supplier's invoice.</p>
        <div>
          <label className={labelClass} htmlFor="dp-wo">Job</label>
          <select id="dp-wo" value={workOrderId} onChange={(event) => setWorkOrderId(event.target.value)} className={fieldClass}>
            <option value="">Select the work order…</option>
            {(workOrders ?? []).map((wo) => <option key={wo.id} value={wo.id}>{wo.workOrderNumber} — {wo.title}</option>)}
          </select>
        </div>
        <div className="grid grid-cols-[1fr_7rem] gap-3">
          <div>
            <label className={labelClass} htmlFor="dp-part">Part</label>
            <select id="dp-part" value={materialId} onChange={(event) => setMaterialId(event.target.value)} className={fieldClass}>
              <option value="">Select a part…</option>
              {(materials ?? []).map((material) => <option key={material.id} value={material.id}>{material.itemName} ({material.itemCode})</option>)}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="dp-qty">Qty</label>
            <input id="dp-qty" type="number" min="0" step="any" value={quantity} onChange={(event) => setQuantity(event.target.value)} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="dp-sup">Supplier</label>
          <select id="dp-sup" value={supplierId} onChange={(event) => setSupplierId(event.target.value)} className={fieldClass}>
            <option value="">Not on the supplier list — type it below</option>
            {(suppliers ?? []).map((supplier) => <option key={supplier.id} value={supplier.id}>{supplier.supplierName}</option>)}
          </select>
          {supplierId ? null : <input value={supplierName} onChange={(event) => setSupplierName(event.target.value)} placeholder="Supplier name" aria-label="Supplier name" className={cn(fieldClass, 'mt-2')} />}
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="dp-ref">Receipt no.</label>
            <input id="dp-ref" value={reference} onChange={(event) => setReference(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="dp-cost">Price paid each</label>
            <input id="dp-cost" type="number" min="0" step="any" value={unitCost} onChange={(event) => setUnitCost(event.target.value)} className={fieldClass} />
          </div>
        </div>
      </div>
    </FormDrawer>
  )
}
