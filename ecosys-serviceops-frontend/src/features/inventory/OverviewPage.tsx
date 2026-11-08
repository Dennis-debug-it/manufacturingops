import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { usePartsReturns } from '../work-orders/partsReturnsApi'
import { useStockByLocation } from './api'
import { usePurchaseOrders } from './purchaseOrdersApi'
import { requestReference, sourceLabels, useInventoryRequests } from './requestsApi'
import { useStockCounts } from './stockCountsApi'
import { useHeldParts } from './FaultyPartsPage'
import { useDirectPurchases } from './DirectPurchasesPage'
import { SummaryStrip } from './components/SummaryStrip'

interface TodoRow {
  id: string
  kind: string
  item: string
  detail: string
  to: string
  action: string
  urgency: 'high' | 'normal'
  sortAt: string
}

interface LocationRow {
  id: string
  name: string
  type: string
  parts: number
  low: number
  value: number
  to: string
}

const urgencyClass = { high: 'bg-amber-500', normal: 'bg-slate-300' }

/**
 * Inventory's front page: one table of what needs doing now (requests to approve or issue, low
 * stock, late deliveries, returns at the counter, counts in progress) and the stock at each
 * location. Each row links to the page where it is done.
 */
export function OverviewPage() {
  const requests = useInventoryRequests()
  const stock = useStockByLocation()
  const latePurchaseOrders = usePurchaseOrders('All', 'late')
  const openPurchaseOrders = usePurchaseOrders('All', 'open')
  const openCounts = useStockCounts('Open')
  const pendingReturns = usePartsReturns('Pending')
  const heldParts = useHeldParts('Held')
  const directPurchases = useDirectPurchases('Pending')

  const todo = useMemo(() => {
    const rows: TodoRow[] = []
    for (const request of requests.data ?? []) {
      if (request.stage !== 'Requested' && request.stage !== 'Approved') continue
      rows.push({
        id: `r-${request.id}`,
        kind: request.stage === 'Requested' ? 'Approve request' : 'Issue request',
        item: requestReference(request),
        detail: `${sourceLabels[request.source]} · ${request.lines.length} part${request.lines.length === 1 ? '' : 's'}${request.requestedByName ? ` · ${request.requestedByName}` : ''}`,
        to: `/inventory/requests?stage=${request.stage}&q=${encodeURIComponent(request.number ?? request.workOrderNumber ?? '')}`,
        action: request.stage === 'Requested' ? 'Review' : 'Issue',
        urgency: 'high',
        sortAt: request.createdAt,
      })
    }
    for (const order of latePurchaseOrders.data ?? []) {
      rows.push({
        id: `po-${order.id}`,
        kind: 'Late delivery',
        item: order.poNumber,
        detail: `${order.supplierName}${order.expectedAt ? ` · expected ${formatDate(order.expectedAt)}` : ''}`,
        to: `/inventory/purchase-orders/${order.id}`,
        action: 'Chase or receive',
        urgency: 'high',
        sortAt: order.expectedAt ?? order.orderedAt,
      })
    }
    for (const partsReturn of pendingReturns.data ?? []) {
      rows.push({
        id: `ret-${partsReturn.id}`,
        kind: 'Return to receive',
        item: partsReturn.returnNumber,
        detail: `${partsReturn.workOrderNumber ?? 'Work order'} · ${partsReturn.lines.length} part${partsReturn.lines.length === 1 ? '' : 's'}`,
        to: '/inventory/parts-returns',
        action: 'Receive',
        urgency: 'normal',
        sortAt: partsReturn.requestedAt,
      })
    }
    for (const count of openCounts.data ?? []) {
      rows.push({
        id: `count-${count.id}`,
        kind: 'Count in progress',
        item: count.countNumber,
        detail: `${count.locationName ?? count.branchName ?? 'All stock'} · ${count.countedLines} of ${count.lineCount} counted`,
        to: `/inventory/stock-counts/${count.id}`,
        action: 'Continue',
        urgency: 'normal',
        sortAt: count.createdAt,
      })
    }
    const direct = directPurchases.data ?? []
    if (direct.length > 0) {
      rows.push({
        id: 'direct-purchases',
        kind: 'Direct purchases to reconcile',
        item: `${direct.length} part${direct.length === 1 ? '' : 's'} bought from suppliers for jobs`,
        detail: direct.slice(0, 3).map((row) => `${row.itemName ?? 'Part'} × ${row.quantity}${row.supplierName ? ` (${row.supplierName})` : ''}`).join(', ') + (direct.length > 3 ? '…' : ''),
        to: '/inventory/direct-purchases',
        action: 'Reconcile',
        urgency: 'high',
        sortAt: direct[direct.length - 1]!.createdAt,
      })
    }
    const negative = (stock.data ?? []).filter((row) => row.quantityOnHand < 0)
    if (negative.length > 0) {
      rows.push({
        id: 'negative-stock',
        kind: 'Below zero',
        item: `${negative.length} part${negative.length === 1 ? '' : 's'} with negative stock`,
        detail: negative.slice(0, 3).map((row) => `${row.itemName} (${row.locationName}: ${row.quantityOnHand})`).join(', ') + (negative.length > 3 ? '…' : ''),
        to: '/inventory/stock?view=location&negative=1',
        action: 'Review',
        urgency: 'high',
        sortAt: '',
      })
    }
    const held = heldParts.data ?? []
    if (held.length > 0) {
      rows.push({
        id: 'held-parts',
        kind: 'Faulty parts held',
        item: `${held.length} faulty part${held.length === 1 ? '' : 's'} in stores`,
        detail: held.slice(0, 3).map((part) => `${part.itemName ?? 'Part'} × ${part.quantity}`).join(', ') + (held.length > 3 ? '…' : ''),
        to: '/inventory/faulty-parts',
        action: 'Return or write off',
        urgency: 'normal',
        sortAt: held[held.length - 1]!.createdAt,
      })
    }
    const lowRows = (stock.data ?? []).filter((row) => row.isLowStock)
    if (lowRows.length > 0) {
      rows.push({
        id: 'low-stock',
        kind: 'Low stock',
        item: `${lowRows.length} part${lowRows.length === 1 ? '' : 's'} at or below reorder level`,
        detail: lowRows.slice(0, 3).map((row) => `${row.itemName} (${row.locationName})`).join(', ') + (lowRows.length > 3 ? '…' : ''),
        to: '/inventory/alerts',
        action: 'Reorder',
        urgency: 'high',
        sortAt: '',
      })
    }
    return rows.sort((a, b) => (a.urgency === b.urgency ? a.sortAt.localeCompare(b.sortAt) : a.urgency === 'high' ? -1 : 1))
  }, [requests.data, latePurchaseOrders.data, pendingReturns.data, openCounts.data, stock.data, heldParts.data, directPurchases.data])

  const locations = useMemo(() => {
    const map = new Map<string, LocationRow>()
    for (const row of stock.data ?? []) {
      const key = `${row.locationType}:${row.locationId ?? 'stores'}`
      const entry = map.get(key) ?? {
        id: key,
        name: row.locationName,
        type: row.locationType === 'Branch' ? 'Branch' : 'Stores',
        parts: 0,
        low: 0,
        value: 0,
        to: row.locationType === 'Branch' && row.locationId ? `/inventory/locations/${row.locationId}` : `/inventory/stock?view=location&location=${encodeURIComponent(key)}`,
      }
      if (row.quantityOnHand !== 0) entry.parts += 1
      if (row.isLowStock) entry.low += 1
      entry.value += row.quantityOnHand * (row.unitCost ?? 0)
      map.set(key, entry)
    }
    return Array.from(map.values()).sort((a, b) => a.name.localeCompare(b.name))
  }, [stock.data])

  const stockValue = locations.reduce((sum, row) => sum + row.value, 0)
  const lowCount = (stock.data ?? []).filter((row) => row.isLowStock).length
  const openRequests = (requests.data ?? []).filter((request) => request.stage === 'Requested' || request.stage === 'Approved').length

  const todoColumns: DataTableColumn<TodoRow>[] = [
    {
      key: 'kind',
      header: 'What',
      cell: (row) => (
        <span className="inline-flex items-center gap-2 font-medium text-slate-900">
          <span aria-hidden className={cn('h-2 w-2 rounded-full', urgencyClass[row.urgency])} />
          {row.kind}
        </span>
      ),
    },
    { key: 'item', header: 'Item', cell: (row) => row.item },
    { key: 'detail', header: 'Detail', cell: (row) => <span className="text-slate-600">{row.detail}</span> },
    {
      key: 'action',
      header: '',
      cell: (row) => (
        <Link to={row.to} className="inline-block whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
          {row.action}
        </Link>
      ),
    },
  ]

  const locationColumns: DataTableColumn<LocationRow>[] = [
    { key: 'name', header: 'Location', cell: (row) => <Link to={row.to} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{row.name}</Link>, sortValue: (row) => row.name },
    { key: 'type', header: 'Type', cell: (row) => row.type, sortValue: (row) => row.type },
    { key: 'parts', header: 'Parts held', cell: (row) => <span className="tabular-nums">{row.parts}</span>, sortValue: (row) => row.parts },
    { key: 'low', header: 'Low stock', cell: (row) => <span className={cn('tabular-nums', row.low > 0 && 'font-semibold text-amber-700')}>{row.low}</span>, sortValue: (row) => row.low },
    { key: 'value', header: 'Stock value', cell: (row) => formatCurrency(row.value), sortValue: (row) => row.value },
  ]

  const todoLoading = requests.isLoading || stock.isLoading || latePurchaseOrders.isLoading || pendingReturns.isLoading || openCounts.isLoading
  const somethingFailed = requests.isError || stock.isError || latePurchaseOrders.isError || pendingReturns.isError || openCounts.isError

  return (
    <div className="space-y-5">
      <PageHeader title="Inventory" subtitle="What needs doing in stores today, and where your stock is." />

      <SummaryStrip
        figures={[
          { label: 'Stock value', value: stock.isError ? '—' : stock.isLoading ? '…' : formatCurrency(stockValue), to: '/inventory/stock' },
          { label: 'Low stock', value: stock.isError ? '—' : stock.isLoading ? '…' : lowCount, tone: lowCount > 0 ? 'warning' : 'success', to: '/inventory/stock?low=1' },
          { label: 'Open requests', value: requests.isError ? '—' : requests.isLoading ? '…' : openRequests, tone: openRequests > 0 ? 'warning' : 'default', to: '/inventory/requests' },
          { label: 'Open purchase orders', value: openPurchaseOrders.isError ? '—' : openPurchaseOrders.isLoading ? '…' : (openPurchaseOrders.data ?? []).length, to: '/inventory/purchase-orders?state=open' },
        ]}
      />

      <section className="space-y-2">
        <h2 className="text-sm font-semibold text-slate-900">To do</h2>
        {somethingFailed ? <p className="text-xs text-amber-700">Some of this list could not be loaded, so it may be incomplete.</p> : null}
        <DataTable columns={todoColumns} data={todo} loading={todoLoading} emptyTitle="Nothing waiting" emptyDescription="No requests to act on, no late deliveries, nothing low on stock." />
      </section>

      <section className="space-y-2">
        <h2 className="text-sm font-semibold text-slate-900">Stock by location</h2>
        <DataTable
          columns={locationColumns}
          data={locations}
          loading={stock.isLoading}
          emptyTitle={stock.isError ? 'Stock unavailable' : 'No stock yet'}
          emptyDescription={stock.isError ? 'Stock levels could not be loaded.' : 'Receive stock or bulk import your parts list to get started.'}
        />
      </section>
    </div>
  )
}
