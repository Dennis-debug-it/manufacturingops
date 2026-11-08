import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { CalendarClock, Repeat, Sparkles, TrendingDown } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { StatCard } from '../../../shared/components/StatCard'
import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { useAnyPermission } from '../../../shared/hooks/usePermission'
import { formatDate } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { LocationSelect, inputClass, primaryButton, secondaryButton } from '../components/ui'
import { useFactoryLinks } from '../links'
import {
  formatMtbf, suggestedCadence, useApplySuggestion, useRepeatFailures, useReliabilityAssets, useSuggestions,
  type AssetReliability, type PmSuggestion, type PmSuggestionKind,
} from './api'

const TABS = [{ id: 'assets', label: 'Assets' }, { id: 'suggestions', label: 'PM suggestions' }, { id: 'repeats', label: 'Repeat failures' }] as const
type Tab = (typeof TABS)[number]['id']
const PERIODS = [{ days: 180, label: 'Last 6 months' }, { days: 365, label: 'Last 12 months' }, { days: 730, label: 'Last 2 years' }]
const PATTERN_TONES: Record<AssetReliability['pattern'], BadgeTone> = { Regular: 'info', Irregular: 'warning', Unknown: 'neutral' }
const KIND_LABELS: Record<PmSuggestionKind, { label: string; tone: BadgeTone }> = {
  NoPlan: { label: 'Start a PM plan', tone: 'info' },
  PlanTooLong: { label: 'Shorten the PM', tone: 'warning' },
  PlanNotPreventing: { label: 'Review the PM', tone: 'danger' },
  Random: { label: 'Watch condition', tone: 'neutral' },
}

/** Reliability: MTBF per asset, PM changes the breakdown history argues for, and faults that keep coming back. */
export function ReliabilityPage() {
  const [params, setParams] = useSearchParams()
  const tab = (TABS.find((x) => x.id === params.get('tab'))?.id ?? 'assets') as Tab
  const [orgNodeId, setOrgNodeId] = useState('')
  const [days, setDays] = useState(365)
  const assets = useReliabilityAssets(orgNodeId, days)
  const suggestions = useSuggestions(orgNodeId, days)
  const rows = assets.data ?? []
  const breakdowns = rows.reduce((sum, row) => sum + row.failures, 0)

  return (
    <div className="space-y-6">
      <PageHeader
        title="Reliability"
        subtitle="Groundwork for predictive maintenance: how often each asset breaks down, how the PM should change, and which faults keep coming back."
        secondaryActions={
          <div className="flex flex-wrap gap-2">
            <div className="w-56"><LocationSelect value={orgNodeId} onChange={setOrgNodeId} emptyLabel="All locations" includeGroups /></div>
            <select className={cn(inputClass, 'w-44')} value={days} onChange={(e) => setDays(Number(e.target.value))} aria-label="Period">
              {PERIODS.map((period) => <option key={period.days} value={period.days}>{period.label}</option>)}
            </select>
          </div>
        }
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Breakdowns" value={assets.isLoading ? '—' : breakdowns} icon={<TrendingDown className="h-4 w-4 text-slate-400" />} />
        <StatCard label="Assets that broke down" value={assets.isLoading ? '—' : rows.length} />
        <StatCard label="Breaking down regularly" value={assets.isLoading ? '—' : rows.filter((x) => x.pattern === 'Regular').length} icon={<CalendarClock className="h-4 w-4 text-sky-500" />} />
        <StatCard label="PM suggestions" value={suggestions.data?.length ?? '—'} tone={suggestions.data?.length ? 'warning' : 'default'} icon={<Sparkles className="h-4 w-4 text-amber-500" />} />
      </div>

      <div className="flex gap-2 border-b border-slate-200" role="tablist">
        {TABS.map((item) => (
          <button key={item.id} type="button" role="tab" aria-selected={tab === item.id}
            className={cn('-mb-px border-b-2 px-3 py-2 text-sm font-medium', tab === item.id ? 'border-[var(--app-primary)] text-slate-900' : 'border-transparent text-slate-500 hover:text-slate-700')}
            onClick={() => setParams(item.id === 'assets' ? {} : { tab: item.id }, { replace: true })}>{item.label}</button>
        ))}
      </div>

      {tab === 'assets' ? <AssetsTab rows={rows} loading={assets.isLoading} /> : null}
      {tab === 'suggestions' ? <SuggestionsTab suggestions={suggestions.data ?? []} loading={suggestions.isLoading} days={days} /> : null}
      {tab === 'repeats' ? <RepeatsTab orgNodeId={orgNodeId} days={days} /> : null}
    </div>
  )
}

