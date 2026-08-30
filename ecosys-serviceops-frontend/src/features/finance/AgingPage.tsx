import { useState } from 'react'
import { Mail } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { formatCurrency } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { usePermission } from '../../shared/hooks/usePermission'
import { useAgingReport } from './api'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { ClientStatementDrawer } from './components/ClientStatementDrawer'
import type { AgingRow } from './types'

function bucketClass(value: number, total: number) {
  if (total === 0 || value === 0) return 'text-slate-700'
  const ratio = value / total
  return ratio > 0.5 ? 'font-semibold text-rose-600' : ratio > 0.2 ? 'font-semibold text-amber-600' : 'text-slate-700'
}

export function AgingPage() {
  const canView = usePermission('finance.view')
  const { data, isLoading, isError } = useAgingReport()
  const [statementClient, setStatementClient] = useState<{ id: string; name: string } | null>(null)

  const columns: DataTableColumn<AgingRow & { id: string }>[] = [
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName },
    { key: 'current', header: 'Current', cell: (row) => formatCurrency(row.current) },
    { key: 'days1To30', header: '1-30 Days', cell: (row) => <span className={cn(bucketClass(row.days1To30, row.total))}>{formatCurrency(row.days1To30)}</span> },
    { key: 'days31To60', header: '31-60 Days', cell: (row) => <span className={cn(bucketClass(row.days31To60, row.total))}>{formatCurrency(row.days31To60)}</span> },
    { key: 'days61To90', header: '61-90 Days', cell: (row) => <span className={cn(bucketClass(row.days61To90, row.total))}>{formatCurrency(row.days61To90)}</span> },
    { key: 'days90Plus', header: '90+ Days', cell: (row) => <span className={cn('font-semibold', row.days90Plus > 0 ? 'text-rose-600' : 'text-slate-700')}>{formatCurrency(row.days90Plus)}</span> },
    { key: 'total', header: 'Total', cell: (row) => <span className="font-semibold text-slate-900">{formatCurrency(row.total)}</span> },
    {
      key: 'actions',
      header: '',
      cell: (row) =>
        canView ? (
          <button
            type="button"
            onClick={() => setStatementClient({ id: row.clientId, name: row.clientName })}
            className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50"
          >
            <Mail className="h-3.5 w-3.5" />
            Statement
          </button>
        ) : null,
    },
  ]

  const rows = (data ?? []).map((row) => ({ ...row, id: row.clientId }))
  const totals = rows.reduce(
    (sum, row) => ({
      current: sum.current + row.current,
      days1To30: sum.days1To30 + row.days1To30,
      days31To60: sum.days31To60 + row.days31To60,
      days61To90: sum.days61To90 + row.days61To90,
      days90Plus: sum.days90Plus + row.days90Plus,
      total: sum.total + row.total,
    }),
    { current: 0, days1To30: 0, days31To60: 0, days61To90: 0, days90Plus: 0, total: 0 },
  )

  return (
    <div className="space-y-6">
      <PageHeader title="Aging" subtitle="Receivables aging buckets, colour-coded by days overdue. Send a statement straight from any row." />
      <FinanceWorkspaceNav />
      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 text-sm text-slate-600 shadow-sm">
        Total outstanding: <span className="font-semibold text-slate-900">{formatCurrency(totals.total)}</span>
      </div>
      <DataTable columns={columns} data={rows} loading={isLoading} emptyTitle={isError ? 'Aging unavailable' : 'No outstanding balances'} emptyDescription={isError ? 'Aging report could not be loaded.' : 'All invoices are settled.'} />

      <ClientStatementDrawer
        open={Boolean(statementClient)}
        clientId={statementClient?.id ?? null}
        clientName={statementClient?.name ?? null}
        onClose={() => setStatementClient(null)}
      />
    </div>
  )
}
