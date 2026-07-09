import { PageHeader } from '../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { StatusBadge } from '../shared/components/StatusBadge'
import { formatDate } from '../shared/utils/dates'
import { usePortalAssets, type PortalAssetListItem } from './api'

export function PortalAssetsPage() {
  const { data, isLoading, isError } = usePortalAssets()

  const columns: DataTableColumn<PortalAssetListItem>[] = [
    { key: 'assetName', header: 'Asset', cell: (row) => <span className="font-medium text-slate-900">{row.assetName}</span> },
    { key: 'assetCode', header: 'Code', cell: (row) => row.assetCode },
    { key: 'assetType', header: 'Type', cell: (row) => row.assetType ?? '—' },
    { key: 'siteName', header: 'Site', cell: (row) => row.siteName ?? '—' },
    { key: 'manufacturer', header: 'Make / Model', cell: (row) => [row.manufacturer, row.model].filter(Boolean).join(' / ') || '—' },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'nextPmDate', header: 'Next PM', cell: (row) => (row.nextPmDate ? formatDate(row.nextPmDate) : '—') },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Assets" subtitle="Your managed equipment portfolio." />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Assets unavailable' : 'No assets on file'}
        emptyDescription={isError ? 'Assets could not be loaded.' : 'Assets managed for your account will appear here.'}
      />
    </div>
  )
}
