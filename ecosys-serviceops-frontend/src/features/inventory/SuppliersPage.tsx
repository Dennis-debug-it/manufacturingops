import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { ErrorState } from '../../shared/components/ErrorState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import {
  useCreateSupplier,
  useSetSupplierStatus,
  useSupplier,
  useSuppliers,
  useUpdateSupplier,
  type CreateSupplierPayload,
  type SupplierDetail,
  type SupplierListItem,
} from './purchaseOrdersApi'
import { InventoryTabNav } from './components/InventoryTabNav'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

interface SupplierFormValues {
  supplierName: string
  contactPerson: string
  phone: string
  email: string
  address: string
  notes: string
  leadTimeDays: string
}

const blankForm: SupplierFormValues = { supplierName: '', contactPerson: '', phone: '', email: '', address: '', notes: '', leadTimeDays: '' }

function toPayload(values: SupplierFormValues): CreateSupplierPayload {
  const text = (value: string) => value.trim() || undefined
  return {
    supplierName: values.supplierName.trim(),
    contactPerson: text(values.contactPerson),
    phone: text(values.phone),
    email: text(values.email),
    address: text(values.address),
    notes: text(values.notes),
    leadTimeDays: values.leadTimeDays.trim() ? Number(values.leadTimeDays) : null,
  }
}

