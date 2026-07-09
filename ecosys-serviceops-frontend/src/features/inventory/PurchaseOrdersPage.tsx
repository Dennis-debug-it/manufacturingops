import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import {
  useCreatePurchaseOrder,
  usePurchaseOrder,
  usePurchaseOrders,
  useReceivePurchaseOrderLine,
  type PurchaseOrderListItem,
  type PurchaseOrderStatus,
} from './purchaseOrdersApi'

const backendGapNote = 'No PurchaseOrdersController exists in the current backend yet. This is the full intended list + receiving workflow UI wired against typed stub data — creating or receiving a PO will not persist until a real endpoint is connected.'
const statusOptions: Array<PurchaseOrderStatus | 'All'> = ['All', 'Draft', 'Sent', 'PartiallyReceived', 'Received', 'Cancelled']

export function PurchaseOrdersPage() {
  const canEdit = usePermission('inventory.edit')
  const [statusFilter, setStatusFilter] = useState<PurchaseOrderStatus | 'All'>('All')
  const { data, isLoading } = usePurchaseOrders(statusFilter)
  const createPo = useCreatePurchaseOrder()
  const [showNewPo, setShowNewPo] = useState(false)
  const [supplierId, setSupplierId] = useState('')
  const [expectedAt, setExpectedAt] = useState('')
  const [notes, setNotes] = useState('')

  const submit = async () => {
    try {
      await createPo.mutateAsync({ supplierId, expectedAt: expectedAt || undefined, lines: [], notes: notes || undefined })
      toast.success('Purchase order created')
      setShowNewPo(false)
    } catch {
      toast.error('Purchase orders are not yet backed by a live API — this is a UI stub.')
    }
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
    <div className="space-y-6">
      <PageHeader
        title="Purchase Orders"
        subtitle="Draft, sent, received, and cancelled purchase orders."
        primaryAction={
          canEdit ? (
            <button type="button" onClick={() => setShowNewPo(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
              <Plus className="h-4 w-4" />New Purchase Order
            </button>
          ) : undefined
        }
      />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">{backendGapNote}</div>
      <div className="flex flex-wrap gap-2 rounded-2xl border border-slate-200 bg-white p-3 shadow-sm">
        {statusOptions.map((status) => (
          <button
            key={status}
            type="button"
            onClick={() => setStatusFilter(status)}
            className={`inline-flex h-10 items-center rounded-lg px-4 text-sm font-medium transition ${statusFilter === status ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'}`}
          >
            {status === 'PartiallyReceived' ? 'Partially Received' : status}
          </button>
        ))}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle="No purchase orders yet"
        emptyDescription="Purchase orders raised against suppliers will appear here once the backend endpoint is connected."
      />

      <FormDrawer open={showNewPo} title="New Purchase Order" onClose={() => setShowNewPo(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Supplier ID</label>
            <input value={supplierId} onChange={(event) => setSupplierId(event.target.value)} placeholder="Supplier GUID" className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Expected Delivery Date</label>
            <input type="date" value={expectedAt} onChange={(event) => setExpectedAt(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Notes</label>
            <textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
          <p className="text-xs text-slate-500">Line items are added after the PO is created once a receiving workflow exists.</p>
          <button
            type="button"
            disabled={!supplierId.trim() || createPo.isPending}
            onClick={() => void submit()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
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
  const { data, isLoading } = usePurchaseOrder(id)
  const receiveLine = useReceivePurchaseOrderLine(id ?? '')

  const receive = async (lineId: string, quantity: number) => {
    try {
      await receiveLine.mutateAsync({ lineId, quantity })
      toast.success('Receipt recorded')
    } catch {
      toast.error('Receiving is not yet backed by a live API — this is a UI stub.')
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={data?.poNumber ?? 'Purchase Order'}
        subtitle="Line items, receiving, and documents."
        breadcrumbs={[{ label: 'Purchase Orders', to: '/inventory/purchase-orders' }, { label: data?.poNumber ?? 'Detail' }]}
        secondaryActions={data ? <StatusBadge status={data.status} /> : undefined}
      />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">{backendGapNote}</div>
      {isLoading ? null : !data ? (
        <EmptyState title="No purchase order detail yet" description="Line items, receiving history, and documents will appear here once the backend endpoint is connected." />
      ) : (
        <>
          <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
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
                      <button
                        type="button"
                        disabled={receiveLine.isPending || line.quantityReceived >= line.quantityOrdered}
                        onClick={() => void receive(line.id, line.quantityOrdered - line.quantityReceived)}
                        className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
                      >
                        Receive Remaining
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
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
