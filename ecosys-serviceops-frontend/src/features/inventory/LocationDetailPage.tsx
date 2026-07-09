import { useMemo } from 'react'
import { Link, useParams } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { SummarySection } from '../../shared/pages/ModulePage'
import { useBranches, useMaterials, useStockTransfers } from './api'
import type { Material } from './types'

export function LocationDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data: branches, isLoading, isError } = useBranches()
  const { data: materials } = useMaterials()
  const { data: transfers } = useStockTransfers()

  const branch = useMemo(() => (branches ?? []).find((item) => item.id === id), [branches, id])
  const stock = useMemo(() => (materials ?? []).filter((item) => item.branchId === id), [materials, id])
  const relatedTransfers = useMemo(
    () => (transfers ?? []).filter((transfer) => transfer.fromBranchId === id || transfer.toBranchId === id),
    [transfers, id],
  )

  if (isLoading) {
    return <Spinner label="Loading location" />
  }

  if (isError || !branch) {
    return <EmptyState title="Location unavailable" description="This location could not be loaded." />
  }

  const stockColumns: DataTableColumn<Material>[] = [
    { key: 'itemName', header: 'Part', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${row.id}`}>{row.itemName}</Link> },
    { key: 'quantityOnHand', header: 'On Hand', cell: (row) => `${row.quantityOnHand} ${row.unitOfMeasure}` },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.isLowStock ? 'Low Stock' : 'OK'} /> },
  ]

  const header = (
    <PageHeader
      title={branch.name}
      subtitle={branch.location ?? branch.code}
      breadcrumbs={[{ label: 'Locations', to: '/inventory/locations' }, { label: branch.name }]}
      secondaryActions={<StatusBadge status={branch.isActive ? 'Active' : 'Inactive'} />}
    />
  )

  return (
    <DetailLayout
      header={header}
      tabs={[
        {
          id: 'summary',
          label: 'Summary',
          content: (
            <SummarySection title="Location Summary" description="Branch identity and stock counts.">
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                {[
                  ['Code', branch.code],
                  ['Address', branch.location ?? '—'],
                  ['Parts Stocked', String(stock.length)],
                ].map(([label, value]) => (
                  <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                    <div className="mt-2 text-sm text-slate-900">{value}</div>
                  </div>
                ))}
              </div>
            </SummarySection>
          ),
        },
        {
          id: 'stock',
          label: 'Stock',
          content: <DataTable columns={stockColumns} data={stock} emptyTitle="No stock" emptyDescription="No parts are stocked at this location." />,
        },
        {
          id: 'transfers',
          label: 'Transfers',
          content: (
            <DataTable
              columns={[
                { key: 'materialName', header: 'Part', cell: (row) => row.materialName ?? '—' },
                { key: 'direction', header: 'Direction', cell: (row) => (row.fromBranchId === id ? 'Outgoing' : 'Incoming') },
                { key: 'quantity', header: 'Qty', cell: (row) => row.quantity },
                { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
              ]}
              data={relatedTransfers}
              emptyTitle="No transfers"
              emptyDescription="Stock transfers involving this location will appear here."
            />
          ),
        },
      ]}
    />
  )
}
