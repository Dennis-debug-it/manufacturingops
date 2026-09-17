import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDateTime } from '../../shared/utils/dates'
import { useStockTransfers } from './api'
import type { StockTransfer } from './types'
import { InventoryTabNav } from './components/InventoryTabNav'

export function MovementsPage() {
  const { data, isLoading, isError } = useStockTransfers()

  const columns: DataTableColumn<StockTransfer>[] = [
    { key: 'materialName', header: 'Part', cell: (row) => row.materialName ?? '—' },
    { key: 'fromBranchName', header: 'From', cell: (row) => row.fromBranchName ?? '—' },
    { key: 'toBranchName', header: 'To', cell: (row) => row.toBranchName ?? '—' },
    { key: 'quantity', header: 'Qty', cell: (row) => row.quantity },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDateTime(row.createdAt) },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Stock Movements"
        subtitle="Branch-to-branch stock transfers. Per-part movement history (receipts, adjustments, job usage) lives on each Part Detail page's Movement History tab — there is no single global movement feed on the backend, so this page shows the transfer log, which is the only tenant-wide movement resource."
      />
      <InventoryTabNav />
      <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle={isError ? 'Movements unavailable' : 'No transfers'} emptyDescription={isError ? 'Stock transfers could not be loaded.' : 'Branch-to-branch stock transfers will appear here.'} />
    </div>
  )
}
