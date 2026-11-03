import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
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
import { formatCurrency, formatNumber, formatPercent } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { useRevenueReport, type RevenueReportRow } from '../finance/api'
import { usePmComplianceReport } from '../maintenance/api'
import { useProjectSummary } from '../projects/api'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import { usePermission } from '../../shared/hooks/usePermission'
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
  const { hasModule } = useEntitlements()
  const canViewProjects = usePermission('projects.view')
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

  // Top clients by revenue: `/api/finance/reports/revenue?groupBy=client` already returns exactly
  // this — one row per client, ordered by amount invoiced descending (TenantFinanceController
  // `GetRevenueReport`). The panel that used to sit here claimed no such endpoint existed.
  const revenueByClient = useRevenueReport({ from: from.toISOString(), to: to.toISOString(), groupBy: 'client' })

  // PM compliance: `/api/reports/pm-compliance` (ReportsController `PmCompliance`). Also already
  // real, and already typed and hooked for the Reports module — it was never called from here.
  const pmCompliance = usePmComplianceReport({ from: from.toISOString(), to: to.toISOString() })

  const statusColumns: DataTableColumn<{ id: string; status: string; count: number }>[] = [
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'count', header: 'Count', cell: (row) => formatNumber(row.count) },
  ]

  const topClientColumns: DataTableColumn<RevenueReportRow & { id: string }>[] = [
    { key: 'group', header: 'Client', cell: (row) => <span className="font-medium text-slate-900">{row.group}</span>, sortValue: (row) => row.group },
    { key: 'invoiced', header: 'Invoiced', cell: (row) => formatCurrency(row.invoiced), sortValue: (row) => row.invoiced },
    { key: 'collected', header: 'Collected', cell: (row) => formatCurrency(row.collected), sortValue: (row) => row.collected },
    { key: 'outstanding', header: 'Outstanding', cell: (row) => formatCurrency(row.outstanding), sortValue: (row) => row.outstanding },
  ]

  const topClients = useMemo(
    () => (revenueByClient.data ?? []).slice(0, 10).map((row) => ({ id: row.group, ...row })),
    [revenueByClient.data],
  )

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

          {hasModule('projects') && canViewProjects ? <ProjectsRow /> : null}

          <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
            <section className="space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
              <SectionHeader title="Top 10 Clients" description="Ranked by amount invoiced in the selected period." />
              {revenueByClient.isError ? (
                <ErrorState
                  title="Client revenue unavailable"
                  description="We couldn’t load the revenue breakdown, so no ranking is shown — an empty list here would be indistinguishable from a failed request."
                  onRetry={() => void revenueByClient.refetch()}
                />
              ) : (
                <DataTable
                  columns={topClientColumns}
                  data={topClients}
                  loading={revenueByClient.isLoading}
                  emptyTitle="No invoiced revenue in this period"
                  emptyDescription="Adjust the period selector, or raise an invoice to see clients ranked here."
                />
              )}
            </section>

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

/** Shown when the tenant has Projects: today's register totals (not tied to the period selector). */
function ProjectsRow() {
  const { data, isLoading, isError, refetch } = useProjectSummary()
  const tiles = data
    ? [
        { label: 'Active Projects', value: formatNumber(data.activeProjects), tone: 'text-slate-900' },
        { label: 'Behind Schedule', value: formatNumber(data.behindSchedule), tone: data.behindSchedule > 0 ? 'text-rose-600' : 'text-slate-900' },
        { label: 'Pending Variations', value: formatNumber(data.pendingVariationsCount), tone: data.pendingVariationsCount > 0 ? 'text-amber-600' : 'text-slate-900' },
        { label: 'Open Contract Value', value: formatCurrency(data.totalRevisedValue), tone: 'text-slate-900' },
      ]
    : []
  return (
    <section className="space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <SectionHeader title="Projects" description="Open projects today, from the project register." />
      {isError ? (
        <ErrorState title="Projects unavailable" description="We couldn’t load the project register totals." onRetry={() => void refetch()} />
      ) : isLoading || !data ? (
        <Spinner label="Loading projects" />
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {tiles.map((tile) => (
            <Link key={tile.label} to="/projects" className="rounded-xl border border-slate-200 px-4 py-3 transition hover:bg-white">
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{tile.label}</div>
              <div className={cn('mt-2 text-xl font-semibold', tile.tone)}>{tile.value}</div>
            </Link>
          ))}
        </div>
      )}
    </section>
  )
}
