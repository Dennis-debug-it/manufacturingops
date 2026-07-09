import { PageHeader } from '../shared/components/PageHeader'
import { StatCard } from '../shared/components/StatCard'
import { Spinner } from '../shared/components/Spinner'
import { usePortalDashboardSummary, usePortalInvoices, usePortalWorkOrders } from './api'

export function PortalReportsPage() {
  const { data: summary, isLoading: loadingSummary } = usePortalDashboardSummary()
  const { data: workOrders, isLoading: loadingWorkOrders } = usePortalWorkOrders()
  const { data: invoices, isLoading: loadingInvoices } = usePortalInvoices()

  if (loadingSummary || loadingWorkOrders || loadingInvoices) {
    return <Spinner label="Loading reports" />
  }

  const closedCount = (workOrders ?? []).filter((wo) => wo.status === 'Closed').length
  const completedCount = (workOrders ?? []).filter((wo) => wo.status === 'Completed' || wo.status === 'Client Accepted').length
  const totalInvoiced = (invoices ?? []).reduce((sum, invoice) => sum + invoice.totalAmount, 0)
  const totalPaid = (invoices ?? []).reduce((sum, invoice) => sum + invoice.amountPaid, 0)

  return (
    <div className="space-y-6">
      <PageHeader title="Reports" subtitle="A quick summary composed from your requests and invoices — a dedicated portal reporting endpoint doesn't exist yet, so these figures are derived client-side from the same data shown on Requests and Invoices." />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Total Requests" value={(workOrders ?? []).length} />
        <StatCard label="Completed" value={completedCount} tone="success" />
        <StatCard label="Closed" value={closedCount} />
        <StatCard label="Overdue" value={summary?.overdueWorkOrders ?? 0} tone={summary && summary.overdueWorkOrders > 0 ? 'danger' : 'default'} />
        <StatCard label="Total Invoiced" value={totalInvoiced.toLocaleString(undefined, { style: 'currency', currency: 'KES' })} />
        <StatCard label="Total Paid" value={totalPaid.toLocaleString(undefined, { style: 'currency', currency: 'KES' })} tone="success" />
        <StatCard label="Outstanding Balance" value={(summary?.outstandingInvoiceBalance ?? 0).toLocaleString(undefined, { style: 'currency', currency: 'KES' })} tone={summary && summary.outstandingInvoiceBalance > 0 ? 'warning' : 'default'} />
      </div>
    </div>
  )
}
