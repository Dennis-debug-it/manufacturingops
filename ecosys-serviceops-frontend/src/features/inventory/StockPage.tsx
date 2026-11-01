import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Plus, Search, Upload } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ErrorState } from '../../shared/components/ErrorState'
import { cn } from '../../shared/utils/cn'
import { formatCurrency } from '../../shared/utils/format'
import { usePermission } from '../../shared/hooks/usePermission'
import { useStockByLocation, type StockLocationRow } from './api'
import { MaterialBulkImportDrawer } from './components/MaterialBulkImportDrawer'
import { PartFormDrawer } from './components/PartFormDrawer'
import { ManageAccessHint } from './components/ManageAccessHint'
import { SummaryStrip } from './components/SummaryStrip'

const fieldClass = 'h-10 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]'

type View = 'part' | 'location'

interface PartTotal {
  id: string
  materialId: string
  itemCode: string
  itemName: string
  category: string | null
  unitOfMeasure: string
  inStores: number
  onVans: number
  total: number
  locations: number
  value: number
  lowAt: string[]
}

const locationKey = (row: StockLocationRow) => `${row.locationType}:${row.locationId ?? 'stores'}`

/**
 * Stock where it actually is. Rows come from each branch's (or the stores') balance and each van's,
 * so "by location" is real per-branch stock — the old page grouped parts by a branch name the list
 * never sent and put everything under "Unassigned".
 */
