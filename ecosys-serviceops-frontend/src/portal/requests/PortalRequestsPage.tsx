import { Link, useNavigate } from 'react-router-dom'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDate } from '../../shared/utils/dates'
import { usePortalWorkOrders, type PortalWorkOrderListItem } from '../api'

export function PortalRequestsPage() {
  const navigate = useNavigate()
  const { data, isLoading, isError } = usePortalWorkOrders()

  const columns: DataTableColumn<PortalWorkOrderListItem>[] = [
    { key: 'workOrderNumber', header: 'Request #', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/portal/requests/${row.id}`}>{row.workOrderNumber}</Link> },
    { key: 'title', header: 'Title', cell: (row) => row.title },
    { key: 'siteName', header: 'Site', cell: (row) => row.siteName ?? '—' },
    { key: 'assetName', header: 'Asset', cell: (row) => row.assetName ?? '—' },
    { key: 'priority', header: 'Priority', cell: (row) => <StatusBadge status={row.priority} /> },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'dueDate', header: 'Due', cell: (row) => (row.dueDate ? formatDate(row.dueDate) : '—') },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Service Requests"
        subtitle="Track requests submitted for your sites and assets."
        primaryAction={<Link to="/portal/requests/new" className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]"><Plus className="h-4 w-4" />New Request</Link>}
      />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Requests unavailable' : 'No requests yet'}
        emptyDescription={isError ? 'Requests could not be loaded.' : 'Submit your first service request to get started.'}
        emptyActionLabel="New Request"
        onEmptyAction={() => navigate('/portal/requests/new')}
      />
    </div>
  )
}
