import type { ReactNode } from 'react'
import { PageHeader } from '../shared/components/PageHeader'
import { Spinner } from '../shared/components/Spinner'
import { EmptyState } from '../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { formatDate } from '../shared/utils/dates'
import { formatCurrency } from '../shared/utils/format'
import { usePlatformReportsAudit, usePlatformReportsRevenue, usePlatformReportsSubscriptions, usePlatformReportsSummary, usePlatformReportsTenants } from './api'
import type { PlatformReportsAuditRow, PlatformReportsRevenueRow, PlatformReportsSubscriptionRow, PlatformReportsTenantRow, PlatformReportsSummary } from './types'

export function ReportsPage() {
  const summary = usePlatformReportsSummary()
  const tenants = usePlatformReportsTenants()
  const revenue = usePlatformReportsRevenue()
  const subscriptions = usePlatformReportsSubscriptions()
  const audit = usePlatformReportsAudit()

  if (summary.isLoading || tenants.isLoading || revenue.isLoading || subscriptions.isLoading || audit.isLoading) {
    return <Spinner label="Loading reports" />
  }

  if (summary.isError || tenants.isError || revenue.isError || subscriptions.isError || audit.isError) {
    return <EmptyState title="Reports unavailable" description="Platform reports could not be loaded." />
  }

  const summaryData = summary.data as PlatformReportsSummary
  const revenueRow = revenue.data?.[0] as PlatformReportsRevenueRow | undefined

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
      <section className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
        <Kpi label="Total tenants" value={summaryData.totalTenants} />
        <Kpi label="Active tenants" value={summaryData.activeTenants} />
        <Kpi label="Total revenue" value={formatCurrency(summaryData.totalRevenue)} />
        <Kpi label="Outstanding" value={formatCurrency(summaryData.outstandingBalance)} />
      </section>

      <section className="grid grid-cols-1 gap-5 xl:grid-cols-2">
        <Panel title="Tenant growth">
          <DataTable<PlatformReportsTenantRow> columns={tenantColumns} data={tenants.data ?? []} emptyTitle="No tenant report data" emptyDescription="Tenant growth records will appear here." />
        </Panel>
        <Panel title="Subscription funnel">
          <DataTable<PlatformReportsSubscriptionRow> columns={subscriptionColumns} data={subscriptions.data ?? []} emptyTitle="No subscription report data" emptyDescription="Subscription records will appear here." />
        </Panel>
        <Panel title="Revenue summary">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <Kpi label="Revenue" value={formatCurrency(revenueRow?.revenue ?? revenueRow?.totalRevenue ?? 0)} />
            <Kpi label="Expenses" value={formatCurrency(revenueRow?.expenses ?? revenueRow?.totalExpenses ?? 0)} />
            <Kpi label="Net" value={formatCurrency(revenueRow?.net ?? revenueRow?.netPosition ?? 0)} />
            <Kpi label="Outstanding" value={formatCurrency(revenueRow?.outstandingBalance ?? 0)} />
          </div>
        </Panel>
        <Panel title="Platform activity">
          <DataTable<PlatformReportsAuditRow> columns={auditColumns} data={audit.data ?? []} emptyTitle="No audit data" emptyDescription="Activity rows will appear here." />
        </Panel>
      </section>
    </div>
  )
}

function Kpi({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
      <div className="mt-2 text-2xl font-semibold text-slate-900">{value}</div>
    </div>
  )
}

function Panel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <p className="text-base font-semibold text-slate-900">{title}</p>
      <div className="mt-4">{children}</div>
    </section>
  )
}
