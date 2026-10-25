import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatCurrency } from '../../shared/utils/format'
import { useBranchStockSummary, useBranches } from './api'
import type { Branch } from './types'
import { InventoryTabNav } from './components/InventoryTabNav'

export function LocationsPage() {
  const { data: branches, isLoading, isError } = useBranches()
  const { data: summary, isError: summaryError } = useBranchStockSummary()

  const rows = useMemo(() => {
    const byBranch = new Map((summary ?? []).map((row) => [row.branchId, row]))
    return (branches ?? []).map((branch) => {
      const totals = byBranch.get(branch.id)
      return { ...branch, itemCount: totals?.partsStocked ?? 0, lowStockCount: totals?.lowStockParts ?? 0, stockValue: totals?.stockValue ?? 0 }
    })
  }, [branches, summary])

  // Zeros from a failed summary would read as empty branches.
  const count = (value: number) => (summaryError ? '—' : value)

  const columns: DataTableColumn<Branch & { itemCount: number; lowStockCount: number; stockValue: number }>[] = [
    { key: 'name', header: 'Location', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/locations/${row.id}`}>{row.name}</Link> },
    { key: 'code', header: 'Code', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.code}</span> },
    { key: 'location', header: 'Address', cell: (row) => row.location ?? '—' },
    { key: 'itemCount', header: 'Parts Stocked', cell: (row) => count(row.itemCount) },
    { key: 'lowStockCount', header: 'Low Stock', cell: (row) => count(row.lowStockCount) },
    { key: 'stockValue', header: 'Stock Value', cell: (row) => (summaryError ? '—' : formatCurrency(row.stockValue)) },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Locations"
        subtitle="Branches that hold stock. Mobile stock on vehicles is under Vans."
      />
      <InventoryTabNav />
      <DataTable columns={columns} data={rows} loading={isLoading} emptyTitle={isError ? 'Locations unavailable' : 'No locations'} emptyDescription={isError ? 'Branches could not be loaded.' : 'No branches configured yet.'} />
    </div>
  )
}
