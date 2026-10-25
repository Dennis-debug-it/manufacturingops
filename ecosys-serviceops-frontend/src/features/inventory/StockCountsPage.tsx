import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { ClipboardList } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { EmptyState } from '../../shared/components/EmptyState'
import { ErrorState } from '../../shared/components/ErrorState'
import { Spinner } from '../../shared/components/Spinner'
import { StatCard } from '../../shared/components/StatCard'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { formatDateTime } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useBranches, useCatalogueSuggestions } from './api'
import {
  useCancelStockCount,
  useCreateStockCount,
  usePostStockCount,
  useSaveStockCountLines,
  useStockCount,
  useStockCounts,
  type StockCountLine,
  type StockCountStatus,
  type StockCountSummary,
} from './stockCountsApi'
import { InventoryTabNav } from './components/InventoryTabNav'

const statuses: Array<StockCountStatus | 'All'> = ['Open', 'Posted', 'Cancelled', 'All']
const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

function useCanCount() {
  const canIssue = usePermission('inventory.requests.issue')
  const canManage = usePermission('inventory.materials.manage')
  return canIssue || canManage
}

export function StockCountsPage() {
  const canCount = useCanCount()
  const [status, setStatus] = useState<StockCountStatus | 'All'>('Open')
  const [showNew, setShowNew] = useState(false)
  const { data, isLoading, isError, refetch } = useStockCounts(status)

  const columns: DataTableColumn<StockCountSummary>[] = [
    { key: 'countNumber', header: 'Count', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/stock-counts/${row.id}`}>{row.countNumber}</Link> },
    { key: 'branchName', header: 'Branch', cell: (row) => row.branchName ?? 'All stock' },
    { key: 'category', header: 'Category', cell: (row) => row.category ?? 'All parts' },
    { key: 'progress', header: 'Counted', cell: (row) => `${row.countedLines} / ${row.lineCount}` },
    { key: 'varianceLines', header: 'Variances', cell: (row) => row.varianceLines },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'startedByName', header: 'Started By', cell: (row) => row.startedByName ?? '—' },
    { key: 'createdAt', header: 'Opened', cell: (row) => formatDateTime(row.createdAt) },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Stock Counts"
        subtitle="Count what is on the shelf and post the differences as adjustments."
        primaryAction={
          canCount ? (
            <button type="button" onClick={() => setShowNew(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <ClipboardList className="h-4 w-4" />New Count
            </button>
          ) : undefined
        }
      />
      <InventoryTabNav />
      <div className="flex flex-wrap gap-2">
        {statuses.map((value) => (
          <button key={value} type="button" onClick={() => setStatus(value)} className={cn('inline-flex h-9 items-center rounded-lg border px-3 text-sm font-medium transition', status === value ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}>
            {value}
          </button>
        ))}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Stock counts unavailable' : status === 'Open' ? 'No count in progress' : 'No stock counts'}
        emptyDescription={isError ? 'Stock counts could not be loaded.' : 'Start one with New Count. Opening a count snapshots the book balances to count against.'}
        emptyActionLabel={isError ? 'Retry' : undefined}
        onEmptyAction={isError ? () => void refetch() : undefined}
      />
      {showNew ? <NewCountDrawer onClose={() => setShowNew(false)} /> : null}
    </div>
  )
}

function NewCountDrawer({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate()
  const { data: branches } = useBranches()
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)
  const { categories } = useCatalogueSuggestions()
  const [branchId, setBranchId] = useState('')
  const [category, setCategory] = useState('')
  const [notes, setNotes] = useState('')
  const create = useCreateStockCount()

  const submit = async () => {
    if (activeBranches.length > 1 && !branchId) return toast.error('Choose the branch to count')
    try {
      const count = await create.mutateAsync({ branchId: branchId || null, category: category || null, notes: notes.trim() || null })
      toast.success(`Stock count ${count.countNumber} opened`)
      navigate(`/inventory/stock-counts/${count.id}`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title="New Stock Count"
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={create.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {create.isPending ? 'Opening…' : 'Open Count'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        {activeBranches.length > 0 ? (
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="count-branch">Branch</label>
            <select id="count-branch" value={branchId} onChange={(event) => setBranchId(event.target.value)} className={fieldClass}>
              <option value="">{activeBranches.length > 1 ? 'Select…' : 'My default branch'}</option>
              {activeBranches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
            </select>
          </div>
        ) : null}
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="count-category">Category</label>
          <select id="count-category" value={category} onChange={(event) => setCategory(event.target.value)} className={fieldClass}>
            <option value="">All active parts</option>
            {categories.map((value) => <option key={value} value={value}>{value}</option>)}
          </select>
          <p className="mt-1 text-xs text-slate-500">Count a category at a time to keep each count small (a cycle count).</p>
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="count-notes">Notes</label>
          <input id="count-notes" value={notes} onChange={(event) => setNotes(event.target.value)} className={fieldClass} />
        </div>
        <p className="text-xs text-slate-500">The current book balance of every part is recorded now. Posting adjusts each counted part by counted − recorded, so receipts and issues while you count are kept.</p>
      </div>
    </FormDrawer>
  )
}

type LineFilter = 'all' | 'uncounted' | 'variance'

export function StockCountDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canCount = useCanCount()
  const canManage = usePermission('inventory.materials.manage')
  const canApprove = usePermission('inventory.requests.approve')
  const canPost = canManage || canApprove
  const { data, isLoading, isError, refetch } = useStockCount(id)
  const save = useSaveStockCountLines(id ?? '')
  const post = usePostStockCount(id ?? '')
  const cancel = useCancelStockCount(id ?? '')
  // Unsaved entries, keyed by line id; the server copy is the fallback.
  const [drafts, setDrafts] = useState<Record<string, { counted: string; notes: string }>>({})
  const [filter, setFilter] = useState<LineFilter>('all')
  const [search, setSearch] = useState('')
  const [confirm, setConfirm] = useState<'post' | 'cancel' | null>(null)

  const open = data?.status === 'Open'
  const editable = open && canCount
  const dirtyIds = Object.keys(drafts)

  const valueOf = (line: StockCountLine) => drafts[line.id] ?? { counted: line.countedQuantity != null ? String(line.countedQuantity) : '', notes: line.notes ?? '' }

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase()
    return (data?.lines ?? []).filter((line) => {
      const draft = drafts[line.id]
      const counted = draft ? (draft.counted.trim() === '' ? null : Number(draft.counted)) : line.countedQuantity
      if (filter === 'uncounted' && counted != null) return false
      if (filter === 'variance' && (counted == null || counted === line.expectedQuantity)) return false
      if (!term) return true
      return [line.itemName, line.itemCode, line.category].some((value) => value?.toLowerCase().includes(term))
    })
  }, [data, drafts, filter, search])

  const saveDrafts = async () => {
    const lines = dirtyIds.map((lineId) => {
      const draft = drafts[lineId]!
      const counted = draft.counted.trim() === '' ? null : Number(draft.counted)
      return { lineId, countedQuantity: counted, notes: draft.notes.trim() || null }
    })
    if (lines.some((line) => line.countedQuantity != null && (!Number.isFinite(line.countedQuantity) || line.countedQuantity < 0))) {
      toast.error('Counts must be zero or more')
      return false
    }
    try {
      await save.mutateAsync(lines)
      setDrafts({})
      toast.success(`Saved ${lines.length} line${lines.length === 1 ? '' : 's'}`)
      return true
    } catch {
      return false
    }
  }

  if (isLoading) return <Spinner label="Loading stock count" />
  if (isError) return <ErrorState title="Stock count unavailable" description="This stock count could not be loaded." onRetry={() => void refetch()} />
  if (!data) return <EmptyState title="Stock count not found" description="It may have been removed." />

  const counted = data.lines.filter((line) => line.countedQuantity != null).length
  const variances = data.lines.filter((line) => line.variance != null && line.variance !== 0)

  const columns: DataTableColumn<StockCountLine>[] = [
    {
      key: 'item',
      header: 'Part',
      cell: (row) => (
        <Link className="text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${row.materialItemId}`}>
          {row.itemName ?? 'Part'} <span className="font-mono text-xs text-slate-500">{row.itemCode}</span>
        </Link>
      ),
    },
    { key: 'expected', header: 'Expected', cell: (row) => <span className="tabular-nums">{row.expectedQuantity} {row.unitOfMeasure ?? ''}</span> },
    {
      key: 'counted',
      header: 'Counted',
      cell: (row) =>
        editable ? (
          <input
            type="number"
            min="0"
            step="any"
            inputMode="decimal"
            aria-label={`Counted ${row.itemName ?? 'part'}`}
            value={valueOf(row).counted}
            onChange={(event) => setDrafts((prev) => ({ ...prev, [row.id]: { ...valueOf(row), counted: event.target.value } }))}
            className="h-9 w-24 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        ) : (
          <span className="tabular-nums">{row.countedQuantity ?? '—'}</span>
        ),
    },
    {
      key: 'variance',
      header: 'Variance',
      cell: (row) => {
        const draft = drafts[row.id]
        const value = draft ? (draft.counted.trim() === '' ? null : Number(draft.counted) - row.expectedQuantity) : row.variance
        if (value == null || Number.isNaN(value)) return '—'
        return <span className={cn('font-semibold tabular-nums', value === 0 ? 'text-slate-500' : value > 0 ? 'text-emerald-700' : 'text-red-600')}>{value > 0 ? `+${value}` : value}</span>
      },
    },
    { key: 'varianceValue', header: 'Value', cell: (row) => (row.varianceValue != null && row.varianceValue !== 0 ? formatCurrency(row.varianceValue) : '—') },
    {
      key: 'notes',
      header: 'Note',
      cell: (row) =>
        editable ? (
          <input
            aria-label={`Note for ${row.itemName ?? 'part'}`}
            value={valueOf(row).notes}
            onChange={(event) => setDrafts((prev) => ({ ...prev, [row.id]: { ...valueOf(row), notes: event.target.value } }))}
            placeholder="e.g. 2 damaged"
            className="h-9 w-40 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        ) : (
          row.notes ?? '—'
        ),
    },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title={`Stock Count ${data.countNumber}`}
        subtitle={[data.branchName ?? 'All stock', data.category ?? 'All parts', `opened ${formatDateTime(data.createdAt)}${data.startedByName ? ` by ${data.startedByName}` : ''}`].join(' · ')}
        breadcrumbs={[{ label: 'Stock Counts', to: '/inventory/stock-counts' }, { label: data.countNumber }]}
        secondaryActions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={data.status} />
            {editable && dirtyIds.length > 0 ? (
              <button type="button" disabled={save.isPending} onClick={() => void saveDrafts()} className="rounded-lg bg-slate-900 px-3 py-1.5 text-xs font-medium text-white disabled:opacity-60">
                {save.isPending ? 'Saving…' : `Save ${dirtyIds.length} change${dirtyIds.length === 1 ? '' : 's'}`}
              </button>
            ) : null}
            {open && canPost ? (
              <button type="button" disabled={dirtyIds.length > 0 || counted === 0} title={dirtyIds.length > 0 ? 'Save your changes first' : undefined} onClick={() => setConfirm('post')} className="rounded-lg bg-[var(--app-primary)] px-3 py-1.5 text-xs font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-50">
                Post Count
              </button>
            ) : null}
            {editable ? (
              <button type="button" onClick={() => setConfirm('cancel')} className="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50">Cancel Count</button>
            ) : null}
          </div>
        }
      />

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatCard label="Parts" value={data.lines.length} />
        <StatCard label="Counted" value={`${counted} / ${data.lines.length}`} tone={counted === data.lines.length ? 'success' : 'default'} />
        <StatCard label="Variances" value={variances.length} tone={variances.length > 0 ? 'warning' : 'success'} />
        <StatCard label="Net Variance Value" value={formatCurrency(data.netVarianceValue)} />
      </div>
      {data.status === 'Posted' ? (
        <p className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
          Posted {data.postedAt ? formatDateTime(data.postedAt) : ''}{data.postedByName ? ` by ${data.postedByName}` : ''}. Each variance is an adjustment under {data.countNumber} in Stock Movements.
        </p>
      ) : null}

      <div className="flex flex-wrap items-center gap-2">
        <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Find a part…" aria-label="Find a part" className="h-10 min-w-[12rem] flex-1 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        {(['all', 'uncounted', 'variance'] as const).map((value) => (
          <button key={value} type="button" onClick={() => setFilter(value)} className={cn('inline-flex h-10 items-center rounded-lg border px-3 text-sm font-medium transition', filter === value ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}>
            {value === 'all' ? 'All' : value === 'uncounted' ? 'Not counted' : 'Variances'}
          </button>
        ))}
      </div>

      <DataTable columns={columns} data={rows} emptyTitle="No lines" emptyDescription="Nothing matches this filter." virtualise={rows.length > 150} />

      <ConfirmDialog
        open={confirm !== null}
        title={confirm === 'post' ? 'Post Stock Count' : 'Cancel Stock Count'}
        message={
          confirm === 'post'
            ? `This adjusts ${variances.length} part${variances.length === 1 ? '' : 's'} (net ${formatCurrency(data.netVarianceValue)}). ${data.lines.length - counted} uncounted part${data.lines.length - counted === 1 ? ' is' : 's are'} left unchanged. This cannot be undone.`
            : 'The count is closed without changing any stock.'
        }
        confirmLabel={confirm === 'post' ? 'Post' : 'Cancel Count'}
        danger={confirm === 'cancel'}
        onCancel={() => setConfirm(null)}
        onConfirm={async () => {
          try {
            if (confirm === 'post') {
              await post.mutateAsync(undefined)
              toast.success('Stock count posted')
            } else {
              await cancel.mutateAsync(undefined)
              toast.success('Stock count cancelled')
            }
          } catch {
            // interceptor surfaces the error toast
          } finally {
            setConfirm(null)
          }
        }}
      />
    </div>
  )
}
