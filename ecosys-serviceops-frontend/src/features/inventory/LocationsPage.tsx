import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useBranches, useMaterials } from './api'
import type { Branch } from './types'

export function LocationsPage() {
  const { data: branches, isLoading, isError } = useBranches()
  const { data: materials } = useMaterials()

  const rows = useMemo(() => {
    return (branches ?? []).map((branch) => {
      const items = (materials ?? []).filter((material) => material.branchId === branch.id)
      return { ...branch, itemCount: items.length, lowStockCount: items.filter((item) => item.isLowStock).length }
    })
  }, [branches, materials])

  const columns: DataTableColumn<Branch & { itemCount: number; lowStockCount: number }>[] = [
    { key: 'name', header: 'Location', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/locations/${row.id}`}>{row.name}</Link> },
    { key: 'code', header: 'Code', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.code}</span> },
    { key: 'location', header: 'Address', cell: (row) => row.location ?? '—' },
    { key: 'itemCount', header: 'Parts Stocked', cell: (row) => row.itemCount },
    { key: 'lowStockCount', header: 'Low Stock', cell: (row) => row.lowStockCount },
    { key: 'isActive', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Locations"
        subtitle="Warehouse and branch stock locations. Inventory location tracking is branch-scoped — there is no separate warehouse/van entity in the backend yet."
      />
      <DataTable columns={columns} data={rows} loading={isLoading} emptyTitle={isError ? 'Locations unavailable' : 'No locations'} emptyDescription={isError ? 'Branches could not be loaded.' : 'No branches configured yet.'} />
    </div>
  )
}
