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
import { usePermission } from '../../shared/hooks/usePermission'
import { useUrlFilters } from '../../shared/hooks/useUrlFilters'
import { getActiveCurrency } from '../../shared/utils/currency'
import { formatDate } from '../../shared/utils/dates'
import { useBranches } from './api'
import { MaterialAutocomplete } from './components/MaterialAutocomplete'
import { purchaseOrderChoice, purchaseOrderFiltersFor, purchaseOrderListFilterSpec, purchaseOrderStatuses, type PurchaseOrderChoice } from './listFilters'
import { EditPurchaseOrderDrawer, EmailPurchaseOrderDrawer } from './components/PurchaseOrderDrawers'
import {
  downloadPurchaseOrderPdf,
  useCreatePurchaseOrder,
  usePurchaseOrder,
  usePurchaseOrders,
  useReceivePurchaseOrderLine,
  useSetPurchaseOrderStatus,
  useSuppliers,
  type PurchaseOrderLine,
  type PurchaseOrderListItem,
} from './purchaseOrdersApi'

const choices: PurchaseOrderChoice[] = ['All', 'open', 'late', ...purchaseOrderStatuses]
const choiceLabels: Partial<Record<PurchaseOrderChoice, string>> = { open: 'Open', late: 'Past expected date', PartiallyReceived: 'Partially Received' }

interface DraftLine {
  itemName: string
  /** Set when a catalogue part is picked; typing over the name unlinks it. */
  materialItemId: string | null
  itemCode: string | null
  quantityOrdered: string
  unitCost: string
}

