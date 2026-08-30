import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Truck } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatCard } from '../../shared/components/StatCard'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { Spinner } from '../../shared/components/Spinner'
import { ErrorState } from '../../shared/components/ErrorState'
import { cn } from '../../shared/utils/cn'
import { formatCurrency } from '../../shared/utils/format'
import { useMaterials } from './api'
import { useVans } from './vansApi'
import type { Material } from './types'
import { InventoryTabNav } from './components/InventoryTabNav'

export function StockPage() {
  const [view, setView] = useState<'all' | 'by-branch'>('all')
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
    <div className="space-y-6">
      <PageHeader
        title="Stock Levels"
        subtitle="Stock overview across all branches."
        secondaryActions={
          <div className="flex items-center gap-2">
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
        <DataTable columns={columns} data={materials} emptyTitle="No stock records" emptyDescription="Active parts will appear here." virtualise={materials.length > 100} />
      ) : (
        <div className="space-y-6">
          {byBranch.map(([branch, items]) => (
            <section key={branch} className="space-y-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
              <h3 className="text-sm font-semibold text-slate-900">{branch}</h3>
              <DataTable columns={columns} data={items} emptyTitle="No items" emptyDescription="No stock at this branch." />
            </section>
          ))}
        </div>
      )}
    </div>
  )
}
