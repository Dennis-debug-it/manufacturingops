import type { ReactNode } from 'react'
import { PageHeader } from '../shared/components/PageHeader'
import { AsyncPanel } from '../shared/components/AsyncPanel'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { formatDate } from '../shared/utils/dates'
import { formatCurrency } from '../shared/utils/format'
import { usePlatformReportsAudit, usePlatformReportsRevenue, usePlatformReportsSubscriptions, usePlatformReportsSummary, usePlatformReportsTenants } from './api'
import type { PlatformReportsAuditRow, PlatformReportsRevenueRow, PlatformReportsSubscriptionRow, PlatformReportsTenantRow } from './types'

export function ReportsPage() {
  const summary = usePlatformReportsSummary()
  const tenants = usePlatformReportsTenants()
  const revenue = usePlatformReportsRevenue()
  const subscriptions = usePlatformReportsSubscriptions()
  const audit = usePlatformReportsAudit()

  const tenantColumns: DataTableColumn<PlatformReportsTenantRow>[] = [
    { key: 'name', header: 'Tenant', cell: (row) => row.name },
    { key: 'status', header: 'Status', cell: (row) => row.status },
    { key: 'active', header: 'Active', cell: (row) => String(row.isActive) },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDate(row.createdAt) },
  ]

  const subscriptionColumns: DataTableColumn<PlatformReportsSubscriptionRow>[] = [
    { key: 'tenantName', header: 'Tenant', cell: (row) => row.tenantName ?? '—' },
    { key: 'planName', header: 'Plan', cell: (row) => row.planName ?? '—' },
    { key: 'status', header: 'Status', cell: (row) => row.status },
    { key: 'billingCycle', header: 'Billing', cell: (row) => row.billingCycle },
  ]

  const auditColumns: DataTableColumn<PlatformReportsAuditRow>[] = [
    { key: 'createdAt', header: 'When', cell: (row) => formatDate(row.createdAt) },
    { key: 'actorName', header: 'Actor', cell: (row) => row.actorName ?? 'System' },
    { key: 'action', header: 'Action', cell: (row) => row.action },
    { key: 'entityType', header: 'Entity', cell: (row) => `${row.entityType} · ${row.entityId}` },
    { key: 'severity', header: 'Severity', cell: (row) => row.severity },
  ]

  return (
    <div className="space-y-5">
      <PageHeader title="Reports" subtitle="Platform growth, finance, and activity reporting." />

      <AsyncPanel query={summary} loadingLabel="Loading summary" errorTitle="Summary unavailable" errorDescription="Report summary could not be loaded.">
        {(summaryData) => (
          <section className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
            <Kpi label="Total tenants" value={summaryData.totalTenants} />
            <Kpi label="Active tenants" value={summaryData.activeTenants} />
            <Kpi label="Total revenue" value={formatCurrency(summaryData.totalRevenue)} />
            <Kpi label="Outstanding" value={formatCurrency(summaryData.outstandingBalance)} />
          </section>
        )}
      </AsyncPanel>

      <section className="grid grid-cols-1 gap-5 xl:grid-cols-2">
        <Panel title="Tenant growth">
          <AsyncPanel query={tenants} errorTitle="Unavailable" errorDescription="Tenant growth data could not be loaded.">
            {(data) => <DataTable<PlatformReportsTenantRow> columns={tenantColumns} data={data} emptyTitle="No tenant report data" emptyDescription="Tenant growth records will appear here." />}
          </AsyncPanel>
        </Panel>
        <Panel title="Subscription funnel">
          <AsyncPanel query={subscriptions} errorTitle="Unavailable" errorDescription="Subscription data could not be loaded.">
            {(data) => <DataTable<PlatformReportsSubscriptionRow> columns={subscriptionColumns} data={data} emptyTitle="No subscription report data" emptyDescription="Subscription records will appear here." />}
          </AsyncPanel>
        </Panel>
        <Panel title="Revenue summary">
          <AsyncPanel query={revenue} errorTitle="Unavailable" errorDescription="Revenue data could not be loaded.">
            {(data) => {
              const revenueRow = data[0] as PlatformReportsRevenueRow | undefined
              return (
                <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                  <Kpi label="Revenue" value={formatCurrency(revenueRow?.revenue ?? revenueRow?.totalRevenue ?? 0)} />
                  <Kpi label="Expenses" value={formatCurrency(revenueRow?.expenses ?? revenueRow?.totalExpenses ?? 0)} />
                  <Kpi label="Net" value={formatCurrency(revenueRow?.net ?? revenueRow?.netPosition ?? 0)} />
                  <Kpi label="Outstanding" value={formatCurrency(revenueRow?.outstandingBalance ?? 0)} />
                </div>
              )
            }}
          </AsyncPanel>
        </Panel>
        <Panel title="Platform activity">
          <AsyncPanel query={audit} errorTitle="Unavailable" errorDescription="Activity data could not be loaded.">
            {(data) => <DataTable<PlatformReportsAuditRow> columns={auditColumns} data={data} emptyTitle="No audit data" emptyDescription="Activity rows will appear here." />}
          </AsyncPanel>
        </Panel>
      </section>
    </div>
  )
}

function Kpi({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
      <div className="mt-2 text-2xl font-semibold text-slate-900">{value}</div>
    </div>
  )
}

function Panel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <p className="text-base font-semibold text-slate-900">{title}</p>
      <div className="mt-4">{children}</div>
    </section>
  )
}
