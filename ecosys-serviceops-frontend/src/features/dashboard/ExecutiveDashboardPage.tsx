import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import {
  endOfMonth,
  endOfQuarter,
  endOfWeek,
  endOfYear,
  startOfMonth,
  startOfQuarter,
  startOfWeek,
  startOfYear,
} from 'date-fns'
import { CheckCircle2, Clock3, ListChecks, TrendingUp } from 'lucide-react'
import { endpoints } from '../../shared/api/endpoints'
import { api } from '../../shared/api/apiClient'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { ErrorState } from '../../shared/components/ErrorState'
import { Spinner } from '../../shared/components/Spinner'
import { SectionHeader } from '../../shared/components/SectionHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatNumber, formatPercent } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { usePmComplianceReport } from '../maintenance/api'
import type { DashboardSummary } from './types'

type Period = 'week' | 'month' | 'quarter' | 'year' | 'custom'

interface WorkOrderPerformanceReport {
  dateRange: { from: string; to: string }
  total: number
  completed: number
  completedOnTime: number
  overdue: number
  onTimeRate: number
  avgCompletionHours: number
  byStatus: Array<{ status: string; count: number }>
  byPriority: Array<{ priority: string; count: number }>
}

const periodOptions: Array<{ value: Period; label: string }> = [
  { value: 'week', label: 'This Week' },
  { value: 'month', label: 'This Month' },
  { value: 'quarter', label: 'This Quarter' },
  { value: 'year', label: 'This Year' },
  { value: 'custom', label: 'Custom' },
]

function resolveRange(period: Period, customFrom: string, customTo: string) {
  const now = new Date()
  switch (period) {
    case 'week':
      return { from: startOfWeek(now, { weekStartsOn: 1 }), to: endOfWeek(now, { weekStartsOn: 1 }) }
    case 'month':
      return { from: startOfMonth(now), to: endOfMonth(now) }
    case 'quarter':
      return { from: startOfQuarter(now), to: endOfQuarter(now) }
    case 'year':
      return { from: startOfYear(now), to: endOfYear(now) }
    case 'custom':
      return {
        from: customFrom ? new Date(customFrom) : startOfMonth(now),
        to: customTo ? new Date(customTo) : endOfMonth(now),
      }
    default:
      return { from: startOfMonth(now), to: endOfMonth(now) }
  }
}

