import { useMemo, useState } from 'react'
import { format, startOfMonth, startOfYear, subDays } from 'date-fns'
import { AlertTriangle } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatCard } from '../../shared/components/StatCard'
import { useAuth } from '../../shared/hooks/useAuth'
import { formatCurrency, formatPercent } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { useSalesReport } from './api'
import { CrmNav } from './components/CrmNav'
import { OwnerSelect } from './components/OwnerSelect'
import type { SalesForecastBucket, SalesOwnerPerformance, SalesSourcePerformance } from './types'

const selectClass = 'h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

const presets = [
  { id: '30d', label: 'Last 30 days' },
  { id: '90d', label: 'Last 90 days' },
  { id: 'month', label: 'This month' },
  { id: 'year', label: 'This year' },
  { id: 'custom', label: 'Custom' },
] as const
type Preset = (typeof presets)[number]['id']

const day = (date: Date) => format(date, 'yyyy-MM-dd')
/** A yyyy-MM-dd day as the calendar day it names, whatever the browser's time zone. */
const showDay = (value: string) => format(new Date(`${value}T00:00`), 'd MMM yyyy')

/** The inclusive day range a preset stands for, counted back from `now`. */
export function presetRange(preset: Exclude<Preset, 'custom'>, now = new Date()) {
  switch (preset) {
    case '30d':
      return { from: day(subDays(now, 29)), to: day(now) }
    case '90d':
      return { from: day(subDays(now, 89)), to: day(now) }
    case 'month':
      return { from: day(startOfMonth(now)), to: day(now) }
    case 'year':
      return { from: day(startOfYear(now)), to: day(now) }
  }
}

/** "Nov 2026" for a month bucket; the open-ended buckets get a plain label. */
export function forecastLabel(bucket: Pick<SalesForecastBucket, 'key'>) {
  if (bucket.key === 'overdue') return 'Close date passed'
  if (bucket.key === 'later') return 'Later'
  if (bucket.key === 'none') return 'No close date'
  return format(new Date(`${bucket.key}-01T00:00`), 'MMM yyyy')
}

const rate = (value: number | null) => (value === null ? '—' : formatPercent(value))

