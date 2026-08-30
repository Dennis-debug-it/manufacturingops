import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Trash2 } from 'lucide-react'
import { EmptyState } from '../../../shared/components/EmptyState'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Spinner } from '../../../shared/components/Spinner'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { useAuth } from '../../../shared/hooks/useAuth'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDateTime } from '../../../shared/utils/dates'
import { useMaterialOptions } from '../pickerApi'
import { useCreatePartsRequest, useDeliverPartsRequest, usePartsRequestsForWorkOrder } from '../partsRequestsApi'
import type { PartsRequest } from '../partsRequestsApi'

interface DraftItem {
  key: string
  inventoryItemId: string | null
  label: string
  description: string
  quantityRequested: string
}

function emptyDraftItem(): DraftItem {
  return { key: crypto.randomUUID(), inventoryItemId: null, label: '', description: '', quantityRequested: '1' }
}

function RequestCard({ request, canDeliver, currentUserId }: { request: PartsRequest; canDeliver: boolean; currentUserId: string | undefined }) {
  const deliverRequest = useDeliverPartsRequest(request.workOrderId)
  const isOwnRequest = request.technicianId === currentUserId

  const confirmDelivery = async () => {
    try {
      await deliverRequest.mutateAsync(request.id)
      toast.success('Delivery confirmed')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <div className="flex items-start justify-between gap-2">
        <div>
          <StatusBadge status={request.status} />
          <p className="mt-1 text-xs text-slate-500">Requested {formatDateTime(request.requestedAt)}</p>
        </div>
        {canDeliver && isOwnRequest && request.status === 'Dispatched' ? (
          <button
            type="button"
            onClick={confirmDelivery}
            disabled={deliverRequest.isPending}
            className="inline-flex h-9 items-center rounded-lg bg-[var(--app-primary)] px-3 text-xs font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            {deliverRequest.isPending ? 'Confirming…' : 'Confirm Delivery'}
          </button>
        ) : null}
      </div>

      <div className="mt-3 space-y-1.5">
        {request.items.map((item) => (
          <div key={item.id} className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2 text-sm">
            <span className="text-slate-700">{item.itemName ?? item.description ?? 'Item'}</span>
            <span className="text-xs text-slate-500">
              {item.quantityApproved != null ? `${item.quantityApproved} / ${item.quantityRequested}` : `${item.quantityRequested} requested`}
            </span>
          </div>
        ))}
      </div>

      {request.notes ? <p className="mt-3 text-xs text-slate-500">Notes: {request.notes}</p> : null}
      {request.status === 'Rejected' && request.rejectionReason ? (
        <p className="mt-2 text-xs text-rose-600">Rejected: {request.rejectionReason}</p>
      ) : null}
    </div>
  )
}

export function PartsTab({ workOrderId }: { workOrderId: string }) {
  const { user } = useAuth()
  const canRequest = usePermission('parts-requests.create')
  const canDeliver = usePermission('parts-requests.deliver')
  const { data, isLoading, isError } = usePartsRequestsForWorkOrder(workOrderId)
  const createRequest = useCreatePartsRequest(workOrderId)

  const [open, setOpen] = useState(false)
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 250)
  const { data: materials, isLoading: materialsLoading } = useMaterialOptions(debouncedSearch)
  const [items, setItems] = useState<DraftItem[]>([emptyDraftItem()])
  const [notes, setNotes] = useState('')

  const closeDrawer = () => {
    setOpen(false)
    setItems([emptyDraftItem()])
    setNotes('')
    setSearch('')
  }

  const updateItem = (key: string, patch: Partial<DraftItem>) => {
    setItems((prev) => prev.map((item) => (item.key === key ? { ...item, ...patch } : item)))
  }

  const addItemRow = () => setItems((prev) => [...prev, emptyDraftItem()])
  const removeItemRow = (key: string) => setItems((prev) => (prev.length > 1 ? prev.filter((item) => item.key !== key) : prev))

  const submit = async () => {
    const payloadItems = items
      .map((item) => ({
        inventoryItemId: item.inventoryItemId,
        description: item.inventoryItemId ? null : item.description.trim() || null,
        quantityRequested: Number(item.quantityRequested),
      }))
      .filter((item) => (item.inventoryItemId || item.description) && Number.isFinite(item.quantityRequested) && item.quantityRequested > 0)

    if (payloadItems.length === 0) {
      toast.error('Add at least one part with a valid quantity')
      return
    }

    try {
      await createRequest.mutateAsync({ notes: notes.trim() || null, items: payloadItems })
      toast.success('Parts request submitted')
      closeDrawer()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading parts requests" />
  }

  if (isError || !data) {
    return <EmptyState title="Parts requests unavailable" description="Parts requests could not be loaded." />
  }

  return (
    <div className="space-y-4">
      {canRequest ? (
        <div className="flex items-center justify-between">
          <p className="text-sm text-slate-600">Request parts from stores without leaving this work order.</p>
          <button
            type="button"
            onClick={() => setOpen(true)}
            className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]"
          >
            <Plus className="h-4 w-4" />
            Request Parts
          </button>
        </div>
      ) : null}

      {data.length === 0 ? (
        <EmptyState title="No parts requests yet" description="Parts requested for this work order will appear here." />
      ) : (
        <div className="space-y-3">
          {data.map((request) => (
            <RequestCard key={request.id} request={request} canDeliver={canDeliver} currentUserId={user?.userId} />
          ))}
        </div>
      )}

      <FormDrawer
        open={open}
        title="Request Parts"
        onClose={closeDrawer}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={closeDrawer} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              Cancel
            </button>
            <button
              type="button"
              onClick={submit}
              disabled={createRequest.isPending}
              className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60"
            >
              {createRequest.isPending ? 'Submitting…' : 'Submit Request'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="parts-search">Search Inventory</label>
            <input
              id="parts-search"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search by name or code…"
              className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
            />
          </div>

          <div className="space-y-3">
            {items.map((item) => (
              <div key={item.key} className="rounded-xl border border-slate-200 p-3">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-semibold uppercase tracking-[0.14em] text-slate-500">Part</span>
                  {items.length > 1 ? (
                    <button type="button" onClick={() => removeItemRow(item.key)} className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-rose-600">
                      <Trash2 className="h-4 w-4" />
                    </button>
                  ) : null}
                </div>
                <select
                  value={item.inventoryItemId ?? ''}
                  onChange={(event) => {
                    const material = (materials ?? []).find((option) => option.id === event.target.value)
                    updateItem(item.key, { inventoryItemId: event.target.value || null, label: material?.itemName ?? '' })
                  }}
                  className="mt-2 h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
                >
                  <option value="">{materialsLoading ? 'Loading…' : 'Select from inventory (optional)'}</option>
                  {(materials ?? []).map((material) => (
                    <option key={material.id} value={material.id}>{material.itemName} — {material.quantityOnHand} {material.unitOfMeasure} in stock</option>
                  ))}
                </select>
                {!item.inventoryItemId ? (
                  <input
                    value={item.description}
                    onChange={(event) => updateItem(item.key, { description: event.target.value })}
                    placeholder="Or describe a non-catalogued part…"
                    className="mt-2 h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
                  />
                ) : null}
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  value={item.quantityRequested}
                  onChange={(event) => updateItem(item.key, { quantityRequested: event.target.value })}
                  placeholder="Quantity"
                  className="mt-2 h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
                />
              </div>
            ))}
            <button type="button" onClick={addItemRow} className="inline-flex h-10 items-center gap-1.5 rounded-lg border border-dashed border-slate-300 px-3 text-xs font-medium text-slate-600 hover:bg-slate-50">
              <Plus className="h-3.5 w-3.5" />
              Add another part
            </button>
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="parts-notes">Notes</label>
            <textarea id="parts-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
        </div>
      </FormDrawer>
    </div>
  )
}
