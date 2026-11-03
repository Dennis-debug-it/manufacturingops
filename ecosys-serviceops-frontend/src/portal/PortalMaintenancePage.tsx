import { PageHeader } from '../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { StatusBadge } from '../shared/components/StatusBadge'
import { formatDate } from '../shared/utils/dates'
import { usePortalMaintenancePlans, type PortalMaintenancePlanItem } from './api'

export function PortalMaintenancePage() {
  const { data, isLoading, isError } = usePortalMaintenancePlans()

  const columns: DataTableColumn<PortalMaintenancePlanItem>[] = [
    { key: 'assetName', header: 'Asset', cell: (row) => <span className="font-medium text-slate-900">{row.assetName}</span> },
    { key: 'frequency', header: 'Frequency', cell: (row) => row.frequency },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'lastPmDate', header: 'Last Serviced', cell: (row) => (row.lastPmDate ? formatDate(row.lastPmDate) : '—') },
    { key: 'nextPmDate', header: 'Next Due', cell: (row) => (row.nextPmDate ? formatDate(row.nextPmDate) : '—') },
  ]

  return (
    <div className="space-y-4">
      <PageHeader title="Preventive Maintenance" subtitle="Scheduled maintenance coverage for your assets." />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Maintenance schedule unavailable' : 'No maintenance plans yet'}
        emptyDescription={isError ? 'Maintenance plans could not be loaded.' : 'Preventive maintenance schedules for your assets will appear here.'}
      />
    </div>
  )
}
