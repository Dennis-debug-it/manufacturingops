import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { ErrorState } from '../../shared/components/ErrorState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ProjectPicker } from '../../shared/components/ProjectPicker'
import { usePermission } from '../../shared/hooks/usePermission'
import { useUrlFilters } from '../../shared/hooks/useUrlFilters'
import { formatDate } from '../../shared/utils/dates'
import { InventoryTabNav } from './components/InventoryTabNav'
import { MaterialAutocomplete } from './components/MaterialAutocomplete'
import { purchaseOrderChoice, purchaseOrderFiltersFor, purchaseOrderListFilterSpec, purchaseOrderStatuses, type PurchaseOrderChoice } from './listFilters'
import {
  useCreatePurchaseOrder,
  usePurchaseOrder,
  usePurchaseOrders,
  useReceivePurchaseOrderLine,
  useSetPurchaseOrderStatus,
  useSuppliers,
  type PurchaseOrderListItem,
} from './purchaseOrdersApi'

const choices: PurchaseOrderChoice[] = ['All', 'open', 'late', ...purchaseOrderStatuses]
const choiceLabels: Partial<Record<PurchaseOrderChoice, string>> = { open: 'Open', late: 'Past expected date', PartiallyReceived: 'Partially Received' }

interface DraftLine {
  itemName: string
  quantityOrdered: string
  unitCost: string
}

const emptyLine: DraftLine = { itemName: '', quantityOrdered: '1', unitCost: '0' }

