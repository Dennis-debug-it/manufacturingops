import { Link } from 'react-router-dom'
import { ArrowRight, Clock, FileText, Receipt, Wallet } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { EmptyState } from '../../shared/components/EmptyState'
import { StatCard } from '../../shared/components/StatCard'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useAgingReport, useFinanceDashboard, useInvoices, useQuotations } from './api'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { StatIcon } from './components/StatIcon'
import type { AgingRow, InvoiceListItem, QuotationListItem } from './types'

function SectionLink({ to, label }: { to: string; label: string }) {
  return (
    <Link to={to} className="inline-flex items-center gap-2 text-sm font-medium text-slate-700 transition hover:text-slate-900">
      {label}
      <ArrowRight className="h-4 w-4" />
    </Link>
  )
}

export function FinanceOverviewPage() {
  const canManage = usePermission('finance.manage')
  const { data: dashboard, isLoading: dashboardLoading, isError: dashboardError } = useFinanceDashboard()
  const { data: invoices, isLoading: invoicesLoading, isError: invoicesError } = useInvoices()
  const { data: quotes, isLoading: quotesLoading, isError: quotesError } = useQuotations()
  const { data: aging, isLoading: agingLoading, isError: agingError } = useAgingReport()

  const invoiceRows = [...(invoices ?? [])].sort((left, right) => right.invoiceDate.localeCompare(left.invoiceDate)).slice(0, 5)
  const quoteRows = [...(quotes ?? [])].sort((left, right) => right.quoteDate.localeCompare(left.quoteDate)).slice(0, 5)
  const agingRows = [...(aging ?? [])].filter((row) => row.total > 0).sort((left, right) => right.days90Plus - left.days90Plus || right.total - left.total).slice(0, 5)
  const openBalances = (invoices ?? []).filter((invoice) => invoice.balance > 0 && invoice.status.toLowerCase() !== 'paid')
  const outstandingBalance = openBalances.reduce((sum, invoice) => sum + invoice.balance, 0)
  // The currency this workspace actually invoices in. Null when nothing has been invoiced yet, in
  // which case formatCurrency falls back to the workspace currency rather than a literal.
  const invoiceCurrency = invoices?.[0]?.currency ?? dashboard?.currency ?? null

  const invoiceColumns: DataTableColumn<InvoiceListItem>[] = [
    { key: 'invoiceNumber', header: 'Invoice', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/finance/invoices/${row.id}`}>{row.invoiceNumber}</Link> },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '-' },
    { key: 'dueDate', header: 'Due', cell: (row) => formatDate(row.dueDate) },
    { key: 'balance', header: 'Balance', cell: (row) => formatCurrency(row.balance, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  const quoteColumns: DataTableColumn<QuotationListItem>[] = [
    { key: 'quoteNumber', header: 'Quote', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/finance/quotes/${row.id}`}>{row.quoteNumber}</Link> },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '-' },
    { key: 'validUntil', header: 'Valid Until', cell: (row) => formatDate(row.validUntil) },
    { key: 'totalAmount', header: 'Total', cell: (row) => formatCurrency(row.totalAmount, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  const agingColumns: DataTableColumn<AgingRow & { id: string }>[] = [
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName },
    { key: 'days90Plus', header: '90+ Days', cell: (row) => <span className={row.days90Plus > 0 ? 'font-semibold text-rose-600' : 'text-slate-700'}>{formatCurrency(row.days90Plus, invoiceCurrency)}</span> },
    { key: 'total', header: 'Total Outstanding', cell: (row) => <span className="font-semibold text-slate-900">{formatCurrency(row.total, invoiceCurrency)}</span> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Finance"
        subtitle="Revenue, receivables, pricing controls, and billing operations in one workspace."
        primaryAction={
          canManage ? (
            <Link to="/finance/invoices" className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Receipt className="h-4 w-4" />
              Open Invoices
            </Link>
          ) : undefined
        }
        secondaryActions={
          <>
            <SectionLink to="/finance/quotes" label="Quotes" />
            <SectionLink to="/finance/rate-cards" label="Rate Cards" />
          </>
        }
      />

      <FinanceWorkspaceNav />

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {/* A failed dashboard must read as "unknown", not as a real balance of zero. */}
        <StatCard label="Invoiced YTD" value={dashboardLoading || dashboardError ? '—' : formatCurrency(dashboard?.totalInvoicedYtd ?? 0, dashboard?.currency)} icon={<StatIcon tone="default"><FileText className="h-4 w-4" /></StatIcon>} />
        <StatCard label="Collected YTD" value={dashboardLoading || dashboardError ? '—' : formatCurrency(dashboard?.totalCollectedYtd ?? 0, dashboard?.currency)} tone={dashboardError ? 'default' : 'success'} icon={<StatIcon tone="success"><Wallet className="h-4 w-4" /></StatIcon>} />
        <StatCard label="Outstanding" value={invoicesLoading || invoicesError ? '—' : formatCurrency(outstandingBalance, invoiceCurrency)} tone={invoicesError ? 'default' : outstandingBalance > 0 ? 'warning' : 'success'} icon={<StatIcon tone={outstandingBalance > 0 ? 'warning' : 'success'}><Clock className="h-4 w-4" /></StatIcon>} />
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1.35fr)_minmax(0,1fr)]">
        <div className="space-y-6">
          <section className="space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
            <div className="flex items-center justify-between gap-3">
              <div>
                <h2 className="text-lg font-semibold text-slate-900">Recent invoices</h2>
                <p className="text-sm text-slate-600">Latest billing activity and current payment posture.</p>
              </div>
              <SectionLink to="/finance/invoices" label="View all" />
            </div>
            <DataTable columns={invoiceColumns} data={invoiceRows} loading={invoicesLoading} emptyTitle={invoicesError ? 'Invoices unavailable' : 'No invoices yet'} emptyDescription={invoicesError ? 'Invoice data could not be loaded.' : 'Create your first invoice to start tracking collections.'} />
          </section>

          <section className="space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
            <div className="flex items-center justify-between gap-3">
              <div>
                <h2 className="text-lg font-semibold text-slate-900">Recent quotes</h2>
                <p className="text-sm text-slate-600">Open commercial opportunities and conversion progress.</p>
              </div>
              <SectionLink to="/finance/quotes" label="View all" />
            </div>
            <DataTable columns={quoteColumns} data={quoteRows} loading={quotesLoading} emptyTitle={quotesError ? 'Quotes unavailable' : 'No quotes yet'} emptyDescription={quotesError ? 'Quote data could not be loaded.' : 'Create your first quote to start commercial tracking.'} />
          </section>
        </div>

        <div className="space-y-6">
          <section className="space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
            <div className="flex items-center justify-between gap-3">
              <div>
                <h2 className="text-lg font-semibold text-slate-900">Receivables watchlist</h2>
                <p className="text-sm text-slate-600">Clients with the most overdue exposure right now.</p>
              </div>
              <SectionLink to="/finance/aging" label="Open aging" />
            </div>
            <DataTable columns={agingColumns} data={agingRows.map((row) => ({ ...row, id: row.clientId }))} loading={agingLoading} emptyTitle={agingError ? 'Aging unavailable' : 'No overdue balances'} emptyDescription={agingError ? 'Receivables aging could not be loaded.' : 'Outstanding client balances will appear here when invoices age.'} />
          </section>

          <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
            <h2 className="text-lg font-semibold text-slate-900">Pricing and controls</h2>
            <p className="mt-1 text-sm text-slate-600">Jump straight to the places that affect rate application, approvals, and collection follow-up.</p>
            <div className="mt-4 grid gap-3">
              {[
                { to: '/finance/rate-cards', icon: FileText, title: 'Rate cards', description: 'Maintain standard labour, material, and service rates.' },
                { to: '/finance/preferential-rates', icon: Wallet, title: 'Preferential rates', description: 'Manage client-specific pricing overrides and bulk imports.' },
                { to: '/finance/invoice-approvals', icon: Receipt, title: 'Approval rules', description: 'Review threshold-based approval routing for draft invoices.' },
              ].map((item) => (
                <Link key={item.to} to={item.to} className="rounded-xl border border-slate-200 px-4 py-3 transition hover:border-slate-300 hover:bg-slate-50">
                  <div className="flex items-start gap-3">
                    <span className="mt-0.5 rounded-lg bg-slate-100 p-2 text-slate-700"><item.icon className="h-4 w-4" /></span>
                    <div>
                      <p className="text-sm font-semibold text-slate-900">{item.title}</p>
                      <p className="mt-1 text-sm text-slate-600">{item.description}</p>
                    </div>
                  </div>
                </Link>
              ))}
            </div>
          </section>
        </div>
      </div>

      {!dashboardLoading && !dashboard && !invoices?.length && !quotes?.length ? (
        <EmptyState title="Finance workspace is ready" description="Create quotes, invoices, or rate cards to start populating the finance overview." />
      ) : null}
    </div>
  )
}
