import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDateTime } from '../../shared/utils/dates'
import { useTechnicianOptions } from '../work-orders/pickerApi'
import {
  useApprovePartsRequest,
  useDispatchPartsRequest,
  usePartsRequests,
  useRejectPartsRequest,
} from '../work-orders/partsRequestsApi'
import type { PartsRequest } from '../work-orders/partsRequestsApi'
import { InventoryTabNav } from './components/InventoryTabNav'

type PendingAction = { id: string; type: 'approve' | 'dispatch' } | null

function ActionButtons({ request, onReject }: { request: PartsRequest; onReject: (request: PartsRequest) => void }) {
  const canApprove = usePermission('parts-requests.approve')
  const canDispatch = usePermission('parts-requests.dispatch')
  const [pending, setPending] = useState<PendingAction>(null)
  const approve = useApprovePartsRequest()
  const dispatch = useDispatchPartsRequest()

  const run = async () => {
    if (!pending) return
    try {
      if (pending.type === 'approve') await approve.mutateAsync({ id: request.id })
      if (pending.type === 'dispatch') await dispatch.mutateAsync(request.id)
      toast.success(pending.type === 'approve' ? 'Request approved' : 'Request dispatched')
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setPending(null)
    }
  }

  return (
    <div className="flex justify-end gap-2">
      {canApprove && request.status === 'Pending' ? (
        <>
          <button type="button" onClick={() => setPending({ id: request.id, type: 'approve' })} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Approve</button>
          <button type="button" onClick={() => onReject(request)} className="rounded-lg border border-rose-200 bg-white px-3 py-1.5 text-xs font-medium text-rose-600 hover:bg-rose-50">Reject</button>
        </>
      ) : null}
      {canDispatch && request.status === 'Approved' ? (
        <button type="button" onClick={() => setPending({ id: request.id, type: 'dispatch' })} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Dispatch</button>
      ) : null}
      <ConfirmDialog
        open={pending !== null}
        title={pending?.type === 'approve' ? 'Approve Parts Request' : 'Dispatch Parts Request'}
        message={pending?.type === 'approve' ? 'This approves the requested quantities in full.' : 'This marks the request as dispatched from stores.'}
        onCancel={() => setPending(null)}
        onConfirm={run}
      />
    </div>
  )
}

export function PartsRequestsPage() {
  const [status, setStatus] = useState('')
  const [technicianId, setTechnicianId] = useState('')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [woSearch, setWoSearch] = useState('')
  const [rejecting, setRejecting] = useState<PartsRequest | null>(null)
  const [rejectionReason, setRejectionReason] = useState('')

  const { data: technicians } = useTechnicianOptions()
  const { data, isLoading, isError } = usePartsRequests({
    status: status || undefined,
    technicianId: technicianId || undefined,
    fromDate: fromDate || undefined,
    toDate: toDate || undefined,
  })
  const rejectRequest = useRejectPartsRequest()

  const filtered = useMemo(() => {
    const rows = data ?? []
    if (!woSearch.trim()) return rows
    const term = woSearch.trim().toLowerCase()
    return rows.filter((row) => (row.workOrderNumber ?? '').toLowerCase().includes(term))
  }, [data, woSearch])

  const closeReject = () => {
    setRejecting(null)
    setRejectionReason('')
  }

  const submitReject = async () => {
    if (!rejecting) return
    if (!rejectionReason.trim()) {
      toast.error('A rejection reason is required')
      return
    }
    try {
      await rejectRequest.mutateAsync({ id: rejecting.id, reason: rejectionReason.trim() })
      toast.success('Request rejected')
      closeReject()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const columns: DataTableColumn<PartsRequest>[] = [
    { key: 'workOrderNumber', header: 'Work Order', cell: (row) => row.workOrderNumber ?? '—' },
    { key: 'technicianName', header: 'Technician', cell: (row) => row.technicianName ?? '—' },
    { key: 'items', header: 'Parts', cell: (row) => row.items.map((item) => item.itemName ?? item.description ?? 'Item').join(', ') || '—' },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'requestedAt', header: 'Requested', cell: (row) => formatDateTime(row.requestedAt) },
    { key: 'actions', header: '', cell: (row) => <ActionButtons request={row} onReject={setRejecting} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader title="Parts Requests" subtitle="Approval workflow: Pending → Approved → Dispatched → Delivered." />
      <InventoryTabNav />

      <div className="flex flex-wrap items-center gap-2">
        <select value={status} onChange={(event) => setStatus(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
          <option value="">All statuses</option>
          <option value="Pending">Pending</option>
          <option value="Approved">Approved</option>
          <option value="Dispatched">Dispatched</option>
          <option value="Delivered">Delivered</option>
          <option value="Rejected">Rejected</option>
        </select>
        <select value={technicianId} onChange={(event) => setTechnicianId(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
          <option value="">All technicians</option>
          {(technicians ?? []).map((technician) => (
            <option key={technician.id} value={technician.id}>{technician.fullName}</option>
          ))}
        </select>
        <input
          value={woSearch}
          onChange={(event) => setWoSearch(event.target.value)}
          placeholder="Search work order…"
          className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
        <input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        <span className="text-sm text-slate-400">to</span>
        <input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
      </div>

      <DataTable
        columns={columns}
        data={filtered}
        loading={isLoading}
        emptyTitle={isError ? 'Requests unavailable' : 'No parts requests'}
        emptyDescription={isError ? 'Parts requests could not be loaded.' : 'Requests raised from work orders will appear here.'}
      />

      <FormDrawer
        open={rejecting !== null}
        title="Reject Parts Request"
        onClose={closeReject}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={closeReject} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button
              type="button"
              onClick={submitReject}
              disabled={rejectRequest.isPending}
              className="inline-flex h-11 items-center justify-center rounded-lg bg-rose-600 px-4 text-sm font-medium text-white transition hover:bg-rose-700 disabled:opacity-60"
            >
              {rejectRequest.isPending ? 'Rejecting…' : 'Reject Request'}
            </button>
          </div>
        }
      >
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="reject-reason">Reason</label>
          <textarea id="reject-reason" value={rejectionReason} onChange={(event) => setRejectionReason(event.target.value)} rows={4} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
      </FormDrawer>
    </div>
  )
}
