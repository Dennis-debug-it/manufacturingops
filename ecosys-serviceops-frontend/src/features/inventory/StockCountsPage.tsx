import { useMemo, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { ClipboardList, Download, FileUp, Plus, Printer, ScanLine } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { EmptyState } from '../../shared/components/EmptyState'
import { ErrorState } from '../../shared/components/ErrorState'
import { Spinner } from '../../shared/components/Spinner'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { formatDateTime } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useBranches, useCatalogueSuggestions, useMaterials } from './api'
import { useVans } from './vansApi'
import {
  useAddStockCountLine,
  useCancelStockCount,
  useCreateStockCount,
  usePostStockCount,
  useSaveStockCountLines,
  useStockCount,
  useStockCounts,
  type SaveStockCountLinePayload,
  type StockCountLine,
  type StockCountStatus,
  type StockCountSummary,
} from './stockCountsApi'
import { countSheetCsv, downloadText, printCountSheet, readCountSheet } from './stockCountSheet'
import { SummaryStrip } from './components/SummaryStrip'

const statuses: Array<StockCountStatus | 'All'> = ['Open', 'Posted', 'Cancelled', 'All']
const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const toolButton = 'inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60'

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
    { key: 'countNumber', header: 'Count', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/stock-counts/${row.id}`}>{row.countNumber}</Link>, sortValue: (row) => row.countNumber },
    { key: 'location', header: 'Location', cell: (row) => <span>{row.locationName ?? row.branchName ?? 'All stock'}{row.vanId ? <span className="ml-1 text-xs text-slate-500">Van</span> : null}</span>, sortValue: (row) => row.locationName },
    { key: 'category', header: 'Category', cell: (row) => row.category ?? 'All parts', sortValue: (row) => row.category },
    { key: 'progress', header: 'Counted', cell: (row) => `${row.countedLines} / ${row.lineCount}`, sortValue: (row) => (row.lineCount ? row.countedLines / row.lineCount : 0) },
    { key: 'varianceLines', header: 'Variances', cell: (row) => (row.isBlind && row.status === 'Open' ? <span className="text-slate-400">Blind</span> : row.varianceLines), sortValue: (row) => row.varianceLines },
    { key: 'recount', header: 'Recount', cell: (row) => (row.recountLines > 0 ? <span className="font-semibold text-amber-700">{row.recountLines}</span> : '—'), sortValue: (row) => row.recountLines },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} />, sortValue: (row) => row.status },
    { key: 'startedByName', header: 'Started by', cell: (row) => row.startedByName ?? '—', sortValue: (row) => row.startedByName },
    { key: 'createdAt', header: 'Opened', cell: (row) => formatDateTime(row.createdAt), sortValue: (row) => row.createdAt },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Stock counts"
        subtitle="Count what is on the shelf or on a van, and post the differences as adjustments."
        primaryAction={
          canCount ? (
            <button type="button" onClick={() => setShowNew(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <ClipboardList className="h-4 w-4" />New count
            </button>
          ) : undefined
        }
      />
      <div className="flex flex-wrap gap-2">
        {statuses.map((value) => (
          <button key={value} type="button" aria-pressed={status === value} onClick={() => setStatus(value)} className={cn('inline-flex h-9 items-center rounded-lg border px-3 text-sm font-medium transition', status === value ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}>
            {value}
          </button>
        ))}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Stock counts unavailable' : status === 'Open' ? 'No count in progress' : 'No stock counts'}
        emptyDescription={isError ? 'Stock counts could not be loaded.' : 'Start one with New count. Opening a count records the book balances to count against.'}
        emptyActionLabel={isError ? 'Retry' : canCount && status === 'Open' ? 'New count' : undefined}
        onEmptyAction={isError ? () => void refetch() : canCount && status === 'Open' ? () => setShowNew(true) : undefined}
      />
      {showNew ? <NewCountDrawer onClose={() => setShowNew(false)} /> : null}
    </div>
  )
}

function NewCountDrawer({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate()
  const { data: branches } = useBranches()
  const { data: vans } = useVans()
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)
  const activeVans = (vans ?? []).filter((van) => van.isActive)
  const { categories } = useCatalogueSuggestions()
  const [location, setLocation] = useState('')
  const [category, setCategory] = useState('')
  const [notes, setNotes] = useState('')
  const [onlyStocked, setOnlyStocked] = useState(true)
  const [blind, setBlind] = useState(false)
  const create = useCreateStockCount()

  const submit = async () => {
    const [kind, id] = location.split(':')
    if (activeBranches.length > 1 && !location) return void toast.error('Choose what to count')
    try {
      const count = await create.mutateAsync({
        branchId: kind === 'branch' ? id ?? null : null,
        vanId: kind === 'van' ? id ?? null : null,
        category: category || null,
        notes: notes.trim() || null,
        onlyStocked,
        blind,
      })
      toast.success(`Stock count ${count.countNumber} opened`)
      navigate(`/inventory/stock-counts/${count.id}`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title="New stock count"
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={create.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {create.isPending ? 'Opening…' : 'Open count'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="count-location">What are you counting?</label>
          <select id="count-location" value={location} onChange={(event) => setLocation(event.target.value)} className={fieldClass}>
            <option value="">{activeBranches.length > 1 ? 'Select…' : activeBranches.length === 1 ? activeBranches[0]!.name : 'Main stores'}</option>
            {activeBranches.length > 1 ? (
              <optgroup label="Branches">
                {activeBranches.map((branch) => <option key={branch.id} value={`branch:${branch.id}`}>{branch.name}</option>)}
              </optgroup>
            ) : null}
            {activeVans.length > 0 ? (
              <optgroup label="Vans">
                {activeVans.map((van) => <option key={van.id} value={`van:${van.id}`}>{van.name}{van.technicianName ? ` (${van.technicianName})` : ''}</option>)}
              </optgroup>
            ) : null}
          </select>
        </div>
        <div>
          <label className={labelClass} htmlFor="count-category">Category</label>
          <select id="count-category" value={category} onChange={(event) => setCategory(event.target.value)} className={fieldClass}>
            <option value="">All parts</option>
            {categories.map((value) => <option key={value} value={value}>{value}</option>)}
          </select>
          <p className="mt-1 text-xs text-slate-500">Count one category at a time to keep each count short (a cycle count).</p>
        </div>
        <label className="flex items-start gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={onlyStocked} onChange={(event) => setOnlyStocked(event.target.checked)} className="mt-0.5 h-4 w-4 rounded border-slate-300" />
          <span>Only parts held here<span className="block text-xs text-slate-500">Leaves out parts never stocked at this location. Anything else found on the shelf can be added during the count.</span></span>
        </label>
        <label className="flex items-start gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={blind} onChange={(event) => setBlind(event.target.checked)} className="mt-0.5 h-4 w-4 rounded border-slate-300" />
          <span>Blind count<span className="block text-xs text-slate-500">Counters don't see the book quantity, so they count what is there instead of confirming a number. Whoever posts the count still sees the differences.</span></span>
        </label>
        <div>
          <label className={labelClass} htmlFor="count-notes">Notes</label>
          <input id="count-notes" value={notes} onChange={(event) => setNotes(event.target.value)} className={fieldClass} />
        </div>
        <p className="text-xs text-slate-500">The book balance of every listed part is recorded now. Posting adjusts each counted part by counted − recorded, so receipts and issues while you count are kept.</p>
      </div>
    </FormDrawer>
  )
}

type LineFilter = 'all' | 'uncounted' | 'variance' | 'recount'

interface LineDraft {
  counted: string
  notes: string
  needsRecount: boolean
}

export function StockCountDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canCount = useCanCount()
  const canManage = usePermission('inventory.materials.manage')
  const canApprove = usePermission('inventory.requests.approve')
  const canPost = canManage || canApprove
  const { data, isLoading, isError, refetch } = useStockCount(id)
  const save = useSaveStockCountLines(id ?? '')
  const addLine = useAddStockCountLine(id ?? '')
  const post = usePostStockCount(id ?? '')
  const cancel = useCancelStockCount(id ?? '')
  const { data: materials } = useMaterials({ status: 'active' })
  // Unsaved entries, keyed by line id; the server copy is the fallback.
  const [drafts, setDrafts] = useState<Record<string, LineDraft>>({})
  const [filter, setFilter] = useState<LineFilter>('all')
  const [search, setSearch] = useState('')
  const [confirm, setConfirm] = useState<'post' | 'cancel' | null>(null)
  const [quickCode, setQuickCode] = useState('')
  const [quickQty, setQuickQty] = useState('')
  const [quickMode, setQuickMode] = useState<'set' | 'add'>('set')
  const [showAdd, setShowAdd] = useState(false)
  const [addPartId, setAddPartId] = useState('')
  const [addQty, setAddQty] = useState('')
  const codeInput = useRef<HTMLInputElement | null>(null)
  const fileInput = useRef<HTMLInputElement | null>(null)

  const open = data?.status === 'Open'
  const editable = open && canCount
  const dirtyIds = Object.keys(drafts)
  const hidden = data?.expectedHidden ?? false

  const valueOf = (line: StockCountLine): LineDraft =>
    drafts[line.id] ?? { counted: line.countedQuantity != null ? String(line.countedQuantity) : '', notes: line.notes ?? '', needsRecount: line.needsRecount }
  const setDraft = (line: StockCountLine, patch: Partial<LineDraft>) => setDrafts((prev) => ({ ...prev, [line.id]: { ...valueOf(line), ...patch } }))
  const countedOf = (line: StockCountLine) => {
    const draft = drafts[line.id]
    return draft ? (draft.counted.trim() === '' ? null : Number(draft.counted)) : line.countedQuantity
  }

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase()
    return (data?.lines ?? []).filter((line) => {
      const counted = countedOf(line)
      const recount = drafts[line.id]?.needsRecount ?? line.needsRecount
      if (filter === 'uncounted' && counted != null) return false
      if (filter === 'variance' && (counted == null || line.expectedQuantity == null || counted === line.expectedQuantity)) return false
      if (filter === 'recount' && !recount) return false
      if (!term) return true
      return [line.itemName, line.itemCode, line.category, line.barcode].some((value) => value?.toLowerCase().includes(term))
    })
    // countedOf reads drafts, which is a dependency already.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data, drafts, filter, search])

  const saveDrafts = async () => {
    const lines: SaveStockCountLinePayload[] = dirtyIds.map((lineId) => {
      const draft = drafts[lineId]!
      const original = data?.lines.find((line) => line.id === lineId)
      const counted = draft.counted.trim() === '' ? null : Number(draft.counted)
      const payload: SaveStockCountLinePayload = { lineId, countedQuantity: counted, notes: draft.notes.trim() || null }
      if (!original || original.needsRecount !== draft.needsRecount) payload.needsRecount = draft.needsRecount
      return payload
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

  /** Quick entry: scan or type a code, enter a quantity, press Enter. Unknown codes can be added. */
  const quickEnter = async () => {
    if (!data) return
    const code = quickCode.trim().toLowerCase()
    if (!code) return
    const qty = quickQty.trim() === '' ? 1 : Number(quickQty)
    if (!Number.isFinite(qty) || qty < 0) return void toast.error('Enter a quantity of 0 or more')
    const line = data.lines.find((item) => item.itemCode?.toLowerCase() === code || item.barcode?.toLowerCase() === code)
    if (line) {
      const current = countedOf(line) ?? 0
      const next = quickMode === 'add' ? current + qty : qty
      setDraft(line, { counted: String(next) })
      toast.success(`${line.itemName ?? 'Part'}: ${next}`)
    } else {
      const part = (materials ?? []).find((item) => item.itemCode.toLowerCase() === code || item.barcode?.toLowerCase() === code)
      if (!part) return void toast.error(`No part with code ${quickCode.trim()}`)
      try {
        await addLine.mutateAsync({ materialItemId: part.id, countedQuantity: qty })
        toast.success(`${part.itemName} added to the count: ${qty}`)
      } catch {
        return
      }
    }
    setQuickCode('')
    setQuickQty('')
    codeInput.current?.focus()
  }

  const importSheet = async (file: File) => {
    if (!data) return
    try {
      const result = readCountSheet(await file.text(), data.lines)
      const ids = Object.keys(result.updates)
      setDrafts((prev) => {
        const next = { ...prev }
        for (const lineId of ids) {
          const line = data.lines.find((item) => item.id === lineId)!
          const update = result.updates[lineId]!
          next[lineId] = { ...(prev[lineId] ?? valueOf(line)), counted: update.counted, ...(update.notes ? { notes: update.notes } : {}) }
        }
        return next
      })
      const problems = [
        result.unknown.length ? `${result.unknown.length} code${result.unknown.length === 1 ? '' : 's'} not on this count (${result.unknown.slice(0, 3).join(', ')}${result.unknown.length > 3 ? '…' : ''})` : '',
        result.invalid.length ? `${result.invalid.length} row${result.invalid.length === 1 ? '' : 's'} with a count that isn't a number` : '',
      ].filter(Boolean)
      toast.success(`Filled in ${ids.length} line${ids.length === 1 ? '' : 's'} — review and Save.`)
      if (problems.length) toast.error(`Skipped ${problems.join('; ')}.`, { duration: 8000 })
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'That file could not be read.')
    }
  }

  if (isLoading) return <Spinner label="Loading stock count" />
  if (isError) return <ErrorState title="Stock count unavailable" description="This stock count could not be loaded." onRetry={() => void refetch()} />
  if (!data) return <EmptyState title="Stock count not found" description="It may have been removed." />

  const counted = data.lines.filter((line) => line.countedQuantity != null).length
  const variances = data.lines.filter((line) => line.variance != null && line.variance !== 0)
  const recounts = data.lines.filter((line) => line.needsRecount).length
  const onList = new Set(data.lines.map((line) => line.materialItemId))

  const columns: DataTableColumn<StockCountLine>[] = [
    {
      key: 'item',
      header: 'Part',
      cell: (row) => (
        <Link className="text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${row.materialItemId}`}>
          {row.itemName ?? 'Part'} <span className="font-mono text-xs text-slate-500">{row.itemCode}</span>
        </Link>
      ),
      sortValue: (row) => row.itemName,
    },
    { key: 'expected', header: 'Expected', cell: (row) => (row.expectedQuantity == null ? <span className="text-slate-400">Hidden</span> : <span className="tabular-nums">{row.expectedQuantity} {row.unitOfMeasure ?? ''}</span>), sortValue: (row) => row.expectedQuantity },
    {
      key: 'counted',
      header: 'Counted',
      cell: (row) =>
        editable ? (
          <input type="number" min="0" step="any" inputMode="decimal" aria-label={`Counted ${row.itemName ?? 'part'}`} value={valueOf(row).counted} onChange={(event) => setDraft(row, { counted: event.target.value })} className="h-9 w-24 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        ) : (
          <span className="tabular-nums">{row.countedQuantity ?? '—'}</span>
        ),
      sortValue: (row) => countedOf(row),
    },
    {
      key: 'variance',
      header: 'Variance',
      cell: (row) => {
        if (row.expectedQuantity == null) return '—'
        const current = countedOf(row)
        const value = current == null || Number.isNaN(current) ? null : current - row.expectedQuantity
        if (value == null) return '—'
        return <span className={cn('font-semibold tabular-nums', value === 0 ? 'text-slate-500' : value > 0 ? 'text-emerald-700' : 'text-red-600')}>{value > 0 ? `+${value}` : value}</span>
      },
    },
    { key: 'varianceValue', header: 'Value', cell: (row) => (row.varianceValue != null && row.varianceValue !== 0 ? formatCurrency(row.varianceValue) : '—'), sortValue: (row) => row.varianceValue },
    {
      key: 'recount',
      header: 'Recount',
      cell: (row) =>
        editable ? (
          <input type="checkbox" aria-label={`Recount ${row.itemName ?? 'part'}`} checked={valueOf(row).needsRecount} onChange={(event) => setDraft(row, { needsRecount: event.target.checked })} className="h-4 w-4 rounded border-slate-300" />
        ) : row.needsRecount ? (
          <span className="text-xs font-semibold text-amber-700">Recount</span>
        ) : null,
    },
    {
      key: 'notes',
      header: 'Note',
      cell: (row) =>
        editable ? (
          <input aria-label={`Note for ${row.itemName ?? 'part'}`} value={valueOf(row).notes} onChange={(event) => setDraft(row, { notes: event.target.value })} placeholder="e.g. 2 damaged" className="h-9 w-40 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        ) : (
          row.notes ?? '—'
        ),
    },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title={`Stock count ${data.countNumber}`}
        subtitle={[data.locationName ?? data.branchName ?? 'All stock', data.vanId ? 'van' : null, data.category ?? 'All parts', data.isBlind ? 'blind count' : null, `opened ${formatDateTime(data.createdAt)}${data.startedByName ? ` by ${data.startedByName}` : ''}`].filter(Boolean).join(' · ')}
        breadcrumbs={[{ label: 'Stock counts', to: '/inventory/stock-counts' }, { label: data.countNumber }]}
        secondaryActions={
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={data.status} />
            {editable && dirtyIds.length > 0 ? (
              <button type="button" disabled={save.isPending} onClick={() => void saveDrafts()} className="inline-flex h-10 items-center rounded-lg bg-slate-900 px-3 text-sm font-medium text-white disabled:opacity-60">
                {save.isPending ? 'Saving…' : `Save ${dirtyIds.length} change${dirtyIds.length === 1 ? '' : 's'}`}
              </button>
            ) : null}
            {open && canPost ? (
              <button type="button" disabled={dirtyIds.length > 0 || counted === 0 || recounts > 0} title={dirtyIds.length > 0 ? 'Save your changes first' : recounts > 0 ? 'Recount or clear the marked lines first' : undefined} onClick={() => setConfirm('post')} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-3 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-50">
                Post count
              </button>
            ) : null}
            {editable ? (
              <button type="button" onClick={() => setConfirm('cancel')} className="inline-flex h-10 items-center rounded-lg border border-red-200 bg-white px-3 text-sm font-medium text-red-600 hover:bg-red-50">Cancel count</button>
            ) : null}
          </div>
        }
      />

      <SummaryStrip
        figures={[
          { label: 'Parts', value: data.lines.length },
          { label: 'Counted', value: `${counted} / ${data.lines.length}`, tone: counted === data.lines.length ? 'success' : 'default' },
          { label: 'Variances', value: hidden ? 'Hidden' : variances.length, tone: !hidden && variances.length > 0 ? 'warning' : 'default' },
          { label: 'Marked for recount', value: recounts, tone: recounts > 0 ? 'warning' : 'default' },
          { label: 'Net variance value', value: hidden ? 'Hidden' : formatCurrency(data.netVarianceValue) },
        ]}
      />
      {hidden ? <p className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-600">Blind count: book quantities are hidden while you count. Count what is there; whoever posts the count reviews the differences.</p> : null}
      {data.status === 'Posted' ? (
        <p className="rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
          Posted {data.postedAt ? formatDateTime(data.postedAt) : ''}{data.postedByName ? ` by ${data.postedByName}` : ''}. Each variance is an adjustment under {data.countNumber} in Movements.
        </p>
      ) : null}

      <div className="flex flex-wrap items-center gap-2">
        <button type="button" onClick={() => downloadText(`${data.countNumber}-count-sheet.csv`, countSheetCsv(data))} className={toolButton}><Download className="h-4 w-4" />Count sheet (CSV)</button>
        <button type="button" onClick={() => { if (!printCountSheet(data)) toast.error('Allow pop-ups to print the count sheet') }} className={toolButton}><Printer className="h-4 w-4" />Print sheet</button>
        {editable ? (
          <>
            <button type="button" onClick={() => fileInput.current?.click()} className={toolButton}><FileUp className="h-4 w-4" />Import counted sheet</button>
            <input ref={fileInput} type="file" accept=".csv,text/csv" className="hidden" onChange={(event) => { const file = event.target.files?.[0]; if (file) void importSheet(file); event.target.value = '' }} />
            <button type="button" onClick={() => setShowAdd(true)} className={toolButton}><Plus className="h-4 w-4" />Add part</button>
          </>
        ) : null}
      </div>

      {editable ? (
        <form
          className="flex flex-wrap items-end gap-2 rounded-lg border border-slate-200 bg-white p-3"
          onSubmit={(event) => {
            event.preventDefault()
            void quickEnter()
          }}
        >
          <div className="min-w-[12rem] flex-1">
            <label className="mb-1 flex items-center gap-1.5 text-xs font-medium text-slate-600" htmlFor="quick-code"><ScanLine className="h-3.5 w-3.5" />Quick entry — scan or type a code</label>
            <input id="quick-code" ref={codeInput} value={quickCode} onChange={(event) => setQuickCode(event.target.value)} placeholder="Part code or barcode" autoComplete="off" className="h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1 block text-xs font-medium text-slate-600" htmlFor="quick-qty">Qty</label>
            <input id="quick-qty" type="number" min="0" step="any" inputMode="decimal" value={quickQty} onChange={(event) => setQuickQty(event.target.value)} placeholder="1" className="h-10 w-24 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div className="inline-flex rounded-lg border border-slate-300 p-0.5" role="group" aria-label="Quick entry mode">
            {(['set', 'add'] as const).map((mode) => (
              <button key={mode} type="button" aria-pressed={quickMode === mode} onClick={() => setQuickMode(mode)} className={cn('h-9 rounded-md px-3 text-xs font-medium', quickMode === mode ? 'bg-slate-900 text-white' : 'text-slate-700')}>
                {mode === 'set' ? 'Set count' : 'Add to count'}
              </button>
            ))}
          </div>
          <button type="submit" disabled={addLine.isPending} className="inline-flex h-10 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white disabled:opacity-60">Enter</button>
          <p className="w-full text-xs text-slate-500">"Add to count" adds each scan to the running total — scan every item in a bin. Entries are drafts until you Save.</p>
        </form>
      ) : null}

      <div className="flex flex-wrap items-center gap-2">
        <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Find a part…" aria-label="Find a part" className="h-10 min-w-[12rem] flex-1 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
        {(['all', 'uncounted', 'variance', 'recount'] as const).map((value) => (
          <button key={value} type="button" aria-pressed={filter === value} onClick={() => setFilter(value)} className={cn('inline-flex h-10 items-center rounded-lg border px-3 text-sm font-medium transition', filter === value ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}>
            {value === 'all' ? 'All' : value === 'uncounted' ? 'Not counted' : value === 'variance' ? 'Variances' : 'Recount'}
          </button>
        ))}
      </div>

      <DataTable columns={columns} data={rows} emptyTitle="No lines" emptyDescription="Nothing matches this filter." virtualise={rows.length > 150} />

      <FormDrawer
        open={showAdd}
        title="Add a part to this count"
        onClose={() => setShowAdd(false)}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => setShowAdd(false)} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancel</button>
            <button
              type="button"
              disabled={addLine.isPending || !addPartId}
              onClick={() => {
                const qty = addQty.trim() === '' ? null : Number(addQty)
                if (qty != null && (!Number.isFinite(qty) || qty < 0)) return void toast.error('Enter a count of 0 or more')
                void addLine.mutateAsync({ materialItemId: addPartId, countedQuantity: qty }).then(() => {
                  toast.success('Part added')
                  setShowAdd(false)
                  setAddPartId('')
                  setAddQty('')
                }).catch(() => undefined)
              }}
              className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
            >
              Add part
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <p className="text-sm text-slate-600">For a part you found here that the count didn't list. Its current book balance at this location is recorded as the expected quantity.</p>
          <div>
            <label className={labelClass} htmlFor="add-part">Part</label>
            <select id="add-part" value={addPartId} onChange={(event) => setAddPartId(event.target.value)} className={fieldClass}>
              <option value="">Select a part…</option>
              {(materials ?? []).filter((material) => !onList.has(material.id)).map((material) => <option key={material.id} value={material.id}>{material.itemName} ({material.itemCode})</option>)}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="add-qty">Counted (optional)</label>
            <input id="add-qty" type="number" min="0" step="any" value={addQty} onChange={(event) => setAddQty(event.target.value)} className={fieldClass} />
          </div>
        </div>
      </FormDrawer>

      <ConfirmDialog
        open={confirm !== null}
        title={confirm === 'post' ? 'Post stock count' : 'Cancel stock count'}
        message={
          confirm === 'post'
            ? `This adjusts ${variances.length} part${variances.length === 1 ? '' : 's'} (net ${formatCurrency(data.netVarianceValue)}; ${formatCurrency(data.grossVarianceValue)} in differences either way). ${data.lines.length - counted} uncounted part${data.lines.length - counted === 1 ? ' is' : 's are'} left unchanged. If the differences are above your approval limit (Settings → Inventory), only someone who approves materials can post. This cannot be undone.`
            : 'The count is closed without changing any stock.'
        }
        confirmLabel={confirm === 'post' ? 'Post' : 'Cancel count'}
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
