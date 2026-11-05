import { useState } from 'react'
import { Link } from 'react-router-dom'
import { AlertOctagon } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { StatCard } from '../../shared/components/StatCard'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { LocationSelect, formatNumber } from './components/ui'
import { useFactoryLinks } from './links'
import { useFactoryDashboard } from './jobs/api'
import { useComplianceSummary, usePmCompliance, useWindows } from './planning/api'

/** The maintenance manager's view of the factory: what is down now, what is late, and what keeps failing. */
export function FactoryDashboardPage() {
  const links = useFactoryLinks()
  const [orgNodeId, setOrgNodeId] = useState('')
  const { data, isLoading, isError } = useFactoryDashboard(orgNodeId || undefined)
  const pm = usePmCompliance(orgNodeId || undefined, 90)
  const statutory = useComplianceSummary(orgNodeId || undefined)
  const windows = useWindows()
  const nextWindow = (windows.data ?? []).find((x) => x.status === 'Planned' || x.status === 'Packed' || x.status === 'InProgress')

  return (
    <div className="space-y-5">
      <PageHeader title="Factory overview" subtitle="Breakdowns, open work, running checks and repeat failures."
        secondaryActions={<div className="w-56"><LocationSelect value={orgNodeId} onChange={setOrgNodeId} emptyLabel="Whole factory" includeGroups /></div>} />

      {isLoading ? <Spinner label="Loading overview" /> : isError || !data ? <EmptyState title="Overview unavailable" description="It could not be loaded." /> : (
        <>
          <section aria-label="Down now" className={cn('rounded-[var(--radius-card)] border p-4 shadow-sm', data.openBreakdowns.length ? 'border-rose-200 bg-rose-50' : 'border-emerald-200 bg-emerald-50')}>
            <h2 className={cn('flex items-center gap-2 text-sm font-semibold', data.openBreakdowns.length ? 'text-rose-900' : 'text-emerald-900')}>
              <AlertOctagon className="h-4 w-4" />
              {data.openBreakdowns.length ? `${data.assetsDown} of ${data.assets} assets down` : `All ${data.assets} assets running`}
            </h2>
            {data.openBreakdowns.length ? (
              <ul className="mt-3 divide-y divide-rose-100">
                {data.openBreakdowns.map((row) => (
                  <li key={row.workOrderId} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm">
                    <Link to={links.job(row.workOrderId)} className="min-w-0 font-medium text-rose-950 underline decoration-rose-300 underline-offset-4">
                      <span className="font-mono text-xs">{row.assetCode}</span> {row.assetName} — {row.title}
                    </Link>
                    <span className="whitespace-nowrap text-rose-800"><span className="font-semibold tabular-nums">{formatNumber(row.hoursDown)} h</span> since {formatDateTime(row.downSince)}</span>
                  </li>
                ))}
              </ul>
            ) : null}
          </section>

          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Link to={links.jobs()}><StatCard label="Open jobs" value={data.jobs.open} /></Link>
            <StatCard label="Overdue" value={data.jobs.overdue} tone={data.jobs.overdue ? 'danger' : 'default'} />
            <StatCard label="To approve" value={data.jobs.awaitingApproval} tone={data.jobs.awaitingApproval ? 'warning' : 'default'} />
            <StatCard label="To verify" value={data.jobs.awaitingVerification} />
            <StatCard label="On hold" value={data.jobs.onHold} />
            <StatCard label="Need the line stopped" value={data.jobs.needLineStop} />
            <StatCard label="Downtime, last 7 days" value={`${formatNumber(data.downtimeHoursLast7Days)} h`} />
            <StatCard label="Production lost, 7 days" value={`${formatNumber(data.productionLostLast7Days)} ${data.productionUnit}`} />
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <Link to={links.pm() + '?tab=compliance'} className="block"><StatCard label="PM on time, 90 days" value={pm.data?.percent == null ? '—' : `${pm.data.percent}%`} tone={pm.data?.percent != null && pm.data.percent < 80 ? 'warning' : 'default'} /></Link>
            <Link to={links.statutory() + '?status=Overdue'} className="block"><StatCard label="Statutory overdue" value={statutory.data ? `${statutory.data.overdue}${statutory.data.dueSoon ? ` · ${statutory.data.dueSoon} due soon` : ''}` : '—'} tone={statutory.data?.overdue ? 'danger' : 'default'} /></Link>
            {nextWindow ? (
              <Link to={links.window(nextWindow.id)} className="block"><StatCard label={`Next window · ${nextWindow.factoryName}`} value={`${formatDate(nextWindow.windowDate, 'EEE dd MMM')} · ${nextWindow.jobs} jobs`} tone={nextWindow.capacityHours > 0 && nextWindow.plannedHours > nextWindow.capacityHours ? 'warning' : 'default'} /></Link>
            ) : <StatCard label="Next window" value="—" />}
          </div>

          <div className="grid gap-4 lg:grid-cols-3">
            <Panel title="Running checks, last 24 hours">
              <ChecksBar {...data.runningChecksLast24Hours} />
              <Link to={`${links.checks()}?view=missed`} className="mt-3 inline-block text-sm font-medium text-[var(--app-primary)] underline underline-offset-4">See missed checks</Link>
            </Panel>
            <Panel title="Reactive vs planned, 30 days">
              <PlannedBar reactive={data.reactiveJobsLast30Days} planned={data.plannedJobsLast30Days} />
            </Panel>
            <Panel title="Top failure modes, 90 days">
              {data.topFailureModes.length === 0 ? <p className="text-sm text-slate-500">No failure codes recorded yet.</p> : (
                <ol className="space-y-1.5 text-sm">
                  {data.topFailureModes.map((row) => (
                    <li key={row.code} className="flex justify-between gap-2"><span className="text-slate-700">{row.name} <span className="font-mono text-xs text-slate-400">{row.code}</span></span><span className="tabular-nums text-slate-900">{row.jobs}</span></li>
                  ))}
                </ol>
              )}
            </Panel>
          </div>

          <Panel title="Assets with the most jobs, 90 days">
            {data.topFailingAssets.length === 0 ? <p className="text-sm text-slate-500">No corrective work yet.</p> : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead className="text-left text-xs font-medium text-slate-500"><tr><th className="py-1.5 pr-4">Asset</th><th className="py-1.5 pr-4 text-right">Jobs</th><th className="py-1.5 pr-4 text-right">Breakdowns</th><th className="py-1.5 text-right">Rework</th></tr></thead>
                  <tbody className="divide-y divide-slate-100">
                    {data.topFailingAssets.map((row) => (
                      <tr key={row.assetId}>
                        <td className="py-1.5 pr-4"><Link to={links.asset(row.assetId)} className="text-slate-900 underline decoration-slate-300 underline-offset-4"><span className="font-mono text-xs">{row.assetCode}</span> {row.assetName}</Link></td>
                        <td className="py-1.5 pr-4 text-right tabular-nums">{row.jobs}</td>
                        <td className="py-1.5 pr-4 text-right tabular-nums">{row.breakdowns}</td>
                        <td className={cn('py-1.5 text-right tabular-nums', row.rework > 0 && 'font-medium text-amber-700')}>{row.rework}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Panel>
        </>
      )}
    </div>
  )
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
      <h2 className="mb-3 text-sm font-semibold text-slate-800">{title}</h2>
      {children}
    </section>
  )
}

/** Done / missed / still open, as one bar with the numbers written out beside it. */
function ChecksBar({ due, completed, missed, outstanding, failedItems }: { due: number; completed: number; missed: number; outstanding: number; failedItems: number }) {
  if (due === 0) return <p className="text-sm text-slate-500">No checks were due.</p>
  const parts = [
    { label: 'Done', value: completed, className: 'bg-emerald-600' },
    { label: 'Missed', value: missed, className: 'bg-rose-600' },
    { label: 'Still open', value: outstanding, className: 'bg-slate-300' },
  ]
  return (
    <div>
      <p className="text-2xl font-semibold tabular-nums text-slate-900">{Math.round((completed / due) * 100)}%<span className="ml-2 text-sm font-normal text-slate-500">of {due} done</span></p>
      <div className="mt-2 flex h-2.5 gap-0.5 overflow-hidden rounded-full" role="img" aria-label={parts.map((x) => `${x.label} ${x.value}`).join(', ')}>
        {parts.filter((x) => x.value > 0).map((x) => <span key={x.label} className={x.className} style={{ width: `${(x.value / due) * 100}%` }} />)}
      </div>
      <ul className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-600">
        {parts.map((x) => <li key={x.label} className="flex items-center gap-1.5"><span className={cn('h-2 w-2 rounded-full', x.className)} />{x.label} {x.value}</li>)}
        <li>{failedItems} failed items</li>
      </ul>
    </div>
  )
}

function PlannedBar({ reactive, planned }: { reactive: number; planned: number }) {
  const total = reactive + planned
  if (total === 0) return <p className="text-sm text-slate-500">No jobs in the last 30 days.</p>
  const plannedPct = Math.round((planned / total) * 100)
  return (
    <div>
      <p className="text-2xl font-semibold tabular-nums text-slate-900">{plannedPct}%<span className="ml-2 text-sm font-normal text-slate-500">planned</span></p>
      <div className="mt-2 flex h-2.5 gap-0.5 overflow-hidden rounded-full" role="img" aria-label={`Planned ${planned}, reactive ${reactive}`}>
        {planned > 0 ? <span className="bg-[var(--app-primary)]" style={{ width: `${plannedPct}%` }} /> : null}
        {reactive > 0 ? <span className="bg-amber-500" style={{ width: `${100 - plannedPct}%` }} /> : null}
      </div>
      <ul className="mt-2 flex gap-4 text-xs text-slate-600">
        <li className="flex items-center gap-1.5"><span className="h-2 w-2 rounded-full bg-[var(--app-primary)]" />Planned {planned}</li>
        <li className="flex items-center gap-1.5"><span className="h-2 w-2 rounded-full bg-amber-500" />Reactive {reactive}</li>
      </ul>
    </div>
  )
}
