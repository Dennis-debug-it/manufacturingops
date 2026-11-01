import { useEffect, useMemo, useState, type ReactNode } from 'react'
import toast from 'react-hot-toast'
import { DndContext, PointerSensor, TouchSensor, useDraggable, useDroppable, useSensor, useSensors, type DragEndEvent } from '@dnd-kit/core'
import { Link, useSearchParams } from 'react-router-dom'
import { AlertTriangle, CalendarClock, ClipboardList, Coins, Download, FileText, GripVertical, LayoutGrid, List, Plus, Search, Upload } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatCard } from '../../shared/components/StatCard'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { useAuth } from '../../shared/hooks/useAuth'
import { formatDate, isPast } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { useChangeStage, useDealFields, useOpportunities, usePipelines, usePipelineSummary } from './api'
import { OpportunityFormDrawer } from './components/OpportunityFormDrawer'
import { CrmNav } from './components/CrmNav'
import { ExchangeRatesDrawer } from './components/ExchangeRatesDrawer'
import { DealImportDrawer } from './components/DealImportDrawer'
import { importService } from '../../shared/services/importService'
import { OwnerSelect } from './components/OwnerSelect'
import { SavedViewsMenu } from './components/SavedViewsMenu'
import { SALES_STAGES, fieldAppliesTo, isClosedStage, opportunityParty, openStagesOf, pipelineFor, stageLabels, type SalesOpportunityListItem, type SalesStage } from './types'

type View = 'board' | 'list'

/** The page's filters as a query string, for saved views and shareable links; empty filters are left out. */
export function pipelineQuery(filters: { view: View; search: string; stage: string; owner: string; field: string; value: string; pipeline: string | undefined }) {
  const params = new URLSearchParams()
  if (filters.pipeline) params.set('pipeline', filters.pipeline)
  if (filters.view === 'list') params.set('view', 'list')
  if (filters.search.trim()) params.set('q', filters.search.trim())
  if (filters.view === 'list' && filters.stage) params.set('stage', filters.stage)
  if (filters.owner) params.set('owner', filters.owner)
  if (filters.view === 'list' && filters.field) {
    params.set('field', filters.field)
    if (filters.value) params.set('value', filters.value)
  }
  return params.toString()
}