function AssetsTab({ rows, loading }: { rows: AssetReliability[]; loading: boolean }) {
  const links = useFactoryLinks()
  if (loading) return <Spinner label="Loading breakdown history" />
  if (rows.length === 0) return <EmptyState title="No breakdowns in this period" description="Assets appear here once a job marked as a breakdown is raised against them." />
  return (
    <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
      <table className="w-full min-w-[900px] text-sm">
        <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
          <tr>
            <th className="px-3 py-2">Asset</th><th className="px-3 py-2 text-right">Breakdowns</th><th className="px-3 py-2 text-right">MTBF</th>
            <th className="px-3 py-2 text-right">MTTR</th><th className="px-3 py-2 text-right">Downtime</th><th className="px-3 py-2">Interval</th>
            <th className="px-3 py-2">Pattern</th><th className="px-3 py-2">Main fault</th><th className="px-3 py-2 text-right">PM plans</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {rows.map((row) => (
            <tr key={row.assetId}>
              <td className="px-3 py-2">
                <Link to={links.asset(row.assetId)} className="font-medium text-slate-900 hover:underline">{row.assetName}</Link>
                <div className="text-xs text-slate-500">{row.assetCode}{row.location ? ` · ${row.location}` : ''}{row.criticality ? ` · class ${row.criticality}` : ''}</div>
              </td>
              <td className="px-3 py-2 text-right tabular-nums">{row.failures}</td>
              <td className="px-3 py-2 text-right tabular-nums">{formatMtbf(row.mtbfHours)}</td>
              <td className="px-3 py-2 text-right tabular-nums">{row.mttrHours === null ? '—' : `${row.mttrHours} h`}</td>
              <td className="px-3 py-2 text-right tabular-nums">{row.downtimeHours} h</td>
              <td className="px-3 py-2 text-slate-600">
                {row.meanDaysBetween === null ? '—' : `~${Math.round(row.meanDaysBetween)} days`}
                {row.meanRunBetween !== null ? <span className="block text-xs text-slate-500">{row.meanRunBetween} {row.meterUnit} running</span> : null}
              </td>
              <td className="px-3 py-2"><Badge tone={PATTERN_TONES[row.pattern]} size="sm">{row.pattern === 'Unknown' ? 'Too few' : row.pattern}</Badge></td>
              <td className="px-3 py-2 text-slate-600">{row.topProblem ? `${row.topProblem} (${row.topProblemCount})` : '—'}</td>
              <td className="px-3 py-2 text-right tabular-nums">{row.activePlans}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="border-t border-slate-100 px-3 py-2 text-xs text-slate-500">
        MTBF is the period's hours less downtime, per breakdown. Regular means the gaps between breakdowns are steady (wear-out, which PM can catch); irregular means they are random (better watched through condition monitoring).
      </p>
    </div>
  )
}

function SuggestionsTab({ suggestions, loading, days }: { suggestions: PmSuggestion[]; loading: boolean; days: number }) {
  const links = useFactoryLinks()
  const apply = useApplySuggestion(days)
  const canApply = useAnyPermission('settings.manage', 'work-orders.create')
  if (loading) return <Spinner label="Working out suggestions" />
  if (suggestions.length === 0) return <EmptyState title="No suggestions" description="Suggestions appear for assets with at least three breakdowns in the period." />

  const run = async (suggestion: PmSuggestion) => {
    try {
      const plan = await apply.mutateAsync(suggestion.assetId)
      toast.success(suggestion.kind === 'NoPlan' ? `Plan "${plan.name}" created` : `"${plan.name}" shortened`)
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <ul className="space-y-3">
      {suggestions.map((suggestion) => {
        const kind = KIND_LABELS[suggestion.kind]
        const cadence = suggestedCadence(suggestion)
        const actionable = (suggestion.kind === 'NoPlan' || suggestion.kind === 'PlanTooLong') && cadence !== null
        return (
          <li key={suggestion.assetId} className="space-y-2 rounded-lg border border-slate-200 bg-white p-4">
            <div className="flex flex-wrap items-center gap-2">
              <Badge tone={kind.tone} size="sm">{kind.label}</Badge>
              <Link to={links.asset(suggestion.assetId)} className="font-medium text-slate-900 hover:underline">{suggestion.assetName}</Link>
              <span className="text-xs text-slate-500">{suggestion.assetCode}</span>
              {suggestion.planName ? <span className="text-xs text-slate-500">· plan "{suggestion.planName}" ({suggestion.currentInterval})</span> : null}
            </div>
            <p className="text-sm text-slate-700">{suggestion.reason}</p>
            {actionable && canApply ? (
              <button type="button" className={suggestion.kind === 'NoPlan' ? primaryButton : secondaryButton} disabled={apply.isPending} onClick={() => void run(suggestion)}>
                {suggestion.kind === 'NoPlan' ? `Create a plan ${cadence}` : `Change it to ${cadence}`}
              </button>
            ) : null}
            {suggestion.kind === 'Random' ? <Link to={links.asset(suggestion.assetId, 'meters')} className="text-sm text-[var(--app-primary)] hover:underline">Set meter limits on this asset</Link> : null}
          </li>
        )
      })}
    </ul>
  )
}

function RepeatsTab({ orgNodeId, days }: { orgNodeId: string; days: number }) {
  const [within, setWithin] = useState<number | null>(null)
  const { data, isLoading } = useRepeatFailures(orgNodeId, days, within)
  const links = useFactoryLinks()
  if (isLoading || !data) return <Spinner label="Looking for repeat failures" />
  const { summary } = data
  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center gap-3 text-sm text-slate-600">
        <Repeat className="h-4 w-4 text-slate-400" />
        <span><strong className="text-slate-900">{summary.repeats}</strong> of {summary.failures} breakdowns ({summary.repeatRatePercent ?? 0}%) were the same fault on the same asset within</span>
        <select className={cn(inputClass, 'w-28')} value={within ?? summary.repeatWithinDays} onChange={(e) => setWithin(Number(e.target.value))} aria-label="Repeat window">
          {[7, 14, 30, 60, 90].concat([summary.repeatWithinDays]).filter((v, i, a) => a.indexOf(v) === i).sort((a, b) => a - b).map((value) => <option key={value} value={value}>{value} days</option>)}
        </select>
        <span>of the last, on {summary.assetsWithRepeats} asset{summary.assetsWithRepeats === 1 ? '' : 's'}.</span>
      </div>

      <section>
        <h2 className="mb-2 text-sm font-semibold text-slate-900">By fault</h2>
        {data.byMode.length === 0 ? <p className="text-sm text-slate-500">No breakdowns in this period.</p> : (
          <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
            <table className="w-full min-w-[720px] text-sm">
              <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                <tr><th className="px-3 py-2">Fault</th><th className="px-3 py-2 text-right">Breakdowns</th><th className="px-3 py-2 text-right">Repeats</th><th className="px-3 py-2 text-right">Assets</th><th className="px-3 py-2 text-right">Back within</th><th className="px-3 py-2">Usual cause</th><th className="px-3 py-2">Usual fix</th></tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.byMode.map((row) => (
                  <tr key={row.problemId ?? row.problem}>
                    <td className="px-3 py-2 font-medium text-slate-900">{row.problem}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{row.failures}</td>
                    <td className={cn('px-3 py-2 text-right tabular-nums', row.repeats > 0 && 'font-semibold text-rose-700')}>{row.repeats}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{row.assets}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{row.meanDaysBetweenRepeats === null ? '—' : `${row.meanDaysBetweenRepeats} days`}</td>
                    <td className="px-3 py-2 text-slate-600">{row.topCause ?? '—'}</td>
                    <td className="px-3 py-2 text-slate-600">{row.topRemedy ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section>
        <h2 className="mb-2 text-sm font-semibold text-slate-900">Faults that keep coming back</h2>
        {data.chains.length === 0 ? <p className="text-sm text-slate-500">No fault came back within the window.</p> : (
          <ul className="space-y-3">
            {data.chains.map((chain) => (
              <li key={`${chain.assetId}-${chain.problem}-${chain.failures[0]!.workOrderId}`} className="rounded-lg border border-slate-200 bg-white p-3">
                <div className="mb-2 flex flex-wrap items-center gap-2">
                  <Badge tone="danger" size="sm">{chain.failures.length}×</Badge>
                  <Link to={links.asset(chain.assetId)} className="font-medium text-slate-900 hover:underline">{chain.assetName}</Link>
                  <span className="text-sm text-slate-600">{chain.problem}</span>
                </div>
                <ol className="space-y-1 text-sm">
                  {chain.failures.map((failure) => (
                    <li key={failure.workOrderId} className="flex flex-wrap gap-2 text-slate-600">
                      <Link to={links.workOrder(failure.workOrderId)} className="font-mono text-xs text-[var(--app-primary)] hover:underline">{failure.workOrderNumber}</Link>
                      <span>{formatDate(failure.at)}</span>
                      {failure.daysSincePrevious !== null ? <span className="text-rose-700">{failure.daysSincePrevious} days after the last</span> : null}
                      {failure.cause ? <span>· cause: {failure.cause}</span> : null}
                      {failure.remedy ? <span>· fix: {failure.remedy}</span> : null}
                    </li>
                  ))}
                </ol>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  )
}
