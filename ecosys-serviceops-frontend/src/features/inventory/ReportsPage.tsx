import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Download } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { ErrorState } from '../../shared/components/ErrorState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { StatCard } from '../../shared/components/StatCard'
import { downloadCsv } from '../../shared/utils/csv'
import { cn } from '../../shared/utils/cn'
import { formatCurrency } from '../../shared/utils/format'
import { formatDate } from '../../shared/utils/dates'
import { useBranches } from './api'
import { usePurchaseOrders, type PurchaseOrderListItem } from './purchaseOrdersApi'
import { downloadInventoryReport, useStockUsage, useStockValuation, type StockUsageRow, type StockValuationRow } from './reportsApi'

const tabs = [
  { id: 'valuation', label: 'Stock Valuation' },
  { id: 'usage', label: 'Usage' },
  { id: 'po', label: 'Purchase Orders' },
  { id: 'movement', label: 'Movements Export' },
] as const

const inputClass = 'h-10 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]'

/** Today and 30 days back, as local yyyy-mm-dd. */
function defaultRange() {
  const format = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
  const to = new Date()
  const from = new Date()
  from.setDate(from.getDate() - 30)
  return { from: format(from), to: format(to) }
}

function ExportButton({ onExport, disabled = false }: { onExport: () => Promise<void> | void; disabled?: boolean }) {
  const [busy, setBusy] = useState(false)
  return (
    <button
      type="button"
      disabled={disabled || busy}
      onClick={async () => {
        setBusy(true)
        try {
          await onExport()
        } catch {
          toast.error('The export could not be downloaded')
        } finally {
          setBusy(false)
        }
      }}
      className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50"
    >
      <Download className="h-4 w-4" />{busy ? 'Exporting…' : 'Export CSV'}
    </button>
  )
}

export function ReportsPage() {
  const [tab, setTab] = useState<(typeof tabs)[number]['id']>('valuation')
  const [branchId, setBranchId] = useState('')
  const [range, setRange] = useState(defaultRange)
  const { data: branches } = useBranches()
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)
  const usesDates = tab === 'usage' || tab === 'movement'

  return (
    <div className="space-y-4">
      <PageHeader title="Inventory Reports" subtitle="Stock valuation by location, parts used over a period, purchase orders and the movement ledger, with CSV export." />

      <div className="flex flex-wrap items-end justify-between gap-3">
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
        <div className="flex flex-wrap items-end gap-2">
          {tab !== 'po' && activeBranches.length > 0 ? (
            <select aria-label="Branch" value={branchId} onChange={(event) => setBranchId(event.target.value)} className={inputClass}>
              <option value="">All branches</option>
              {activeBranches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
            </select>
          ) : null}
          {usesDates ? (
            <>
              <label className="flex flex-col gap-1 text-xs font-medium text-slate-500">
                From
                <input type="date" value={range.from} onChange={(event) => setRange((prev) => ({ ...prev, from: event.target.value }))} className={inputClass} />
              </label>
              <label className="flex flex-col gap-1 text-xs font-medium text-slate-500">
                To
                <input type="date" value={range.to} onChange={(event) => setRange((prev) => ({ ...prev, to: event.target.value }))} className={inputClass} />
              </label>
            </>
          ) : null}
        </div>
      </div>

      {tab === 'valuation' ? <ValuationReport branchId={branchId} /> : null}
      {tab === 'usage' ? <UsageReport branchId={branchId} from={range.from} to={range.to} /> : null}
      {tab === 'po' ? <PurchaseOrderReport /> : null}
      {tab === 'movement' ? <MovementExport branchId={branchId} from={range.from} to={range.to} /> : null}
    </div>
  )
}

function ValuationReport({ branchId }: { branchId: string }) {
  const { data, isLoading, isError, refetch } = useStockValuation(branchId || undefined)

  if (isError) {
    return <ErrorState title="Stock valuation unavailable" description="We couldn’t load stock holdings, so a zero stock value would be a fabricated figure." onRetry={() => void refetch()} />
  }

  const columns: DataTableColumn<StockValuationRow & { id: string }>[] = [
    { key: 'location', header: 'Location', cell: (row) => row.location },
    { key: 'item', header: 'Part', cell: (row) => <Link className="underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${row.materialItemId}`}>{row.itemName} <span className="font-mono text-xs text-slate-500">{row.itemCode}</span></Link> },
    { key: 'category', header: 'Category', cell: (row) => row.category ?? '—' },
    { key: 'quantity', header: 'Qty', cell: (row) => `${row.quantity} ${row.unitOfMeasure}` },
    { key: 'unitCost', header: 'Unit Cost', cell: (row) => (row.unitCost != null ? formatCurrency(row.unitCost) : '—') },
    { key: 'value', header: 'Value', cell: (row) => formatCurrency(row.value), sortValue: (row) => row.value },
  ]

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatCard label="Total Stock Value" value={isLoading ? '—' : formatCurrency(data?.totalValue ?? 0)} />
        {(data?.locations ?? []).slice(0, 3).map((location) => (
          <StatCard key={`${location.locationType}-${location.location}`} label={location.location} value={formatCurrency(location.value)} />
        ))}
      </div>
      <div className="flex justify-end">
        <ExportButton disabled={!data} onExport={() => downloadInventoryReport('valuation', { branchId: branchId || undefined }, 'stock-valuation.csv')} />
      </div>
      <DataTable
        columns={columns}
        data={(data?.rows ?? []).map((row) => ({ ...row, id: `${row.locationType}-${row.location}-${row.materialItemId}` }))}
        loading={isLoading}
        emptyTitle="No stock on hand"
        emptyDescription="Parts held at branches appear here with their value."
        virtualise={(data?.rows.length ?? 0) > 150}
      />
    </div>
  )
}

