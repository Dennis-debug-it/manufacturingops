import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { AlertTriangle, CheckCircle2, ChevronRight, CircleDashed, Route as RouteIcon } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { Badge } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { formatTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { LocationSelect } from '../components/ui'
import { useFactoryLinks } from '../links'
import { useCheckSummary, useDueChecks, usePrefetchDueRuns, type CheckRunSummary, type CheckStatus } from './api'

const VIEWS: { id: 'due' | 'missed' | 'done' | 'history'; label: string }[] = [
  { id: 'due', label: 'To do' },
  { id: 'missed', label: 'Missed' },
  { id: 'done', label: 'Done' },
  { id: 'history', label: 'Last 7 days' },
]
type View = (typeof VIEWS)[number]['id']

const STATUS_FOR: Record<Exclude<View, 'history'>, CheckStatus> = { due: 'Due', missed: 'Missed', done: 'Completed' }

/** This shift's running checks, grouped by schedule (a route is walked in order). */
export function RunningChecksPage() {
  const links = useFactoryLinks()
  const [params, setParams] = useSearchParams()
  const view = (VIEWS.find((x) => x.id === params.get('view'))?.id ?? 'due')
  const [orgNodeId, setOrgNodeId] = useState('')
  const due = useDueChecks(orgNodeId || undefined)
  usePrefetchDueRuns(due.data)

  const counts = useMemo(() => {
    const rows = due.data ?? []
    return { due: rows.filter((x) => x.status === 'Due').length, missed: rows.filter((x) => x.status === 'Missed').length, done: rows.filter((x) => x.status === 'Completed').length }
  }, [due.data])

  const groups = useMemo(() => {
    if (view === 'history') return []
    const rows = (due.data ?? []).filter((x) => x.status === STATUS_FOR[view])
    const map = new Map<string, CheckRunSummary[]>()
    for (const row of rows) map.set(`${row.scheduleId}|${row.periodKey}`, [...(map.get(`${row.scheduleId}|${row.periodKey}`) ?? []), row])
    return [...map.values()].map((items) => items.sort((a, b) => a.sequence - b.sequence))
  }, [due.data, view])

  return (
    <div className="space-y-4">
      <PageHeader title="Running checks" subtitle="Shift and daily checks on the machines. Failed items raise jobs on their own." />

      <div className="flex flex-wrap items-center gap-2">
        <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
          {VIEWS.map((item) => (
            <button key={item.id} type="button" role="tab" aria-selected={view === item.id}
              onClick={() => setParams(item.id === 'due' ? {} : { view: item.id }, { replace: true })}
              className={cn('h-9 rounded-md px-3 text-sm font-medium', view === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>
              {item.label}
              {item.id !== 'history' && counts[item.id] > 0 ? <span className="ml-1.5 tabular-nums opacity-80">{counts[item.id]}</span> : null}
            </button>
          ))}
        </div>
        <div className="w-full sm:w-64"><LocationSelect value={orgNodeId} onChange={setOrgNodeId} emptyLabel="All locations" includeGroups /></div>
      </div>

      {view === 'history' ? <History orgNodeId={orgNodeId || undefined} /> : due.isLoading ? <Spinner label="Loading checks" /> : groups.length === 0 ? (
        <EmptyState title={view === 'due' ? 'All checks done' : view === 'missed' ? 'Nothing missed' : 'None done yet'}
          description={view === 'due' ? 'Nothing is waiting for this shift.' : 'Checks from the last 24 hours show here.'} />
      ) : (
        <div className="grid gap-4 lg:grid-cols-2">
          {groups.map((items) => {
            const first = items[0]!
            return (
              <section key={`${first.scheduleId}|${first.periodKey}`} className="rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
                <header className="flex items-center justify-between gap-2 border-b border-slate-100 px-4 py-3">
                  <div>
                    <h2 className="flex items-center gap-1.5 text-sm font-semibold text-slate-900">{first.isRoute ? <RouteIcon className="h-4 w-4 text-slate-400" /> : null}{first.scheduleName}</h2>
                    <p className="text-xs text-slate-500">{first.shiftName ? `${first.shiftName} shift · ` : ''}{formatTime(first.dueFrom)}–{formatTime(first.dueBy)}</p>
                  </div>
                  <span className="text-xs text-slate-500">{items.length} {items.length === 1 ? 'machine' : 'machines'}</span>
                </header>
                <ul className="divide-y divide-slate-100">
                  {items.map((run) => (
                    <li key={run.id}>
                      <Link to={links.check(run.id)} className="flex min-h-14 items-center gap-3 px-4 py-2 hover:bg-slate-50">
                        <StatusIcon run={run} />
                        <span className="min-w-0 flex-1">
                          <span className="block truncate text-sm font-medium text-slate-900">{run.assetName}</span>
                          <span className="block text-xs text-slate-500">
                            <span className="font-mono">{run.assetCode}</span>
                            {run.completedAt ? ` · ${formatTime(run.completedAt)}${run.completedByName ? ` by ${run.completedByName}` : ''}` : ''}
                          </span>
                        </span>
                        {run.failedCount > 0 ? <Badge tone="danger" size="sm">{run.failedCount} failed</Badge> : null}
                        <ChevronRight className="h-4 w-4 shrink-0 text-slate-300" />
                      </Link>
                    </li>
                  ))}
                </ul>
              </section>
            )
          })}
        </div>
      )}
    </div>
  )
}

function StatusIcon({ run }: { run: CheckRunSummary }) {
  if (run.status === 'Completed') return run.failedCount > 0
    ? <AlertTriangle className="h-5 w-5 shrink-0 text-amber-600" aria-label="Done, with failures" />
    : <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-600" aria-label="Done" />
  if (run.status === 'Missed') return <AlertTriangle className="h-5 w-5 shrink-0 text-rose-600" aria-label="Missed" />
  return <CircleDashed className="h-5 w-5 shrink-0 text-slate-400" aria-label="To do" />
}

/** Completion per schedule and shift, for supervisors. */
function History({ orgNodeId }: { orgNodeId: string | undefined }) {
  const summary = useCheckSummary(7, orgNodeId)
  if (summary.isLoading) return <Spinner label="Loading" />
  const rows = summary.data ?? []
  if (rows.length === 0) return <EmptyState title="No checks yet" description="Set up check schedules in Settings › Running checks." />
  return (
    <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
      <table className="w-full text-sm">
        <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500">
          <tr><th className="px-4 py-2">Schedule</th><th className="px-4 py-2">Shift</th><th className="px-4 py-2 text-right">Done</th><th className="px-4 py-2 text-right">Missed</th><th className="px-4 py-2 text-right">Failed items</th></tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {rows.map((row) => {
            const pct = row.total ? Math.round((row.completed / row.total) * 100) : 0
            return (
              <tr key={`${row.scheduleId}|${row.periodKey}`}>
                <td className="px-4 py-2 font-medium text-slate-900">{row.scheduleName}</td>
                <td className="whitespace-nowrap px-4 py-2 text-slate-600">{row.periodKey.slice(0, 10)}{row.shiftName ? ` · ${row.shiftName}` : ''}</td>
                <td className="px-4 py-2 text-right tabular-nums">{row.completed}/{row.total} <span className={cn('text-xs', pct < 100 ? 'text-amber-700' : 'text-slate-500')}>({pct}%)</span></td>
                <td className={cn('px-4 py-2 text-right tabular-nums', row.missed > 0 && 'font-medium text-rose-700')}>{row.missed}</td>
                <td className="px-4 py-2 text-right tabular-nums">{row.failedItems}</td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
