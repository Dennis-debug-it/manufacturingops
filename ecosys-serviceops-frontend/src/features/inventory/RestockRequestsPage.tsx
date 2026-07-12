import { useState } from 'react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDateTime } from '../../shared/utils/dates'
import { useApproveMaterialRequest, useCloseMaterialRequest, useIssueMaterialRequest, useMaterialRequests } from './api'
import type { MaterialRequest } from './types'
import { InventoryTabNav } from './components/InventoryTabNav'

type PendingAction = { id: string; type: 'approve' | 'issue' | 'close' } | null

function ActionButtons({ request }: { request: MaterialRequest }) {
  const canApprove = usePermission('inventory.requests.approve')
  const canIssue = usePermission('inventory.requests.issue')
  const [pending, setPending] = useState<PendingAction>(null)
  const approve = useApproveMaterialRequest(request.id)
  const issue = useIssueMaterialRequest(request.id)
  const close = useCloseMaterialRequest(request.id)

  const run = async () => {
    if (!pending) return
    try {
      if (pending.type === 'approve') await approve.mutateAsync(undefined)
      if (pending.type === 'issue') await issue.mutateAsync({ lines: request.lines.map((line) => ({ materialRequestLineId: line.id, quantityIssued: line.quantityRequested })) })
      if (pending.type === 'close') await close.mutateAsync({ lines: request.lines.map((line) => ({ materialRequestLineId: line.id, quantityUsed: line.quantityIssued, quantityReturned: 0 })) })
      toast.success(`Request ${pending.type === 'approve' ? 'approved' : pending.type === 'issue' ? 'issued' : 'closed'}`)
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setPending(null)
    }
  }

  return (
    <div className="flex justify-end gap-2">
      {canApprove && request.status === 'Pending' ? (
        <button type="button" onClick={() => setPending({ id: request.id, type: 'approve' })} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Approve</button>
      ) : null}
      {canIssue && request.status === 'Approved' ? (
        <button type="button" onClick={() => setPending({ id: request.id, type: 'issue' })} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Issue</button>
      ) : null}
      {canApprove && request.status === 'Issued' ? (
        <button type="button" onClick={() => setPending({ id: request.id, type: 'close' })} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Close</button>
      ) : null}
      <ConfirmDialog
        open={pending !== null}
        title={`${pending?.type === 'approve' ? 'Approve' : pending?.type === 'issue' ? 'Issue' : 'Close'} Request`}
        message={`This will ${pending?.type} request ${request.requestNumber}.`}
        onCancel={() => setPending(null)}
        onConfirm={run}
      />
    </div>
  )
}

export function RestockRequestsPage() {
  const { data, isLoading, isError } = useMaterialRequests()

  const columns: DataTableColumn<MaterialRequest>[] = [
    { key: 'requestNumber', header: 'Request', cell: (row) => <span className="font-mono text-sm">{row.requestNumber}</span> },
    { key: 'workOrderNumber', header: 'Work Order', cell: (row) => row.workOrderNumber ?? '—' },
    { key: 'requestedByName', header: 'Requested By', cell: (row) => row.requestedByName ?? '—' },
    { key: 'lines', header: 'Lines', cell: (row) => row.lines.length },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDateTime(row.createdAt) },
    { key: 'actions', header: '', cell: (row) => <ActionButtons request={row} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Restock Requests" subtitle="Approval workflow: Pending → Approved → Issued → Closed." />
      <InventoryTabNav />
      <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle={isError ? 'Requests unavailable' : 'No restock requests'} emptyDescription={isError ? 'Material requests could not be loaded.' : 'Requests raised against work orders will appear here.'} />
    </div>
  )
}