function UsageReport({ branchId, from, to }: { branchId: string; from: string; to: string }) {
  const filters = { branchId: branchId || undefined, from: from || undefined, to: to || undefined }
  const { data, isLoading, isError, refetch } = useStockUsage(filters)

  if (isError) {
    return <ErrorState title="Usage report unavailable" description="Parts usage could not be loaded for this period." onRetry={() => void refetch()} />
  }

  const columns: DataTableColumn<StockUsageRow & { id: string }>[] = [
    { key: 'item', header: 'Part', cell: (row) => <Link className="underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${row.materialItemId}`}>{row.itemName} <span className="font-mono text-xs text-slate-500">{row.itemCode}</span></Link> },
    { key: 'category', header: 'Category', cell: (row) => row.category ?? '—' },
    { key: 'quantityUsed', header: 'Used', cell: (row) => row.quantityUsed, sortValue: (row) => row.quantityUsed },
    { key: 'quantityReturned', header: 'Returned', cell: (row) => row.quantityReturned },
    { key: 'netQuantity', header: 'Net Used', cell: (row) => `${row.netQuantity} ${row.unitOfMeasure}`, sortValue: (row) => row.netQuantity },
    { key: 'jobCount', header: 'Jobs', cell: (row) => row.jobCount },
    { key: 'netValue', header: 'Value', cell: (row) => formatCurrency(row.netValue), sortValue: (row) => row.netValue },
  ]

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-slate-600">
          Parts issued to jobs, requests and issues, less unused parts returned. Transfers are moves, not use.
          {data ? <span className="ml-1 font-semibold text-slate-900">Total {formatCurrency(data.totalValue)}</span> : null}
        </p>
        <ExportButton disabled={!data} onExport={() => downloadInventoryReport('usage', filters, `stock-usage-${from}-${to}.csv`)} />
      </div>
      <DataTable
        columns={columns}
        data={(data?.rows ?? []).map((row) => ({ ...row, id: row.materialItemId }))}
        loading={isLoading}
        emptyTitle="No parts used in this period"
        emptyDescription="Widen the dates or choose another branch."
      />
    </div>
  )
}

function PurchaseOrderReport() {
  const { data, isLoading, isError, refetch } = usePurchaseOrders('All')

  if (isError) {
    return <ErrorState title="Purchase order report unavailable" description="We couldn’t load purchase orders, so a zero committed spend would be a fabricated figure." onRetry={() => void refetch()} />
  }

  const orders = data ?? []
  const open = orders.filter((order) => order.status !== 'Received' && order.status !== 'Cancelled')
  const byCurrency = (list: PurchaseOrderListItem[]) =>
    Object.entries(list.reduce<Record<string, number>>((totals, order) => ({ ...totals, [order.currency]: (totals[order.currency] ?? 0) + order.totalAmount }), {}))
      .map(([currency, total]) => `${currency} ${total.toLocaleString()}`)
      .join(' · ') || '—'

  const columns: DataTableColumn<PurchaseOrderListItem>[] = [
    { key: 'poNumber', header: 'PO No', cell: (row) => <Link className="font-mono text-xs underline decoration-slate-300 underline-offset-4" to={`/inventory/purchase-orders/${row.id}`}>{row.poNumber}</Link> },
    { key: 'supplierName', header: 'Supplier', cell: (row) => row.supplierName },
    { key: 'orderedAt', header: 'Ordered', cell: (row) => formatDate(row.orderedAt) },
    { key: 'expectedAt', header: 'Expected', cell: (row) => (row.expectedAt ? formatDate(row.expectedAt) : '—') },
    { key: 'totalAmount', header: 'Total', cell: (row) => `${row.currency} ${row.totalAmount.toLocaleString()}` },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <StatCard label="Orders" value={isLoading ? '—' : orders.length} />
        <StatCard label="Open Commitment" value={isLoading ? '—' : byCurrency(open)} />
        <StatCard label="All Orders" value={isLoading ? '—' : byCurrency(orders)} />
      </div>
      <div className="flex justify-end">
        <ExportButton
          disabled={orders.length === 0}
          onExport={() =>
            downloadCsv('purchase-orders.csv', orders.map((order) => ({
              'PO No': order.poNumber,
              Supplier: order.supplierName,
              Ordered: order.orderedAt.slice(0, 10),
              Expected: order.expectedAt?.slice(0, 10) ?? '',
              Currency: order.currency,
              Total: order.totalAmount,
              Status: order.status,
            })))
          }
        />
      </div>
      <DataTable columns={columns} data={orders} loading={isLoading} emptyTitle="No purchase orders" emptyDescription="Purchase orders raised against suppliers will appear here." />
    </div>
  )
}

function MovementExport({ branchId, from, to }: { branchId: string; from: string; to: string }) {
  return (
    <div className="space-y-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 text-sm text-slate-600 shadow-sm backdrop-blur-[var(--glass-blur-md)]">
      <p>Every receipt, issue, return, adjustment, transfer between the chosen dates{branchId ? ' at the chosen branch' : ''}, with signed quantity, balance after, reference, reason and batch/serial numbers.</p>
      <p>To browse or filter further first, use <Link className="font-medium text-slate-900 underline underline-offset-4" to="/inventory/movements">Movements</Link>.</p>
      <ExportButton onExport={() => downloadInventoryReport('movements', { branchId: branchId || undefined, from: from || undefined, to: to || undefined }, `stock-movements-${from}-${to}.csv`)} />
    </div>
  )
}