export function StockPage() {
  const canManage = usePermission('inventory.materials.manage')
  const [params, setParams] = useSearchParams()
  const view: View = params.get('view') === 'location' ? 'location' : 'part'
  const location = params.get('location') ?? ''
  const category = params.get('category') ?? ''
  const lowOnly = params.get('low') === '1'
  // Below zero: parts used before stores had them (bought direct from a supplier, or negative stock
  // allowed), waiting for a receipt to catch up.
  const negativeOnly = params.get('negative') === '1'
  const [search, setSearch] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [showImport, setShowImport] = useState(false)
  const { data, isLoading, isError, refetch } = useStockByLocation()

  const setParam = (key: string, value: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    setParams(next, { replace: true })
  }

  const rows = useMemo(() => data ?? [], [data])
  const locations = useMemo(() => {
    const seen = new Map<string, { key: string; name: string; type: StockLocationRow['locationType'] }>()
    for (const row of rows) seen.set(locationKey(row), { key: locationKey(row), name: row.locationName, type: row.locationType })
    return Array.from(seen.values()).sort((a, b) => (a.type === b.type ? a.name.localeCompare(b.name) : a.type === 'Van' ? 1 : -1))
  }, [rows])
  const categories = useMemo(() => Array.from(new Set(rows.map((row) => row.category).filter((value): value is string => Boolean(value)))).sort(), [rows])

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase()
    return rows.filter((row) =>
      (!location || locationKey(row) === location)
      && (!category || row.category === category)
      && (!term || row.itemName.toLowerCase().includes(term) || row.itemCode.toLowerCase().includes(term)),
    )
  }, [rows, location, category, search])

  const byPart = useMemo(() => {
    const totals = new Map<string, PartTotal>()
    for (const row of filtered) {
      const entry = totals.get(row.materialId) ?? {
        id: row.materialId, materialId: row.materialId, itemCode: row.itemCode, itemName: row.itemName, category: row.category,
        unitOfMeasure: row.unitOfMeasure, inStores: 0, onVans: 0, total: 0, locations: 0, value: 0, lowAt: [],
      }
      if (row.locationType === 'Van') entry.onVans += row.quantityOnHand
      else entry.inStores += row.quantityOnHand
      entry.total += row.quantityOnHand
      if (row.quantityOnHand !== 0) entry.locations += 1
      entry.value += row.quantityOnHand * (row.unitCost ?? 0)
      if (row.isLowStock) entry.lowAt.push(row.locationName)
      totals.set(row.materialId, entry)
    }
    const list = Array.from(totals.values())
    return list.filter((part) => (!lowOnly || part.lowAt.length > 0) && (!negativeOnly || part.total < 0 || part.inStores < 0))
  }, [filtered, lowOnly, negativeOnly])

  const byLocation = useMemo(
    () => filtered
      .filter((row) => (!lowOnly || row.isLowStock) && (!negativeOnly || row.quantityOnHand < 0))
      .map((row) => ({ ...row, id: `${row.materialId}:${locationKey(row)}` })),
    [filtered, lowOnly, negativeOnly],
  )

  const summary = useMemo(() => ({
    parts: new Set(filtered.filter((row) => row.quantityOnHand !== 0).map((row) => row.materialId)).size,
    low: filtered.filter((row) => row.isLowStock).length,
    value: filtered.reduce((sum, row) => sum + row.quantityOnHand * (row.unitCost ?? 0), 0),
    locations: new Set(filtered.map(locationKey)).size,
  }), [filtered])

  const partLink = (materialId: string, name: string) => (
    <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${materialId}`}>{name}</Link>
  )

  const partColumns: DataTableColumn<PartTotal>[] = [
    { key: 'itemName', header: 'Part', cell: (row) => partLink(row.materialId, row.itemName), sortValue: (row) => row.itemName },
    { key: 'itemCode', header: 'Code', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.itemCode}</span>, sortValue: (row) => row.itemCode },
    { key: 'category', header: 'Category', cell: (row) => row.category ?? '—', sortValue: (row) => row.category },
    { key: 'inStores', header: 'In stores', cell: (row) => <span className="tabular-nums">{row.inStores}</span>, sortValue: (row) => row.inStores },
    { key: 'onVans', header: 'On vans', cell: (row) => <span className="tabular-nums">{row.onVans}</span>, sortValue: (row) => row.onVans },
    { key: 'total', header: 'Total', cell: (row) => <span className="font-semibold tabular-nums">{row.total} {row.unitOfMeasure}</span>, sortValue: (row) => row.total },
    { key: 'value', header: 'Value', cell: (row) => formatCurrency(row.value), sortValue: (row) => row.value },
    {
      key: 'status',
      header: 'Status',
      cell: (row) => (row.inStores < 0 ? <StatusBadge status="Negative" /> : row.lowAt.length > 0 ? <span title={`Low at ${row.lowAt.join(', ')}`}><StatusBadge status="Low Stock" /></span> : <StatusBadge status="OK" />),
      sortValue: (row) => row.lowAt.length,
    },
  ]

  const locationColumns: DataTableColumn<StockLocationRow & { id: string }>[] = [
    { key: 'location', header: 'Location', cell: (row) => <span>{row.locationName} <span className="text-xs text-slate-500">{row.locationType === 'Van' ? 'Van' : row.locationType === 'Branch' ? 'Branch' : ''}</span></span>, sortValue: (row) => row.locationName },
    { key: 'itemName', header: 'Part', cell: (row) => partLink(row.materialId, row.itemName), sortValue: (row) => row.itemName },
    { key: 'itemCode', header: 'Code', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.itemCode}</span>, sortValue: (row) => row.itemCode },
    { key: 'quantityOnHand', header: 'On hand', cell: (row) => <span className="tabular-nums">{row.quantityOnHand} {row.unitOfMeasure}</span>, sortValue: (row) => row.quantityOnHand },
    { key: 'reorderLevel', header: 'Reorder level', cell: (row) => (row.reorderLevel > 0 ? row.reorderLevel : '—'), sortValue: (row) => row.reorderLevel },
    { key: 'value', header: 'Value', cell: (row) => formatCurrency(row.quantityOnHand * (row.unitCost ?? 0)), sortValue: (row) => row.quantityOnHand * (row.unitCost ?? 0) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.quantityOnHand < 0 ? 'Negative' : row.isLowStock ? 'Low Stock' : 'OK'} />, sortValue: (row) => (row.quantityOnHand < 0 ? 2 : row.isLowStock ? 1 : 0) },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Stock levels"
        subtitle="What is on hand at each branch and on each van."
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

      {canManage ? null : <ManageAccessHint what="parts" />}

      {isError ? (
        // Zero totals after a failed request would be indistinguishable from an empty store.
        <ErrorState title="Stock levels unavailable" description="Stock could not be loaded, so no totals are shown." onRetry={() => void refetch()} />
      ) : (
        <>
          <SummaryStrip
            figures={[
              { label: 'Parts in stock', value: isLoading ? '…' : summary.parts },
              { label: 'Low stock', value: isLoading ? '…' : summary.low, tone: summary.low > 0 ? 'warning' : 'success' },
              { label: 'Stock value', value: isLoading ? '…' : formatCurrency(summary.value) },
              { label: 'Locations', value: isLoading ? '…' : summary.locations },
            ]}
          />

          <div className="flex flex-wrap items-center gap-2">
            <label className="flex h-10 min-w-[14rem] flex-1 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3">
              <Search className="h-4 w-4 text-slate-400" />
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search part name or code" aria-label="Search parts" className="w-full border-0 bg-transparent p-0 text-sm outline-none" />
            </label>
            <select aria-label="Location" value={location} onChange={(event) => setParam('location', event.target.value)} className={fieldClass}>
              <option value="">All locations</option>
              {locations.map((option) => <option key={option.key} value={option.key}>{option.type === 'Van' ? `Van · ${option.name}` : option.name}</option>)}
            </select>
            {categories.length > 0 ? (
              <select aria-label="Category" value={category} onChange={(event) => setParam('category', event.target.value)} className={fieldClass}>
                <option value="">All categories</option>
                {categories.map((value) => <option key={value} value={value}>{value}</option>)}
              </select>
            ) : null}
            <label className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-700">
              <input type="checkbox" checked={lowOnly} onChange={(event) => setParam('low', event.target.checked ? '1' : '')} className="h-4 w-4 rounded border-slate-300" />
              Low stock only
            </label>
            <label className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 text-sm text-slate-700">
              <input type="checkbox" checked={negativeOnly} onChange={(event) => setParam('negative', event.target.checked ? '1' : '')} className="h-4 w-4 rounded border-slate-300" />
              Below zero
            </label>
            <div className="inline-flex rounded-lg border border-slate-300 bg-white p-0.5" role="group" aria-label="View">
              {(['part', 'location'] as const).map((option) => (
                <button
                  key={option}
                  type="button"
                  aria-pressed={view === option}
                  onClick={() => setParam('view', option === 'part' ? '' : option)}
                  className={cn('h-9 rounded-md px-3 text-sm font-medium transition', view === option ? 'bg-[var(--app-primary)] text-white' : 'text-slate-700 hover:bg-slate-50')}
                >
                  {option === 'part' ? 'By part' : 'By location'}
                </button>
              ))}
            </div>
          </div>

          {view === 'part' ? (
            <DataTable
              columns={partColumns}
              data={byPart}
              loading={isLoading}
              emptyTitle={rows.length === 0 ? 'No stock yet' : 'Nothing matches'}
              emptyDescription={rows.length === 0 ? (canManage ? 'Add parts one by one, or bulk import your stock list from a CSV with opening quantities.' : 'Stock appears here once parts are received.') : 'Try another location, category or search.'}
              emptyActionLabel={rows.length === 0 && canManage ? 'Bulk Import' : undefined}
              onEmptyAction={rows.length === 0 && canManage ? () => setShowImport(true) : undefined}
              virtualise={byPart.length > 100}
            />
          ) : (
            <DataTable
              columns={locationColumns}
              data={byLocation}
              loading={isLoading}
              emptyTitle="Nothing matches"
              emptyDescription="Try another location, category or search."
              virtualise={byLocation.length > 100}
            />
          )}
        </>
      )}

      <PartFormDrawer open={showForm} onClose={() => setShowForm(false)} />
      <MaterialBulkImportDrawer open={showImport} onClose={() => setShowImport(false)} />
    </div>
  )
}
