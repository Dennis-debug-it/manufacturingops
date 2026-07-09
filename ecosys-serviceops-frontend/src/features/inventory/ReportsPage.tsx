import { useMemo, useState } from 'react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { EmptyState } from '../../shared/components/EmptyState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { cn } from '../../shared/utils/cn'
import { formatCurrency } from '../../shared/utils/format'
import { formatDateTime } from '../../shared/utils/dates'
import { useMaterialIssues, useMaterials, useStockTransfers } from './api'
import type { Material } from './types'

const tabs = [
  { id: 'valuation', label: 'Stock Valuation' },
  { id: 'usage', label: 'Usage' },
  { id: 'po', label: 'Purchase Orders' },
  { id: 'movement', label: 'Movement' },
] as const

export function ReportsPage() {
  const [tab, setTab] = useState<(typeof tabs)[number]['id']>('valuation')
  const { data: materials } = useMaterials({ status: 'active' })
  const { data: issues } = useMaterialIssues()
  const { data: transfers } = useStockTransfers()

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
    <div className="space-y-6">
      <PageHeader title="Inventory Reports" subtitle="Stock valuation, usage, purchase order, and movement reports." />

      <div className="flex flex-wrap gap-2">
        {tabs.map((option) => (
          <button
            key={option.id}
            type="button"
            onClick={() => setTab(option.id)}
            className={cn('inline-flex h-10 items-center rounded-full border px-3 text-sm font-medium transition', tab === option.id ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}
          >
            {option.label}
          </button>
        ))}
      </div>

      {tab === 'valuation' ? (
        <div className="space-y-4">
          <div className="rounded-2xl border border-slate-200 bg-white p-4 text-sm text-slate-600 shadow-sm">Total stock value: <span className="font-semibold text-slate-900">{formatCurrency(totalValue)}</span></div>
          <DataTable columns={valuationColumns} data={valuationRows} emptyTitle="No stock" emptyDescription="Active parts will appear here." />
        </div>
      ) : null}

      {tab === 'usage' ? (
        <DataTable columns={usageColumns} data={usageRows} emptyTitle="No usage recorded" emptyDescription="Material issued against work orders will appear here." />
      ) : null}

      {tab === 'po' ? (
        <EmptyState title="Pending backend Purchase Orders endpoint" description="No PurchaseOrdersController exists yet — this report will populate once purchase orders are tracked server-side." />
      ) : null}

      {tab === 'movement' ? (
        <DataTable
          columns={[
            { key: 'materialName', header: 'Part', cell: (row) => row.materialName ?? '—' },
            { key: 'route', header: 'Route', cell: (row) => `${row.fromBranchName ?? '—'} → ${row.toBranchName ?? '—'}` },
            { key: 'quantity', header: 'Qty', cell: (row) => row.quantity },
            { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
            { key: 'createdAt', header: 'Date', cell: (row) => formatDateTime(row.createdAt) },
          ]}
          data={transfers ?? []}
          emptyTitle="No movements"
          emptyDescription="Branch-to-branch transfers will appear here."
        />
      ) : null}
    </div>
  )
}
