import { useState } from 'react'
import { AlertTriangle, CheckCircle2, Clock, FileStack, FileText, Percent, Wallet } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { EmptyState } from '../../shared/components/EmptyState'
import { ErrorState } from '../../shared/components/ErrorState'
import { Spinner } from '../../shared/components/Spinner'
import { StatCard } from '../../shared/components/StatCard'
import { cn } from '../../shared/utils/cn'
import { formatCurrency } from '../../shared/utils/format'
import { formatDate } from '../../shared/utils/dates'
import { useAgingReport, useFinanceDashboard, useRevenueReport, type RevenueReportRow } from './api'
import { usePayables, type PayableListItem } from './expensesPayablesApi'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { StatIcon } from './components/StatIcon'

const tabs = [
  { id: 'revenue', label: 'Revenue Summary' },
  { id: 'receivables', label: 'Outstanding Receivables' },
  { id: 'collections', label: 'Collections' },
  { id: 'by-client', label: 'By Client' },
  { id: 'contract-pnl', label: 'Contract P&L' },
  { id: 'payables-aging', label: 'Payables Aging' },
] as const

function agingBucket(payable: PayableListItem) {
  const days = Math.floor((Date.now() - new Date(payable.dueDate).getTime()) / 86_400_000)
  if (days <= 0) return 'Current'
  if (days <= 30) return '1–30 days'
  if (days <= 60) return '31–60 days'
  return '60+ days'
}

function monthLabel(group: string) {
  const [year, month] = group.split('-')
  const names = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  const index = Number(month) - 1
  return names[index] ? `${names[index]} ${year}` : group
}

function RevenueTrendChart({ rows }: { rows: RevenueReportRow[] }) {
  if (rows.length === 0) {
    return <EmptyState title="No invoicing activity yet" description="Monthly invoiced and collected totals will chart here once invoices exist." />
  }

  const width = 720
  const height = 260
  const paddingX = 46
  const paddingY = 28
  const chartWidth = width - paddingX * 2
  const chartHeight = height - paddingY * 2
  const max = Math.max(...rows.map((row) => Math.max(row.invoiced, row.collected)), 1)
  const groupWidth = chartWidth / rows.length
  const barWidth = Math.min(26, groupWidth / 3)

  const gridLines = [0, 0.25, 0.5, 0.75, 1]

  return (
    <div className="space-y-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-slate-900">Invoiced vs collected by month</h3>
        <div className="flex items-center gap-4 text-xs text-slate-600">
          <span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-sm bg-[var(--app-primary)]" />Invoiced</span>
          <span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-sm bg-emerald-500" />Collected</span>
        </div>
      </div>
      <div className="overflow-x-auto">
        <svg viewBox={`0 0 ${width} ${height}`} className="h-64 w-full min-w-[560px]" role="img" aria-label="Monthly invoiced versus collected totals">
          {gridLines.map((fraction) => {
            const y = paddingY + chartHeight * (1 - fraction)
            return (
              <g key={fraction}>
                <line x1={paddingX} x2={width - paddingX} y1={y} y2={y} stroke="#e2e8f0" strokeWidth={1} />
                <text x={paddingX - 8} y={y + 4} textAnchor="end" fontSize={10} fill="#64748b">
                  {formatCurrency(max * fraction).replace(/\.00$/, '')}
                </text>
              </g>
            )
          })}
          {rows.map((row, index) => {
            const groupX = paddingX + index * groupWidth + groupWidth / 2
            const invoicedHeight = (row.invoiced / max) * chartHeight
            const collectedHeight = (row.collected / max) * chartHeight
            return (
              <g key={row.group}>
                <rect x={groupX - barWidth - 2} y={paddingY + chartHeight - invoicedHeight} width={barWidth} height={invoicedHeight} rx={3} fill="#0f172a">
                  <title>{`${monthLabel(row.group)} · Invoiced ${formatCurrency(row.invoiced)}`}</title>
                </rect>
                <rect x={groupX + 2} y={paddingY + chartHeight - collectedHeight} width={barWidth} height={collectedHeight} rx={3} fill="#10b981">
                  <title>{`${monthLabel(row.group)} · Collected ${formatCurrency(row.collected)}`}</title>
                </rect>
                <text x={groupX} y={height - 8} textAnchor="middle" fontSize={10} fill="#64748b">
                  {monthLabel(row.group)}
                </text>
              </g>
            )
          })}
        </svg>
      </div>
    </div>
  )
}

