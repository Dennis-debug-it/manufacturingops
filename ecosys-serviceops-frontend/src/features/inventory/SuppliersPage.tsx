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
import { useCreateSupplier, useSupplier, useSuppliers, type SupplierListItem } from './purchaseOrdersApi'

const backendGapNote = 'No SuppliersController exists in the current backend yet. This is the full intended list + detail UI wired against typed stub data — creating a supplier will not persist until a real endpoint is connected.'

export function SuppliersPage() {
  const canEdit = usePermission('inventory.edit')
  const { data, isLoading } = useSuppliers()
  const createSupplier = useCreateSupplier()
  const [showNewSupplier, setShowNewSupplier] = useState(false)
  const [supplierName, setSupplierName] = useState('')
  const [contactPerson, setContactPerson] = useState('')
  const [phone, setPhone] = useState('')
  const [email, setEmail] = useState('')

  const submit = async () => {
    try {
      await createSupplier.mutateAsync({ supplierName, contactPerson: contactPerson || undefined, phone: phone || undefined, email: email || undefined })
      toast.success('Supplier created')
      setShowNewSupplier(false)
    } catch {
      toast.error('Suppliers are not yet backed by a live API — this is a UI stub.')
    }
  }

  const columns: DataTableColumn<SupplierListItem>[] = [
    { key: 'supplierName', header: 'Supplier', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/suppliers/${row.id}`}>{row.supplierName}</Link> },
    { key: 'contactPerson', header: 'Contact', cell: (row) => row.contactPerson ?? '—' },
    { key: 'phone', header: 'Phone', cell: (row) => row.phone ?? '—' },
    { key: 'onTimeDeliveryPercent', header: 'On-Time Delivery', cell: (row) => `${row.onTimeDeliveryPercent}%` },
    { key: 'openPurchaseOrders', header: 'Open POs', cell: (row) => row.openPurchaseOrders },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Suppliers"
        subtitle="Supplier list and performance metrics."
        primaryAction={
          canEdit ? (
            <button type="button" onClick={() => setShowNewSupplier(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
              <Plus className="h-4 w-4" />New Supplier
            </button>
          ) : undefined
        }
      />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">{backendGapNote}</div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle="No suppliers yet"
        emptyDescription="Suppliers used for purchase orders and parts sourcing will appear here once the backend endpoint is connected."
      />

      <FormDrawer open={showNewSupplier} title="New Supplier" onClose={() => setShowNewSupplier(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Supplier Name</label>
            <input value={supplierName} onChange={(event) => setSupplierName(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Contact Person</label>
            <input value={contactPerson} onChange={(event) => setContactPerson(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Phone</label>
            <input value={phone} onChange={(event) => setPhone(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Email</label>
            <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <button
            type="button"
            disabled={!supplierName.trim() || createSupplier.isPending}
            onClick={() => void submit()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
          >
            {createSupplier.isPending ? 'Creating…' : 'Create Supplier'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}

export function SupplierDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading } = useSupplier(id)

  return (
    <div className="space-y-6">
      <PageHeader
        title={data?.supplierName ?? 'Supplier'}
        subtitle="Parts, purchase orders, and supplier performance."
        breadcrumbs={[{ label: 'Suppliers', to: '/inventory/suppliers' }, { label: data?.supplierName ?? 'Detail' }]}
        secondaryActions={data ? <StatusBadge status={data.status} /> : undefined}
      />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">{backendGapNote}</div>
      {isLoading ? null : !data ? (
        <EmptyState title="No supplier detail yet" description="Contact info, supplied parts, and purchase order history will appear here once the backend endpoint is connected." />
      ) : (
        <>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {[
              ['Contact Person', data.contactPerson ?? '—'],
              ['Phone', data.phone ?? '—'],
              ['Email', data.email ?? '—'],
              ['On-Time Delivery', `${data.onTimeDeliveryPercent}%`],
            ].map(([label, value]) => (
              <div key={label} className="rounded-xl border border-slate-200 bg-white px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                <div className="mt-2 text-sm text-slate-900">{value}</div>
              </div>
            ))}
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Supplied Parts</h2>
            {data.suppliedParts.length === 0 ? (
              <p className="mt-3 text-sm text-slate-500">No parts recorded for this supplier yet.</p>
            ) : (
              <ul className="mt-4 space-y-3">
                {data.suppliedParts.map((part, index) => (
                  <li key={index} className="flex items-center justify-between border-b border-slate-100 pb-3 text-sm last:border-0 last:pb-0">
                    <span className="text-slate-700">{part.itemName}</span>
                    <span className="text-slate-500">Last cost {part.lastUnitCost.toLocaleString()} · {formatDate(part.lastSuppliedAt)}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Recent Purchase Orders</h2>
            {data.recentPurchaseOrders.length === 0 ? (
              <p className="mt-3 text-sm text-slate-500">No purchase orders yet.</p>
            ) : (
              <ul className="mt-4 space-y-3">
                {data.recentPurchaseOrders.map((po) => (
                  <li key={po.id} className="flex items-center justify-between border-b border-slate-100 pb-3 text-sm last:border-0 last:pb-0">
                    <Link to={`/inventory/purchase-orders/${po.id}`} className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4">{po.poNumber}</Link>
                    <StatusBadge status={po.status} />
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
