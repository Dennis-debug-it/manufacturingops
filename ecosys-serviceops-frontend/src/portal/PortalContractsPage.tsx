import { PageHeader } from '../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { StatusBadge } from '../shared/components/StatusBadge'
import { formatDate } from '../shared/utils/dates'
import { usePortalContracts, type PortalContractListItem } from './api'

export function PortalContractsPage() {
  const { data, isLoading, isError } = usePortalContracts()

  const columns: DataTableColumn<PortalContractListItem>[] = [
    { key: 'contractNumber', header: 'Contract #', cell: (row) => <span className="font-medium text-slate-900">{row.contractNumber}</span> },
    { key: 'name', header: 'Name', cell: (row) => row.name },
    { key: 'contractType', header: 'Type', cell: (row) => row.contractType },
    { key: 'startDate', header: 'Start', cell: (row) => formatDate(row.startDate) },
    { key: 'endDate', header: 'End', cell: (row) => formatDate(row.endDate) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader title="Contracts" subtitle="Service coverage and contract terms for your account." />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Contracts unavailable' : 'No contracts on file'}
        emptyDescription={isError ? 'Contracts could not be loaded.' : 'Service contracts covering your account will appear here.'}
      />
    </div>
  )
}
