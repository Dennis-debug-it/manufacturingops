import { Plus } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { DataTable } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageScaffold, SearchToolbar, SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { contractService } from '../../services/contractService'
import type { ContractListItem } from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import { contractRenewalRiskOptions, contractStatusOptions, formatContractLabel, renderBillingModelBadge, renderContractStatusBadge, renderRenewalRiskBadge } from './contractUi'

export function ContractsPage() {
  const navigate = useNavigate()
  const [statusFilter, setStatusFilter] = useState('')
  const [riskFilter, setRiskFilter] = useState('')
  const [expiringWithinDays, setExpiringWithinDays] = useState('')

  const { data, loading, error, reload } = useAsyncData<ContractListItem[]>(
    (signal) => contractService.list({
      status: statusFilter || null,
      renewalRisk: riskFilter || null,
      expiringWithinDays: expiringWithinDays ? Number(expiringWithinDays) : null,
    }, signal),
    [],
    [statusFilter, riskFilter, expiringWithinDays],
  )

  const metrics = useMemo(() => ({
    active: data.filter((item) => item.status === 'Active').length,
    expiringSoon: data.filter((item) => item.daysToExpiry <= 30).length,
    highRisk: data.filter((item) => item.renewalRisk === 'High' || item.renewalRisk === 'Critical').length,
    expired: data.filter((item) => item.status === 'Expired' || item.daysToExpiry < 0).length,
  }), [data])

  return (
    <PageScaffold
      eyebrow="Contracts"
      title="Contract management"
      description="Coverage, SLA, renewal risk, and billing."
      actions={(
        <div className="flex flex-wrap gap-2">
          <button type="button" className="button-secondary" onClick={() => void reload()}>Refresh</button>
          <Link to="/contracts/new" className="button-primary">
            <Plus className="h-4 w-4" />
            New contract
          </Link>
        </div>
      )}
    >
      <div className="flex flex-wrap gap-2">
        <SummaryChip label="Active" value={metrics.active} />
        <SummaryChip label="Expiring in 30 days" value={metrics.expiringSoon} tone="warning" />
        <SummaryChip label="High / critical risk" value={metrics.highRisk} tone="danger" />
        <SummaryChip label="Expired" value={metrics.expired} tone="danger" />
      </div>

      <SectionCard title="Contracts" description="Filter by status, risk, and expiry window.">
        <SearchToolbar
          filters={(
            <>
              <label className="space-y-2">
                <span className="text-sm font-medium text-app">Status</span>
                <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)} className="field-input">
                  <option value="">All statuses</option>
                  {contractStatusOptions.map((item) => <option key={item} value={item}>{formatContractLabel(item)}</option>)}
                </select>
              </label>
              <label className="space-y-2">
                <span className="text-sm font-medium text-app">Renewal risk</span>
                <select value={riskFilter} onChange={(event) => setRiskFilter(event.target.value)} className="field-input">
                  <option value="">All risks</option>
                  {contractRenewalRiskOptions.map((item) => <option key={item} value={item}>{item}</option>)}
                </select>
              </label>
              <label className="space-y-2">
                <span className="text-sm font-medium text-app">Expiring within</span>
                <select value={expiringWithinDays} onChange={(event) => setExpiringWithinDays(event.target.value)} className="field-input">
                  <option value="">Any time</option>
                  <option value="90">90 days</option>
                  <option value="60">60 days</option>
                  <option value="30">30 days</option>
                </select>
              </label>
            </>
          )}
          actions={(
            <button type="button" className="button-secondary" onClick={() => { setStatusFilter(''); setRiskFilter(''); setExpiringWithinDays('') }}>
              Clear filters
            </button>
          )}
        />

        {loading ? <LoadingState label="Loading contracts" /> : null}
        {!loading && error ? <ErrorState title="Unable to load contracts" description={error} /> : null}
        {!loading && !error ? (
          data.length === 0 ? (
            <EmptyState
              title="No contracts match the current filters"
              description="Create a new contract or clear the filters."
              action={(
                <Link to="/contracts/new" className="button-primary">
                  <Plus className="h-4 w-4" />
                  New contract
                </Link>
              )}
            />
          ) : (
            <DataTable
              rows={data}
              rowKey={(row) => row.id}
              pageSize={10}
              variant="open"
              minTableWidth="min-w-[1100px] w-full"
              onRowClick={(row) => navigate(`/contracts/${row.id}`)}
              emptyTitle="No contracts match the current filters"
              emptyDescription="Create a new contract or clear the filters."
              columns={[
                {
                  key: 'contract',
                  header: 'Contract No',
                  cell: (item) => (
                    <div>
                      <Link to={`/contracts/${item.id}`} className="font-semibold text-accent-strong hover:underline">{item.contractNumber}</Link>
                      <p className="mt-1 text-xs text-muted">{item.name}</p>
                    </div>
                  ),
                },
                { key: 'client', header: 'Client', cell: (item) => <span>{item.clientName || '-'}</span> },
                { key: 'type', header: 'Type', cell: (item) => <span className="text-sm text-secondary">{formatContractLabel(item.contractType)}</span> },
                { key: 'status', header: 'Status', cell: (item) => renderContractStatusBadge(item.status) },
                { key: 'start', header: 'Start Date', cell: (item) => <span className="text-sm text-secondary">{formatDateOnly(item.startDate)}</span> },
                { key: 'end', header: 'End Date', cell: (item) => <span className="text-sm text-secondary">{formatDateOnly(item.endDate)}</span> },
                { key: 'days', header: 'Days to Expiry', cell: (item) => <span>{item.daysToExpiry}</span> },
                { key: 'risk', header: 'Renewal Risk', cell: (item) => renderRenewalRiskBadge(item.renewalRisk) },
                { key: 'billing', header: 'Billing Model', cell: (item) => renderBillingModelBadge(item.billingModel) },
                { key: 'owner', header: 'Owner', cell: (item) => <span className="text-sm text-secondary">{item.ownerName || '-'}</span> },
                {
                  key: 'actions',
                  header: 'Actions',
                  cell: (item) => (
                    <Link to={`/contracts/${item.id}`} className="button-secondary px-3 py-1.5 text-xs">Open</Link>
                  ),
                },
              ]}
            />
          )
        ) : null}
      </SectionCard>
    </PageScaffold>
  )
}

function SummaryChip({
  label,
  value,
  tone = 'default',
}: {
  label: string
  value: number
  tone?: 'default' | 'warning' | 'danger'
}) {
  const toneClass =
    tone === 'warning'
      ? 'border-amber-500/20 bg-amber-500/10'
      : tone === 'danger'
        ? 'border-rose-500/20 bg-rose-500/10'
        : 'border-[var(--app-divider)] bg-[var(--app-surface-strong)]'

  return (
    <div className={`inline-flex min-h-[40px] items-center gap-2 rounded-full border px-3 py-1.5 ${toneClass}`}>
      <span className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{label}</span>
      <span className="rounded-full bg-black/5 px-2 py-0.5 text-xs font-semibold tabular-nums text-app">{value}</span>
    </div>
  )
}
