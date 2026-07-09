import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { useAssets } from './api'
import { TriTabNav } from './components/TriTabNav'
import { AssetFormDrawer } from './components/AssetFormDrawer'
import type { Asset } from './types'

export function AssetsPage() {
  const canCreate = usePermission('assets.manage')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [showForm, setShowForm] = useState(false)
  const { data, isLoading, isError } = useAssets({ search: debouncedSearch })

  const rows = useMemo(() => data ?? [], [data])

  const columns: DataTableColumn<Asset>[] = [
    { key: 'assetName', header: 'Asset', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/assets/${row.id}`}>{row.assetName}</Link> },
    { key: 'assetCode', header: 'Code', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.assetCode}</span> },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '—' },
    { key: 'siteName', header: 'Site', cell: (row) => row.siteName ?? '—' },
    { key: 'assetType', header: 'Type', cell: (row) => row.assetType ?? '—' },
    { key: 'manufacturer', header: 'Manufacturer', cell: (row) => row.manufacturer ?? '—' },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status ?? 'Active'} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Assets"
        subtitle="Asset register with serial numbers and warranty status."
        primaryAction={
          canCreate ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
              <Plus className="h-4 w-4" />New Asset
            </button>
          ) : undefined
        }
      />
      <TriTabNav />

      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search assets by name, code, serial, or manufacturer" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Assets unavailable' : 'No assets yet'}
        emptyDescription={isError ? 'The asset list could not be loaded.' : 'Create your first asset to get started.'}
        virtualise={rows.length > 100}
      />

      <AssetFormDrawer open={showForm} onClose={() => setShowForm(false)} />
    </div>
  )
}