/** Win/loss, forecast and who-is-winning for the sales CRM over a chosen period. */
export function SalesReportsPage() {
  const { user } = useAuth()
  const [preset, setPreset] = useState<Preset>('90d')
  const [custom, setCustom] = useState(() => presetRange('90d'))
  const [ownerFilter, setOwnerFilter] = useState('')
  const range = useMemo(() => (preset === 'custom' ? custom : presetRange(preset)), [preset, custom])
  const customInvalid = preset === 'custom' && (!custom.from || !custom.to || custom.from > custom.to)
  const { data: report, isLoading, isError } = useSalesReport({ ...range, ownerUserId: ownerFilter || undefined }, { enabled: !customInvalid })
  const currency = report?.reportingCurrency
  const money = (value: number) => formatCurrency(value, currency)

  const ownerColumns: DataTableColumn<SalesOwnerPerformance & { id: string }>[] = [
    { key: 'owner', header: 'Owner', cell: (row) => row.ownerName ?? 'Unassigned' },
    { key: 'won', header: 'Won', cell: (row) => (row.wonCount ? `${row.wonCount} · ${money(row.wonValue)}` : '—') },
    { key: 'lost', header: 'Lost', cell: (row) => row.lostCount || '—' },
    { key: 'winRate', header: 'Win rate', cell: (row) => rate(row.winRate) },
    { key: 'open', header: 'Open pipeline', cell: (row) => (row.openCount ? `${row.openCount} · ${money(row.openValue)}` : '—') },
    { key: 'weighted', header: 'Weighted', cell: (row) => (row.openCount ? money(row.weightedValue) : '—') },
  ]

  const sourceColumns: DataTableColumn<SalesSourcePerformance & { id: string }>[] = [
    { key: 'source', header: 'Source', cell: (row) => row.source ?? 'Not recorded' },
    { key: 'newDeals', header: 'New deals', cell: (row) => row.newDeals || '—' },
    { key: 'won', header: 'Won', cell: (row) => (row.wonCount ? `${row.wonCount} · ${money(row.wonValue)}` : '—') },
    { key: 'lost', header: 'Lost', cell: (row) => row.lostCount || '—' },
    { key: 'winRate', header: 'Win rate', cell: (row) => rate(row.winRate) },
  ]

  const forecastMax = Math.max(1, ...(report?.forecast ?? []).map((bucket) => bucket.value))
  const reasonMax = Math.max(1, ...(report?.lostReasons ?? []).map((reason) => reason.count))

  return (
    <div className="space-y-4">
      <PageHeader title="Sales Reports" subtitle="What you won and lost, why, who is winning, and what is due to close." />

      <CrmNav />

      <div className="flex flex-col flex-wrap gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)] sm:flex-row sm:items-center">
        <select aria-label="Period" value={preset} onChange={(event) => setPreset(event.target.value as Preset)} className={selectClass}>
          {presets.map((option) => (
            <option key={option.id} value={option.id}>{option.label}</option>
          ))}
        </select>
        {preset === 'custom' ? (
          <div className="flex items-center gap-2 text-sm text-slate-600">
            <input type="date" aria-label="From" value={custom.from} max={custom.to || undefined} onChange={(event) => setCustom((current) => ({ ...current, from: event.target.value }))} className={selectClass} />
            <span>to</span>
            <input type="date" aria-label="To" value={custom.to} min={custom.from || undefined} onChange={(event) => setCustom((current) => ({ ...current, to: event.target.value }))} className={selectClass} />
          </div>
        ) : null}
        <OwnerSelect aria-label="Filter by owner" value={ownerFilter} onChange={setOwnerFilter} emptyLabel="All owners" className={selectClass} />
        {user && ownerFilter !== user.userId ? (
          <button type="button" onClick={() => setOwnerFilter(user.userId)} className="inline-flex h-10 items-center self-start rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-600 transition hover:bg-slate-50 sm:self-auto">
            Mine
          </button>
        ) : null}
        {!customInvalid ? <span className="text-sm text-slate-500 sm:ml-auto">{showDay(range.from)} – {showDay(range.to)}</span> : null}
      </div>

      {customInvalid ? <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">Pick a start date on or before the end date.</div> : null}
      {isError && !customInvalid ? <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-900">The sales report could not be loaded.</div> : null}
      {isLoading && !customInvalid ? <div className="py-10 text-center text-sm text-slate-400">Loading…</div> : null}

      {report && !customInvalid ? (
        <>
          {report.unconvertedDeals > 0 ? (
            <div className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>
                {report.unconvertedDeals} deal{report.unconvertedDeals === 1 ? ' is' : 's are'} counted but left out of the values — there is no exchange rate to {currency} yet. Add one from Pipeline › Rates.
              </span>
            </div>
          ) : null}

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <StatCard label="Won" value={`${report.wonCount} · ${money(report.wonValue)}`} tone="success" />
            <StatCard label="Lost" value={`${report.lostCount} · ${money(report.lostValue)}`} tone={report.lostCount > 0 ? 'danger' : 'default'} />
            <StatCard label="Win rate" value={rate(report.winRate)} />
            <StatCard label="Average won deal" value={report.averageWonValue === null ? '—' : money(report.averageWonValue)} />
            <StatCard label="Average days to win" value={report.averageDaysToWin === null ? '—' : report.averageDaysToWin} />
            <StatCard label="New deals" value={report.newDeals} />
          </div>

          <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
            <div className="flex flex-col gap-1 sm:flex-row sm:items-baseline sm:justify-between">
              <h2 className="text-sm font-semibold text-slate-900">Forecast by expected close month</h2>
              <p className="text-xs text-slate-500">
                {report.openCount} open deal{report.openCount === 1 ? '' : 's'} · {money(report.openValue)} · weighted {money(report.weightedValue)}. Not limited to the period.
              </p>
            </div>
            <ul className="mt-3 space-y-2">
              {report.forecast.map((bucket) => (
                <li key={bucket.key} className="grid grid-cols-[7.5rem_1fr] items-center gap-3 text-sm sm:grid-cols-[9rem_1fr_19rem]">
                  <span className={cn('text-slate-700', bucket.key === 'overdue' && bucket.count > 0 && 'font-medium text-amber-700')}>{forecastLabel(bucket)}</span>
                  <div className="relative h-5 overflow-hidden rounded bg-slate-100" title={`${bucket.count} deal${bucket.count === 1 ? '' : 's'}`}>
                    <div className="absolute inset-y-0 left-0 rounded bg-[var(--app-primary)] opacity-25" style={{ width: `${(bucket.value / forecastMax) * 100}%` }} />
                    <div className="absolute inset-y-0 left-0 rounded bg-[var(--app-primary)]" style={{ width: `${(bucket.weightedValue / forecastMax) * 100}%` }} />
                  </div>
                  <span className="col-span-2 text-xs text-slate-500 sm:col-span-1 sm:text-right">
                    {bucket.count ? `${bucket.count} · ${money(bucket.value)} · weighted ${money(bucket.weightedValue)}` : '—'}
                  </span>
                </li>
              ))}
            </ul>
            <p className="mt-3 text-xs text-slate-500">Light bar: full value. Dark bar: weighted by each deal's probability.</p>
          </section>

          <section className="space-y-2">
            <h2 className="text-sm font-semibold text-slate-900">By owner</h2>
            <DataTable
              columns={ownerColumns}
              data={report.owners.map((row) => ({ ...row, id: row.ownerUserId ?? 'unassigned' }))}
              emptyTitle="No deals in this period"
              emptyDescription="Nothing was won, lost or open for the selected owner and period."
            />
          </section>

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <section className="space-y-2">
              <h2 className="text-sm font-semibold text-slate-900">By lead source</h2>
              <DataTable
                columns={sourceColumns}
                data={report.sources.map((row) => ({ ...row, id: row.source ?? 'none' }))}
                emptyTitle="No new or closed deals"
                emptyDescription="No deal was added, won or lost in this period."
              />
            </section>

            <section className="space-y-4">
              <div className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
                <h2 className="text-sm font-semibold text-slate-900">Why deals were lost</h2>
                {report.lostReasons.length === 0 ? <p className="mt-2 text-sm text-slate-500">No deals lost in this period.</p> : null}
                <ul className="mt-3 space-y-2">
                  {report.lostReasons.map((reason) => (
                    <li key={reason.reason} className="text-sm">
                      <div className="flex justify-between gap-3">
                        <span className="truncate text-slate-700">{reason.reason}</span>
                        <span className="shrink-0 text-xs text-slate-500">{reason.count} · {money(reason.value)}</span>
                      </div>
                      <div className="mt-1 h-1.5 rounded bg-slate-100">
                        <div className="h-1.5 rounded bg-rose-400" style={{ width: `${(reason.count / reasonMax) * 100}%` }} />
                      </div>
                    </li>
                  ))}
                </ul>
              </div>

              <div className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
                <h2 className="text-sm font-semibold text-slate-900">RFPs & RFQs</h2>
                <dl className="mt-3 grid grid-cols-3 gap-3 text-sm sm:grid-cols-6">
                  {[
                    ['Submitted', report.tenders.submitted],
                    ['Won', report.tenders.won],
                    ['Lost', report.tenders.lost],
                    ['No bid', report.tenders.noBid],
                    ['Cancelled', report.tenders.cancelled],
                    ['Bid win rate', rate(report.tenders.winRate)],
                  ].map(([label, value]) => (
                    <div key={label}>
                      <dt className="text-xs text-slate-500">{label}</dt>
                      <dd className="mt-0.5 font-semibold text-slate-900">{value}</dd>
                    </div>
                  ))}
                </dl>
              </div>
            </section>
          </div>
        </>
      ) : null}
    </div>
  )
}
