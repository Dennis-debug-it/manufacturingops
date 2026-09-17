import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Search, Upload } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatCurrency } from '../../shared/utils/format'
import { useMaterials } from './api'
import { MaterialBulkImportDrawer } from './components/MaterialBulkImportDrawer'
import { PartFormDrawer } from './components/PartFormDrawer'
import type { Material } from './types'
import { InventoryTabNav } from './components/InventoryTabNav'

export function PartsPage() {
  const canManage = usePermission('inventory.materials.manage')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [showForm, setShowForm] = useState(false)
  const [showImport, setShowImport] = useState(false)
  const { data, isLoading, isError } = useMaterials({ search: debouncedSearch, status: 'all' })

  const rows = useMemo(() => data ?? [], [data])

  const columns: DataTableColumn<Material>[] = [
    { key: 'itemName', header: 'Part', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${row.id}`}>{row.itemName}</Link> },
    { key: 'itemCode', header: 'Code', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.itemCode}</span> },
    { key: 'category', header: 'Category', cell: (row) => row.category ?? '—' },
    { key: 'branchName', header: 'Branch', cell: (row) => row.branchName ?? 'All Branches' },
    { key: 'quantityOnHand', header: 'On Hand', cell: (row) => `${row.quantityOnHand} ${row.unitOfMeasure}` },
    { key: 'unitCost', header: 'Unit Cost', cell: (row) => (row.unitCost != null ? formatCurrency(row.unitCost) : '—') },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.isLowStock ? 'Low Stock' : row.isActive ? 'Active' : 'Inactive'} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Parts Catalogue"
        subtitle="Searchable parts catalogue and stock metadata."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Part
            </button>
          ) : undefined
        }
        secondaryActions={
          canManage ? (
            <button type="button" onClick={() => setShowImport(true)} className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              <Upload className="h-4 w-4" />Bulk Import
            </button>
          ) : undefined
        }
      />
      <InventoryTabNav />

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search parts by name or code" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
      </div>

      <DataTable columns={columns} data={rows} loading={isLoading} emptyTitle={isError ? 'Parts unavailable' : 'No parts yet'} emptyDescription={isError ? 'The parts catalogue could not be loaded.' : 'Create your first part to get started.'} virtualise={rows.length > 100} />

      <PartFormDrawer open={showForm} onClose={() => setShowForm(false)} />
      <MaterialBulkImportDrawer open={showImport} onClose={() => setShowImport(false)} />
    </div>
  )
}
