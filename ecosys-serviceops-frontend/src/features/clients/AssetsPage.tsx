import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Archive, Plus, Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { useAssetsPaged, useBulkRetireAssets, type AssetListFilters, type RetireAssetPayload } from './api'
import { TriTabNav } from './components/TriTabNav'
import { AssetFormDrawer } from './components/AssetFormDrawer'
import { RetireAssetDialog } from './components/RetireAssetDialog'
import type { Asset } from './types'

type AssetView = 'active' | 'out-of-support' | 'retired' | 'all'

const VIEWS: { id: AssetView; label: string; filters: Pick<AssetListFilters, 'status' | 'support'> }[] = [
  { id: 'active', label: 'Active', filters: {} },
  { id: 'out-of-support', label: 'Out of Support', filters: { support: 'out' } },
  { id: 'retired', label: 'Retired', filters: { status: 'inactive' } },
  { id: 'all', label: 'All', filters: { status: 'all' } },
]

export function AssetsPage() {
  const canCreate = usePermission('assets.manage')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [showForm, setShowForm] = useState(false)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const [view, setView] = useState<AssetView>('active')
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  // Bumped to remount the table, which clears its internal row selection.
  const [selectionKey, setSelectionKey] = useState(0)
  const [showRetire, setShowRetire] = useState(false)
  const bulkRetire = useBulkRetireAssets()

  const clearSelection = () => {
    setSelectedIds([])
    setSelectionKey((key) => key + 1)
  }

  // Reset to the first page (and drop a stale selection) whenever the search term or view changes.
  useEffect(() => {
    setPage(1)
    setSelectedIds([])
    setSelectionKey((key) => key + 1)
  }, [debouncedSearch, view])
  const viewFilters = VIEWS.find((item) => item.id === view)!.filters
  const { data, isLoading, isError } = useAssetsPaged({ search: debouncedSearch, ...viewFilters }, page, pageSize)

  const rows = useMemo(() => data?.items ?? [], [data])
  const canRetire = canCreate && view !== 'retired'

  const retireSelected = async (payload: RetireAssetPayload) => {
    try {
      const result = await bulkRetire.mutateAsync({ ...payload, assetIds: selectedIds })
      toast.success(`Retired ${result.retired} asset${result.retired === 1 ? '' : 's'}${result.skipped ? ` (${result.skipped} already retired or not found)` : ''}`)
      setShowRetire(false)
      clearSelection()
    } catch {
      // The API client already shows the error toast; keep the dialog open to retry.
    }
  }

  const columns: DataTableColumn<Asset>[] = [
    { key: 'assetName', header: 'Asset', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/assets/${row.id}`}>{row.assetName}</Link> },
    { key: 'assetCode', header: 'Code', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.assetCode}</span> },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '—' },
    { key: 'siteName', header: 'Site', cell: (row) => row.siteName ?? '—' },
    { key: 'assetType', header: 'Type', cell: (row) => row.assetType ?? '—' },
    { key: 'manufacturer', header: 'Manufacturer', cell: (row) => row.manufacturer ?? '—' },
    { key: 'lastPmDate', header: 'Last PM', cell: (row) => (row.lastPmDate ? formatDate(row.lastPmDate) : '—') },
    { key: 'nextPmDate', header: 'Next PM', cell: (row) => (row.nextPmDate ? formatDate(row.nextPmDate) : '—') },
    view === 'retired'
      ? { key: 'decommissionDate', header: 'Retired', cell: (row) => (row.decommissionDate ? formatDate(row.decommissionDate) : '—') }
      : { key: 'supportStatus', header: 'Support', cell: (row) => <StatusBadge status={row.supportStatus ?? row.warrantyStatus ?? 'No Warranty'} /> },
    view === 'retired'
      ? { key: 'decommissionReason', header: 'Reason', cell: (row) => row.decommissionReason ?? '—' }
      : { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status === 'Inactive' ? 'Retired' : row.status ?? 'Active'} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Assets"
        subtitle="Asset register with serial numbers, warranty, and support status."
        primaryAction={
          canCreate ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Asset
            </button>
          ) : undefined
        }
      />
      <TriTabNav />

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div role="tablist" aria-label="Asset view" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
          {VIEWS.map((item) => (
            <button
              key={item.id}
              type="button"
              role="tab"
              aria-selected={view === item.id}
              onClick={() => setView(item.id)}
              className={cn(
                'h-9 rounded-md px-3 text-sm font-medium transition',
                view === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50',
              )}
            >
              {item.label}
            </button>
          ))}
        </div>
        {canRetire && selectedIds.length > 0 ? (
          <button type="button" onClick={() => setShowRetire(true)} className="inline-flex h-10 items-center gap-2 rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-700 transition hover:bg-rose-50">
            <Archive className="h-4 w-4" />Retire {selectedIds.length} selected
          </button>
        ) : null}
      </div>
      {view === 'out-of-support' ? (
        <p className="text-sm text-slate-600">Assets still in service with no active warranty and no contract covering them. Select the ones you no longer support and retire them.</p>
      ) : null}

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search assets by name, code, serial, or manufacturer" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
      </div>

      <DataTable
        key={selectionKey}
        columns={columns}
        data={rows}
        loading={isLoading}
        batchSelect={canRetire}
        onSelectionChange={setSelectedIds}
        emptyTitle={isError ? 'Assets unavailable' : view === 'active' ? 'No assets yet' : view === 'out-of-support' ? 'Every asset is supported' : view === 'retired' ? 'No retired assets' : 'No assets yet'}
        emptyDescription={isError ? 'The asset list could not be loaded.' : view === 'out-of-support' ? 'All active assets have an active warranty or contract cover.' : view === 'retired' ? 'Assets you retire will be listed here.' : 'Create your first asset to get started.'}
        emptyActionLabel={!isError && canCreate && view === 'active' ? 'New Asset' : undefined}
        onEmptyAction={!isError && canCreate && view === 'active' ? () => setShowForm(true) : undefined}
        pagination={{
          page,
          pageSize,
          total: data?.totalCount ?? 0,
          onPageChange: setPage,
          onPageSizeChange: (size) => {
            setPageSize(size)
            setPage(1)
          },
        }}
      />

      <AssetFormDrawer open={showForm} onClose={() => setShowForm(false)} />
      <RetireAssetDialog
        open={showRetire}
        count={selectedIds.length}
        defaultReason={view === 'out-of-support' ? 'No longer under support' : ''}
        submitting={bulkRetire.isPending}
        onCancel={() => setShowRetire(false)}
        onConfirm={(payload) => void retireSelected(payload)}
      />
    </div>
  )
}
