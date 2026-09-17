import { useMemo, useState } from 'react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { ErrorState } from '../../shared/components/ErrorState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { cn } from '../../shared/utils/cn'
import { formatCurrency } from '../../shared/utils/format'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { useMaterialIssues, useMaterials, useStockTransfers } from './api'
import { usePurchaseOrders, type PurchaseOrderListItem } from './purchaseOrdersApi'
import type { Material } from './types'
import { InventoryTabNav } from './components/InventoryTabNav'

const tabs = [
  { id: 'valuation', label: 'Stock Valuation' },
  { id: 'usage', label: 'Usage' },
  { id: 'po', label: 'Purchase Orders' },
  { id: 'movement', label: 'Movement' },
] as const

export function ReportsPage() {
  const [tab, setTab] = useState<(typeof tabs)[number]['id']>('valuation')
  const materialsQuery = useMaterials({ status: 'active' })
  const issuesQuery = useMaterialIssues()
  const transfersQuery = useStockTransfers()
  const purchaseOrdersQuery = usePurchaseOrders('All')

  const { data: materials } = materialsQuery
  const { data: issues } = issuesQuery
  const { data: transfers } = transfersQuery
  const { data: purchaseOrders, isLoading: poLoading } = purchaseOrdersQuery

  const poColumns: DataTableColumn<PurchaseOrderListItem>[] = [
    { key: 'poNumber', header: 'PO No', cell: (row) => <span className="font-mono text-xs text-slate-700">{row.poNumber}</span> },
    { key: 'supplierName', header: 'Supplier', cell: (row) => row.supplierName },
    { key: 'orderedAt', header: 'Ordered', cell: (row) => formatDate(row.orderedAt) },
    { key: 'expectedAt', header: 'Expected', cell: (row) => (row.expectedAt ? formatDate(row.expectedAt) : '—') },
    { key: 'totalAmount', header: 'Total', cell: (row) => formatCurrency(row.totalAmount) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]
  const poTotal = (purchaseOrders ?? []).reduce((sum, po) => sum + po.totalAmount, 0)

  const valuationRows = useMemo(
    () => (materials ?? []).map((item) => ({ ...item, value: item.quantityOnHand * (item.unitCost ?? 0) })),
    [materials],
  )
  const totalValue = valuationRows.reduce((sum, item) => sum + item.value, 0)

  const valuationColumns: DataTableColumn<(typeof valuationRows)[number]>[] = [
    { key: 'itemName', header: 'Part', cell: (row) => row.itemName },
    { key: 'branchName', header: 'Branch', cell: (row) => row.branchName ?? 'All Branches' },
    { key: 'quantityOnHand', header: 'Qty', cell: (row) => row.quantityOnHand },
    { key: 'unitCost', header: 'Unit Cost', cell: (row) => (row.unitCost != null ? formatCurrency(row.unitCost) : '—') },
    { key: 'value', header: 'Value', cell: (row) => formatCurrency(row.value) },
  ]

  const usageColumns: DataTableColumn<Material & { totalIssued: number }>[] = [
    { key: 'itemName', header: 'Part', cell: (row) => row.itemName },
    { key: 'totalIssued', header: 'Total Issued (all-time)', cell: (row) => row.totalIssued },
  ]

  const usageRows = useMemo(() => {
    const totals = new Map<string, number>()
    for (const issue of issues ?? []) {
      for (const line of issue.lines) {
        totals.set(line.materialItemId, (totals.get(line.materialItemId) ?? 0) + line.quantityIssued)
      }
    }
    return (materials ?? [])
      .map((item) => ({ ...item, totalIssued: totals.get(item.id) ?? 0 }))
      .filter((item) => item.totalIssued > 0)
      .sort((a, b) => b.totalIssued - a.totalIssued)
  }, [materials, issues])

  return (
    <div className="space-y-4">
      <PageHeader title="Inventory Reports" subtitle="Stock valuation, usage, purchase order, and movement reports." />
      <InventoryTabNav />

      <div className="flex flex-wrap gap-2">
        {tabs.map((option) => (
          <button
            key={option.id}
            type="button"
            onClick={() => setTab(option.id)}
            className={cn('inline-flex h-10 items-center rounded-full border px-3 text-sm font-medium transition', tab === option.id ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}
          >
            {option.label}
          </button>
        ))}
      </div>

      {tab === 'valuation' ? (
        materialsQuery.isError ? (
          <ErrorState
            title="Stock valuation unavailable"
            description="We couldn’t load the parts this valuation totals, so a zero stock value would be a fabricated figure."
            onRetry={() => void materialsQuery.refetch()}
          />
        ) : (
          <div className="space-y-4">
            <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 text-sm text-slate-600 shadow-sm">Total stock value: <span className="font-semibold text-slate-900">{materialsQuery.isLoading ? '—' : formatCurrency(totalValue)}</span></div>
            <DataTable columns={valuationColumns} data={valuationRows} loading={materialsQuery.isLoading} emptyTitle="No stock" emptyDescription="Active parts will appear here." />
          </div>
        )
      ) : null}

      {tab === 'usage' ? (
        materialsQuery.isError || issuesQuery.isError ? (
          <ErrorState
            title="Usage report unavailable"
            description="Usage is computed from parts and material issues together, and at least one of those could not be loaded."
            onRetry={() => {
              void materialsQuery.refetch()
              void issuesQuery.refetch()
            }}
          />
        ) : (
          <DataTable columns={usageColumns} data={usageRows} loading={materialsQuery.isLoading || issuesQuery.isLoading} emptyTitle="No usage recorded" emptyDescription="Material issued against work orders will appear here." />
        )
      ) : null}

      {tab === 'po' ? (
        purchaseOrdersQuery.isError ? (
          <ErrorState
            title="Purchase order report unavailable"
            description="We couldn’t load purchase orders, so a zero committed spend would be a fabricated figure."
            onRetry={() => void purchaseOrdersQuery.refetch()}
          />
        ) : (
          <div className="space-y-4">
            <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 text-sm text-slate-600 shadow-sm">Total committed spend: <span className="font-semibold text-slate-900">{poLoading ? '—' : formatCurrency(poTotal)}</span> across {poLoading ? '—' : (purchaseOrders ?? []).length} order(s)</div>
            <DataTable columns={poColumns} data={purchaseOrders ?? []} loading={poLoading} emptyTitle="No purchase orders" emptyDescription="Purchase orders raised against suppliers will appear here." />
          </div>
        )
      ) : null}

      {tab === 'movement' ? (
        transfersQuery.isError ? (
          <ErrorState
            title="Movement report unavailable"
            description="We couldn’t load branch-to-branch transfers, so an empty table would be misleading."
            onRetry={() => void transfersQuery.refetch()}
          />
        ) : (
          <DataTable
            columns={[
              { key: 'materialName', header: 'Part', cell: (row) => row.materialName ?? '—' },
              { key: 'route', header: 'Route', cell: (row) => `${row.fromBranchName ?? '—'} → ${row.toBranchName ?? '—'}` },
              { key: 'quantity', header: 'Qty', cell: (row) => row.quantity },
              { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
              { key: 'createdAt', header: 'Date', cell: (row) => formatDateTime(row.createdAt) },
            ]}
            data={transfers ?? []}
            loading={transfersQuery.isLoading}
            emptyTitle="No movements"
            emptyDescription="Branch-to-branch transfers will appear here."
          />
        )
      ) : null}
    </div>
  )
}
