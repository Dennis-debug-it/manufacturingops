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
import { useAssetReliability, useTechnicianProductivity, useWorkOrderPerformance } from './api'

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
        <EmptyState title="Pending backend CSAT endpoint" description="No customer-satisfaction survey/CSAT controller exists in the current backend — this tab is a shell until one is available." />
      ) : null}
    </div>
  )
}