export function PipelinePage() {
  const canManage = usePermission('crm.manage')
  const [searchParams, setSearchParams] = useSearchParams()
  const [view, setView] = useState<View>(searchParams.get('view') === 'list' ? 'list' : 'board')
  const [search, setSearch] = useState(searchParams.get('q') ?? '')
  const [stageFilter, setStageFilter] = useState(searchParams.get('stage') ?? '')
  const [showForm, setShowForm] = useState(false)
  const [showRates, setShowRates] = useState(false)
  const [showImport, setShowImport] = useState(false)
  const [ownerFilter, setOwnerFilter] = useState(searchParams.get('owner') ?? '')
  const [fieldFilter, setFieldFilter] = useState(searchParams.get('field') ?? '')
  const [fieldValue, setFieldValue] = useState(searchParams.get('value') ?? '')
  const [losing, setLosing] = useState<SalesOpportunityListItem | null>(null)
  const [lostReason, setLostReason] = useState('')
  const debouncedFieldValue = useDebounce(fieldValue, 300)
  const { user } = useAuth()
  const debouncedSearch = useDebounce(search, 300)
  const { data: pipelines } = usePipelines()
  const usable = useMemo(() => (pipelines ?? []).filter((p) => p.isActive && p.available), [pipelines])
  const pipeline = pipelineFor(usable, searchParams.get('pipeline'))
  // With one pipeline there is nothing to narrow, so the first load does not wait for the pipeline list.
  const pipelineId = usable.length > 1 ? pipeline?.id : undefined
  const { data: dealFields } = useDealFields()
  const filterableFields = (dealFields?.fields ?? []).filter((field) => fieldAppliesTo(field, pipeline?.id))
  const filterField = filterableFields.find((field) => field.key === fieldFilter)
  const choosePipeline = (id: string) => {
    setStageFilter('')
    setFieldFilter('')
    setFieldValue('')
    setSearchParams((params) => { params.set('pipeline', id); return params }, { replace: true })
  }
  const { data: summary } = usePipelineSummary(pipelineId)
  const { data, isLoading, isError } = useOpportunities({
    search: debouncedSearch,
    stage: view === 'list' ? stageFilter || undefined : undefined,
    ownerUserId: ownerFilter || undefined,
    pipelineId,
    field: view === 'list' && filterField ? filterField.key : undefined,
    fieldValue: view === 'list' && filterField ? debouncedFieldValue || undefined : undefined,
  })
  const boardStages = useMemo(
    () => pipeline?.stages.map((s) => ({ key: s.key, name: s.name })) ?? SALES_STAGES.map((key) => ({ key, name: stageLabels[key] ?? key })),
    [pipeline],
  )
  const openStages = useMemo(() => openStagesOf(pipeline), [pipeline])
  const nameOf = (stage: SalesStage) => boardStages.find((s) => s.key === stage)?.name ?? stageLabels[stage] ?? stage
  const reporting = summary?.reportingCurrency
  const changeStage = useChangeStage()
  const rows = useMemo(() => data ?? [], [data])

  // Keep the address bar in step with the filters, so a view can be bookmarked, shared or saved.
  const currentQuery = pipelineQuery({ view, search: debouncedSearch, stage: stageFilter, owner: ownerFilter, field: fieldFilter, value: debouncedFieldValue, pipeline: searchParams.get('pipeline') ?? undefined })
  useEffect(() => {
    if (currentQuery !== searchParams.toString()) setSearchParams(new URLSearchParams(currentQuery), { replace: true })
  }, [currentQuery, searchParams, setSearchParams])
  const applyQuery = (query: string) => {
    const params = new URLSearchParams(query)
    setView(params.get('view') === 'list' ? 'list' : 'board')
    setSearch(params.get('q') ?? '')
    setStageFilter(params.get('stage') ?? '')
    setOwnerFilter(params.get('owner') ?? '')
    setFieldFilter(params.get('field') ?? '')
    setFieldValue(params.get('value') ?? '')
    setSearchParams(params, { replace: true })
  }

  // A card dragged to another column shows there at once; the server has the last word.
  const [moved, setMoved] = useState<Record<string, SalesStage>>({})
  const moveDeal = (item: SalesOpportunityListItem, stage: SalesStage) => {
    if (stage === item.stage) return
    if (stage === 'Lost') {
      setLosing(item)
      return
    }
    setMoved((current) => ({ ...current, [item.id]: stage }))
    changeStage.mutate(
      { id: item.id, stage },
      {
        onSuccess: () => toast.success(`${item.title} moved to ${nameOf(stage)}`),
        onSettled: () => setMoved((current) => { const next = { ...current }; delete next[item.id]; return next }),
      },
    )
  }
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    // No keyboard dragging: keys pressed in a card's stage menu would start one. That menu is the keyboard way to move a deal.
    useSensor(TouchSensor, { activationConstraint: { delay: 250, tolerance: 8 } }),
  )
  const onDragEnd = (event: DragEndEvent) => {
    const item = rows.find((row) => row.id === event.active.id)
    const stage = event.over?.id
    if (item && typeof stage === 'string') moveDeal(item, stage)
  }

  const byStage = useMemo(() => {
    const groups = new Map<SalesStage, SalesOpportunityListItem[]>(boardStages.map((stage) => [stage.key, []]))
    rows.forEach((row) => groups.get(moved[row.id] ?? row.stage)?.push(row))
    return groups
  }, [rows, boardStages, moved])

  const columns: DataTableColumn<SalesOpportunityListItem>[] = [
    { key: 'title', header: 'Opportunity', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/crm/opportunities/${row.id}`}>{row.title}</Link> },
    { key: 'party', header: 'Customer', cell: (row) => opportunityParty(row) },
    { key: 'stage', header: 'Stage', cell: (row) => <StatusBadge status={nameOf(row.stage)} /> },
    { key: 'estimatedValue', header: 'Value', cell: (row) => <DealValue item={row} reportingCurrency={reporting} /> },
    { key: 'probability', header: 'Prob.', cell: (row) => `${row.probability}%` },
    { key: 'expectedCloseDate', header: 'Expected close', cell: (row) => (row.expectedCloseDate ? formatDate(row.expectedCloseDate) : '—') },
    { key: 'ownerName', header: 'Owner', cell: (row) => row.ownerName ?? '—' },
    { key: 'workOrderCount', header: 'Work orders', cell: (row) => row.workOrderCount || '—' },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Sales Pipeline"
        subtitle="Track leads and deals, then turn the ones you win into work orders."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Opportunity
            </button>
          ) : undefined
        }
      />

      <CrmNav />

      {usable.length > 1 ? (
        <div className="flex flex-wrap gap-2" role="tablist" aria-label="Pipelines">
          {usable.map((item) => (
            <button
              key={item.id}
              type="button"
              role="tab"
              aria-selected={item.id === pipeline?.id}
              onClick={() => choosePipeline(item.id)}
              className={cn('inline-flex h-9 items-center rounded-full border px-4 text-sm font-medium transition', item.id === pipeline?.id ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50')}
            >
              {item.name}
            </button>
          ))}
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
        <StatCard label="Open pipeline" value={summary ? formatCurrency(summary.openValue, reporting) : '—'} />
        <StatCard label="Weighted forecast" value={summary ? formatCurrency(summary.weightedValue, reporting) : '—'} />
        <StatCard label="Won this month" value={summary ? `${summary.wonThisMonthCount} · ${formatCurrency(summary.wonThisMonthValue, reporting)}` : '—'} tone="success" />
        <StatCard label="Win rate (90 days)" value={summary?.winRate != null ? `${summary.winRate}%` : '—'} />
        <Link to="/crm/follow-ups" className="block rounded-[var(--radius-card)] transition hover:opacity-90">
          <StatCard
            label="Overdue follow-ups"
            value={summary ? summary.overdueFollowUps : '—'}
            tone={summary && summary.overdueFollowUps > 0 ? 'warning' : 'default'}
            className="h-full"
          />
        </Link>
      </div>

      {summary && summary.unconvertedCurrencies.length > 0 ? (
        <div className="flex flex-col gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-start gap-2">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              Totals leave out{' '}
              {summary.unconvertedCurrencies.map((group, index) => (
                <span key={group.currency}>
                  {index > 0 ? ', ' : ''}
                  {group.count} {group.currency} deal{group.count === 1 ? '' : 's'} ({formatCurrency(group.value, group.currency)})
                </span>
              ))}{' '}
              — there is no exchange rate to {reporting} yet.
            </span>
          </div>
          <button type="button" onClick={() => setShowRates(true)} className="inline-flex h-9 shrink-0 items-center rounded-lg border border-amber-300 bg-white px-3 text-sm font-medium text-amber-900 transition hover:bg-amber-100">
            Enter rates
          </button>
        </div>
      ) : null}

      {summary && (summary.stalledDeals ?? 0) > 0 ? (
        <button type="button" onClick={() => { setView('list'); setStageFilter('Stalled') }} className="flex w-full items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-left text-sm text-amber-900 transition hover:bg-amber-100">
          <AlertTriangle className="h-4 w-4 shrink-0" />
          <span>
            {summary.stalledDeals} open deal{summary.stalledDeals === 1 ? ' has' : 's have'} gone quiet: nothing logged for {summary.stalledAfterDays}+ days.
          </span>
        </button>
      ) : null}

      {summary && summary.openTenders > 0 ? (
        <Link to="/crm/tenders" className="flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700 transition hover:bg-slate-50">
          <FileText className="h-4 w-4 text-[var(--app-primary)]" />
          <span>
            {summary.openTenders} open RFP/RFQ{summary.openTenders === 1 ? '' : 's'}
            {summary.tendersDueThisWeek > 0 ? <span className="font-medium text-amber-700"> · {summary.tendersDueThisWeek} due in the next 7 days</span> : null}
          </span>
        </Link>
      ) : null}

      <div className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)] sm:flex-row sm:items-center">
        <label className="flex flex-1 items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search opportunities, companies or contacts" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
        {view === 'list' ? (
          <select value={stageFilter} onChange={(event) => setStageFilter(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" aria-label="Filter by stage">
            <option value="">All stages</option>
            <option value="Open">Open deals</option>
            <option value="Stalled">Stalled deals</option>
            {boardStages.map((stage) => (
              <option key={stage.key} value={stage.key}>{stage.name}</option>
            ))}
          </select>
        ) : null}
        {view === 'list' && filterableFields.length > 0 ? (
          <div className="flex items-center gap-2">
            <select value={fieldFilter} onChange={(event) => { setFieldFilter(event.target.value); setFieldValue('') }} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" aria-label="Filter by field">
              <option value="">Any field</option>
              {filterableFields.map((field) => <option key={field.key} value={field.key}>{field.label}</option>)}
            </select>
            {filterField?.fieldType === 'Dropdown' || filterField?.fieldType === 'Checkbox' ? (
              <select value={fieldValue} onChange={(event) => setFieldValue(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" aria-label={`${filterField.label} is`}>
                <option value="">{filterField.fieldType === 'Checkbox' ? 'Ticked or not' : 'Any value'}</option>
                {(filterField.fieldType === 'Checkbox' ? ['Yes', 'No'] : filterField.options).map((option) => <option key={option} value={option}>{option}</option>)}
              </select>
            ) : filterField ? (
              <input
                value={fieldValue}
                onChange={(event) => setFieldValue(event.target.value)}
                type={filterField.fieldType === 'Date' ? 'date' : filterField.fieldType === 'Number' ? 'number' : 'text'}
                placeholder={filterField.fieldType === 'Text' ? 'Contains…' : 'Any value'}
                aria-label={`${filterField.label} value`}
                className="h-10 w-36 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
              />
            ) : null}
          </div>
        ) : null}
        <OwnerSelect
          aria-label="Filter by owner"
          value={ownerFilter}
          onChange={setOwnerFilter}
          emptyLabel="All owners"
          className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
        {user && ownerFilter !== user.userId ? (
          <button type="button" onClick={() => setOwnerFilter(user.userId)} className="inline-flex h-10 items-center self-start rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-600 transition hover:bg-slate-50 sm:self-auto">
            Mine
          </button>
        ) : null}
        <button type="button" onClick={() => setShowRates(true)} className="inline-flex h-10 items-center gap-1.5 self-start rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-600 transition hover:bg-slate-50 sm:self-auto">
          <Coins className="h-4 w-4" />Rates
        </button>
        {canManage ? (
          <button type="button" onClick={() => setShowImport(true)} className="inline-flex h-10 items-center gap-1.5 self-start rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-600 transition hover:bg-slate-50 sm:self-auto">
            <Upload className="h-4 w-4" />Import
          </button>
        ) : null}
        <details className="relative self-start sm:self-auto">
          <summary className="inline-flex h-10 cursor-pointer list-none items-center gap-1.5 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-600 transition hover:bg-slate-50">
            <Download className="h-4 w-4" />Export
          </summary>
          <div className="absolute right-0 z-20 mt-1 w-48 rounded-lg border border-slate-200 bg-white p-1 shadow-lg">
            <button type="button" onClick={() => void importService.exportSales('deals', { stage: view === 'list' ? stageFilter || undefined : undefined, ownerUserId: ownerFilter || undefined, pipelineId })} className="block w-full rounded-md px-3 py-2 text-left text-sm text-slate-700 hover:bg-slate-50">Deals (as filtered)</button>
            <button type="button" onClick={() => void importService.exportSales('contacts')} className="block w-full rounded-md px-3 py-2 text-left text-sm text-slate-700 hover:bg-slate-50">Contacts</button>
          </div>
        </details>
        <SavedViewsMenu currentQuery={currentQuery} onApply={applyQuery} />
        <div className="inline-flex self-start rounded-lg border border-slate-200 p-1 sm:self-auto">
          {([['board', LayoutGrid, 'Board'], ['list', List, 'List']] as const).map(([key, Icon, label]) => (
            <button
              key={key}
              type="button"
              onClick={() => setView(key)}
              className={cn('inline-flex h-9 items-center gap-1.5 rounded-md px-3 text-sm font-medium transition', view === key ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}
            >
              <Icon className="h-4 w-4" />{label}
            </button>
          ))}
        </div>
      </div>

      {view === 'list' ? (
        <DataTable
          columns={columns}
          data={rows}
          loading={isLoading}
          emptyTitle={isError ? 'Pipeline unavailable' : 'No opportunities yet'}
          emptyDescription={isError ? 'The pipeline could not be loaded.' : 'Add your first lead to start tracking sales.'}
        />
      ) : (
        <DndContext sensors={sensors} onDragEnd={onDragEnd}>
        <div className="-mx-1 overflow-x-auto pb-2">
          <div className="flex min-w-max gap-3 px-1">
            {boardStages.map(({ key: stage, name }) => {
              const items = byStage.get(stage) ?? []
              const total = items.reduce((sum, item) => sum + (item.convertedValue ?? 0), 0)
              const hasUnconverted = items.some((item) => item.convertedValue === null)
              return (
                <BoardColumn key={stage} stage={stage} enabled={canManage}>
                  <header className="flex items-center justify-between border-b border-slate-200 px-3 py-2.5">
                    <div className="text-sm font-semibold text-slate-900">
                      {name} <span className="font-normal text-slate-500">({items.length})</span>
                    </div>
                    <div className="text-xs text-slate-500" title={hasUnconverted ? 'Some deals here have no exchange rate and are not counted' : undefined}>
                      {formatCurrency(total, reporting)}{hasUnconverted ? ' *' : ''}
                    </div>
                  </header>
                  <div className="flex flex-col gap-2 p-2">
                    {isLoading ? <div className="px-2 py-6 text-center text-sm text-slate-400">Loading…</div> : null}
                    {!isLoading && items.length === 0 ? <div className="px-2 py-6 text-center text-sm text-slate-400">No deals</div> : null}
                    {items.map((item) => (
                      <DraggableCard key={item.id} id={item.id} enabled={canManage}>
                        <OpportunityCard
                          item={item}
                          canManage={canManage}
                          reportingCurrency={reporting}
                          openStages={openStages}
                          moving={changeStage.isPending && changeStage.variables?.id === item.id}
                          onMove={(next) => moveDeal(item, next)}
                        />
                      </DraggableCard>
                    ))}
                  </div>
                </BoardColumn>
              )
            })}
          </div>
        </div>
        </DndContext>
      )}

      <FormDrawer
        open={Boolean(losing)}
        title="Mark Deal as Lost"
        onClose={() => setLosing(null)}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => setLosing(null)} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700">Cancel</button>
            <button
              type="button"
              disabled={!lostReason.trim() || changeStage.isPending}
              onClick={() => {
                if (!losing) return
                changeStage.mutate({ id: losing.id, stage: 'Lost', lostReason: lostReason.trim() }, {
                  onSuccess: () => {
                    toast.success(`${losing.title} marked lost`)
                    setLosing(null)
                    setLostReason('')
                  },
                })
              }}
              className="inline-flex h-11 items-center rounded-lg bg-rose-600 px-4 text-sm font-medium text-white transition hover:bg-rose-700 disabled:opacity-60"
            >
              Mark Lost
            </button>
          </div>
        }
      >
        <p className="mb-3 text-sm text-slate-600">{losing?.title}</p>
        <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="board-lost-reason">Why was it lost?</label>
        <textarea id="board-lost-reason" value={lostReason} onChange={(event) => setLostReason(event.target.value)} rows={4} maxLength={500} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" placeholder="e.g. Price, timing, went with a competitor" />
      </FormDrawer>

      <OpportunityFormDrawer open={showForm} onClose={() => setShowForm(false)} defaultPipelineId={pipeline?.id} />
      <ExchangeRatesDrawer open={showRates} onClose={() => setShowRates(false)} canManage={canManage} />
      <DealImportDrawer open={showImport} onClose={() => setShowImport(false)} />
    </div>
  )
}

/** A board column that takes dropped cards. */
function BoardColumn({ stage, enabled, children }: { stage: string; enabled: boolean; children: ReactNode }) {
  const { setNodeRef, isOver } = useDroppable({ id: stage, disabled: !enabled })
  return (
    <section ref={setNodeRef} className={cn('flex w-72 shrink-0 flex-col rounded-[var(--radius-card)] border bg-slate-50/70 transition', isOver ? 'border-[var(--app-primary)] bg-sky-50/60' : 'border-slate-200')}>
      {children}
    </section>
  )
}

/** A card that can be dragged to another stage (by its handle on touch screens, anywhere with a mouse). */
function DraggableCard({ id, enabled, children }: { id: string; enabled: boolean; children: ReactNode }) {
  const { listeners, setNodeRef, transform, isDragging } = useDraggable({ id, disabled: !enabled })
  return (
    <div
      ref={setNodeRef}
      {...listeners}
      style={transform ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)` } : undefined}
      className={cn('relative', enabled && 'cursor-grab', isDragging && 'z-30 cursor-grabbing opacity-90 shadow-lg')}
    >
      {enabled ? <GripVertical className="absolute right-1.5 top-2 h-3.5 w-3.5 text-slate-300" aria-hidden="true" /> : null}
      {children}
    </div>
  )
}

