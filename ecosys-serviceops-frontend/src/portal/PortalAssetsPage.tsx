import { useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import { PageHeader } from '../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { StatusBadge } from '../shared/components/StatusBadge'
import { cn } from '../shared/utils/cn'
import { formatDate } from '../shared/utils/dates'
import { usePortalAssets, type PortalAssetListItem } from './api'

/** Warranty filters; "covered" is every asset still in warranty, expiring soon included. */
const filters = [
  { id: 'all', label: 'All', test: () => true },
  { id: 'covered', label: 'Under warranty', test: (row: PortalAssetListItem) => row.warrantyStatus === 'Under Warranty' || row.warrantyStatus === 'Expiring Soon' },
  { id: 'expiring', label: 'Expiring soon', test: (row: PortalAssetListItem) => row.warrantyStatus === 'Expiring Soon' },
  { id: 'expired', label: 'Out of warranty', test: (row: PortalAssetListItem) => row.warrantyStatus === 'Out of Warranty' },
  { id: 'none', label: 'No warranty on file', test: (row: PortalAssetListItem) => row.warrantyStatus === null },
] as const

type FilterId = (typeof filters)[number]['id']

function WarrantyCell({ row }: { row: PortalAssetListItem }) {
  if (!row.warrantyStatus || !row.warrantyExpiryDate) return <span className="text-slate-400">Not on file</span>
  const label = row.warrantyStatus === 'Expiring Soon' ? 'Warranty Expiring' : row.warrantyStatus
  const detail =
    row.warrantyStatus === 'Out of Warranty'
      ? `Ended ${formatDate(row.warrantyExpiryDate)}`
      : `Until ${formatDate(row.warrantyExpiryDate)}${row.warrantyStatus === 'Expiring Soon' && row.warrantyDaysLeft != null ? ` · ${row.warrantyDaysLeft} day${row.warrantyDaysLeft === 1 ? '' : 's'} left` : ''}`
  return (
    <div className="space-y-1">
      <StatusBadge status={label} />
      <p className="text-xs text-slate-500">{detail}</p>
    </div>
  )
}

export function PortalAssetsPage() {
  const { data, isLoading, isError } = usePortalAssets()
  const [params, setParams] = useSearchParams()
  const active = (filters.find((filter) => filter.id === params.get('warranty'))?.id ?? 'all') as FilterId
  const assets = useMemo(() => data ?? [], [data])
  const counts = useMemo(() => Object.fromEntries(filters.map((filter) => [filter.id, assets.filter(filter.test).length])), [assets])
  const rows = assets.filter(filters.find((filter) => filter.id === active)!.test)

  const columns: DataTableColumn<PortalAssetListItem>[] = [
    { key: 'assetName', header: 'Asset', cell: (row) => <span className="font-medium text-slate-900">{row.assetName}</span> },
    { key: 'assetCode', header: 'Code', cell: (row) => row.assetCode },
    { key: 'assetType', header: 'Type', cell: (row) => row.assetType ?? '—' },
    { key: 'siteName', header: 'Site', cell: (row) => row.siteName ?? '—' },
    { key: 'manufacturer', header: 'Make / Model', cell: (row) => [row.manufacturer, row.model].filter(Boolean).join(' / ') || '—' },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'warranty', header: 'Warranty', cell: (row) => <WarrantyCell row={row} />, sortValue: (row) => row.warrantyExpiryDate ?? '' },
    { key: 'nextPmDate', header: 'Next PM', cell: (row) => (row.nextPmDate ? formatDate(row.nextPmDate) : '—') },
  ]

  return (
    <div className="space-y-4">
      <PageHeader title="Assets" subtitle="Your managed equipment portfolio and its warranty cover." />
      {assets.length > 0 ? (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Filter by warranty">
          {filters.map((filter) => (
            <button
              key={filter.id}
              type="button"
              aria-pressed={active === filter.id}
              onClick={() => setParams(filter.id === 'all' ? {} : { warranty: filter.id }, { replace: true })}
              className={cn(
                'inline-flex h-9 items-center gap-1.5 rounded-lg border px-3 text-sm font-medium transition',
                active === filter.id ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
              )}
            >
              {filter.label}
              <span className={cn('tabular-nums', active === filter.id ? 'text-white/70' : 'text-slate-400')}>{counts[filter.id]}</span>
            </button>
          ))}
        </div>
      ) : null}
      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Assets unavailable' : assets.length === 0 ? 'No assets on file' : 'No assets in this view'}
        emptyDescription={isError ? 'Assets could not be loaded.' : assets.length === 0 ? 'Assets managed for your account will appear here.' : 'Choose another warranty filter to see the rest.'}
      />
    </div>
  )
}
