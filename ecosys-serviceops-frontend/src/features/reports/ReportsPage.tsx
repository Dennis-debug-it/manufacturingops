import { useMemo, useState } from 'react'
import { subDays } from 'date-fns'
import { Download } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { EmptyState } from '../../shared/components/EmptyState'
import { StatCard } from '../../shared/components/StatCard'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'
import { formatCurrency, formatPercent } from '../../shared/utils/format'
import { apiClient } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { useWorkOrders } from '../work-orders/api'
import { useFinanceDashboard } from '../finance/api'
import { usePmComplianceReport } from '../maintenance/api'
import { useAssetReliability, useCsatReport, useTechnicianProductivity, useWorkOrderPerformance } from './api'

const tabs = [
  { id: 'wo-performance', label: 'Work Order' },
  { id: 'sla', label: 'SLA' },
  { id: 'pm-compliance', label: 'PM Compliance' },
  { id: 'finance', label: 'Finance' },
  { id: 'technician', label: 'Technician Performance' },
  { id: 'csat', label: 'CSAT' },
] as const

export function ReportsPage() {
  const [tab, setTab] = useState<(typeof tabs)[number]['id']>('wo-performance')
  const from = useMemo(() => subDays(new Date(), 30).toISOString(), [])
  const to = useMemo(() => new Date().toISOString(), [])

  const { data: performance, isLoading: performanceLoading } = useWorkOrderPerformance({ from, to })
  const { data: technicianReport, isLoading: technicianLoading } = useTechnicianProductivity({ from, to })
  const { data: assetReliability } = useAssetReliability({ from, to })
  const { data: pmCompliance, isLoading: pmLoading } = usePmComplianceReport({ from, to })
  const { data: financeDashboard, isLoading: financeLoading } = useFinanceDashboard()
  const { data: csat, isLoading: csatLoading } = useCsatReport({ from, to })
  const { data: workOrders } = useWorkOrders()

  const slaCounts = useMemo(() => {
    const counts: Record<string, number> = {}
    for (const wo of workOrders ?? []) {
      counts[wo.slaStatus] = (counts[wo.slaStatus] ?? 0) + 1
    }
    return Object.entries(counts).map(([status, count]) => ({ id: status, status, count }))
  }, [workOrders])

  const exportReport = async (url: string, filename: string) => {
    const response = await apiClient.get(url, { params: { from, to }, responseType: 'blob' })
    const objectUrl = URL.createObjectURL(response.data as Blob)
    const link = document.createElement('a')
    link.href = objectUrl
    link.download = filename
    link.click()
    URL.revokeObjectURL(objectUrl)
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Reports" subtitle="Operational analytics for the last 30 days." />

      <div className="flex flex-wrap gap-2">
        {tabs.map((option) => (
          <button
            key={option.id}
            type="button"
            onClick={() => setTab(option.id)}
            className={cn('inline-flex h-10 items-center rounded-full border px-3 text-sm font-medium transition', tab === option.id ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}
          >
            {option.label}
          </button>
        ))}
      </div>

      {tab === 'wo-performance' ? (
        performanceLoading ? <Spinner label="Loading report" /> : (
          <div className="space-y-4">
            <div className="flex justify-end">
              <button type="button" onClick={() => exportReport(endpoints.reports.workOrderPerformanceExport, 'work-order-performance.csv')} className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
                <Download className="h-4 w-4" />Export CSV
              </button>
            </div>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <StatCard label="Total" value={performance?.total ?? 0} />
              <StatCard label="Completed" value={performance?.completed ?? 0} tone="success" />
              <StatCard label="On-Time Rate" value={formatPercent(performance?.onTimeRate ?? 0)} />
              <StatCard label="Overdue" value={performance?.overdue ?? 0} tone="danger" />
            </div>
            <DataTable
              columns={[
                { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
                { key: 'count', header: 'Count', cell: (row) => row.count },
              ] satisfies DataTableColumn<{ id: string; status: string; count: number }>[]}
              data={(performance?.byStatus ?? []).map((row) => ({ id: row.status, ...row }))}
              emptyTitle="No data"
              emptyDescription="No work orders in this period."
            />
          </div>
        )
      ) : null}

      {tab === 'sla' ? (
        <DataTable
          columns={[
            { key: 'status', header: 'SLA Status', cell: (row) => <StatusBadge status={row.status} /> },
            { key: 'count', header: 'Work Orders', cell: (row) => row.count },
          ] satisfies DataTableColumn<{ id: string; status: string; count: number }>[]}
          data={slaCounts}
          emptyTitle="No data"
          emptyDescription="SLA status is derived from each work order's response/resolution deadlines — this table aggregates the current snapshot, not a historical trend."
        />
      ) : null}

      {tab === 'pm-compliance' ? (
        pmLoading ? <Spinner label="Loading report" /> : (
          <div className="space-y-4">
            <div className="flex justify-end">
              <button type="button" onClick={() => exportReport(endpoints.reports.pmComplianceExport, 'pm-compliance.csv')} className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
                <Download className="h-4 w-4" />Export CSV
              </button>
            </div>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <StatCard label="Active Plans" value={pmCompliance?.activePlans ?? 0} />
              <StatCard label="Due In Period" value={pmCompliance?.dueInPeriod ?? 0} />
              <StatCard label="Completed On Time" value={pmCompliance?.completedOnTime ?? 0} tone="success" />
              <StatCard label="Compliance Rate" value={formatPercent(pmCompliance?.complianceRate ?? 0)} />
            </div>
            <p className="text-sm text-slate-600">See <a className="underline" href="/maintenance/compliance">Maintenance → PM Compliance</a> for the full breakdown by client, category, and technician.</p>
          </div>
        )
      ) : null}

      {tab === 'finance' ? (
        financeLoading ? <Spinner label="Loading report" /> : (
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <StatCard label="Invoiced (YTD)" value={formatCurrency(financeDashboard?.totalInvoicedYtd ?? 0)} />
            <StatCard label="Collected (YTD)" value={formatCurrency(financeDashboard?.totalCollectedYtd ?? 0)} tone="success" />
            <StatCard label="Outstanding" value={formatCurrency(financeDashboard?.outstandingBalance ?? 0)} tone="warning" />
            <StatCard label="Overdue" value={formatCurrency(financeDashboard?.overdueBalance ?? 0)} tone="danger" />
          </div>
        )
      ) : null}

      {tab === 'technician' ? (
        technicianLoading ? <Spinner label="Loading report" /> : (
          <div className="space-y-4">
            <div className="flex justify-end">
              <button type="button" onClick={() => exportReport(endpoints.reports.technicianProductivityExport, 'technician-productivity.csv')} className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
                <Download className="h-4 w-4" />Export CSV
              </button>
            </div>
            <DataTable
              columns={[
                { key: 'name', header: 'Technician', cell: (row) => row.name },
                { key: 'totalJobs', header: 'Total Jobs', cell: (row) => row.totalJobs },
                { key: 'completed', header: 'Completed', cell: (row) => row.completed },
                { key: 'onTimeRate', header: 'On-Time Rate', cell: (row) => formatPercent(row.onTimeRate) },
                { key: 'avgTimeOnSiteHours', header: 'Avg Time on Site (hrs)', cell: (row) => row.avgTimeOnSiteHours },
              ]}
              data={(technicianReport?.technicians ?? []).map((row) => ({ ...row, id: row.technicianId ?? row.name }))}
              emptyTitle="No data"
              emptyDescription="No technician activity in this period."
            />
            {assetReliability && assetReliability.recurringFaultCount > 0 ? (
              <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
                {assetReliability.recurringFaultCount} asset(s) have 3+ corrective work orders in this period (recurring faults) — see Asset Reliability data via the assets module.
              </div>
            ) : null}
          </div>
        )
      ) : null}

      {tab === 'csat' ? (
        csatLoading ? (
          <Spinner label="Loading CSAT report" />
        ) : !csat || csat.sentCount === 0 ? (
          <EmptyState title="No surveys sent yet" description="CSAT surveys are emailed to clients automatically when work orders are completed. Results will appear here." />
        ) : (
          <div className="space-y-6">
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <StatCard label="Average Score" value={csat.averageScore != null ? `${csat.averageScore} / 5` : '—'} tone={csat.averageScore != null && csat.averageScore >= 4 ? 'success' : csat.averageScore != null && csat.averageScore < 3 ? 'warning' : 'default'} />
              <StatCard label="Surveys Sent" value={csat.sentCount} />
              <StatCard label="Responses" value={csat.responseCount} />
              <StatCard label="Response Rate" value={`${csat.responseRate}%`} />
            </div>

            <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
              <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                <h3 className="text-sm font-semibold text-slate-900">Score Distribution</h3>
                <div className="mt-4 space-y-2">
                  {csat.scoreDistribution.slice().reverse().map((row) => {
                    const max = Math.max(1, ...csat.scoreDistribution.map((r) => r.count))
                    return (
                      <div key={row.score} className="flex items-center gap-3 text-sm">
                        <span className="w-14 shrink-0 text-slate-600">{row.score} star{row.score > 1 ? 's' : ''}</span>
                        <div className="h-3 flex-1 overflow-hidden rounded-full bg-slate-100">
                          <div className="h-full rounded-full bg-amber-400" style={{ width: `${(row.count / max) * 100}%` }} />
                        </div>
                        <span className="w-8 shrink-0 text-right text-slate-700">{row.count}</span>
                      </div>
                    )
                  })}
                </div>
              </section>

              <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                <h3 className="text-sm font-semibold text-slate-900">Monthly Trend</h3>
                <DataTable
                  className="mt-4"
                  columns={[
                    { key: 'month', header: 'Month', cell: (row) => row.month },
                    { key: 'averageScore', header: 'Avg Score', cell: (row) => `${row.averageScore} / 5` },
                    { key: 'responseCount', header: 'Responses', cell: (row) => row.responseCount },
                  ]}
                  data={csat.monthlyTrend.map((row) => ({ id: row.month, ...row }))}
                  emptyTitle="No responses yet"
                  emptyDescription="Trend appears once clients start responding."
                />
              </section>
            </div>

            <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
              <h3 className="text-sm font-semibold text-slate-900">By Technician</h3>
              <DataTable
                className="mt-4"
                columns={[
                  { key: 'technicianName', header: 'Technician', cell: (row) => row.technicianName },
                  { key: 'averageScore', header: 'Avg Score', cell: (row) => `${row.averageScore} / 5` },
                  { key: 'responseCount', header: 'Responses', cell: (row) => row.responseCount },
                ]}
                data={csat.byTechnician.map((row) => ({ id: row.technicianId, ...row }))}
                emptyTitle="No technician ratings yet"
                emptyDescription="Scores are attributed to the lead technician on each work order."
              />
            </section>

            {csat.recentComments.length > 0 ? (
              <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                <h3 className="text-sm font-semibold text-slate-900">Recent Comments</h3>
                <ul className="mt-4 space-y-3">
                  {csat.recentComments.map((row, index) => (
                    <li key={index} className="rounded-xl border border-slate-200 px-4 py-3">
                      <div className="flex items-center justify-between text-xs text-slate-500">
                        <span>{row.clientName ?? 'Client'}</span>
                        <span>{row.score} / 5</span>
                      </div>
                      <p className="mt-1 text-sm leading-6 text-slate-700">{row.comment}</p>
                    </li>
                  ))}
                </ul>
              </section>
            ) : null}
          </div>
        )
      ) : null}
    </div>
  )
}