export function PurchaseOrdersPage() {
  const canEdit = usePermission('inventory.edit')
  // The filter lives in the URL, so the dashboard's "Open" and "Past expected date" open it filtered.
  const { filters, update } = useUrlFilters(purchaseOrderListFilterSpec)
  const choice = purchaseOrderChoice(filters)
  const { data, isLoading, isError, refetch } = usePurchaseOrders(
    choice === 'open' || choice === 'late' ? 'All' : choice,
    choice === 'open' || choice === 'late' ? choice : undefined,
  )
  const { data: suppliers } = useSuppliers()
  const createPo = useCreatePurchaseOrder()
  const [showNewPo, setShowNewPo] = useState(false)
  const [supplierId, setSupplierId] = useState('')
  const [expectedAt, setExpectedAt] = useState('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<DraftLine[]>([{ ...emptyLine }])
  const [projectId, setProjectId] = useState('')

  const resetForm = () => {
    setSupplierId('')
    setProjectId('')
    setExpectedAt('')
    setNotes('')
    setLines([{ ...emptyLine }])
  }

  const validLines = lines
    .filter((l) => l.itemName.trim() && Number(l.quantityOrdered) > 0)
    .map((l) => ({ itemName: l.itemName.trim(), quantityOrdered: Number(l.quantityOrdered), unitCost: Number(l.unitCost) || 0 }))

  const submit = async () => {
    if (!supplierId) return toast.error('Select a supplier')
    if (validLines.length === 0) return toast.error('Add at least one line item')
    await createPo.mutateAsync({ supplierId, expectedAt: expectedAt || undefined, lines: validLines, notes: notes || undefined, projectId: projectId || null })
    toast.success('Purchase order created')
    setShowNewPo(false)
    resetForm()
  }

  const columns: DataTableColumn<PurchaseOrderListItem>[] = [
    { key: 'poNumber', header: 'PO No', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/purchase-orders/${row.id}`}>{row.poNumber}</Link> },
    { key: 'supplierName', header: 'Supplier', cell: (row) => row.supplierName },
    { key: 'orderedAt', header: 'Ordered', cell: (row) => formatDate(row.orderedAt) },
    { key: 'expectedAt', header: 'Expected', cell: (row) => (row.expectedAt ? formatDate(row.expectedAt) : '—') },
    { key: 'totalAmount', header: 'Total', cell: (row) => `${row.currency} ${row.totalAmount.toLocaleString()}` },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Purchase Orders"
        subtitle="Draft, sent, received, and cancelled purchase orders."
        primaryAction={
          canEdit ? (
            <button type="button" onClick={() => setShowNewPo(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Purchase Order
            </button>
          ) : undefined
        }
      />
      <InventoryTabNav />
      <div className="flex flex-wrap gap-2 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-3 shadow-sm">
        {choices.map((option) => (
          <button
            key={option}
            type="button"
            onClick={() => update(purchaseOrderFiltersFor(option))}
            className={`inline-flex h-10 items-center rounded-lg px-4 text-sm font-medium transition ${choice === option ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-100'}`}
          >
            {choiceLabels[option] ?? option}
          </button>
        ))}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Purchase orders unavailable' : 'No purchase orders yet'}
        emptyDescription={isError ? 'Purchase orders could not be loaded — this is not a confirmation that none exist.' : 'Raise a purchase order against a supplier to get started.'}
        emptyActionLabel={isError ? 'Retry' : undefined}
        onEmptyAction={isError ? () => void refetch() : undefined}
      />

      <FormDrawer open={showNewPo} title="New Purchase Order" onClose={() => setShowNewPo(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Supplier</label>
            <select value={supplierId} onChange={(event) => setSupplierId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              <option value="">Select a supplier…</option>
              {(suppliers ?? []).map((s) => <option key={s.id} value={s.id}>{s.supplierName}</option>)}
            </select>
            {suppliers && suppliers.length === 0 ? <p className="mt-1 text-xs text-amber-600">No suppliers yet — add one on the Suppliers page first.</p> : null}
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Expected Delivery Date</label>
            <input type="date" value={expectedAt} onChange={(event) => setExpectedAt(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <ProjectPicker id="po-project" value={projectId} onChange={setProjectId} />

          <div>
            <div className="mb-1.5 flex items-center justify-between">
              <label className="text-sm font-medium text-slate-700">Line Items</label>
              <button type="button" onClick={() => setLines((prev) => [...prev, { ...emptyLine }])} className="text-xs font-medium text-slate-600 underline underline-offset-2 hover:text-slate-900">+ Add line</button>
            </div>
            <div className="space-y-3">
              {lines.map((line, index) => {
                const subtotal = (Number(line.quantityOrdered) || 0) * (Number(line.unitCost) || 0)
                return (
                  <div key={index} className="rounded-xl border border-slate-200 bg-slate-50/60 p-3">
                    <MaterialAutocomplete
                      value={line.itemName}
                      onChange={(next) => setLines((prev) => prev.map((l, i) => (i === index ? { ...l, itemName: next } : l)))}
                      onSelectMaterial={(material) =>
                        setLines((prev) =>
                          prev.map((l, i) =>
                            i === index
                              ? {
                                  ...l,
                                  itemName: material.itemName,
                                  // Auto-fill the unit cost when it hasn't been set yet.
                                  unitCost: !l.unitCost || Number(l.unitCost) === 0 ? String(material.unitCost ?? l.unitCost) : l.unitCost,
                                }
                              : l,
                          ),
                        )
                      }
                      aria-label="Item description"
                      placeholder="Search or type an item description…"
                    />
                    <div className="mt-2 flex items-end gap-3">
                      <label className="flex flex-col gap-1">
                        <span className="text-xs font-medium text-slate-500">Qty</span>
                        <input value={line.quantityOrdered} onChange={(e) => setLines((prev) => prev.map((l, i) => (i === index ? { ...l, quantityOrdered: e.target.value } : l)))} type="number" min="0" placeholder="0" aria-label="Quantity" className="h-10 w-20 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                      </label>
                      <label className="flex flex-col gap-1">
                        <span className="text-xs font-medium text-slate-500">Unit Cost</span>
                        <input value={line.unitCost} onChange={(e) => setLines((prev) => prev.map((l, i) => (i === index ? { ...l, unitCost: e.target.value } : l)))} type="number" min="0" placeholder="0.00" aria-label="Unit cost" className="h-10 w-28 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                      </label>
                      <div className="flex flex-1 flex-col gap-1 text-right">
                        <span className="text-xs font-medium text-slate-500">Subtotal</span>
                        <span className="flex h-10 items-center justify-end px-1 text-sm font-semibold tabular-nums text-slate-900">{subtotal.toLocaleString()}</span>
                      </div>
                      <button type="button" disabled={lines.length === 1} onClick={() => setLines((prev) => prev.filter((_, i) => i !== index))} className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-400 hover:bg-slate-50 disabled:opacity-30" aria-label="Remove line">
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                )
              })}
            </div>
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Notes</label>
            <textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <button
            type="button"
            disabled={!supplierId || validLines.length === 0 || createPo.isPending}
            onClick={() => void submit()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            {createPo.isPending ? 'Creating…' : 'Create Purchase Order'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}

export function PurchaseOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canEdit = usePermission('inventory.edit')
  const { data, isLoading, isError, refetch } = usePurchaseOrder(id)
  const receiveLine = useReceivePurchaseOrderLine(id ?? '')
  const setStatus = useSetPurchaseOrderStatus(id ?? '')

  const receive = async (lineId: string, quantity: number) => {
    await receiveLine.mutateAsync({ lineId, quantity })
    toast.success('Receipt recorded')
  }

  const changeStatus = async (status: 'Sent' | 'Cancelled') => {
    await setStatus.mutateAsync(status)
    toast.success(`Purchase order ${status === 'Sent' ? 'sent' : 'cancelled'}`)
  }

  const canTransition = data && data.status !== 'Received' && data.status !== 'Cancelled'

  return (
    <div className="space-y-4">
      <PageHeader
        title={data?.poNumber ?? 'Purchase Order'}
        subtitle="Line items, receiving, and documents."
        breadcrumbs={[{ label: 'Purchase Orders', to: '/inventory/purchase-orders' }, { label: data?.poNumber ?? 'Detail' }]}
        secondaryActions={
          data ? (
            <div className="flex items-center gap-2">
              <StatusBadge status={data.status} />
              {canEdit && canTransition && data.status === 'Draft' ? (
                <button type="button" disabled={setStatus.isPending} onClick={() => void changeStatus('Sent')} className="rounded-lg bg-[var(--app-primary)] px-3 py-1.5 text-xs font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">Mark Sent</button>
              ) : null}
              {canEdit && canTransition ? (
                <button type="button" disabled={setStatus.isPending} onClick={() => void changeStatus('Cancelled')} className="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-60">Cancel</button>
              ) : null}
            </div>
          ) : undefined
        }
      />
      {isLoading ? null : isError ? (
        <ErrorState
          title="Purchase order unavailable"
          description="We couldn’t load this purchase order, so we can’t say whether it still exists."
          onRetry={() => void refetch()}
        />
      ) : !data ? (
        <EmptyState title="Purchase order not found" description="This purchase order may have been removed." />
      ) : (
        <>
          <div className="overflow-x-auto rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
            <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
              <thead className="bg-slate-50">
                <tr>
                  <th className="px-4 py-3 font-medium text-slate-600">Item</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Ordered</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Received</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Unit Cost</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Line Total</th>
                  <th className="px-4 py-3 font-medium text-slate-600"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.lines.map((line) => (
                  <tr key={line.id}>
                    <td className="px-4 py-3 text-slate-700">{line.itemName}</td>
                    <td className="px-4 py-3 text-slate-700">{line.quantityOrdered}</td>
                    <td className="px-4 py-3 text-slate-700">{line.quantityReceived}</td>
                    <td className="px-4 py-3 text-slate-700">{line.unitCost.toLocaleString()}</td>
                    <td className="px-4 py-3 font-medium text-slate-900">{line.lineTotal.toLocaleString()}</td>
                    <td className="px-4 py-3 text-right">
                      {canEdit && data.status !== 'Cancelled' ? (
                        <button
                          type="button"
                          disabled={receiveLine.isPending || line.quantityReceived >= line.quantityOrdered}
                          onClick={() => void receive(line.id, line.quantityOrdered - line.quantityReceived)}
                          className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
                        >
                          Receive Remaining
                        </button>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
            <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Receiving History</h2>
            {data.receivingHistory.length === 0 ? (
              <p className="mt-3 text-sm text-slate-500">No receipts recorded yet.</p>
            ) : (
              <ul className="mt-4 space-y-3">
                {data.receivingHistory.map((entry, index) => (
                  <li key={index} className="flex items-center justify-between border-b border-slate-100 pb-3 text-sm last:border-0 last:pb-0">
                    <span className="text-slate-700">{entry.itemName} × {entry.quantity} — <span className="text-slate-500">{entry.receivedBy}</span></span>
                    <span className="text-slate-500">{formatDate(entry.receivedAt)}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </>
      )}
    </div>
  )
}