/** The deal's own amount, plus its reporting-currency equivalent when it is in another currency. */
function DealValue({ item, reportingCurrency }: { item: SalesOpportunityListItem; reportingCurrency: string | undefined }) {
  const foreign = Boolean(reportingCurrency) && item.currency !== reportingCurrency
  return (
    <span>
      {formatCurrency(item.estimatedValue, item.currency)}
      {foreign ? (
        <span className={cn('block text-xs font-normal', item.convertedValue === null ? 'text-amber-700' : 'text-slate-500')}>
          {item.convertedValue === null ? 'No rate yet' : `≈ ${formatCurrency(item.convertedValue, reportingCurrency)}`}
        </span>
      ) : null}
    </span>
  )
}

function OpportunityCard({ item, canManage, reportingCurrency, openStages, moving, onMove }: { item: SalesOpportunityListItem; canManage: boolean; reportingCurrency: string | undefined; openStages: Array<{ key: string; name: string }>; moving: boolean; onMove: (stage: SalesStage) => void }) {
  const followUpOverdue = item.nextFollowUpAt ? isPast(item.nextFollowUpAt) : false
  return (
    <article className={cn('rounded-xl border border-slate-200 bg-white p-3 shadow-sm', moving && 'opacity-60')}>
      <Link to={`/crm/opportunities/${item.id}`} className="block text-sm font-medium text-slate-900 hover:underline">{item.title}</Link>
      <div className="mt-0.5 truncate text-xs text-slate-500">{opportunityParty(item)}</div>
      {item.isStalled ? (
        <div className="mt-1 inline-flex items-center gap-1 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800" title="Nothing logged on this deal for a while">
          Quiet {item.daysIdle} days
        </div>
      ) : null}
      <div className="mt-2 flex items-start justify-between text-sm">
        <span className="font-semibold text-slate-900"><DealValue item={item} reportingCurrency={reportingCurrency} /></span>
        <span className="text-right text-xs text-slate-500">
          {item.probability}%
          {item.suggestedProbability != null && Math.abs(item.suggestedProbability - item.probability) >= 10 ? (
            <span className="block text-[11px] text-slate-400" title="What deals like this have done in the past">history {item.suggestedProbability}%</span>
          ) : null}
        </span>
      </div>
      {item.ownerName ? <div className="mt-1 text-xs text-slate-500">Owner: {item.ownerName}</div> : null}
      <div className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-xs text-slate-500">
        {item.nextFollowUpAt ? (
          <span className={cn('inline-flex items-center gap-1', followUpOverdue && 'text-amber-700')}>
            <CalendarClock className="h-3 w-3" />{formatDate(item.nextFollowUpAt)}
          </span>
        ) : null}
        {item.workOrderCount > 0 ? (
          <span className="inline-flex items-center gap-1 text-emerald-700">
            <ClipboardList className="h-3 w-3" />{item.workOrderCount} work order{item.workOrderCount === 1 ? '' : 's'}
          </span>
        ) : null}
      </div>
      {/* Won and Lost need the deal page (lost reason, hand-off), so the quick move only covers open stages. */}
      {canManage && !isClosedStage(item.stage) ? (
        <select
          aria-label={`Move ${item.title} to stage`}
          value={item.stage}
          disabled={moving}
          onChange={(event) => onMove(event.target.value)}
          className="mt-2 h-8 w-full rounded-md border border-slate-200 bg-white px-2 text-xs text-slate-600 outline-none focus:border-[var(--app-primary)]"
        >
          {openStages.map((stage) => (
            <option key={stage.key} value={stage.key}>{stage.name}</option>
          ))}
        </select>
      ) : null}
    </article>
  )
}
