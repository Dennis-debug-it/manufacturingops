import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Truck, Upload } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatCard } from '../../shared/components/StatCard'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { Spinner } from '../../shared/components/Spinner'
import { ErrorState } from '../../shared/components/ErrorState'
import { cn } from '../../shared/utils/cn'
import { formatCurrency } from '../../shared/utils/format'
import { usePermission } from '../../shared/hooks/usePermission'
import { useMaterials } from './api'
import { useVans } from './vansApi'
import type { Material } from './types'
import { InventoryTabNav } from './components/InventoryTabNav'
import { MaterialBulkImportDrawer } from './components/MaterialBulkImportDrawer'
import { PartFormDrawer } from './components/PartFormDrawer'

export function StockPage() {
  const [view, setView] = useState<'all' | 'by-branch'>('all')
  // Stock is the page Inventory opens on, so adding parts and bulk-loading opening stock live here
  // too — not only on the Parts tab, where they were easy to miss.
  const canManage = usePermission('inventory.materials.manage')
  const [showForm, setShowForm] = useState(false)
  const [showImport, setShowImport] = useState(false)
  const { data, isLoading, isError, refetch } = useMaterials({ status: 'active' })
  const { data: vans, isError: vansError, refetch: refetchVans } = useVans()

  const materials = useMemo(() => data ?? [], [data])
  const activeVans = vans ?? []

  const stats = useMemo(() => {
    const totalValue = materials.reduce((sum, item) => sum + item.quantityOnHand * (item.unitCost ?? 0), 0)
    return {
      totalItems: materials.length,
      lowStock: materials.filter((item) => item.isLowStock).length,
      totalValue,
      branches: new Set(materials.map((item) => item.branchName ?? 'Unassigned')).size,
    }
  }, [materials])

  const byBranch = useMemo(() => {
    const groups = new Map<string, Material[]>()
    for (const item of materials) {
      const key = item.branchName ?? 'Unassigned'
      const bucket = groups.get(key) ?? []
      bucket.push(item)
      groups.set(key, bucket)
    }
    return Array.from(groups.entries())
  }, [materials])

  const columns: DataTableColumn<Material>[] = [
    { key: 'itemName', header: 'Part', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${row.id}`}>{row.itemName}</Link> },
    { key: 'itemCode', header: 'Code', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.itemCode}</span> },
    { key: 'quantityOnHand', header: 'On Hand', cell: (row) => `${row.quantityOnHand} ${row.unitOfMeasure}` },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.isLowStock ? 'Low Stock' : 'OK'} /> },
  ]

  if (isLoading) {
    return <Spinner label="Loading stock levels" />
  }

  // Every tile and table on this page is derived from `materials`; rendering them from an empty
  // array after a failed fetch would report a stock count, a low-stock count and a stock value of
  // zero that the server never said.
  if (isError) {
    return (
      <ErrorState
        title="Stock levels unavailable"
        description="We couldn’t load stock records. No totals are shown, because zero here would be indistinguishable from a failed request."
        onRetry={() => void refetch()}
      />
    )
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Stock Levels"
        subtitle="Stock overview across all branches."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Part
            </button>
          ) : undefined
        }
        secondaryActions={
          <div className="flex flex-wrap items-center gap-2">
            {canManage ? (
              <button type="button" onClick={() => setShowImport(true)} className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
                <Upload className="h-4 w-4" />Bulk Import
              </button>
            ) : null}
            {(['all', 'by-branch'] as const).map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => setView(option)}
                className={cn('inline-flex h-10 items-center rounded-full border px-3 text-sm font-medium transition', view === option ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}
              >
                {option === 'all' ? 'All Items' : 'By Branch'}
              </button>
            ))}
          </div>
        }
      />
      <InventoryTabNav />

      {vansError ? (
        <ErrorState
          variant="inline"
          title="Van stock unavailable."
          description="The van count could not be loaded, so it is not shown here."
          onRetry={() => void refetchVans()}
        />
      ) : (
        <Link to="/inventory/vans" className="flex items-center justify-between gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] px-4 py-3 text-sm text-slate-700 shadow-sm transition hover:border-slate-300 hover:bg-slate-50">
          <span className="flex items-center gap-2">
            <Truck className="h-4 w-4 text-slate-400" />
            Stock is also held on <strong className="font-semibold text-slate-900">{activeVans.length}</strong> van{activeVans.length === 1 ? '' : 's'}. Load parts from a branch, consume on jobs, and return the rest.
          </span>
          <span className="shrink-0 font-medium text-slate-900">Manage vans →</span>
        </Link>
      )}

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatCard label="Total Items" value={stats.totalItems} />
        <StatCard label="Low Stock" value={stats.lowStock} tone={stats.lowStock > 0 ? 'warning' : 'success'} />
        <StatCard label="Stock Value" value={formatCurrency(stats.totalValue)} />
        <StatCard label="Branches" value={stats.branches} />
      </div>

      {view === 'all' ? (
        <DataTable columns={columns} data={materials} emptyTitle="No stock records" emptyDescription={canManage ? 'Add a part or bulk import your stock list from a CSV.' : 'Active parts will appear here.'} virtualise={materials.length > 100} />
      ) : (
        <div className="space-y-4">
          {byBranch.map(([branch, items]) => (
            <section key={branch} className="space-y-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <h3 className="text-sm font-semibold text-slate-900">{branch}</h3>
              <DataTable columns={columns} data={items} emptyTitle="No items" emptyDescription="No stock at this branch." />
            </section>
          ))}
        </div>
      )}

      <PartFormDrawer open={showForm} onClose={() => setShowForm(false)} />
      <MaterialBulkImportDrawer open={showImport} onClose={() => setShowImport(false)} />
    </div>
  )
}
