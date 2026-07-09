import { useState } from 'react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatCard } from '../../shared/components/StatCard'
import { cn } from '../../shared/utils/cn'
import { formatCurrency } from '../../shared/utils/format'
import { useAgingReport, useFinanceDashboard, useRevenueReport, type RevenueReportRow } from './api'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'

const tabs = [
  { id: 'revenue', label: 'Revenue Summary' },
  { id: 'receivables', label: 'Outstanding Receivables' },
  { id: 'collections', label: 'Collections' },
  { id: 'by-client', label: 'By Client' },
  { id: 'contract-pnl', label: 'Contract P&L' },
  { id: 'payables-aging', label: 'Payables Aging' },
] as const

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
    <div className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-slate-900">Invoiced vs collected by month</h3>
        <div className="flex items-center gap-4 text-xs text-slate-600">
          <span className="inline-flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-sm bg-slate-900" />Invoiced</span>
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
  const { data: dashboard, isLoading } = useFinanceDashboard()
  const { data: aging } = useAgingReport()
  const monthlyRevenue = useRevenueReport({ groupBy: 'month' })
  const clientRevenue = useRevenueReport({ groupBy: 'client' })

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
    <div className="space-y-6">
      <PageHeader title="Finance Reports" subtitle="Revenue trend, collections, per-client performance, and aging." />

      <FinanceWorkspaceNav />
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

      {tab === 'revenue' ? (
        <>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <StatCard label="Total Invoiced (YTD)" value={isLoading ? '—' : formatCurrency(dashboard?.totalInvoicedYtd ?? 0)} />
            <StatCard label="Total Collected (YTD)" value={isLoading ? '—' : formatCurrency(dashboard?.totalCollectedYtd ?? 0)} tone="success" />
            <StatCard label="Outstanding" value={isLoading ? '—' : formatCurrency(dashboard?.outstandingBalance ?? 0)} tone="warning" />
            <StatCard label="Overdue" value={isLoading ? '—' : formatCurrency(dashboard?.overdueBalance ?? 0)} tone="danger" />
          </div>
          {monthlyRevenue.isLoading ? <Spinner label="Loading revenue trend" /> : <RevenueTrendChart rows={monthlyRevenue.data ?? []} />}
          <DataTable
            columns={monthColumns}
            data={(monthlyRevenue.data ?? []).map((row) => ({ ...row, id: row.group }))}
            loading={monthlyRevenue.isLoading}
            emptyTitle="No revenue data"
            emptyDescription="Monthly revenue will appear here once invoices exist."
          />
        </>
      ) : null}

      {tab === 'receivables' ? (
        <DataTable
          columns={[
            { key: 'clientName', header: 'Client', cell: (row) => row.clientName },
            { key: 'total', header: 'Total Outstanding', cell: (row) => formatCurrency(row.total) },
          ] satisfies DataTableColumn<{ id: string; clientName: string; total: number }>[]}
          data={(aging ?? []).map((row) => ({ id: row.clientId, clientName: row.clientName, total: row.total }))}
          emptyTitle="No data"
          emptyDescription="No outstanding balances by client."
        />
      ) : null}

      {tab === 'collections' ? (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          <StatCard label="Quotations" value={dashboard?.totalQuotations ?? 0} />
          <StatCard label="Converted" value={dashboard?.convertedQuotations ?? 0} tone="success" />
          <StatCard label="Conversion Rate" value={`${dashboard?.quoteConversionRate ?? 0}%`} />
        </div>
      ) : null}

      {tab === 'by-client' ? (
        <DataTable
          columns={clientColumns}
          data={(clientRevenue.data ?? []).map((row) => ({ ...row, id: row.group }))}
          loading={clientRevenue.isLoading}
          emptyTitle="No client revenue yet"
          emptyDescription="Invoiced, collected, and outstanding totals per client will appear here."
        />
      ) : null}

      {tab === 'contract-pnl' ? (
        <EmptyState title="See Contract P&L tab" description="Contract-level P&L is composed per-contract on the Contract Detail page's P&L tab — there is no dedicated cross-contract P&L report endpoint." />
      ) : null}

      {tab === 'payables-aging' ? (
        <EmptyState title="Pending backend Payables endpoint" description="No tenant-level Payables controller exists yet, so a payables aging report cannot be built against real data." />
      ) : null}
    </div>
  )
}