export function ExecutiveDashboardPage() {
  const [period, setPeriod] = useState<Period>('month')
  const [customFrom, setCustomFrom] = useState('')
  const [customTo, setCustomTo] = useState('')

  const { from, to } = useMemo(() => resolveRange(period, customFrom, customTo), [period, customFrom, customTo])

  const { data: summary, isLoading: summaryLoading, refetch: refetchSummary } = useQuery({
    queryKey: ['dashboard', 'summary'],
    queryFn: () => api.get<DashboardSummary>(endpoints.dashboard.summary),
    staleTime: 30_000,
  })

  const { data: performance, isLoading: performanceLoading, isError: performanceError, refetch: refetchPerformance } = useQuery({
    queryKey: ['reports', 'work-order-performance', from.toISOString(), to.toISOString()],
    queryFn: () =>
      api.get<WorkOrderPerformanceReport>(endpoints.reports.workOrderPerformance, {
        from: from.toISOString(),
        to: to.toISOString(),
      }),
    staleTime: 30_000,
  })

  // PM compliance: `/api/reports/pm-compliance` (ReportsController `PmCompliance`). Also already
  // real, and already typed and hooked for the Reports module — it was never called from here.
  const pmCompliance = usePmComplianceReport({ from: from.toISOString(), to: to.toISOString() })

  const statusColumns: DataTableColumn<{ id: string; status: string; count: number }>[] = [
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'count', header: 'Count', cell: (row) => formatNumber(row.count) },
  ]

  const isLoading = summaryLoading || performanceLoading

  return (
    <div className="space-y-4">
      <PageHeader
        title="Executive Dashboard"
        subtitle="Period analysis, KPI strip, and operational trends."
        secondaryActions={
          <div className="flex flex-wrap items-center gap-2">
            {periodOptions.map((option) => (
              <button
                key={option.value}
                type="button"
                onClick={() => setPeriod(option.value)}
                className={cn(
                  'inline-flex h-10 items-center rounded-full border px-3 text-sm font-medium transition',
                  period === option.value ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                )}
              >
                {option.label}
              </button>
            ))}
            {period === 'custom' ? (
              <div className="flex items-center gap-2">
                <input type="date" value={customFrom} onChange={(event) => setCustomFrom(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                <span className="text-slate-400">to</span>
                <input type="date" value={customTo} onChange={(event) => setCustomTo(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
              </div>
            ) : null}
          </div>
        }
      />

      {isLoading ? (
        <Spinner label="Loading executive dashboard" />
      ) : performanceError || !performance || !summary ? (
        // Every KPI and panel below is derived from these two payloads, so there is no partial
        // view worth rendering — and a full-page reload is not a retry, it just re-runs the same
        // failing request from scratch.
        <ErrorState
          title="Executive data unavailable"
          description="We couldn’t load the reporting data for this period, so no figures are shown — zeroes here would be indistinguishable from a failed request."
          onRetry={() => {
            void refetchPerformance()
            void refetchSummary()
          }}
        />
      ) : (
        <>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <div className="flex items-center justify-between"><span className="text-sm font-medium text-slate-600">Work Orders (Period)</span><ListChecks className="h-4 w-4 text-slate-400" /></div>
              <div className="mt-3 text-2xl font-semibold text-slate-900">{formatNumber(performance.total)}</div>
            </div>
            <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <div className="flex items-center justify-between"><span className="text-sm font-medium text-slate-600">On-Time Completion</span><CheckCircle2 className="h-4 w-4 text-emerald-600" /></div>
              <div className="mt-3 text-2xl font-semibold text-emerald-600">{formatPercent(performance.onTimeRate)}</div>
            </div>
            <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <div className="flex items-center justify-between"><span className="text-sm font-medium text-slate-600">Overdue</span><Clock3 className="h-4 w-4 text-rose-600" /></div>
              <div className="mt-3 text-2xl font-semibold text-rose-600">{formatNumber(performance.overdue)}</div>
            </div>
            <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <div className="flex items-center justify-between"><span className="text-sm font-medium text-slate-600">Avg Completion Time</span><TrendingUp className="h-4 w-4 text-slate-400" /></div>
              <div className="mt-3 text-2xl font-semibold text-slate-900">{performance.avgCompletionHours.toFixed(1)}h</div>
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
            <section className="space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <SectionHeader title="Work Order Health" description="Status breakdown for the selected period." />
              <DataTable
                columns={statusColumns}
                data={performance.byStatus.map((row) => ({ id: row.status, ...row }))}
                emptyTitle="No work orders in this period"
                emptyDescription="Adjust the period selector to see data."
              />
            </section>

            <section className="space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <SectionHeader title="Technician Utilisation" description="Active load and on-site status (all-time snapshot)." />
              <DataTable
                columns={[
                  { key: 'technicianName', header: 'Technician', cell: (row) => row.technicianName },
                  { key: 'activeWorkOrders', header: 'Active WOs', cell: (row) => formatNumber(row.activeWorkOrders) },
                  { key: 'onSiteCount', header: 'On Site', cell: (row) => formatNumber(row.onSiteCount) },
                ]}
                data={summary.technicianWorkload.map((row) => ({ id: row.technicianName, ...row }))}
                emptyTitle="No technician activity"
                emptyDescription="Technician workload will appear here."
              />
            </section>
          </div>

          <section className="space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
            <SectionHeader title="Contract Health" description="Renewal risk and expiry pipeline." />
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <div className="rounded-xl border border-slate-200 px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Expiring 90d</div>
                <div className="mt-2 text-xl font-semibold text-slate-900">{formatNumber(summary.contractsExpiringIn90Days)}</div>
              </div>
              <div className="rounded-xl border border-slate-200 px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Expiring 30d</div>
                <div className="mt-2 text-xl font-semibold text-amber-600">{formatNumber(summary.contractsExpiringIn30Days)}</div>
              </div>
              <div className="rounded-xl border border-slate-200 px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Critical Renewal Risk</div>
                <div className="mt-2 text-xl font-semibold text-rose-600">{formatNumber(summary.criticalRenewalRiskContracts)}</div>
              </div>
              <div className="rounded-xl border border-slate-200 px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Expired</div>
                <div className="mt-2 text-xl font-semibold text-rose-600">{formatNumber(summary.expiredContracts)}</div>
              </div>
            </div>
          </section>


          <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
            <section className="space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <SectionHeader title="PM Compliance" description="Preventive maintenance plans due and completed on time in the selected period." />
              {pmCompliance.isError ? (
                <ErrorState
                  title="PM compliance unavailable"
                  description="We couldn’t load the compliance report, so no rate is shown — a 100% here would be indistinguishable from a failed request."
                  onRetry={() => void pmCompliance.refetch()}
                />
              ) : pmCompliance.isLoading ? (
                <Spinner label="Loading PM compliance" />
              ) : pmCompliance.data ? (
                <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                  <div className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Compliance</div>
                    <div className="mt-2 text-xl font-semibold text-emerald-600">{formatPercent(pmCompliance.data.complianceRate)}</div>
                  </div>
                  <div className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Active Plans</div>
                    <div className="mt-2 text-xl font-semibold text-slate-900">{formatNumber(pmCompliance.data.activePlans)}</div>
                  </div>
                  <div className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Due In Period</div>
                    <div className="mt-2 text-xl font-semibold text-slate-900">{formatNumber(pmCompliance.data.dueInPeriod)}</div>
                  </div>
                  <div className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Overdue</div>
                    <div className="mt-2 text-xl font-semibold text-rose-600">{formatNumber(pmCompliance.data.overdue)}</div>
                  </div>
                </div>
              ) : (
                <EmptyState title="No PM compliance data" description="No preventive maintenance plans fall in the selected period." />
              )}
            </section>

            {/*
              The two panels below have no endpoint behind them, and say so rather than implying
              work is queued. Checked against the backend controllers:
                • Revenue vs Target — nothing in the API stores a revenue target. `/api/finance/
                  reports/revenue` returns actuals only; there is no targets table or settings key.
                • SLA Performance — `/api/sla-definitions` and `/api/settings/sla-policies` return
                  SLA *configuration*. No endpoint measures achievement against it.
            */}
            <EmptyState
              title="Revenue vs Target"
              description="Not available. Revenue actuals are reported, but the API stores no revenue target to compare them against, so there is nothing to chart here."
            />
            <EmptyState
              title="SLA Performance"
              description="Not available. SLA policies and definitions are configurable, but the API does not measure achievement against them, so compliance cannot be reported."
            />
          </div>
        </>
      )}
    </div>
  )
}
