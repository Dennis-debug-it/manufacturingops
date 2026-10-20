import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { CalendarClock, ClipboardList, LayoutGrid, List, Plus, Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatCard } from '../../shared/components/StatCard'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate, isPast } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { useChangeStage, useOpportunities, usePipelineSummary } from './api'
import { OpportunityFormDrawer } from './components/OpportunityFormDrawer'
import { OPEN_STAGES, SALES_STAGES, opportunityParty, stageLabels, type SalesOpportunityListItem, type SalesStage } from './types'

type View = 'board' | 'list'

export function PipelinePage() {
  const canManage = usePermission('crm.manage')
  const [view, setView] = useState<View>('board')
  const [search, setSearch] = useState('')
  const [stageFilter, setStageFilter] = useState('')
  const [showForm, setShowForm] = useState(false)
  const debouncedSearch = useDebounce(search, 300)
  const { data: summary } = usePipelineSummary()
  const { data, isLoading, isError } = useOpportunities({ search: debouncedSearch, stage: view === 'list' ? stageFilter || undefined : undefined })
  const changeStage = useChangeStage()
  const rows = useMemo(() => data ?? [], [data])

  const byStage = useMemo(() => {
    const groups = new Map<SalesStage, SalesOpportunityListItem[]>(SALES_STAGES.map((stage) => [stage, []]))
    rows.forEach((row) => groups.get(row.stage)?.push(row))
    return groups
  }, [rows])

  const columns: DataTableColumn<SalesOpportunityListItem>[] = [
    { key: 'title', header: 'Opportunity', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/crm/opportunities/${row.id}`}>{row.title}</Link> },
    { key: 'party', header: 'Customer', cell: (row) => opportunityParty(row) },
    { key: 'stage', header: 'Stage', cell: (row) => <StatusBadge status={stageLabels[row.stage]} /> },
    { key: 'estimatedValue', header: 'Value', cell: (row) => formatCurrency(row.estimatedValue, row.currency) },
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

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Open pipeline" value={summary ? formatCurrency(summary.openValue) : '—'} />
        <StatCard label="Weighted forecast" value={summary ? formatCurrency(summary.weightedValue) : '—'} />
        <StatCard label="Won this month" value={summary ? `${summary.wonThisMonthCount} · ${formatCurrency(summary.wonThisMonthValue)}` : '—'} tone="success" />
        <StatCard
          label="Overdue follow-ups"
          value={summary ? summary.overdueFollowUps : '—'}
          tone={summary && summary.overdueFollowUps > 0 ? 'warning' : 'default'}
        />
      </div>

      <div className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)] sm:flex-row sm:items-center">
        <label className="flex flex-1 items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search opportunities, companies or contacts" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
        {view === 'list' ? (
          <select value={stageFilter} onChange={(event) => setStageFilter(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" aria-label="Filter by stage">
            <option value="">All stages</option>
            <option value="Open">Open deals</option>
            {SALES_STAGES.map((stage) => (
              <option key={stage} value={stage}>{stageLabels[stage]}</option>
            ))}
          </select>
        ) : null}
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
        <div className="-mx-1 overflow-x-auto pb-2">
          <div className="flex min-w-max gap-3 px-1">
            {SALES_STAGES.map((stage) => {
              const items = byStage.get(stage) ?? []
              const total = items.reduce((sum, item) => sum + item.estimatedValue, 0)
              return (
                <section key={stage} className="flex w-72 shrink-0 flex-col rounded-[var(--radius-card)] border border-slate-200 bg-slate-50/70">
                  <header className="flex items-center justify-between border-b border-slate-200 px-3 py-2.5">
                    <div className="text-sm font-semibold text-slate-900">
                      {stageLabels[stage]} <span className="font-normal text-slate-500">({items.length})</span>
                    </div>
                    <div className="text-xs text-slate-500">{formatCurrency(total)}</div>
                  </header>
                  <div className="flex flex-col gap-2 p-2">
                    {isLoading ? <div className="px-2 py-6 text-center text-sm text-slate-400">Loading…</div> : null}
                    {!isLoading && items.length === 0 ? <div className="px-2 py-6 text-center text-sm text-slate-400">No deals</div> : null}
                    {items.map((item) => (
                      <OpportunityCard
                        key={item.id}
                        item={item}
                        canManage={canManage}
                        moving={changeStage.isPending && changeStage.variables?.id === item.id}
                        onMove={(next) => changeStage.mutate({ id: item.id, stage: next })}
                      />
                    ))}
                  </div>
                </section>
              )
            })}
          </div>
        </div>
      )}

      <OpportunityFormDrawer open={showForm} onClose={() => setShowForm(false)} />
    </div>
  )
}

function OpportunityCard({ item, canManage, moving, onMove }: { item: SalesOpportunityListItem; canManage: boolean; moving: boolean; onMove: (stage: SalesStage) => void }) {
  const followUpOverdue = item.nextFollowUpAt ? isPast(item.nextFollowUpAt) : false
  return (
    <article className={cn('rounded-xl border border-slate-200 bg-white p-3 shadow-sm', moving && 'opacity-60')}>
      <Link to={`/crm/opportunities/${item.id}`} className="block text-sm font-medium text-slate-900 hover:underline">{item.title}</Link>
      <div className="mt-0.5 truncate text-xs text-slate-500">{opportunityParty(item)}</div>
      <div className="mt-2 flex items-center justify-between text-sm">
        <span className="font-semibold text-slate-900">{formatCurrency(item.estimatedValue, item.currency)}</span>
        <span className="text-xs text-slate-500">{item.probability}%</span>
      </div>
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
      {canManage && OPEN_STAGES.includes(item.stage) ? (
        <select
          aria-label={`Move ${item.title} to stage`}
          value={item.stage}
          disabled={moving}
          onChange={(event) => onMove(event.target.value as SalesStage)}
          className="mt-2 h-8 w-full rounded-md border border-slate-200 bg-white px-2 text-xs text-slate-600 outline-none focus:border-[var(--app-primary)]"
        >
          {OPEN_STAGES.map((stage) => (
            <option key={stage} value={stage}>{stageLabels[stage]}</option>
          ))}
        </select>
      ) : null}
    </article>
  )
}