/** Shared by New Supplier and Edit Supplier. */
function SupplierFormDrawer({
  title,
  initial,
  submitLabel,
  pending,
  onSubmit,
  onClose,
}: {
  title: string
  initial: SupplierFormValues
  submitLabel: string
  pending: boolean
  onSubmit: (payload: CreateSupplierPayload) => Promise<void>
  onClose: () => void
}) {
  const [values, setValues] = useState(initial)
  const set = (key: keyof SupplierFormValues) => (event: { target: { value: string } }) => setValues((prev) => ({ ...prev, [key]: event.target.value }))

  const submit = async () => {
    const lead = values.leadTimeDays.trim() ? Number(values.leadTimeDays) : null
    if (lead != null && (!Number.isInteger(lead) || lead < 0 || lead > 365)) return toast.error('Lead time must be a whole number of days, 0–365')
    try {
      await onSubmit(toPayload(values))
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={title}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" disabled={!values.supplierName.trim() || pending} onClick={() => void submit()} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {pending ? 'Saving…' : submitLabel}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="supplier-name">Supplier Name</label>
          <input id="supplier-name" value={values.supplierName} onChange={set('supplierName')} className={fieldClass} />
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label className={labelClass} htmlFor="supplier-contact">Contact Person</label>
            <input id="supplier-contact" value={values.contactPerson} onChange={set('contactPerson')} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="supplier-phone">Phone</label>
            <input id="supplier-phone" value={values.phone} onChange={set('phone')} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="supplier-email">Email</label>
          <input id="supplier-email" type="email" value={values.email} onChange={set('email')} className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="supplier-address">Address</label>
          <input id="supplier-address" value={values.address} onChange={set('address')} className={fieldClass} />
        </div>
        <div>
          <label className={labelClass} htmlFor="supplier-lead">Lead Time (days)</label>
          <input id="supplier-lead" type="number" min="0" max="365" step="1" value={values.leadTimeDays} onChange={set('leadTimeDays')} placeholder="Workspace default" className={fieldClass} />
          <p className="mt-1 text-xs text-slate-500">Sets the expected delivery date on new purchase orders and the timing of reorder suggestions.</p>
        </div>
        <div>
          <label className={labelClass} htmlFor="supplier-notes">Notes</label>
          <textarea id="supplier-notes" rows={3} value={values.notes} onChange={set('notes')} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
      </div>
    </FormDrawer>
  )
}

export function SuppliersPage() {
  const canEdit = usePermission('inventory.edit')
  const { data, isLoading, isError, refetch } = useSuppliers()
  const createSupplier = useCreateSupplier()
  const [showNewSupplier, setShowNewSupplier] = useState(false)
  const [showInactive, setShowInactive] = useState(false)
  const rows = (data ?? []).filter((supplier) => showInactive || supplier.status === 'Active')
  const inactiveCount = (data ?? []).filter((supplier) => supplier.status !== 'Active').length

  const columns: DataTableColumn<SupplierListItem>[] = [
    { key: 'supplierName', header: 'Supplier', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/suppliers/${row.id}`}>{row.supplierName}</Link> },
    { key: 'contactPerson', header: 'Contact', cell: (row) => row.contactPerson ?? '—' },
    { key: 'phone', header: 'Phone', cell: (row) => row.phone ?? '—' },
    { key: 'leadTimeDays', header: 'Lead Time', cell: (row) => (row.leadTimeDays != null ? `${row.leadTimeDays} d` : 'Default') },
    { key: 'onTimeDeliveryPercent', header: 'On-Time Delivery', cell: (row) => `${row.onTimeDeliveryPercent}%` },
    { key: 'openPurchaseOrders', header: 'Open POs', cell: (row) => row.openPurchaseOrders },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Suppliers"
        subtitle="Supplier list and performance metrics."
        primaryAction={
          canEdit ? (
            <button type="button" onClick={() => setShowNewSupplier(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Supplier
            </button>
          ) : undefined
        }
      />
      <InventoryTabNav />
      {inactiveCount > 0 ? (
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" checked={showInactive} onChange={(event) => setShowInactive(event.target.checked)} />
          Show {inactiveCount} inactive supplier{inactiveCount === 1 ? '' : 's'}
        </label>
      ) : null}
      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Suppliers unavailable' : 'No suppliers yet'}
        emptyDescription={isError ? 'The supplier list could not be loaded — this is not a confirmation that none exist.' : 'Add a supplier to source parts and raise purchase orders.'}
        emptyActionLabel={isError ? 'Retry' : undefined}
        onEmptyAction={isError ? () => void refetch() : undefined}
      />

      {showNewSupplier ? (
        <SupplierFormDrawer
          title="New Supplier"
          initial={blankForm}
          submitLabel="Create Supplier"
          pending={createSupplier.isPending}
          onSubmit={async (payload) => {
            await createSupplier.mutateAsync(payload)
            toast.success('Supplier created')
          }}
          onClose={() => setShowNewSupplier(false)}
        />
      ) : null}
    </div>
  )
}

function SupplierActions({ supplier }: { supplier: SupplierDetail }) {
  const updateSupplier = useUpdateSupplier(supplier.id)
  const setStatus = useSetSupplierStatus(supplier.id)
  const [editing, setEditing] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const active = supplier.status === 'Active'

  return (
    <div className="flex items-center gap-2">
      <StatusBadge status={supplier.status} />
      <button type="button" onClick={() => setEditing(true)} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Edit</button>
      <button
        type="button"
        onClick={() => setConfirming(true)}
        className={active ? 'rounded-lg border border-red-200 px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50' : 'rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50'}
      >
        {active ? 'Deactivate' : 'Reactivate'}
      </button>
      {editing ? (
        <SupplierFormDrawer
          title={`Edit ${supplier.supplierName}`}
          initial={{
            supplierName: supplier.supplierName,
            contactPerson: supplier.contactPerson ?? '',
            phone: supplier.phone ?? '',
            email: supplier.email ?? '',
            address: supplier.address ?? '',
            notes: supplier.notes ?? '',
            leadTimeDays: supplier.leadTimeDays != null ? String(supplier.leadTimeDays) : '',
          }}
          submitLabel="Save Changes"
          pending={updateSupplier.isPending}
          onSubmit={async (payload) => {
            await updateSupplier.mutateAsync(payload)
            toast.success('Supplier updated')
          }}
          onClose={() => setEditing(false)}
        />
      ) : null}
      <ConfirmDialog
        open={confirming}
        title={active ? 'Deactivate Supplier' : 'Reactivate Supplier'}
        message={active
          ? `${supplier.supplierName} will no longer be offered for new purchase orders. Its history, open orders and bills are kept.`
          : `${supplier.supplierName} can be ordered from again.`}
        confirmLabel={active ? 'Deactivate' : 'Reactivate'}
        danger={active}
        onCancel={() => setConfirming(false)}
        onConfirm={async () => {
          try {
            await setStatus.mutateAsync(!active)
            toast.success(active ? 'Supplier deactivated' : 'Supplier reactivated')
          } catch {
            // interceptor surfaces the error toast
          } finally {
            setConfirming(false)
          }
        }}
      />
    </div>
  )
}

export function SupplierDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canEdit = usePermission('inventory.edit')
  const { data, isLoading, isError, refetch } = useSupplier(id)

  return (
    <div className="space-y-4">
      <PageHeader
        title={data?.supplierName ?? 'Supplier'}
        subtitle="Parts, purchase orders, and supplier performance."
        breadcrumbs={[{ label: 'Suppliers', to: '/inventory/suppliers' }, { label: data?.supplierName ?? 'Detail' }]}
        secondaryActions={data ? (canEdit ? <SupplierActions supplier={data} /> : <StatusBadge status={data.status} />) : undefined}
      />
      {isLoading ? null : isError ? (
        <ErrorState
          title="Supplier unavailable"
          description="We couldn’t load this supplier, so we can’t say whether it still exists."
          onRetry={() => void refetch()}
        />
      ) : !data ? (
        <EmptyState title="Supplier not found" description="This supplier may have been removed." />
      ) : (
        <>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {[
              ['Contact Person', data.contactPerson ?? '—'],
              ['Phone', data.phone ?? '—'],
              ['Email', data.email ?? '—'],
              ['On-Time Delivery', `${data.onTimeDeliveryPercent}%`],
              ['Lead Time', data.leadTimeDays != null ? `${data.leadTimeDays} days` : 'Workspace default'],
              ['Address', data.address ?? '—'],
            ].map(([label, value]) => (
              <div key={label} className="rounded-xl border border-slate-200 bg-white px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                <div className="mt-2 text-sm text-slate-900">{value}</div>
              </div>
            ))}
          </div>
          {data.notes ? <p className="rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700">{data.notes}</p> : null}

          <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
            <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Preferred Supplier For</h2>
            {(data.preferredParts ?? []).length === 0 ? (
              <p className="mt-3 text-sm text-slate-500">No parts name this as their preferred supplier. Set it on a part’s Edit form.</p>
            ) : (
              <ul className="mt-4 space-y-3">
                {(data.preferredParts ?? []).map((part) => (
                  <li key={part.id} className="flex items-center justify-between border-b border-slate-100 pb-3 text-sm last:border-0 last:pb-0">
                    <Link to={`/inventory/parts/${part.id}`} className="text-slate-900 underline decoration-slate-300 underline-offset-4">{part.itemName} <span className="font-mono text-xs text-slate-500">{part.itemCode}</span></Link>
                    <span className={part.quantityOnHand <= part.reorderLevel ? 'font-medium text-amber-700' : 'text-slate-500'}>{part.quantityOnHand} on hand · reorder at {part.reorderLevel}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
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

          <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
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