export function FinanceReportsPage() {
  const [tab, setTab] = useState<(typeof tabs)[number]['id']>('revenue')
  const dashboardQuery = useFinanceDashboard()
  const agingQuery = useAgingReport()
  const monthlyRevenue = useRevenueReport({ groupBy: 'month' })
  const clientRevenue = useRevenueReport({ groupBy: 'client' })
  const payablesQuery = usePayables('All')

  const { data: dashboard, isLoading } = dashboardQuery
  const { data: aging } = agingQuery
  const { data: payables, isLoading: payablesLoading } = payablesQuery
  /**
   * Money tiles are the most dangerous thing on this page to get wrong: "0 outstanding" reads as a
   * fact, not as a failed request. Whenever the dashboard query is in error every tile it feeds
   * falls back to an em dash and the row is preceded by an explicit retry banner.
   */
  const dashboardUnavailable = dashboardQuery.isError

  const payableColumns: DataTableColumn<PayableListItem>[] = [
    { key: 'billNumber', header: 'Bill No', cell: (row) => <span className="font-mono text-xs text-slate-700">{row.billNumber}</span> },
    { key: 'supplierName', header: 'Supplier', cell: (row) => row.supplierName },
    { key: 'dueDate', header: 'Due Date', cell: (row) => formatDate(row.dueDate) },
    { key: 'outstanding', header: 'Outstanding', cell: (row) => formatCurrency(row.amount - row.amountPaid) },
    { key: 'bucket', header: 'Age', cell: (row) => agingBucket(row) },
    { key: 'status', header: 'Status', cell: (row) => row.status },
  ]
  const outstandingPayables = (payables ?? []).filter((p) => p.amount - p.amountPaid > 0.005)
  const payablesTotal = outstandingPayables.reduce((sum, p) => sum + (p.amount - p.amountPaid), 0)

  const monthColumns: DataTableColumn<RevenueReportRow & { id: string }>[] = [
    { key: 'group', header: 'Month', cell: (row) => monthLabel(row.group) },
    { key: 'count', header: 'Invoices', cell: (row) => row.count },
    { key: 'invoiced', header: 'Invoiced', cell: (row) => formatCurrency(row.invoiced) },
    { key: 'collected', header: 'Collected', cell: (row) => <span className="text-emerald-700">{formatCurrency(row.collected)}</span> },
    { key: 'outstanding', header: 'Outstanding', cell: (row) => <span className={row.outstanding > 0 ? 'font-medium text-amber-600' : 'text-slate-700'}>{formatCurrency(row.outstanding)}</span> },
  ]

  const clientColumns: DataTableColumn<RevenueReportRow & { id: string }>[] = [
    { key: 'group', header: 'Client', cell: (row) => <span className="font-medium text-slate-900">{row.group}</span> },
    { key: 'count', header: 'Invoices', cell: (row) => row.count },
    { key: 'invoiced', header: 'Invoiced', cell: (row) => formatCurrency(row.invoiced) },
    { key: 'collected', header: 'Collected', cell: (row) => <span className="text-emerald-700">{formatCurrency(row.collected)}</span> },
    { key: 'outstanding', header: 'Outstanding', cell: (row) => <span className={row.outstanding > 0 ? 'font-medium text-amber-600' : 'text-slate-700'}>{formatCurrency(row.outstanding)}</span> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader title="Finance Reports" subtitle="Revenue trend, collections, per-client performance, and aging." />

      <FinanceWorkspaceNav />
      <div className="flex flex-wrap gap-2">
        {tabs.map((option) => (
          <button
            key={option.id}
            type="button"
            onClick={() => setTab(option.id)}
            className={cn('inline-flex h-10 items-center rounded-full border px-3 text-sm font-medium transition', tab === option.id ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}
          >
            {option.label}
          </button>
        ))}
      </div>

      {tab === 'revenue' ? (
        <>
          {dashboardUnavailable ? (
            <ErrorState
              variant="inline"
              title="Year-to-date totals unavailable."
              description="The finance dashboard could not be loaded, so no balances are shown."
              onRetry={() => void dashboardQuery.refetch()}
            />
          ) : null}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <StatCard label="Total Invoiced (YTD)" value={isLoading || dashboardUnavailable ? '—' : formatCurrency(dashboard?.totalInvoicedYtd ?? 0)} icon={<StatIcon tone="default"><FileText className="h-4 w-4" /></StatIcon>} />
            <StatCard label="Total Collected (YTD)" value={isLoading || dashboardUnavailable ? '—' : formatCurrency(dashboard?.totalCollectedYtd ?? 0)} tone="success" icon={<StatIcon tone="success"><Wallet className="h-4 w-4" /></StatIcon>} />
            <StatCard label="Outstanding" value={isLoading || dashboardUnavailable ? '—' : formatCurrency(dashboard?.outstandingBalance ?? 0)} tone="warning" icon={<StatIcon tone="warning"><Clock className="h-4 w-4" /></StatIcon>} />
            <StatCard label="Overdue" value={isLoading || dashboardUnavailable ? '—' : formatCurrency(dashboard?.overdueBalance ?? 0)} tone="danger" icon={<StatIcon tone="danger"><AlertTriangle className="h-4 w-4" /></StatIcon>} />
          </div>
          {monthlyRevenue.isError ? (
            <ErrorState
              title="Revenue trend unavailable"
              description="We couldn’t load monthly invoiced and collected totals, so an empty chart would be misleading."
              onRetry={() => void monthlyRevenue.refetch()}
            />
          ) : (
            <>
              {monthlyRevenue.isLoading ? <Spinner label="Loading revenue trend" /> : <RevenueTrendChart rows={monthlyRevenue.data ?? []} />}
              <DataTable
                columns={monthColumns}
                data={(monthlyRevenue.data ?? []).map((row) => ({ ...row, id: row.group }))}
                loading={monthlyRevenue.isLoading}
                emptyTitle="No revenue data"
                emptyDescription="Monthly revenue will appear here once invoices exist."
              />
            </>
          )}
        </>
      ) : null}

      {tab === 'receivables' ? (
        agingQuery.isError ? (
          <ErrorState
            title="Outstanding receivables unavailable"
            description="We couldn’t load the aging report — this is not a confirmation that nothing is outstanding."
            onRetry={() => void agingQuery.refetch()}
          />
        ) : (
          <DataTable
            columns={[
              { key: 'clientName', header: 'Client', cell: (row) => row.clientName },
              { key: 'total', header: 'Total Outstanding', cell: (row) => formatCurrency(row.total) },
            ] satisfies DataTableColumn<{ id: string; clientName: string; total: number }>[]}
            data={(aging ?? []).map((row) => ({ id: row.clientId, clientName: row.clientName, total: row.total }))}
            loading={agingQuery.isLoading}
            emptyTitle="No data"
            emptyDescription="No outstanding balances by client."
          />
        )
      ) : null}

      {tab === 'collections' ? (
        <div className="space-y-4">
          {dashboardUnavailable ? (
            <ErrorState
              variant="inline"
              title="Conversion figures unavailable."
              description="The finance dashboard could not be loaded, so no quote totals are shown."
              onRetry={() => void dashboardQuery.refetch()}
            />
          ) : null}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            <StatCard label="Quotations" value={isLoading || dashboardUnavailable ? '—' : dashboard?.totalQuotations ?? 0} icon={<StatIcon tone="default"><FileStack className="h-4 w-4" /></StatIcon>} />
            <StatCard label="Converted" value={isLoading || dashboardUnavailable ? '—' : dashboard?.convertedQuotations ?? 0} tone={dashboardUnavailable ? 'default' : 'success'} icon={<StatIcon tone="success"><CheckCircle2 className="h-4 w-4" /></StatIcon>} />
            <StatCard label="Conversion Rate" value={isLoading || dashboardUnavailable ? '—' : `${dashboard?.quoteConversionRate ?? 0}%`} icon={<StatIcon tone="default"><Percent className="h-4 w-4" /></StatIcon>} />
          </div>
        </div>
      ) : null}

      {tab === 'by-client' ? (
        clientRevenue.isError ? (
          <ErrorState
            title="Per-client revenue unavailable"
            description="We couldn’t load revenue grouped by client, so an empty table would be misleading."
            onRetry={() => void clientRevenue.refetch()}
          />
        ) : (
          <DataTable
            columns={clientColumns}
            data={(clientRevenue.data ?? []).map((row) => ({ ...row, id: row.group }))}
            loading={clientRevenue.isLoading}
            emptyTitle="No client revenue yet"
            emptyDescription="Invoiced, collected, and outstanding totals per client will appear here."
          />
        )
      ) : null}

      {tab === 'contract-pnl' ? (
        <EmptyState title="See Contract P&L tab" description="Contract-level P&L is composed per-contract on the Contract Detail page's P&L tab — there is no dedicated cross-contract P&L report endpoint." />
      ) : null}

      {tab === 'payables-aging' ? (
        payablesQuery.isError ? (
          <ErrorState
            title="Payables aging unavailable"
            description="We couldn’t load supplier bills, so a zero outstanding total would be a fabricated figure."
            onRetry={() => void payablesQuery.refetch()}
          />
        ) : (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
              {(['Current', '1–30 days', '31–60 days', '60+ days'] as const).map((bucket) => {
                const total = outstandingPayables.filter((p) => agingBucket(p) === bucket).reduce((sum, p) => sum + (p.amount - p.amountPaid), 0)
                return <StatCard key={bucket} label={bucket} value={payablesLoading ? '—' : formatCurrency(total)} tone={!payablesLoading && bucket === '60+ days' && total > 0 ? 'danger' : 'default'} />
              })}
            </div>
            <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 text-sm text-slate-600 shadow-sm">Total outstanding: <span className="font-semibold text-slate-900">{payablesLoading ? '—' : formatCurrency(payablesTotal)}</span> across {payablesLoading ? '—' : outstandingPayables.length} bill(s)</div>
            <DataTable columns={payableColumns} data={outstandingPayables} loading={payablesLoading} emptyTitle="No outstanding payables" emptyDescription="Unpaid supplier bills will appear here." />
          </div>
        )
      ) : null}
    </div>
  )
}
