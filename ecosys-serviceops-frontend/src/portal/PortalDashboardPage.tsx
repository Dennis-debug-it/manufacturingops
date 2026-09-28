import { Link } from 'react-router-dom'
import { AlertTriangle, Boxes, Clock, FileText, Receipt, Wrench } from 'lucide-react'
import { PageHeader } from '../shared/components/PageHeader'
import { Spinner } from '../shared/components/Spinner'
import { StatCard } from '../shared/components/StatCard'
import { usePortalAuth } from './auth/PortalAuthContext'
import { formatCurrencyAmounts, usePortalDashboardSummary, usePortalSettings } from './api'
import { PortalCsatPrompt } from './components/PortalCsatPrompt'

export function PortalDashboardPage() {
  const { user } = usePortalAuth()
  const { data, isLoading, isError } = usePortalDashboardSummary()
  const { data: settings } = usePortalSettings()

  if (isLoading) {
    return <Spinner label="Loading dashboard" />
  }

  if (isError || !data) {
    return <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-8 text-center text-sm text-slate-600">Dashboard could not be loaded.</div>
  }

  const hasBalance = data.outstandingBalances.some((entry) => entry.amount > 0)

  return (
    <div className="space-y-4">
      <PageHeader title={`Welcome, ${user?.fullName ?? 'there'}`} subtitle={settings?.welcomeMessage ?? "Here's what's happening across your account."} />
      <PortalCsatPrompt />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Link to="/portal/requests"><StatCard label="Open Requests" value={data.openWorkOrders} icon={<Wrench className="h-4 w-4 text-slate-400" />} /></Link>
        <Link to="/portal/requests"><StatCard label="Overdue Requests" value={data.overdueWorkOrders} tone={data.overdueWorkOrders > 0 ? 'danger' : 'default'} icon={<AlertTriangle className="h-4 w-4 text-slate-400" />} /></Link>
        {settings?.showAssetRegister ? (
          <Link to="/portal/assets"><StatCard label="Assets" value={data.assetCount} icon={<Boxes className="h-4 w-4 text-slate-400" />} /></Link>
        ) : null}
        <Link to="/portal/maintenance"><StatCard label="PM Due (30d)" value={data.upcomingPmCount} tone={data.upcomingPmCount > 0 ? 'warning' : 'default'} icon={<Clock className="h-4 w-4 text-slate-400" />} /></Link>
        {settings?.showInvoices ? (
          <Link to="/portal/invoices"><StatCard label="Outstanding Balance" value={formatCurrencyAmounts(data.outstandingBalances, data.currency)} tone={hasBalance ? 'warning' : 'default'} icon={<Receipt className="h-4 w-4 text-slate-400" />} /></Link>
        ) : null}
        <Link to="/portal/quotes"><StatCard label="Quotes Awaiting Response" value={data.pendingQuotations} tone={data.pendingQuotations > 0 ? 'warning' : 'default'} icon={<FileText className="h-4 w-4 text-slate-400" />} /></Link>
      </div>
      {settings?.allowRequestSubmission ? (
        <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
          <h2 className="text-sm font-semibold text-slate-900">Need something serviced?</h2>
          <p className="mt-1 text-sm text-slate-600">Submit a new service request and our team will be notified immediately.</p>
          <Link to="/portal/requests/new" className="mt-4 inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">Submit a Request</Link>
        </div>
      ) : null}
    </div>
  )
}
