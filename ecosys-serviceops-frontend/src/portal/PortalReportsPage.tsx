import { PageHeader } from '../shared/components/PageHeader'
import { StatCard } from '../shared/components/StatCard'
import { Spinner } from '../shared/components/Spinner'
import { EmptyState } from '../shared/components/EmptyState'
import { formatCurrency } from '../shared/utils/format'
import { usePortalReportSummary, usePortalSettings } from './api'

export function PortalReportsPage() {
  const { data, isLoading, isError } = usePortalReportSummary()
  const { data: settings } = usePortalSettings()

  if (isLoading) {
    return <Spinner label="Loading reports" />
  }

  if (isError || !data) {
    return <EmptyState title="Reports unavailable" description="Reports could not be loaded right now." />
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Reports" subtitle="A summary of your service requests and billing." />

      <section className="space-y-3">
        <h2 className="text-sm font-semibold text-slate-900">Service Requests</h2>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <StatCard label="Total Requests" value={data.totalRequests} />
          <StatCard label="Overdue (open, past due)" value={data.overdueRequests} tone={data.overdueRequests > 0 ? 'danger' : 'default'} />
        </div>
        {/* Each request is counted in exactly one of these four, so they add up to the total. */}
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <StatCard label="Open" value={data.openRequests} />
          <StatCard label="Completed" value={data.completedRequests} tone="success" />
          <StatCard label="Closed" value={data.closedRequests} />
          <StatCard label="Cancelled" value={data.cancelledRequests} />
        </div>
      </section>

      {settings?.showInvoices ? (
        <section className="space-y-3">
          <h2 className="text-sm font-semibold text-slate-900">Billing</h2>
          {data.invoiceTotals.length === 0 ? (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <StatCard label="Total Invoiced" value={formatCurrency(0, data.currency)} />
              <StatCard label="Total Paid" value={formatCurrency(0, data.currency)} />
              <StatCard label="Outstanding Balance" value={formatCurrency(0, data.currency)} />
            </div>
          ) : (
            data.invoiceTotals.map((totals) => (
              <div key={totals.currency} className="space-y-2">
                {data.invoiceTotals.length > 1 ? <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">{totals.currency}</div> : null}
                <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                  <StatCard label="Total Invoiced" value={formatCurrency(totals.invoiced, totals.currency)} />
                  <StatCard label="Total Paid" value={formatCurrency(totals.paid, totals.currency)} tone="success" />
                  <StatCard label="Outstanding Balance" value={formatCurrency(totals.outstanding, totals.currency)} tone={totals.outstanding > 0 ? 'warning' : 'default'} />
                </div>
              </div>
            ))
          )}
        </section>
      ) : null}
    </div>
  )
}
