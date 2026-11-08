import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { FileUp, Plus, Printer, QrCode, ScanLine, Search, Tags } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { downloadLabels, useAssetClasses, useBulkIssueTags, useFactoryAssets, useOrgNodes, type FactoryAssetFilters } from './api'
import { buildTree, flatten, pathOf } from './tree'
import { FactoryAssetFormDrawer } from './components/FactoryAssetFormDrawer'
import { ImportAssetsDrawer } from './components/ImportAssetsDrawer'
import { CriticalityBadge, OperationalBadge, inputClass, primaryButton, secondaryButton } from './components/ui'
import { CRITICALITIES, LEVEL_LABELS, type FactoryAsset } from './types'

type StatusView = 'active' | 'inactive' | 'all'

/** The section, with the factory it belongs to underneath. */
function LocationCell({ path }: { path: string | null }) {
  if (!path) return <span className="text-slate-400">—</span>
  const parts = path.split(' › ')
  const place = parts[parts.length - 1]
  const within = parts.slice(0, -1).join(' › ')
  return (
    <div className="min-w-32">
      <span className="text-slate-900">{place}</span>
      {within ? <span className="block text-xs text-slate-500">{within}</span> : null}
    </div>
  )
}

/** The factory asset register, filtered by place in the organisation tree. */
export function FactoryAssetsPage() {
  const canManage = usePermission('assets.manage')
  const [params, setParams] = useSearchParams()
  const orgNodeId = params.get('node') ?? ''
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [criticality, setCriticality] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [status, setStatus] = useState<StatusView>('active')
  const [topLevelOnly, setTopLevelOnly] = useState(false)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [selectionKey, setSelectionKey] = useState(0)
  const [showForm, setShowForm] = useState(false)
  // ?import=1 (from the welcome checklist) opens the import straight away.
  const [showImport, setShowImport] = useState(() => params.get('import') === '1')
  const [labelLayout, setLabelLayout] = useState('a4-24')
  const [printing, setPrinting] = useState(false)
  const bulkIssue = useBulkIssueTags()

  const { data: nodes } = useOrgNodes()
  const { data: classes } = useAssetClasses()
  const filters: FactoryAssetFilters = {
    search: debouncedSearch || undefined,
    status,
    orgNodeId: orgNodeId || undefined,
    criticality: criticality || undefined,
    categoryId: categoryId || undefined,
    topLevelOnly: topLevelOnly || undefined,
  }
  const { data, isLoading, isError } = useFactoryAssets(filters, page, pageSize)
  const rows = useMemo(() => data?.items ?? [], [data])
  const selectedNode = nodes?.find((node) => node.id === orgNodeId)

  useEffect(() => {
    setPage(1)
    setSelectedIds([])
    setSelectionKey((key) => key + 1)
  }, [debouncedSearch, orgNodeId, criticality, categoryId, status, topLevelOnly])

  const selectNode = (id: string) => setParams(id ? { node: id } : {}, { replace: true })

  const issueTags = async () => {
    try {
      const result = await bulkIssue.mutateAsync(selectedIds.length > 0 ? { assetIds: selectedIds } : { orgNodeId })
      toast.success(result.issued === 0 ? 'Every asset in the selection already has a tag.' : `Issued ${result.issued} tag${result.issued === 1 ? '' : 's'}.`)
    } catch {
      // The API client shows the error.
    }
  }

  const printLabels = async () => {
    setPrinting(true)
    try {
      await downloadLabels(selectedIds.length > 0 ? { assetIds: selectedIds, layout: labelLayout } : { orgNodeId, layout: labelLayout })
    } catch {
      // The API client shows the error.
    } finally {
      setPrinting(false)
    }
  }

  const columns: DataTableColumn<FactoryAsset>[] = [
    {
      key: 'assetName',
      header: 'Asset',
      cell: (row) => (
        <div>
          <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/assets/${row.id}`}>{row.assetName}</Link>
          {row.parentAssetCode ? <span className="block text-xs text-slate-500">Part of {row.parentAssetCode}</span> : null}
        </div>
      ),
    },
    { key: 'assetCode', header: 'Number', cell: (row) => <span className="whitespace-nowrap font-mono text-xs text-slate-600">{row.assetCode}</span> },
    { key: 'location', header: 'Location', cell: (row) => <LocationCell path={pathOf(row.orgNodeId, nodes ?? []) || row.orgNodeName} /> },
    { key: 'class', header: 'Class', cell: (row) => row.assetCategoryName ?? '—' },
    { key: 'criticality', header: 'Criticality', cell: (row) => <CriticalityBadge value={row.criticality} /> },
    { key: 'condition', header: 'Condition', cell: (row) => <OperationalBadge value={row.operationalStatus} retired={row.status === 'Inactive'} /> },
    { key: 'warranty', header: 'Warranty ends', cell: (row) => (row.warrantyExpiryDate ? formatDate(row.warrantyExpiryDate) : '—') },
  ]

  const tree = useMemo(() => flatten(buildTree(nodes ?? [])), [nodes])
  const scopeLabel = selectedIds.length > 0 ? `${selectedIds.length} selected` : selectedNode ? `all in ${selectedNode.name}` : null

  return (
    <div className="space-y-4">
      <PageHeader
        title="Assets"
        subtitle="Every machine and its components, by factory and section."
        primaryAction={canManage ? (
          <button type="button" className={cn(primaryButton, 'h-11')} onClick={() => setShowForm(true)}><Plus className="h-4 w-4" />Add asset</button>
        ) : undefined}
        secondaryActions={
          <div className="flex flex-wrap gap-2">
            <Link to="/scan" className={secondaryButton}><ScanLine className="h-4 w-4" />Scan</Link>
            {canManage ? <button type="button" className={secondaryButton} onClick={() => setShowImport(true)}><FileUp className="h-4 w-4" />Import</button> : null}
          </div>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[260px_minmax(0,1fr)]">
        <nav aria-label="Locations" className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-2 shadow-sm lg:max-h-[70vh] lg:overflow-y-auto">
          <button type="button" onClick={() => selectNode('')} className={cn('w-full rounded-md px-2 py-1.5 text-left text-sm', !orgNodeId ? 'bg-[var(--app-primary)] text-white' : 'text-slate-700 hover:bg-slate-50')}>
            All locations
          </button>
          {tree.map((node) => (
            <button
              key={node.id}
              type="button"
              disabled={!node.canAccess}
              onClick={() => selectNode(node.id)}
              title={LEVEL_LABELS[node.level]}
              className={cn(
                'flex w-full items-center justify-between gap-2 rounded-md py-1.5 pr-2 text-left text-sm disabled:cursor-default disabled:text-slate-400',
                orgNodeId === node.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-700 hover:bg-slate-50',
                node.level === 'Factory' && 'font-medium',
              )}
              style={{ paddingLeft: `${0.5 + node.depth * 0.9}rem` }}
            >
              <span className="truncate">{node.name}</span>
              {node.assetCount > 0 ? <span className="text-xs opacity-70">{node.assetCount}</span> : null}
            </button>
          ))}
          {tree.length === 0 ? <p className="px-2 py-3 text-xs text-slate-500">No factories yet. <Link className="underline" to="/settings/organisation">Set up the organisation</Link>.</p> : null}
        </nav>

        <div className="min-w-0 space-y-3">
          <div className="grid gap-2 rounded-[var(--radius-card)] border border-slate-200 bg-white p-3 shadow-sm sm:grid-cols-2 xl:grid-cols-5">
            <label className="flex items-center gap-2 sm:col-span-2">
              <Search className="h-4 w-4 shrink-0 text-slate-400" />
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search name, number, serial or make" className="h-10 w-full border-0 bg-transparent text-sm outline-none" />
            </label>
            <select aria-label="Criticality" className={inputClass} value={criticality} onChange={(event) => setCriticality(event.target.value)}>
              <option value="">Any criticality</option>
              {CRITICALITIES.map((value) => <option key={value} value={value}>Criticality {value}</option>)}
            </select>
            <select aria-label="Asset class" className={inputClass} value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
              <option value="">Any class</option>
              {(classes ?? []).filter((item) => item.isActive).sort((a, b) => a.name.localeCompare(b.name)).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </select>
            <select aria-label="Status" className={inputClass} value={status} onChange={(event) => setStatus(event.target.value as StatusView)}>
              <option value="active">In service</option>
              <option value="inactive">Retired</option>
              <option value="all">All</option>
            </select>
            <label className="inline-flex items-center gap-2 text-sm text-slate-600 sm:col-span-2">
              <input type="checkbox" checked={topLevelOnly} onChange={(event) => setTopLevelOnly(event.target.checked)} />
              Machines only (hide components)
            </label>
          </div>

          {canManage && scopeLabel ? (
            <div className="flex flex-wrap items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm">
              <QrCode className="h-4 w-4 text-slate-500" />
              <span className="text-slate-700">Tags for {scopeLabel}:</span>
              <button type="button" className={secondaryButton} onClick={() => void issueTags()} disabled={bulkIssue.isPending}><Tags className="h-4 w-4" />Issue missing tags</button>
              <select aria-label="Label size" className={cn(inputClass, 'w-auto')} value={labelLayout} onChange={(event) => setLabelLayout(event.target.value)}>
                <option value="a4-24">Small labels (24 per A4)</option>
                <option value="a4-10">Large labels (10 per A4)</option>
              </select>
              <button type="button" className={secondaryButton} onClick={() => void printLabels()} disabled={printing}><Printer className="h-4 w-4" />{printing ? 'Preparing…' : 'Print labels'}</button>
            </div>
          ) : null}

          <DataTable
            key={selectionKey}
            columns={columns}
            data={rows}
            loading={isLoading}
            batchSelect={canManage}
            onSelectionChange={setSelectedIds}
            emptyTitle={isError ? 'Assets unavailable' : 'No assets here yet'}
            emptyDescription={isError ? 'The asset list could not be loaded.' : 'Add assets one by one, or import your register from Excel.'}
            emptyActionLabel={!isError && canManage ? 'Add asset' : undefined}
            onEmptyAction={!isError && canManage ? () => setShowForm(true) : undefined}
            pagination={{
              page,
              pageSize,
              total: data?.totalCount ?? 0,
              onPageChange: setPage,
              onPageSizeChange: (size) => {
                setPageSize(size)
                setPage(1)
              },
            }}
          />
        </div>
      </div>

      <FactoryAssetFormDrawer open={showForm} onClose={() => setShowForm(false)} defaultOrgNodeId={selectedNode && selectedNode.level !== 'Group' ? selectedNode.id : undefined} />
      <ImportAssetsDrawer open={showImport} onClose={() => setShowImport(false)} />
    </div>
  )
}
