import { useState } from 'react'
import { Ban, Clock, Eye, Mail, Users } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatCard } from '../../shared/components/StatCard'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatCurrency } from '../../shared/utils/format'
import { usePermission } from '../../shared/hooks/usePermission'
import { useCreditSummaries, type ClientCreditSummaryRow } from './api'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { StatIcon } from './components/StatIcon'
import { ClientStatementDrawer } from './components/ClientStatementDrawer'

function stage(summary: ClientCreditSummaryRow): 'Good Standing' | 'Watch' | 'Hold' {
  if (summary.overdueAmount <= 0) return 'Good Standing'
  if (summary.creditLimit != null && summary.currentBalance > summary.creditLimit) return 'Hold'
  return 'Watch'
}

export function CreditControlPage() {
  const canView = usePermission('finance.view')
  const { data, isLoading, isError } = useCreditSummaries()
  const [statementClient, setStatementClient] = useState<{ id: string; name: string } | null>(null)

  const rows = (data ?? []).map((summary) => ({ ...summary, id: summary.clientId, stage: stage(summary) }))

  const onHold = rows.filter((row) => row.stage === 'Hold').length
  const watch = rows.filter((row) => row.stage === 'Watch').length
  const totalOverdue = rows.reduce((sum, row) => sum + row.overdueAmount, 0)

  const columns: DataTableColumn<(typeof rows)[number]>[] = [
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName },
    { key: 'currentBalance', header: 'Balance', cell: (row) => formatCurrency(row.currentBalance) },
    { key: 'creditLimit', header: 'Credit Limit', cell: (row) => (row.creditLimit != null ? formatCurrency(row.creditLimit) : 'No limit') },
    { key: 'overdueAmount', header: 'Overdue', cell: (row) => formatCurrency(row.overdueAmount) },
    { key: 'termName', header: 'Terms', cell: (row) => row.termName ?? '—' },
    { key: 'stage', header: 'Stage', cell: (row) => <StatusBadge status={row.stage} /> },
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

  return (
    <div className="space-y-6">
      <PageHeader
        title="Credit Control"
        subtitle="Stages, hold state, and follow-up actions. Stages are derived from credit limit vs. overdue balance; send a statement straight from any row."
      />

      <FinanceWorkspaceNav />
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatCard label="Clients Tracked" value={rows.length} icon={<StatIcon tone="default"><Users className="h-4 w-4" /></StatIcon>} />
        <StatCard label="On Watch" value={watch} tone={watch > 0 ? 'warning' : 'success'} icon={<StatIcon tone={watch > 0 ? 'warning' : 'success'}><Eye className="h-4 w-4" /></StatIcon>} />
        <StatCard label="On Hold" value={onHold} tone={onHold > 0 ? 'danger' : 'success'} icon={<StatIcon tone={onHold > 0 ? 'danger' : 'success'}><Ban className="h-4 w-4" /></StatIcon>} />
        <StatCard label="Total Overdue" value={formatCurrency(totalOverdue)} tone={totalOverdue > 0 ? 'warning' : 'success'} icon={<StatIcon tone={totalOverdue > 0 ? 'warning' : 'success'}><Clock className="h-4 w-4" /></StatIcon>} />
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Credit data unavailable' : 'No credit data'}
        emptyDescription={isError ? 'Client credit summaries could not be loaded.' : 'Client credit summaries will appear here.'}
      />

      <ClientStatementDrawer
        open={Boolean(statementClient)}
        clientId={statementClient?.id ?? null}
        clientName={statementClient?.name ?? null}
        onClose={() => setStatementClient(null)}
      />
    </div>
  )
}