const emptyLine: DraftLine = { itemName: '', materialItemId: null, itemCode: null, quantityOrdered: '1', unitCost: '0' }

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
  const [currency, setCurrency] = useState(getActiveCurrency)

  const resetForm = () => {
    setSupplierId('')
    setCurrency(getActiveCurrency())
    setExpectedAt('')
    setNotes('')
    setLines([{ ...emptyLine }])
  }

  const validLines = lines
    .filter((l) => l.itemName.trim() && Number(l.quantityOrdered) > 0)
    .map((l) => ({ itemName: l.itemName.trim(), materialItemId: l.materialItemId, quantityOrdered: Number(l.quantityOrdered), unitCost: Number(l.unitCost) || 0 }))

  const submit = async () => {
    if (!supplierId) return toast.error('Select a supplier')
    if (validLines.length === 0) return toast.error('Add at least one line item')
    await createPo.mutateAsync({ supplierId, expectedAt: expectedAt || undefined, lines: validLines, notes: notes || undefined, currency: currency.trim() || getActiveCurrency() })
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
              {(suppliers ?? []).filter((s) => s.status === 'Active').map((s) => <option key={s.id} value={s.id}>{s.supplierName}</option>)}
            </select>
            {suppliers && suppliers.length === 0 ? <p className="mt-1 text-xs text-amber-600">No suppliers yet — add one on the Suppliers page first.</p> : null}
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Expected Delivery Date</label>
            <input type="date" value={expectedAt} onChange={(event) => setExpectedAt(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label htmlFor="po-currency" className="mb-1.5 block text-sm font-medium text-slate-700">Currency</label>
            <input id="po-currency" value={currency} maxLength={3} onChange={(event) => setCurrency(event.target.value.toUpperCase())} className="h-11 w-24 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>

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
                      onChange={(next) => setLines((prev) => prev.map((l, i) => (i === index ? { ...l, itemName: next, materialItemId: null, itemCode: null } : l)))}
                      onSelectMaterial={(material) =>
                        setLines((prev) =>
                          prev.map((l, i) =>
                            i === index
                              ? {
                                  ...l,
                                  itemName: material.itemName,
                                  materialItemId: material.id,
                                  itemCode: material.itemCode,
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
                    {line.itemName.trim() ? (
                      <p className={`mt-1 text-xs ${line.materialItemId ? 'text-emerald-700' : 'text-slate-500'}`}>
                        {line.materialItemId ? `Stocked part ${line.itemCode ?? ''} — receiving adds it to stock.` : 'Not a stocked part — receiving records the delivery only. Pick a part from the list to stock it.'}
                      </p>
                    ) : null}
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
  const setStatus = useSetPurchaseOrderStatus(id ?? '')
  const [receiving, setReceiving] = useState<PurchaseOrderLine | null>(null)
  const [drawer, setDrawer] = useState<'edit' | 'email' | null>(null)
  const [downloading, setDownloading] = useState(false)
  const download = async () => {
    if (!data) return
    setDownloading(true)
    try {
      await downloadPurchaseOrderPdf(data.id, data.poNumber)
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setDownloading(false)
    }
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
              <button type="button" disabled={downloading} onClick={() => void download()} className="rounded-lg bg-[var(--app-primary)] px-3 py-1.5 text-xs font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">{downloading ? 'Preparing…' : 'Download PDF'}</button>
              {canEdit && data.status !== 'Cancelled' ? (
                <button type="button" onClick={() => setDrawer('email')} className="rounded-lg bg-[var(--app-primary)] px-3 py-1.5 text-xs font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">Email to supplier</button>
              ) : null}
              {canEdit && data.status === 'Draft' ? (
                <button type="button" onClick={() => setDrawer('edit')} className="rounded-lg bg-[var(--app-primary)] px-3 py-1.5 text-xs font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">Edit</button>
              ) : null}
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
                    <td className="px-4 py-3 text-slate-700">
                      {line.materialItemId ? (
                        <Link className="underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${line.materialItemId}`}>{line.itemName}</Link>
                      ) : line.itemName}
                      <span className="mt-0.5 block text-xs text-slate-500">{line.materialItemId ? <span className="font-mono">{line.itemCode}</span> : 'Non-stock'}</span>
                    </td>
                    <td className="px-4 py-3 text-slate-700">{line.quantityOrdered}</td>
                    <td className="px-4 py-3 text-slate-700">{line.quantityReceived}</td>
                    <td className="px-4 py-3 text-slate-700">{line.unitCost.toLocaleString()}</td>
                    <td className="px-4 py-3 font-medium text-slate-900">{line.lineTotal.toLocaleString()}</td>
                    <td className="px-4 py-3 text-right">
                      {canEdit && data.status !== 'Cancelled' ? (
                        <button
                          type="button"
                          disabled={line.quantityReceived >= line.quantityOrdered}
                          onClick={() => setReceiving(line)}
                          className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
                        >
                          Receive
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
                    <span className="text-slate-700">{entry.itemName} × {entry.quantity}{entry.branchName ? ` into ${entry.branchName}` : ''} — <span className="text-slate-500">{entry.receivedBy}</span></span>
                    <span className="text-slate-500">{formatDate(entry.receivedAt)}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </>
      )}
      {receiving && id ? <ReceiveLineDrawer poId={id} line={receiving} onClose={() => setReceiving(null)} /> : null}
      {drawer === 'edit' && data ? <EditPurchaseOrderDrawer order={data} onClose={() => setDrawer(null)} /> : null}
      {drawer === 'email' && data ? <EmailPurchaseOrderDrawer order={data} onClose={() => setDrawer(null)} /> : null}
    </div>
  )
}

/** Receive part or all of a line's outstanding quantity; a part-linked line also picks the branch it lands in. */
function ReceiveLineDrawer({ poId, line, onClose }: { poId: string; line: PurchaseOrderLine; onClose: () => void }) {
  const outstanding = line.quantityOrdered - line.quantityReceived
  const [quantity, setQuantity] = useState(String(outstanding))
  const [branchId, setBranchId] = useState('')
  const [batchOrSerial, setBatchOrSerial] = useState('')
  const receiveLine = useReceivePurchaseOrderLine(poId)
  const stocked = Boolean(line.materialItemId)
  const { data: branches } = useBranches({ enabled: stocked })
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)

  const submit = async () => {
    const qty = Number(quantity)
    if (!Number.isFinite(qty) || qty <= 0) return toast.error('Enter a quantity greater than zero')
    if (qty > outstanding) return toast.error(`Only ${outstanding} remain outstanding`)
    try {
      await receiveLine.mutateAsync({ lineId: line.id, quantity: qty, branchId: branchId || null, batchOrSerial: batchOrSerial.trim() || null })
      toast.success(stocked ? 'Receipt recorded and stock updated' : 'Receipt recorded')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={`Receive ${line.itemName}`}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={receiveLine.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {receiveLine.isPending ? 'Saving…' : 'Receive'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="po-receive-qty">Quantity received</label>
          <input id="po-receive-qty" type="number" min="0" step="0.001" value={quantity} onChange={(event) => setQuantity(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          <p className="mt-2 text-xs text-slate-500">{line.quantityReceived} of {line.quantityOrdered} received so far · {outstanding} outstanding</p>
        </div>
        {stocked && activeBranches.length > 0 ? (
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="po-receive-branch">Receive into</label>
            <select id="po-receive-branch" value={branchId} onChange={(event) => setBranchId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              <option value="">My default branch</option>
              {activeBranches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
            </select>
          </div>
        ) : null}
        {stocked ? (
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="po-receive-batch">Batch / serial numbers (optional)</label>
            <input id="po-receive-batch" value={batchOrSerial} onChange={(event) => setBatchOrSerial(event.target.value)} placeholder="e.g. LOT-2291 or SN1001, SN1002" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
        ) : null}
        <p className="text-xs text-slate-500">
          {stocked
            ? 'Stock goes up by this quantity and the part’s cost moves to the average of what was on hand and this delivery.'
            : 'This line is not linked to a stocked part, so only the delivery is recorded.'}
        </p>
      </div>
    </FormDrawer>
  )
}
